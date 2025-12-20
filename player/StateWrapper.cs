using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Connectstate;
using csharp;
using deps.HttpSharp;
using deps.WebSocketSharp.Net;
using lib.audio;
using lib.common;
using lib.core;
using lib.dealer;
using lib.mercury;
using lib.metadata;
using log4net;
using log4net.Util;
using Newtonsoft.Json.Linq;
using player.contexts;
using player.state;
using ProtoBuf;
using spotify.metadata.proto;
using spotify.player.proto;
using spotify.player.proto.transfer;
using spotify.playlist4.proto;
using ContextPlayerOptions = Connectstate.ContextPlayerOptions;
using PlayOrigin = Connectstate.PlayOrigin;
using Restrictions = Connectstate.Restrictions;
using Session = lib.core.Session;

namespace player
{
    public class StateWrapper : DeviceStateHandler.Listener, DealerClient.MessageListener, IDisposable
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(StateWrapper));
        private PlayerState _state;
        private Session _session;
        private Player _player;
        private ScheduledExecutorService _scheduler;
        private DeviceStateHandler _device;
        private AbsSpotifyContext _context;
        private PagesLoader _pages;
        private TracksKeeper _tracksKeeper;
        private ScheduledExecutorService.ScheduledFuture<int> _volumeChangedFuture;
        private Object _funcLock = new Object();

        internal StateWrapper(Session session, Player player, PlayerConfiguration conf)
        {
            _session = session;
            _player = player;
            _device = new DeviceStateHandler(session, conf);
            _state = InitState(new PlayerState());
            
            _device.AddListener(this);
            _session.GetDealer().AddMessageListener(this, "spotify:user:attributes:update", "hm://playlist/", "hm://collection/collection/" + session.Username() + "/json");
        }

        private static PlayerState InitState(PlayerState builder)
        {
            builder.PlaybackSpeed = 1.0;
            builder.SessionId = "";
            builder.PlaybackId = "";
            builder.Suppressions = new Suppressions();
            builder.ContextRestrictions = new Restrictions();
            builder.Options = new ContextPlayerOptions
            {
                RepeatingContext = false,
                ShufflingContext = false,
                RepeatingTrack = false
            };
            builder.PositionAsOfTimestamp = 0;
            builder.Position = 0;
            builder.IsPlaying = false;
            return builder;
        }

        public static String GeneratePlaybackId(Random random)
        {
            byte[] bytes = new byte[16];
            random.NextBytes(bytes);
            bytes[0] = 1;
            return Utils.bytesToHex(bytes).ToLower();
        }

        private static String GenerateSessionId(Random random)
        {
            byte[] bytes = new byte[16];
            random.NextBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=');
        }

        private bool ShouldPlay(ContextTrack track)
        {
            if ((track.Metadatas.TryGetValue("force_remove_reasons", out var value) ? value : "") != "")
                return false;

            if (track.Uri != "")
            {
                if (PlayableId.IsDelimiter(track.Uri))
                    return false;

                if (PlayableId.IsLocal(track.Uri))
                    return false;
            }

            bool filterExplicit = "1".Equals(_session.GetUserAttribute("filter-explicit-content"));
            if (!filterExplicit) return true;

            return !Boolean.Parse(track.Metadatas.TryGetValue("is_explicit", out var isExplicit) ? isExplicit : "false");
        }

        private bool AreAllUnplayable(List<ContextTrack> tracks)
        {
            foreach (ContextTrack track in tracks)
                if (ShouldPlay(track))
                    return false;

            return true;
        }

        internal bool IsActive()
        {
            return _device.IsActive();
        }
        
        internal void SetState(bool playing, bool paused, bool buffering)
        {
            lock (_funcLock)
            {
                if (paused && !playing) throw new InvalidOperationException();
                else if (buffering && !playing) throw new InvalidOperationException();

                bool wasPaused = IsPaused();
                _state.IsPlaying = playing;
                _state.IsPaused = paused;
                _state.IsBuffering = buffering;

                if (wasPaused && !paused)
                    SetPosition(_state.PositionAsOfTimestamp);
            }
        }
        
        internal bool IsPaused()
        {
            lock (_funcLock)
            {
                return _state.IsPlaying && _state.IsPaused;
            }
        }
        
        internal void SetBuffering(bool buffering)
        {
            lock (_funcLock)
            {
                SetState(true, _state.IsPaused, buffering);
            }
        }

        private bool IsShufflingContext()
        {
            return _state.Options.ShufflingContext;
        }

        internal void SetShufflingContext(bool value)
        {
            if (_context == null || _tracksKeeper == null) return;

            bool old = IsShufflingContext();
            _state.Options.ShufflingContext = value && _context.Restrictions.Can(RestrictionsManager.Action.SHUFFLE);

            if (old != IsShufflingContext()) _tracksKeeper.ToggleShuffle(IsShufflingContext());
        }

        private bool IsRepeatingContext()
        {
            return _state.Options.RepeatingContext;
        }

        internal void SetRepeatingContext(bool value)
        {
            if (_context == null) return;

            _state.Options.RepeatingContext =
                value && _context.Restrictions.Can(RestrictionsManager.Action.REPEAT_CONTEXT);
        }

        private bool IsRepeatingTrack()
        {
            return _state.Options.RepeatingTrack;
        }

        internal void SetRepeatingTrack(bool value)
        {
            if (_context == null) return;

            _state.Options.RepeatingTrack = value && _context.Restrictions.Can(RestrictionsManager.Action.REPEAT_TRACK);
        }

        public DeviceStateHandler GetDevice()
        {
            return _device;
        }

        public String GetContextUri()
        {
            return _state.ContextUri;
        }

        public String GetContextUrl()
        {
            return _state.ContextUrl;
        }

        private void LoadTransforming()
        {
            if (_tracksKeeper == null) throw new InvalidOperationException();
            
            String url = _state.ContextMetadatas.TryGetValue("transforming.url", out var value) ? value : null;
            if (url == null) return;

            bool shuffle = false;
            if (_state.ContextMetadatas.TryGetValue("transforming.shuffle", out value))
                shuffle = Boolean.Parse(_state.ContextMetadatas["transforming.shuffle"]);

            bool willRequest = !_tracksKeeper.GetCurrentTrack().Metadatas.ContainsKey("audio.fwdbtn.fade_overlap");
            LOGGER.InfoFormat("Context has transforming! (url: {0}, shuffle: {1}, willRequest: {2})", url, shuffle, willRequest);

            if (!willRequest) return;
            JObject obj = ProtoUtils.CraftContextStateCombo(_state, _tracksKeeper.Tracks);
            try
            {
                HttpResponse resp = _session.GetApi()
                    .Send(ApiClient.RequestMethod.POST, new Uri(url).AbsolutePath, null,
                        Encoding.UTF8.GetBytes(obj.ToString()), "application/json");
                String body = resp.GetResponseString();
                if (!resp.StatusCode.Equals(HttpStatusCode.OK))
                {
                    LOGGER.WarnFormat("Failed loading cuepoints! (code: {0}, msg: {1}, body: {2})", resp.StatusCode,
                        resp.StatusDescription, body);
                    return;
                }

                if (body != null) UpdateContext(JObject.Parse(body));
                else throw new InvalidOperationException();

                LOGGER.Debug("Updated context with transforming information!");
            }
            catch (Exception ex)
            {
                if (ex is MercuryClient.MercuryException || ex is IOException)
                {
                    LOGGER.Warn("Failed loading cuepoints!", ex);
                }
                else throw;
            }
        }

        private String SetContext(String uri)
        {
            _context = AbsSpotifyContext.From(uri);
            _state.ContextUri = uri;

            if (!_context.IsFinite())
            {
                SetRepeatingContext(false);
                SetShufflingContext(false);
            }

            _state.ContextUrl = "";
            _state.Restrictions = new Restrictions();
            _state.ContextRestrictions = new Restrictions();
            _state.ContextMetadatas.Clear();
            
            _pages = PagesLoader.From(_session, uri);
            _tracksKeeper = new TracksKeeper(this);
            
            _device.SetIsActive(true);

            return RenewSessionId();
        }

        private String SetContext(Context ctx)
        {
            String uri = ctx.Uri;
            _context = AbsSpotifyContext.From(uri);
            _state.ContextUri = uri;

            if (!_context.IsFinite())
            {
                SetRepeatingContext(false);
                SetShufflingContext(false);
            }

            if (ctx.Url != "") _state.ContextUrl = ctx.Url;
            else _state.ContextUrl = "";
            
            _state.ContextMetadatas.Clear();
            ProtoUtils.CopyOverMetadata(ctx, _state);
            
            _pages = PagesLoader.From(_session, ctx);
            _tracksKeeper = new TracksKeeper(this);
            
            _device.SetIsActive(true);

            return RenewSessionId();
        }

        private void UpdateRestrictions()
        {
            if (_context == null) return;
            
            if (_tracksKeeper.IsPlayingFirst() && !IsRepeatingContext()) 
                _context.Restrictions.Disallow(RestrictionsManager.Action.SKIP_PREV, RestrictionsManager.REASON_NO_PREV_TRACK);
            else 
                _context.Restrictions.Allow(RestrictionsManager.Action.SKIP_PREV);
            
            if (_tracksKeeper.IsPlayingLast() && !IsRepeatingContext())
                _context.Restrictions.Disallow(RestrictionsManager.Action.SKIP_NEXT, RestrictionsManager.REASON_NO_NEXT_TRACK);
            else
                _context.Restrictions.Allow(RestrictionsManager.Action.SKIP_NEXT);
            
            _state.Restrictions = _context.Restrictions.ToProto();
            _state.ContextRestrictions = _context.Restrictions.ToProto();
        }
        
        internal void Updated()
        {
            lock (_funcLock)
            {
                UpdateRestrictions();
                _device.UpdateState(PutStateReason.PlayerStateChanged, _player.Time(), _state);
            }
        }

        internal void AddListener(DeviceStateHandler.Listener listener)
        {
            _device.AddListener(listener);
        }

        public bool IsReady()
        {
            return _state.IsSystemInitiated;
        }
        
        public void Ready()
        {
            lock (_funcLock)
            {
                _state.IsSystemInitiated = true;
                _device.UpdateState(PutStateReason.NewDevice, _player.Time(), _state);
                LOGGER.Info("Notified new device (us)!");
            }
        }

        public void Command(DeviceStateHandler.Endpoint endpoint, DeviceStateHandler.CommandBody data)
        {
            // Not interested
        }

        public void VolumeChanged()
        {
            lock (_funcLock)
            {
                if (_volumeChangedFuture != null) _volumeChangedFuture.Cancel(false);
                _volumeChangedFuture = new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    _device.UpdateState(PutStateReason.VolumeChanged, _player.Time(), _state);
                    return 0;
                }, 500, ScheduledExecutorService.TimeUnit.MILLISECONDS);
                _scheduler.schedule(_volumeChangedFuture);
            }
        }
        
        public void NotActive()
        {
            lock (_funcLock)
            {
                _state = new PlayerState();
                InitState(_state);

                _device.SetIsActive(false);
                _device.UpdateState(PutStateReason.BecameInactive, _player.Time(), _state);
                LOGGER.Info("Notified inactivity!");
            }
        }

        internal uint GetVolume()
        {
            lock (_funcLock)
            {
                return _device.GetVolume();
            }
        }

        internal void SetVolume(uint val)
        {
            _device.SetVolume(val);
        }

        internal void EnrichWithMetadata(MetadataWrapper metadata)
        {
            if (metadata.IsTrack()) EnrichWithMetadata(metadata._track);
            else if (metadata.IsEpisode()) EnrichWithMetadata(metadata._episode);
        }

        private void EnrichWithMetadata(Track track)
        {
            lock (_funcLock)
            {
                if (_state.Track == null) throw new InvalidOperationException();
                if (!ProtoUtils.IsTrack(_state.Track, track))
                {
                    LOGGER.WarnFormat("Failed updating metadata: tracks do not match. (current: {0}, expected: {1})",
                        ProtoUtils.ToString(_state.Track), ProtoUtils.ToString(track));
                    return;
                }

                if (track.Duration != 0) _tracksKeeper.UpdateTrackDuration(track.Duration);

                ProvidedTrack builder = new ProvidedTrack();
                if (track.Popularity != 0) builder.Metadatas.Add("popularity", track.Popularity.ToString());
                // FIXME: I'm unsure about if it's null or just a default value e.g. false
                if (track.Explicit) builder.Metadatas.Add("is_explicit", track.Explicit.ToString());
                if (track.HasLyrics) builder.Metadatas.Add("has_lyrics", track.HasLyrics.ToString());
                if (track.Name != "") builder.Metadatas.Add("title", track.Name);
                if (track.DiscNumber != 0) builder.Metadatas.Add("album_disc_number", track.DiscNumber.ToString());

                for (int i = 0; i < track.Artists.Count; i++)
                {
                    Artist artist = track.Artists[i];
                    if (artist.Name != "")
                        builder.Metadatas.Add("artist_name" + (i == 0 ? "" : (":" + i)), artist.Name);
                    if (artist.Gid != null)
                        builder.Metadatas.Add("artist_uri" + (i == 0 ? "" : (":" + i)),
                            ArtistId.FromHex(Utils.bytesToHex(artist.Gid)).ToSpotifyUri());
                }

                if (track.Album != null)
                {
                    Album album = track.Album;
                    if (album.Discs.Count > 0)
                    {
                        builder.Metadatas.Add("album_track_count", ProtoUtils.GetTrackCount(album).ToString());
                        builder.Metadatas.Add("album_disc_count", album.Discs.Count.ToString());
                    }

                    if (album.Name != "") builder.Metadatas.Add("album_title", album.Name);
                    if (album.Gid != null)
                        builder.Metadatas.Add("album_uri",
                            AlbumId.FromHex(Utils.bytesToHex(album.Gid)).ToSpotifyUri());

                    for (int i = 0; i < album.Artists.Count; i++)
                    {
                        Artist artist = album.Artists[i];
                        if (artist.Name != "")
                            builder.Metadatas.Add("album_artist_name" + (i == 0 ? "" : (":" + i)), artist.Name);
                        if (artist.Gid != null)
                            builder.Metadatas.Add("album_artist_uri" + (i == 0 ? "" : (":" + i)),
                                ArtistId.FromHex(Utils.bytesToHex(artist.Gid)).ToSpotifyUri());
                    }

                    if (track.DiscNumber != 0)
                    {
                        foreach (Disc disc in album.Discs)
                        {
                            if (disc.Number != track.DiscNumber) continue;

                            for (int i = 0; i < disc.Tracks.Count; i++)
                            {
                                if (disc.Tracks[i].Gid.Equals(track.Gid))
                                {
                                    builder.Metadatas.Add("album_track_number", (i + 1).ToString());
                                    break;
                                }
                            }
                        }
                    }

                    if (album.CoverGroup != null) ImageId.PutAsMetadata(builder, album.CoverGroup);
                }

                ProtoUtils.PutFilesAsMetadata(builder, track.Files);
                _state.Track = builder;
            }
        }
        
        private void EnrichWithMetadata(Episode episode)
        {
            lock (_funcLock)
            {
                if (_state.Track == null) throw new InvalidOperationException();
                if (!ProtoUtils.IsEpisode(_state.Track, episode))
                {
                    LOGGER.WarnFormat("Failed updating metadata: episodes do not match. (current: {0}, expected: {1})",
                        ProtoUtils.ToString(_state.Track), ProtoUtils.ToString(episode));
                    return;
                }

                if (episode.Duration != 0) _tracksKeeper.UpdateTrackDuration(episode.Duration);

                ProvidedTrack builder = new ProvidedTrack();
                if (episode.Explicit) builder.Metadatas.Add("is_explicit", episode.Explicit.ToString());
                if (episode.Name != "") builder.Metadatas.Add("title", episode.Name);

                if (episode.Show != null)
                {
                    Show show = episode.Show;
                    if (show.Name != "") builder.Metadatas.Add("album_title", show.Name);

                    if (show.CoverImage != null) ImageId.PutAsMetadata(builder, show.CoverImage);
                }

                if (episode.Audioes.Count > 0 && episode.Videoes.Count == 0)
                {
                    builder.Metadatas.Add("media.type", "audio");
                }
                else if (episode.Videoes.Count > 0)
                {
                    builder.Metadatas.Add("media.type", "video");
                }

                ProtoUtils.PutFilesAsMetadata(builder, episode.Audioes);
                _state.Track = builder;
            }
        }
        
        internal int GetPosition()
        {
            lock (_funcLock)
            {
                int diff = (int)(TimeProvider.currentTimeMillis() - _state.Timestamp);
                return (int)(_state.PositionAsOfTimestamp + diff);
            }
        }

        internal void SetPosition(long pos)
        {
            lock (_funcLock)
            {
                _state.Timestamp = TimeProvider.currentTimeMillis();
                _state.PositionAsOfTimestamp = pos;
                _state.Position = 0;
            }
        }

        internal String LoadContextWithTracks(String uri, List<ContextTrack> tracks)
        {
            _state.PlayOrigin = new PlayOrigin();
            _state.Options = new ContextPlayerOptions();

            String sessionid = SetContext(uri);
            _pages.PutFirstPage(tracks, uri);
            _tracksKeeper.InitializeStart();
            SetPosition(0);
            
            LoadTransforming();
            return sessionid;
        }

        internal String LoadContext(String uri)
        {
            _state.PlayOrigin = new PlayOrigin();
            _state.Options = new ContextPlayerOptions();
            
            String sessionid = SetContext(uri);
            _tracksKeeper.InitializeStart();
            SetPosition(0);
            
            LoadTransforming();
            return sessionid;
        }

        internal String Transfer(TransferState cmd)
        {
            spotify.player.proto.transfer.Session ps = cmd.CurrentSession;

            _state.PlayOrigin = ProtoUtils.ConvertPlayOrigin(ps.PlayOrigin);
            _state.Options = ProtoUtils.ConvertPlayerOptions(cmd.Options);
            String sessionId = SetContext(ps.Context);

            Playback pb = cmd.Playback;
            try
            {
                _tracksKeeper.InitializeFrom(tracks =>
                {
                    for (int i = 0; i < tracks.Count; i++)
                    {
                        ContextTrack track = tracks[i];
                        if ((track.Uid != "" && ps.CurrentUid.Equals(track.Uid)) ||
                            ProtoUtils.TrackEquals(track, pb.CurrentTrack))
                            return i;
                    }

                    return -1;
                }, pb.CurrentTrack, cmd.Queue);
            }
            catch (InvalidOperationException e)
            {
                LOGGER.WarnFormat("Failed initializing tracks, falling back to start. (uid: {0})", ps.CurrentUid);
                _tracksKeeper.InitializeStart();
            }

            _state.PositionAsOfTimestamp = pb.PositionAsOfTimestamp;
            if (pb.IsPaused) _state.Timestamp = TimeProvider.currentTimeMillis();
            else _state.Timestamp = pb.Timestamp;
            
            LoadTransforming();
            return sessionId;
        }

        internal String Load(JObject obj)
        {
            _state.PlayOrigin = ProtoUtils.JsonToPlayOrigin(DeviceStateHandler.PlayCommandHelper.GetPlayOrigin(obj));
            _state.Options =
                ProtoUtils.JsonToPlayerOptions(DeviceStateHandler.PlayCommandHelper.GetPlayerOptionsOverride(obj),
                    _state.Options);
            String sessionId =
                SetContext(ProtoUtils.JsonToContext(DeviceStateHandler.PlayCommandHelper.GetContext(obj)));

            String trackUid = DeviceStateHandler.PlayCommandHelper.GetSkipToUid(obj);
            String trackUri = DeviceStateHandler.PlayCommandHelper.GetSkipToUri(obj);
            int? trackIndex = DeviceStateHandler.PlayCommandHelper.GetSkipToIndex(obj);

            try
            {
                if (!string.IsNullOrEmpty(trackUri))
                {
                    _tracksKeeper.InitializeFrom(tracks => ProtoUtils.IndexOfTrackByUri(tracks, trackUri), null, null);
                } else if (!string.IsNullOrEmpty(trackUid))
                {
                    _tracksKeeper.InitializeFrom(tracks => ProtoUtils.IndexOfTrackByUid(tracks, trackUid), null, null);
                } else if (trackIndex != null)
                {
                    _tracksKeeper.InitializeFrom(tracks =>
                    {
                        if (trackIndex < tracks.Count) return (int) trackIndex;
                        return -1;
                    }, null, null);
                }
                else
                {
                    _tracksKeeper.InitializeStart();
                }
            }
            catch (InvalidOperationException e)
            {
                LOGGER.WarnFormat("Failed initializing tracks, falling back to start. (uri: {0}, uid: {1}, index: {2}) Reason: {3}", trackUri, trackUid, trackIndex, e);
                _tracksKeeper.InitializeStart();
            }
            
            int? seekTo = DeviceStateHandler.PlayCommandHelper.GetSeekTo(obj);
            if (seekTo != null) SetPosition(seekTo.Value);
            else SetPosition(0);
            
            LoadTransforming();
            return sessionId;
        }
        
        internal void UpdateContext(JObject obj)
        {
            lock (_funcLock)
            {
                String uri = obj["uri"].ToObject<string>();
                if (!_context.Uri().Equals(uri))
                {
                    LOGGER.WarnFormat("Received update for the wrong context! (context: {0}, newUri: {1})", _context,
                        uri);
                    return;
                }

                ProtoUtils.CopyOverMetadata(obj.TryGetValue("metadata", out var metadata) ? metadata.ToObject<JObject>() : null, _state);
                _tracksKeeper.UpdateContext(ProtoUtils.JsonToContextPages(obj.TryGetValue("pages", out var pages) ? pages.ToObject<JArray>() : null));
            }
        }

        internal void SkipTo(ContextTrack track)
        {
            _tracksKeeper.SkipTo(track);
            SetPosition(0);
        }

        public IPlayableId GetCurrentPlayable()
        {
            return _tracksKeeper == null ? null : PlayableId.From(_tracksKeeper.GetCurrentTrack());
        }

        internal IPlayableId GetCurrentPlayableOrThrow()
        {
            IPlayableId id = GetCurrentPlayable();
            if (id == null) throw new InvalidOperationException();
            return id;
        }

        internal NextPlayable GetNextPlayable(bool autoplayEnabled)
        {
            if (_tracksKeeper == null) return NextPlayable.MISSING_TRACKS;

            try
            {
                return _tracksKeeper.NextPlayable(autoplayEnabled);
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is MercuryClient.MercuryException)
                {
                    LOGGER.ErrorExt("Failed fetching next playable.", ex);
                    return NextPlayable.MISSING_TRACKS;
                }
                else throw;
            }
        }

        internal IPlayableId NextPlayableDoNotSet()
        {
            try
            {
                PlayableIdWithIndex id = _tracksKeeper.NextPlayableDoNotSet();
                return id == null ? null : id.Id;
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is MercuryClient.MercuryException)
                {
                    LOGGER.ErrorExt("Failed fetching next playable.", ex);
                    return null;
                }
                else throw;
            }
        }

        internal PreviousPlayable GetPreviousPlayable()
        {
            if (_tracksKeeper == null) return PreviousPlayable.MISSING_TRACKS;
            return _tracksKeeper.PreviousPlayable();
        }

        internal void RemoveListener(DeviceStateHandler.Listener listener)
        {
            _device.RemoveListener(listener);
        }
        
        internal void AddToQueue(ContextTrack track)
        {
            lock (_funcLock)
            {
                _tracksKeeper.AddToQueue(track);
            }
        }

        internal void RemoveFromQueue(String uri)
        {
            lock (_funcLock)
            {
                _tracksKeeper.RemoveFromQueue(uri);
            }
        }
        
        internal void SetQueue(List<ContextTrack> prevTracks, List<ContextTrack> nextTracks)
        {
            lock (_funcLock)
            {
                _tracksKeeper.SetQueue(prevTracks, nextTracks);
            }
        }

        internal Dictionary<String, String> MetadataFor(IPlayableId id)
        {
            if (_tracksKeeper == null) return null;

            ContextTrack current = GetCurrentTrack();
            if (current != null && id.Matches(current))
                return current.Metadatas;

            int index = PlayableId.IndexOfTrack(_tracksKeeper.Tracks, id);
            if (index == -1)
            {
                index = PlayableId.IndexOfTrack(_tracksKeeper.Queue, id);
                if (index == -1) return null;
            }
            
            return _tracksKeeper.Tracks[index].Metadatas;
        }
        
        private void PerformAdd(Add add)
        {
            lock (_funcLock)
            {
                bool wasShuffled = false;
                if (IsShufflingContext())
                {
                    wasShuffled = true;
                    _tracksKeeper.ToggleShuffle(false);
                }

                try
                {
                    if (add.AddFirst)
                        _tracksKeeper.AddToTracks(0, add.Items);
                    else if (add.AddLast)
                        _tracksKeeper.AddToTracks(_tracksKeeper.Length(), add.Items);
                    else if (add.FromIndex != 0)
                        _tracksKeeper.AddToTracks(add.FromIndex, add.Items);
                    else throw new InvalidOperationException(add.ToString());
                }
                finally
                {
                    if (wasShuffled) _tracksKeeper.ToggleShuffle(true);
                }
            }
        }
        
        private void PerformRemove(Rem rem)
        {
            lock (_funcLock)
            {
                bool wasShuffled = false;
                if (IsShufflingContext())
                {
                    wasShuffled = true;
                    _tracksKeeper.ToggleShuffle(false);
                }

                try
                {
                    if (rem.FromIndex != 0 && rem.Length != 0) _tracksKeeper.RemoveTracks(rem.FromIndex, rem.Length);
                    else throw new InvalidOperationException(rem.ToString());
                }
                finally
                {
                    if (wasShuffled) _tracksKeeper.ToggleShuffle(true);
                }
            }
        }

        private void PerformMove(Mov mov)
        {
            lock (_funcLock)
            {
                bool wasShuffled = false;
                if (IsShufflingContext())
                {
                    wasShuffled = true;
                    _tracksKeeper.ToggleShuffle(false);
                }

                try
                {
                    if (mov.FromIndex != 0 && mov.ToIndex != 0 && mov.Length != 0)
                        _tracksKeeper.MoveTracks(mov.FromIndex, mov.ToIndex, mov.Length);
                    else throw new InvalidOperationException(mov.ToString());
                }
                finally
                {
                    if (wasShuffled) _tracksKeeper.ToggleShuffle(true);
                }
            }
        }

        public void OnMessage(String uri, Dictionary<String, String> headers, byte[] payload)
        {
            if (uri.StartsWith("hm://playlist/"))
            {
                PlaylistModificationInfo mod = Serializer.Deserialize<PlaylistModificationInfo>(new MemoryStream(payload));
                String modUri = mod.Uri.ToString();
                if (_context != null && Equals(modUri, _context.Uri()))
                {
                    foreach (Op op in mod.Ops)
                    {
                        switch (op.kind)
                        {
                            case Op.Kind.Add:
                                PerformAdd(op.Add);
                                break;
                            case Op.Kind.Rem:
                                PerformRemove(op.Rem);
                                break;
                            case Op.Kind.Mov:
                                PerformMove(op.Mov);
                                break;
                            case Op.Kind.UpdateItemAttributes:
                            case Op.Kind.UpdateListAttributes:
                                LOGGER.Warn("Unsupported operation: " + op);
                                break;
                            default:
                                LOGGER.Warn("Received unknown op: " + op.kind);
                                break;
                        }
                        
                        LOGGER.InfoFormat("Received update for current context! (uri: {0}, ops: {1})", modUri, mod.Ops);
                        Updated();
                    }
                } else if (_context != null && AbsSpotifyContext.IsCollection(_session, modUri))
                {
                    foreach (Op op in mod.Ops)
                    {
                        List<String> uris = new List<String>();
                        foreach (Item item in op.Add.Items)
                            uris.Add(item.Uri);

                        if (op.kind == Op.Kind.Add)
                            PerformCollectionUpdate(uris, true);
                        else if (op.kind == Op.Kind.Rem)
                            PerformCollectionUpdate(uris, false);
                    }
                    
                    LOGGER.InfoFormat("Updated tracks in collection! (uri: {0}, ops: {1})", modUri, mod.Ops);
                }
            } else if (_context != null && uri.Equals("hm://collection/collection/" + _session.Username() + "/json"))
            {
                List<String> added = null;
                List<String> removed = null;

                JArray items = JObject.Parse(Encoding.UTF8.GetString(payload))["items"].ToObject<JArray>();
                foreach (JToken elm in items)
                {
                    JObject obj = elm as JObject;
                    String itemuri = "spotify:" + obj["type"].ToObject<string>() + ":" +
                                     obj["identifier"].ToObject<string>();
                    if (obj["removed"].ToObject<bool>())
                    {
                        if (removed == null) removed = new List<string>();
                        removed.Add(itemuri);
                    }
                    else
                    {
                        if (added == null) added = new List<string>();
                        added.Add(itemuri);
                    }
                }

                if (added != null) PerformCollectionUpdate(added, true);
                if (removed != null) PerformCollectionUpdate(removed, false);
                
                LOGGER.InfoFormat("Updated tracks in collection! (added: {0}, removed: {1})", added != null, removed != null);
                Updated();
            }
        }
        
        private void PerformCollectionUpdate(List<String> uris, bool inCollection)
        {
            lock (_funcLock)
            {
                foreach (String uri in uris)
                    _tracksKeeper.UpdateMetadataFor(uri, "collection.in_collection", inCollection.ToString());
            }
        }

        public int GetContextSize()
        {
            String trackCount = GetContextMetadata("track_count");
            if (trackCount != null) return int.Parse(trackCount);
            else if (_tracksKeeper != null) return _tracksKeeper.Tracks.Count;
            else return 0;
        }

        public String GetContextMetadata(String key)
        {
            return _state.ContextMetadatas.TryGetValue(key, out var value) ? value : null;
        }

        public void SetContextMetadata(String key, String value)
        {
            if (value == null) _state.ContextMetadatas.Remove(key);
            else _state.ContextMetadatas[key] = value;
        }

        public List<ContextTrack> GetNextTracks(bool withQueue)
        {
            if (_tracksKeeper == null) return new List<ContextTrack>();

            int index = (int) _tracksKeeper.GetCurrentTrackIndex();
            int size = _tracksKeeper.Tracks.Count;
            List<ContextTrack> list = new List<ContextTrack>(size - index);
            for (int i = index + 1; i < size; i++) 
                list.Add(_tracksKeeper.Tracks[i]);
            
            if (withQueue) list.InsertRange(0, _tracksKeeper.Queue);
            
            return list;
        }

        public ContextTrack GetCurrentTrack()
        {
            int index = (int) _tracksKeeper.GetCurrentTrackIndex();
            return _tracksKeeper == null || _tracksKeeper.Tracks.Count < index ? null : _tracksKeeper.Tracks[index];
        }

        public List<ContextTrack> GetPrevTracks()
        {
            if (_tracksKeeper == null) return new List<ContextTrack>();
            
            int index = (int) _tracksKeeper.GetCurrentTrackIndex();
            List<ContextTrack> list = new List<ContextTrack>(index);
            for (int i = 0; i < index; i++) 
                list.Add(_tracksKeeper.Tracks[i]);

            return list;
        }

        private String RenewSessionId()
        {
            String sessionId = GenerateSessionId(new Random());
            _state.SessionId = sessionId;
            return sessionId;
        }

        public String GetSessionId()
        {
            return _state.SessionId;
        }

        public void SetPlaybackId(String playbackId)
        {
            _state.PlaybackId = playbackId;
        }

        public PlayOrigin GetPlayOrigin()
        {
            return _state.PlayOrigin;
        }

        public void Dispose()
        {
            _session.GetDealer().RemoveMessageListener(this);
            
            _device.RemoveListener(this);
            _device.Dispose();
        }

        public class PreviousPlayable : Enumeration 
        {
            public static readonly PreviousPlayable MISSING_TRACKS = new PreviousPlayable();
            public static readonly PreviousPlayable OK = new PreviousPlayable();

            public bool IsOk()
            {
                return Equals(this, OK);
            }
        }

        public class NextPlayable : Enumeration
        {
            public static readonly NextPlayable MISSING_TRACKS = new NextPlayable();
            public static readonly NextPlayable AUTOPLAY = new NextPlayable();
            public static readonly NextPlayable OK_PLAY = new NextPlayable();
            public static readonly NextPlayable OK_PAUSE = new NextPlayable();
            public static readonly NextPlayable OK_REPEAT = new NextPlayable();

            public bool IsOk()
            {
                return Equals(this, OK_PLAY) || Equals(this, OK_PAUSE) || Equals(this, OK_REPEAT);
            }
        }

        internal class PlayableIdWithIndex
        {
            public IPlayableId Id;
            public int Index;

            internal PlayableIdWithIndex(IPlayableId id, int index)
            {
                Id = id;
                Index = index;
            }
        }

        private class TracksKeeper
        {
            internal static int MAX_PREV_TRACKS = 16;
            internal static int MAX_NEXT_TRACKS = 48;
            internal List<ContextTrack> Queue = new List<ContextTrack>();
            internal List<ContextTrack> Tracks = new List<ContextTrack>();
            internal FisherYatesShuffle<ContextTrack> Shuffle;
            internal volatile bool IsPlayingQueue = false;
            internal volatile bool CannotLoadMore = false;
            internal volatile int ShuffleKeepIndex = -1;
            private StateWrapper _wrapper;
            private Object _funcLock = new Object();

            internal TracksKeeper(StateWrapper wrapper)
            {
                _wrapper = wrapper;
                CheckComplete();
                Shuffle = new FisherYatesShuffle<ContextTrack>(new Random());
            }

            private void UpdateTrackCount()
            {
                if (_wrapper._context.IsFinite()) 
                    _wrapper._state.ContextMetadatas.Add("track_count", (Tracks.Count + Queue.Count).ToString());
                else 
                    _wrapper._state.ContextMetadatas.Remove("track_count");
            }

            private void CheckComplete()
            {
                if (CannotLoadMore) return;

                if (_wrapper._context.IsFinite())
                {
                    int total_tracks =
                        int.Parse(_wrapper._state.ContextMetadatas.TryGetValue("track_count", out var value)
                            ? value
                            : "-1");
                    if (total_tracks == -1) CannotLoadMore = false;
                    else CannotLoadMore = total_tracks == Tracks.Count;
                }
                else CannotLoadMore = false;
            }
            
            internal ProvidedTrack GetCurrentTrack()
            {
                lock (_funcLock)
                {
                    return _wrapper._state.Track;
                }
            }

            internal uint GetCurrentTrackIndex()
            {
                return _wrapper._state.Index.Track;
            }

            private void SetCurrentTrackIndex(uint index)
            {
                if (IsPlayingQueue) throw new Exception("Illegal state");
                _wrapper._state.Index = new ContextIndex
                {
                    Track = index
                };
                UpdateState();
            }

            private void ShiftCurrentTrackIndex(int delta)
            {
                if (delta < 0)
                {
                    uint abs = (uint)(-delta);
                    if (_wrapper._state.Index.Track < abs)
                        _wrapper._state.Index.Track = 0;
                    else
                        _wrapper._state.Index.Track -= abs;
                }
                else
                {
                    _wrapper._state.Index.Track += (uint)delta;
                }
            }

            private void UpdatePrevNextTracks()
            {
                int index = (int)GetCurrentTrackIndex();

                _wrapper._state.PrevTracks.Clear();
                for (int i = Math.Max(0, index - MAX_PREV_TRACKS); i < index; i++)
                    _wrapper._state.PrevTracks.Add(ProtoUtils.ToProvidedTrack(Tracks[i], _wrapper.GetContextUri()));

                _wrapper._state.NextTracks.Clear();
                foreach (ContextTrack track in Queue)
                    _wrapper._state.NextTracks.Add(ProtoUtils.ToProvidedTrack(track, _wrapper.GetContextUri()));

                for (int i = index + 1; i < Math.Min(Tracks.Count, index + 1 + MAX_NEXT_TRACKS); i++)
                    _wrapper._state.NextTracks.Add(ProtoUtils.ToProvidedTrack(Tracks[i], _wrapper.GetContextUri()));
            }

            internal void UpdateTrackDuration(int duration)
            {
                _wrapper._state.Duration = duration;
                _wrapper._state.Track.Metadatas.Add("duration", duration.ToString());
                UpdateMetadataFor((int) GetCurrentTrackIndex(), "duration", duration.ToString());
            }

            private void UpdateTrackDuration()
            {
                ProvidedTrack current = GetCurrentTrack();
                if (current.Metadatas.ContainsKey("duration"))
                    _wrapper._state.Duration = current.Metadatas.TryGetValue("duration", out var value)
                        ? long.Parse(value)
                        : throw new Exception();
                else
                    _wrapper._state.Duration = 0;
            }

            private void UpdateLikeDislike()
            {
                if (Equals(
                        _wrapper._state.ContextMetadatas.TryGetValue("like-feedback-enabled", out var value)
                            ? value
                            : "0", "1"))
                {
                    _wrapper._state.ContextMetadatas.Add("like-feedback-selected",
                        _wrapper._state.Track.Metadatas.TryGetValue("like-feedback-selected", out var value2)
                            ? value2
                            : "0");
                }
                else
                {
                    _wrapper._state.ContextMetadatas.Remove("like-feedback-selected");
                }

                if (Equals(
                        _wrapper._state.ContextMetadatas.TryGetValue("dislike-feedback-enabled", out var value3)
                            ? value3
                            : "0", "1"))
                {
                    _wrapper._state.ContextMetadatas.Add("dislike-feedback-selected",
                        _wrapper._state.Track.Metadatas.TryGetValue("dislike-feedback-selected", out var value4)
                            ? value4
                            : "0");
                }
                else
                {
                    _wrapper._state.ContextMetadatas.Remove("dislike-feedback-selected");
                }
            }

            private void UpdateState()
            {
                if (IsPlayingQueue)
                {
                    _wrapper._state.Track = ProtoUtils.ToProvidedTrack(Queue[0], _wrapper.GetContextUri());
                    Queue.RemoveAt(0);
                }
                else
                {
                    _wrapper._state.Track =
                        ProtoUtils.ToProvidedTrack(Tracks[(int)GetCurrentTrackIndex()], _wrapper.GetContextUri());
                }

                UpdateLikeDislike();

                UpdateTrackDuration();
                UpdatePrevNextTracks();
            }
            
            internal void AddToQueue(ContextTrack track)
            {
                lock (_funcLock)
                {
                    track.Metadatas.Add("is_queued", "true");
                    Queue.Add(track);
                    UpdatePrevNextTracks();
                    UpdateTrackCount();
                }
            }
            
            internal void RemoveFromQueue(String uri)
            {
                lock (_funcLock)
                {
                    byte[] gid;
                    IPlayableId playable = PlayableId.FromUri(uri);
                    if (playable.HasGid()) gid = playable.GetGid();
                    else gid = null;
                    
                    if (Queue.RemoveAll(track =>
                            (!string.IsNullOrEmpty(track.Uri) && uri.Equals(track.Uri)) ||
                            (track.Gid != null && track.Gid.Equals(gid))
                        ) > 0)
                    {
                        UpdateTrackCount();
                        UpdatePrevNextTracks();
                    }
                }
            }
            
            internal void SetQueue(List<ContextTrack> prevTracks, List<ContextTrack> nextTracks)
            {
                lock (_funcLock)
                {
                    ContextTrack current = Tracks[(int)GetCurrentTrackIndex()];

                    Queue.Clear();
                    Tracks.Clear();

                    if (prevTracks != null) Tracks.AddRange(prevTracks);
                    Tracks.Add(current);

                    if (nextTracks != null)
                    {
                        foreach (ContextTrack track in nextTracks)
                        {
                            if (ProtoUtils.IsQueued(track)) Queue.Add(track);
                            else Tracks.Add(track);
                        }
                    }

                    UpdateTrackCount();
                    UpdatePrevNextTracks();
                }
            }
            
            internal void UpdateContext(List<ContextPage> updatedPages)
            {
                lock (_funcLock)
                {
                    List<ContextTrack> updatedTracks = ProtoUtils.Join(updatedPages);
                    foreach (ContextTrack track in updatedTracks)
                    {
                        int index = ProtoUtils.IndexOfTrack(Tracks, track);
                        if (index == -1) continue;

                        ContextTrack builder = Tracks[index];
                        ProtoUtils.CopyOverMetadata(track, builder);

                        if (index == GetCurrentTrackIndex())
                        {
                            ProtoUtils.CopyOverMetadata(track, _wrapper._state.Track);
                            _wrapper._tracksKeeper.UpdateLikeDislike();
                        }
                    }
                }
            }
            
            internal void InitializeStart()
            {
                lock (_funcLock)
                {
                    if (!CannotLoadMore)
                    {
                        if (!_wrapper._pages.NextPage()) throw new InvalidOperationException();

                        Tracks.Clear();
                        Tracks.AddRange(_wrapper._pages.CurrentPage());
                    }

                    CheckComplete();
                    if (_wrapper.AreAllUnplayable(Tracks))
                        throw AbsSpotifyContext.UnsupportedContextException.CannotPlayAnything();

                    bool transformingShuffle =
                        Boolean.Parse(
                            _wrapper._state.ContextMetadatas.TryGetValue("transforming.shuffle", out var value)
                                ? value
                                : "true");
                    if (_wrapper._context.IsFinite() && _wrapper.IsShufflingContext() && transformingShuffle)
                        ShuffleEntirely();
                    else _wrapper._state.Options.ShufflingContext = false;

                    SetCurrentTrackIndex(0);
                    if (!_wrapper.ShouldPlay(Tracks[(int)GetCurrentTrackIndex()]))
                    {
                        LOGGER.Debug("Cannot play currently selected track, skipping: " +
                                     _wrapper.GetCurrentPlayable());
                    }
                }
            }

            internal delegate int Find(List<ContextTrack> track);
            
            internal void InitializeFrom(Find finder, ContextTrack track, Queue contextQueue)
            {
                lock (_funcLock)
                {
                    Tracks.Clear();
                    Queue.Clear();

                    while (true)
                    {
                        if (_wrapper._pages.NextPage())
                        {
                            List<ContextTrack> newTracks = _wrapper._pages.CurrentPage();
                            uint index = (uint)finder(newTracks);
                            if (index == -1)
                            {
                                LOGGER.Info("Did not find track, going to next page.");
                                Tracks.AddRange(newTracks);
                                continue;
                            }

                            index += (uint)Tracks.Count;
                            Tracks.AddRange(newTracks);

                            SetCurrentTrackIndex(index);
                            LOGGER.InfoFormat("Initialized current track index to {0}.", index);
                            break;
                        }
                        else
                        {
                            CannotLoadMore = true;
                            UpdateTrackCount();
                            throw new InvalidOperationException("Couldn't find current track!");
                        }
                    }

                    if (contextQueue != null)
                    {
                        Queue.AddRange(contextQueue.Tracks);
                        IsPlayingQueue = contextQueue.IsPlayingQueue;
                        UpdateState();
                    }

                    CheckComplete();
                    if (_wrapper.AreAllUnplayable(Tracks))
                        throw AbsSpotifyContext.UnsupportedContextException.CannotPlayAnything();

                    try
                    {
                        if (track != null) EnrichCurrentTrack(track);
                    }
                    catch (InvalidOperationException ex)
                    {
                        LOGGER.Warn("Failed updating current track metadata.", ex);
                    }

                    if (!_wrapper.ShouldPlay(Tracks[(int)GetCurrentTrackIndex()]))
                    {
                        LOGGER.Debug("Cannot play currently selected track, skipping: " +
                                     _wrapper.GetCurrentPlayable());

                        bool repeatTrack = _wrapper.IsRepeatingTrack();
                        if (repeatTrack) _wrapper._state.Options.RepeatingTrack = false;
                        _wrapper.GetNextPlayable(false);
                        _wrapper._state.Options.RepeatingTrack = repeatTrack;
                    }
                }
            }

            private void EnrichCurrentTrack(ContextTrack track)
            {
                if (IsPlayingQueue)
                {
                    ProvidedTrack builder = _wrapper._state.Track;
                    ProtoUtils.EnrichTrack(builder, track);
                }
                else
                {
                    int index = (int)GetCurrentTrackIndex();
                    ContextTrack current = Tracks[index];
                    ProtoUtils.EnrichTrack(current, track);
                    Tracks[index] = current;
                    _wrapper._state.Track = ProtoUtils.ToProvidedTrack(current, _wrapper.GetContextUri());
                }
            }
            
            internal void SkipTo(ContextTrack track)
            {
                lock (_funcLock)
                {
                    if (Queue.Count != 0)
                    {
                        List<ContextTrack> queueCopy = new List<ContextTrack>(Queue);

                        foreach (ContextTrack ctxTrack in new List<ContextTrack>(queueCopy))
                            if (ProtoUtils.TrackEquals(ctxTrack, track))
                            {
                                IsPlayingQueue = true;
                                UpdateState();
                                return;
                            }
                            else queueCopy.Remove(ctxTrack);

                        Queue.Clear();
                        Queue.AddRange(queueCopy);
                    }

                    for (int i = 0; i < Tracks.Count; i++)
                    {
                        if (ProtoUtils.TrackEquals(Tracks[i], track))
                        {
                            SetCurrentTrackIndex((uint)i);
                            EnrichCurrentTrack(track);
                            return;
                        }
                    }

                    throw new InvalidOperationException("Did not find track to skip to: " + ProtoUtils.ToString(track));
                }
            }
            
            internal PlayableIdWithIndex NextPlayableDoNotSet()
            {
                lock (_funcLock)
                {
                    if (_wrapper.IsRepeatingTrack())
                        return new PlayableIdWithIndex(PlayableId.From(Tracks[(int)GetCurrentTrackIndex()]),
                            (int)GetCurrentTrackIndex());

                    if (Queue.Count != 0)
                        return new PlayableIdWithIndex(PlayableId.From(Queue[0]), -1);

                    int current = (int)GetCurrentTrackIndex();
                    if (current == Tracks.Count - 1)
                    {
                        if (_wrapper.IsShufflingContext() || CannotLoadMore) return null;

                        if (_wrapper._pages.NextPage())
                        {
                            Tracks.AddRange(_wrapper._pages.CurrentPage());
                        }
                        else
                        {
                            CannotLoadMore = true;
                            UpdateTrackCount();
                            return null;
                        }
                    }

                    if (!_wrapper._context.IsFinite() && Tracks.Count - current <= 5)
                    {
                        if (_wrapper._pages.NextPage())
                        {
                            Tracks.AddRange(_wrapper._pages.CurrentPage());
                        }
                        else
                        {
                            LOGGER.Warn("Couldn't (pre)load next page of context!");
                        }
                    }

                    int add = 1;
                    while (true)
                    {
                        ContextTrack track = Tracks[current + add];
                        if (_wrapper.ShouldPlay(track)) break;
                        add++;
                    }

                    return new PlayableIdWithIndex(PlayableId.From(Tracks[current + add]), current + add);
                }
            }

            internal NextPlayable NextPlayable(bool autoplayEnabled)
            {
                lock (_funcLock)
                {
                    if (_wrapper.IsRepeatingTrack())
                        return StateWrapper.NextPlayable.OK_REPEAT;

                    if (Queue.Count != 0)
                    {
                        IsPlayingQueue = true;
                        UpdateState();

                        if (!_wrapper.ShouldPlay(Tracks[(int)GetCurrentTrackIndex()]))
                            return NextPlayable(autoplayEnabled);

                        return StateWrapper.NextPlayable.OK_PLAY;
                    }

                    IsPlayingQueue = false;

                    bool play = true;
                    PlayableIdWithIndex next = NextPlayableDoNotSet();
                    if (next == null || next.Index == -1)
                    {
                        if (!_wrapper._context.IsFinite()) return StateWrapper.NextPlayable.MISSING_TRACKS;

                        if (_wrapper.IsRepeatingContext())
                        {
                            SetCurrentTrackIndex(0);
                        }
                        else
                        {
                            if (autoplayEnabled)
                            {
                                return StateWrapper.NextPlayable.AUTOPLAY;
                            }
                            else
                            {
                                SetCurrentTrackIndex(0);
                                play = false;
                            }
                        }
                    }
                    else
                    {
                        SetCurrentTrackIndex((uint)next.Index);
                    }

                    if (play) return StateWrapper.NextPlayable.OK_PLAY;
                    else return StateWrapper.NextPlayable.OK_PAUSE;
                }
            }
            
            internal PreviousPlayable PreviousPlayable()
            {
                lock (_funcLock)
                {
                    int index = (int)GetCurrentTrackIndex();
                    if (IsPlayingQueue)
                    {
                        index += 1;
                        IsPlayingQueue = false;
                    }

                    if (index == 0)
                    {
                        if (_wrapper.IsRepeatingContext() && _wrapper._context.IsFinite())
                            SetCurrentTrackIndex((uint)Tracks.Count - 1);
                    }
                    else
                    {
                        SetCurrentTrackIndex((uint)index - 1);
                    }

                    if (!_wrapper.ShouldPlay(Tracks[(int)GetCurrentTrackIndex()]))
                        return PreviousPlayable();

                    return StateWrapper.PreviousPlayable.OK;
                }
            }
            
            internal bool IsPlayingFirst()
            {
                lock (_funcLock)
                {
                    return GetCurrentTrackIndex() == 0;
                }
            }

            internal bool IsPlayingLast()
            {
                lock (_funcLock)
                {
                    if (CannotLoadMore && Queue.Count == 0) return GetCurrentTrackIndex() == Tracks.Count;
                    else return false;
                }
            }

            private bool LoadAllTracks()
            {
                if (!_wrapper._context.IsFinite()) throw new InvalidOperationException();

                try
                {
                    while (true)
                    {
                        if (_wrapper._pages.NextPage()) Tracks.AddRange(_wrapper._pages.CurrentPage());
                        else break;
                    }
                }
                catch (Exception e)
                {
                    if (e is IOException || e is MercuryClient.MercuryException)
                    {
                        LOGGER.ErrorExt("Failed loading all tracks!", e);
                        return false;
                    }
                    else throw;
                }

                CannotLoadMore = true;
                UpdateTrackCount();

                return true;
            }

            internal void ShuffleEntirely()
            {
                lock (_funcLock)
                {
                    if (!_wrapper._context.IsFinite())
                        throw new InvalidOperationException("Cannot shuffle infinite context!");
                    if (Tracks.Count <= 1) return;
                    if (IsPlayingQueue) return;

                    if (!CannotLoadMore)
                    {
                        if (!LoadAllTracks())
                        {
                            LOGGER.Error("Cannot shuffle entire context!");
                            return;
                        }
                    }

                    Shuffle.Shuffle(Tracks, true);
                }
            }

            internal void ToggleShuffle(bool value)
            {
                lock (_funcLock)
                {
                    if (!_wrapper._context.IsFinite())
                        throw new InvalidOperationException("Cannot shuffle infinite context!");
                    if (Tracks.Count <= 1) return;
                    if (IsPlayingQueue) return;

                    if (value)
                    {
                        if (!CannotLoadMore)
                        {
                            if (!LoadAllTracks())
                            {
                                LOGGER.Error("Cannot shuffle context!");
                                return;
                            }
                        }

                        IPlayableId currentlyPlaying = _wrapper.GetCurrentPlayableOrThrow();
                        Shuffle.Shuffle(Tracks, true);
                        ShuffleKeepIndex = PlayableId.IndexOfTrack(Tracks, currentlyPlaying);
                        Utils.Swap(Tracks, 0, ShuffleKeepIndex);
                        SetCurrentTrackIndex(0);

                        LOGGER.Debug("Shuffled context! (keepIndex: " + ShuffleKeepIndex + ")");
                    }
                    else
                    {
                        if (Shuffle.CanUnshuffle(Tracks.Count))
                        {
                            IPlayableId currentlyPlaying = _wrapper.GetCurrentPlayableOrThrow();
                            if (ShuffleKeepIndex != -1) Utils.Swap(Tracks, 0, ShuffleKeepIndex);

                            Shuffle.Unshuffle(Tracks);
                            SetCurrentTrackIndex((uint)PlayableId.IndexOfTrack(Tracks, currentlyPlaying));

                            LOGGER.Debug("Unshuffled using Fisher-Yates");
                        }
                        else
                        {
                            IPlayableId id = _wrapper.GetCurrentPlayableOrThrow();

                            Tracks.Clear();
                            _wrapper._pages = PagesLoader.From(_wrapper._session, _wrapper._context.Uri());
                            LoadAllTracks();

                            SetCurrentTrackIndex((uint)PlayableId.IndexOfTrack(Tracks, id));
                            LOGGER.Debug("Unshuffled by reloading context.");
                        }
                    }
                }
            }

            public int Length()
            {
                return Tracks.Count;
            }

            internal void AddToTracks(int from, List<Item> items)
            {
                if (!CannotLoadMore)
                {
                    if (!LoadAllTracks())
                    {
                        LOGGER.Warn("Cannot add new tracks!");
                        return;
                    }
                }

                for (int i = 0; i < items.Count; i++)
                {
                    Item item = items[i];
                    IPlayableId playable = PlayableId.FromUri(item.Uri);

                    ContextTrack builder = new ContextTrack
                    {
                        Uri = item.Uri
                    };

                    if (playable.HasGid())
                        builder.Gid = playable.GetGid();

                    Tracks.Insert(i + from, builder);
                }

                if (!IsPlayingQueue && from <= GetCurrentTrackIndex())
                    ShiftCurrentTrackIndex(items.Count);

                UpdatePrevNextTracks();
            }

            internal void RemoveTracks(int from, int length)
            {
                if (!CannotLoadMore)
                {
                    if (!LoadAllTracks())
                    {
                        LOGGER.Warn("Cannot remove tracks!");
                        return;
                    }
                }

                bool removeCurrent = false;
                int curr = (int)GetCurrentTrackIndex();
                if (from <= curr && length + from > curr)
                    removeCurrent = true;

                ContextTrack current = Tracks[curr];
                for (int i = 0; i < length; i++)
                    Tracks.RemoveAt(from);

                if (!removeCurrent && from <= curr)
                    ShiftCurrentTrackIndex(-length);

                if (removeCurrent)
                {
                    ShiftCurrentTrackIndex(-1);

                    Queue.Insert(0, current);
                    IsPlayingQueue = true;
                    UpdateState();
                }
                else UpdatePrevNextTracks();
            }
            
            internal void MoveTracks(int from, int to, int length)
            {
                if (from == to) return;

                for (int counter = length; counter > 0; counter--)
                {
                    ContextTrack toMove = Tracks[from];
                    Tracks.RemoveAt(from);

                    int newTo = to - (to > from ? 1 : 0);
                    
                    Tracks.Insert(newTo, toMove);
                    
                    uint curr = GetCurrentTrackIndex();
                    if (from < curr && newTo >= curr) 
                        ShiftCurrentTrackIndex(-1);
                    else if (from > curr && newTo <= curr)
                        ShiftCurrentTrackIndex(1);
                    else if (from == curr) 
                        ShiftCurrentTrackIndex((int) (newTo - curr));

                    if (from > to)
                    {
                        from++;
                        to++;
                    }
                }
                
                UpdatePrevNextTracks();
            }
            
            internal void UpdateMetadataFor(int index, String key, String value)
            {
                lock (_funcLock)
                {
                    ContextTrack builder = Tracks[index];
                    if (builder.Metadatas.ContainsKey(key)) builder.Metadatas.Remove(key);
                    builder.Metadatas.Add(key, value);
                    Tracks[index] = builder;
                }
            }

            internal void UpdateMetadataFor(String uri, String key, String value)
            {
                lock (_funcLock)
                {
                    int index = ProtoUtils.IndexOfTrackByUri(Tracks, uri);
                    if (index == -1) return;

                    UpdateMetadataFor(index, key, value);
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using dacp;
using deps.HttpSharp;
using lib.audio;
using lib.common;
using lib.json;
using lib.mercury;
using lib.metadata;
using log4net;
using log4net.Util;
using Newtonsoft.Json.Linq;
using player.contexts;
using player.metrics;
using player.mixing;
using player.playback;
using player.state;
using ProtoBuf;
using spotify.metadata.proto;
using spotify.player.proto;
using spotify.player.proto.transfer;
using HttpRequest = deps.HttpSharp.HttpRequest;
using Session = lib.core.Session;

namespace player
{
    public class Player : IDisposable
    {
        public static int VOLUME_MAX = 65536;
        private static ILog LOGGER = LogManager.GetLogger(typeof(Player));
        private ScheduledExecutorService _scheduler;
        private Session _session;
        private PlayerConfiguration _conf;
        private EventsDispatcher _events;
        private AudioSink _sink;
        private Dictionary<String, PlaybackMetrics> _metrics = new Dictionary<string, PlaybackMetrics>();
        private StateWrapper _state;
        private PlayerSession _playerSession;
        private ScheduledExecutorService.ScheduledFuture<int> _releaseLineFuture = null;
        private DeviceStateHandler.Listener _deviceStateListener;

        public Player(PlayerConfiguration conf, Session session)
        {
            _conf = conf;
            _session = session;
            _scheduler = _session.GetScheduledExecutorService();
            _events = new EventsDispatcher(conf, _scheduler, this);
            _sink = new AudioSink(_conf);

            InitState();
        }

        public void AddEventsListener(IEventsListener listener)
        {
            _events._listeners.Add(listener);
        }

        public void RemoveEventsListener(IEventsListener listener)
        {
            _events._listeners.Remove(listener);
        }

        private void InitState()
        {
            _state = new StateWrapper(_session, this, _conf);
            _state.AddListener(_deviceStateListener = new DeviceStateListenerImpl(this));
        }

        private class DeviceStateListenerImpl : DeviceStateHandler.Listener
        {
            private Player _player;

            public DeviceStateListenerImpl(Player player)
            {
                _player = player;
            }
            
            public void Ready()
            {
                _player._events.VolumeChanged((int)_player._state.GetVolume());
            }

            public void Command(DeviceStateHandler.Endpoint endpoint, DeviceStateHandler.CommandBody data)
            {
                LOGGER.Debug("Receive command: " + endpoint);

                if (endpoint.Equals(DeviceStateHandler.Endpoint.Play))
                {
                    _player.HandlePlay(data.GetObj());
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.Transfer))
                {
                    _player.HandleTransferState(Serializer.Deserialize<TransferState>(new MemoryStream(data.GetData())));
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.Resume))
                {
                    _player.HandleResume();
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.Pause))
                {
                    _player.HandlePause();
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.SeekTo))
                {
                    _player.HandleSeek(data.GetValueInt().Value);
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.SkipNext))
                {
                    _player.HandleSkipNext(data.GetObj(), TransitionInfo.SkippedNext(_player._state));
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.SkipPrev))
                {
                    _player.HandleSkipPrev();
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.SetRepeatingContext))
                {
                    _player._state.SetRepeatingContext(data.GetValueBool().Value);
                    _player._state.Updated();
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.SetShufflingContext))
                {
                    _player._state.SetRepeatingContext(data.GetValueBool().Value);
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.AddToQueue))
                {
                    _player.HandleAddToQueue(data.GetObj());
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.SetQueue))
                {
                     _player.HandleSetQueue(data.GetObj());
                } else if (endpoint.Equals(DeviceStateHandler.Endpoint.UpdateContext))
                {
                    _player._state.UpdateContext(DeviceStateHandler.PlayCommandHelper.GetContext(data.GetObj()));
                    _player._state.Updated();
                }
                else
                {
                    LOGGER.Warn("Endpoint left unhandled: " + endpoint);
                }
            }

            public void VolumeChanged()
            {
                int vol = (int) _player._state.GetVolume();
                if (!_player._conf._bypassSinkVolume) _player._sink.SetVolume(vol);
                _player._events.VolumeChanged(vol);
            }

            public void NotActive()
            {
                _player._events.InactiveSession(false);
                _player._sink.Pause();
            }
        }
        
        // ================================ //
        // =========== Commands =========== //
        // ================================ //

        public void VolumeUp()
        {
            VolumeUp(1);
        }

        public void VolumeUp(int steps)
        {
            if (_state == null) return;
            SetVolume((int) Math.Min(Player.VOLUME_MAX, _state.GetVolume() + steps * OneVolumeStep()));
        }

        public void VolumeDown()
        {
            VolumeDown(1);
        }

        public void VolumeDown(int steps)
        {
            if (_state == null) return;
            SetVolume((int) Math.Max(0, _state.GetVolume() - steps * OneVolumeStep()));
        }

        private int OneVolumeStep()
        {
            return VOLUME_MAX / _conf._volumeSteps;
        }

        public void SetVolume(int val)
        {
            if (val < 0 || val > VOLUME_MAX)
                throw new InvalidOperationException(val.ToString());

            if (_state == null) return;
            _state.SetVolume((uint)val);
        }

        public void SetShuffle(bool val)
        {
            _state.SetShufflingContext(val);
            _state.Updated();
        }

        public void SetRepeat(bool track, bool context)
        {
            if (track && context) 
                throw new InvalidOperationException("Cannot repeat track and context simultaneously.");

            if (track)
            {
                _state.SetRepeatingTrack(true);
            } else if (context)
            {
                _state.SetRepeatingContext(true);
            }
            else
            {
                _state.SetRepeatingContext(false);
                _state.SetRepeatingTrack(false);
            }
            
            _state.Updated();
        }

        public void Play()
        {
            HandleResume();
        }

        public void PlayPause()
        {
            if (_state.IsPaused()) HandleResume();
            else HandlePause();
        }

        public void Pause()
        {
            HandlePause();
        }

        public void Next()
        {
            HandleSkipNext(null, TransitionInfo.SkippedNext(_state));
        }

        public void Previous()
        {
            HandleSkipPrev();
        }

        public void Seek(int pos)
        {
            HandleSeek(pos);
        }

        public void Load(String uri, bool play, bool shuffle)
        {
            try
            {
                String sessionId = _state.LoadContext(uri);
                _events.ContextChanged();
                
                _state.SetShufflingContext(shuffle);

                LoadSession(sessionId, play, true);
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is MercuryClient.MercuryException)
                {
                    LOGGER.ErrorExt("Failed loading context!", ex);
                    PanicState(null);
                }
                else if (ex is AbsSpotifyContext.UnsupportedContextException)
                {
                    LOGGER.ErrorExt("Cannot play context!", ex);
                    PanicState(null);
                }
                else throw;
            }
        }

        public void AddToQueue(String uri)
        {
            _state.AddToQueue(new ContextTrack
            {
                Uri = uri
            });
            _state.Updated();
        }

        public void RemoveFromQueue(String uri)
        {
            _state.RemoveFromQueue(uri);
            _state.Updated();
        }

        public CompletableFuture<Player> Ready()
        {
            CompletableFuture<Player> future = new CompletableFuture<Player>();
            if (IsReady())
            {
                future.Complete(this);
                return future;
            }

            PlayerFutureImpl playerFutureImpl = null;
            playerFutureImpl = new PlayerFutureImpl(() =>
            {
                _state.RemoveListener(playerFutureImpl);
                future.Complete(this);
            });
            
            _state.AddListener(playerFutureImpl);

            return future;
        }

        public void WaitReady()
        {
            Ready().Get();
        }
        
        // ================================ //
        // ======== Internal state ======== //
        // ================================ //

        private void PanicState(PlaybackMetrics.Reason reason)
        {
            _sink.Pause();
            _state.SetState(false, false, false);
            _state.Updated();

            if (reason == null)
            {
                _metrics.Clear();
            } else if (_playerSession != null)
            {
                EndMetrics(_playerSession.CurrentPlaybackId(), reason, _playerSession.CurrentMetrics(),
                    _state.GetPosition());
            }
            
            _events.PanicState();
        }

        private void LoadSession(String sessionId, bool play, bool withSkip)
        {
            LOGGER.DebugFormat("Loading session, id: {0}, play: {1}", sessionId, play);

            TransitionInfo trans = TransitionInfo.ContextChange(_state, withSkip);

            if (_playerSession != null)
            {
                EndMetrics(_playerSession.CurrentPlaybackId(), trans._endedReason, _playerSession.CurrentMetrics(),
                    trans._endedWhen);
                
                _playerSession.Dispose();
                _playerSession = null;
            }

            _playerSession = new PlayerSession(_session, _sink, _conf, sessionId, new PlayerSessionListenerImpl(this));
        }

        private void LoadTrack(bool play, TransitionInfo trans)
        {
            EndMetrics(_playerSession.CurrentPlaybackId(), trans._endedReason, _playerSession.CurrentMetrics(),
                trans._endedWhen);

            LOGGER.DebugFormat("Loading track, id: {0}, session: {1}, playback: {2}, play: {3}",
                _state.GetCurrentPlayable(), _playerSession.CurrentPlaybackId(), play);
            String playbackId = _playerSession.Play(_state.GetCurrentPlayableOrThrow(), _state.GetPosition(),
                trans._startedReason);
            _state.SetPlaybackId(playbackId);
            _session.GetEventService().SendEvent(new NewPlaybackIdEvent(_state.GetSessionId(), playbackId));
            
            if (play) _sink.Resume();
            else _sink.Pause();
            
            _state.SetState(true, !play, true);
            _state.Updated();
            
            _events.TrackChanged(true);
            if (play) _events.PlaybackResumed();
            else _events.PlaybackPaused();

            StartMetrics(playbackId, trans._startedReason, _state.GetPosition());

            if (_releaseLineFuture != null)
            {
                _releaseLineFuture.Cancel();
                _releaseLineFuture = null;
            }
        }

        private void HandlePlay(JObject obj)
        {
            LOGGER.Debug("Loading context (play), uri: " + DeviceStateHandler.PlayCommandHelper.GetContextUri(obj));

            try
            {
                String sessionId = _state.Load(obj);
                _events.ContextChanged();

                bool? paused = DeviceStateHandler.PlayCommandHelper.IsInitiallyPaused(obj);
                if (!paused.HasValue) paused = false;
                LoadSession(sessionId, !paused.Value, DeviceStateHandler.PlayCommandHelper.WillSkipToSomething(obj));
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is MercuryClient.MercuryException)
                {
                    LOGGER.ErrorExt("Failed loading context!", ex);
                    PanicState(null);
                } else if (ex is AbsSpotifyContext.UnsupportedContextException)
                {
                    LOGGER.ErrorExt("Cannot play context!", ex);
                    PanicState(null);
                }
            }
        }

        private void HandleTransferState(TransferState cmd)
        {
            LOGGER.Debug("Loading context (transfer), uri: " + cmd.CurrentSession.Context.Uri);

            try
            {
                String sessionId = _state.Transfer(cmd);
                _events.ContextChanged();
                LoadSession(sessionId, !cmd.Playback.IsPaused, true);
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is MercuryClient.MercuryException)
                {
                    LOGGER.ErrorExt("Failed loading context!", ex);
                    PanicState(null);
                } else if (ex is AbsSpotifyContext.UnsupportedContextException)
                {
                    LOGGER.ErrorExt("Cannot play context!", ex);
                    PanicState(null);
                }
            }
        }

        private void HandleSeek(int pos)
        {
            _playerSession.SeekCurrent(pos);
            _state.SetPosition(pos);
            _events.Seeked(pos);
            
            PlaybackMetrics pm = _metrics[_playerSession.CurrentPlaybackId()];
            if (pm != null)
            {
                pm.EndInterval(_state.GetPosition());
                pm.StartInterval(pos);
            }
        }

        private void HandleResume()
        {
            if (_state.IsPaused())
            {
                _state.SetState(true, false, false);
                _sink.Resume();
                
                _state.Updated();
                _events.PlaybackResumed();

                if (_releaseLineFuture != null)
                {
                    _releaseLineFuture.Cancel();
                    _releaseLineFuture = null;
                }
            }
        }

        private void HandlePause()
        {
            if (!_state.IsPaused())
            {
                _state.SetState(true, true, false);
                _sink.Pause();
                
                if (_playerSession != null) 
                    _state.SetPosition(_playerSession.CurrentTime());
                
                _state.Updated();
                _events.PlaybackPaused();
                
                if (_releaseLineFuture != null) _releaseLineFuture.Cancel();
                _releaseLineFuture = new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    if (!_state.IsActive()) return 0;
                    
                    _events.InactiveSession(true);
                    _sink.Pause();
                    return 0;
                }, _conf._releaseLineDelay);
                _scheduler.schedule(_releaseLineFuture);
            }
        }

        private void HandleSetQueue(JObject obj)
        {
            List<ContextTrack> prevTracks = DeviceStateHandler.PlayCommandHelper.GetPrevTracks(obj);
            List<ContextTrack> nextTracks = DeviceStateHandler.PlayCommandHelper.GetNextTracks(obj);
            if (prevTracks == null && nextTracks == null) throw new InvalidOperationException();
            
            _state.SetQueue(prevTracks, nextTracks);
            _state.Updated();
        }

        private void HandleAddToQueue(JObject obj)
        {
            ContextTrack track = DeviceStateHandler.PlayCommandHelper.GetTrack(obj);
            if (track == null) throw new InvalidOperationException();
            
            _state.AddToQueue(track);
            _state.Updated();
        }

        private void HandleSkipNext(JObject obj, TransitionInfo trans)
        {
            ContextTrack track = null;
            if (obj != null) track = DeviceStateHandler.PlayCommandHelper.GetTrack(obj);

            if (track != null)
            {
                _state.SkipTo(track);
                LoadTrack(true, TransitionInfo.SkipTo(_state));
                return;
            }

            StateWrapper.NextPlayable next = _state.GetNextPlayable(_conf._autoplayEnabled);
            if (next == StateWrapper.NextPlayable.AUTOPLAY)
            {
                LoadAutoplay();
                return;
            }

            if (next.IsOk())
            {
                trans._endedWhen = _state.GetPosition();
                
                _state.SetPosition(0);
                LoadTrack(next == StateWrapper.NextPlayable.OK_PLAY || next == StateWrapper.NextPlayable.OK_REPEAT,
                    trans);
            }
            else
            {
                LOGGER.Error("Failed loading next song: " + next);
                PanicState(PlaybackMetrics.Reason.EndPlay);
            }
        }
        
        private void HandleSkipPrev() {
            if (_state.GetPosition() < 3000)
            {
                StateWrapper.PreviousPlayable prev = _state.GetPreviousPlayable();
                if (prev.IsOk())
                {
                    _state.SetPosition(0);
                    LoadTrack(true, TransitionInfo.SkippedPrev(_state));
                }
                else
                {
                    LOGGER.Error("Failed loading previous song: " + prev);
                    PanicState(null);
                }
            }
            else
            {
                _playerSession.SeekCurrent(0);
                _state.SetPosition(0);
                _state.Updated();
            }
        }

        private void LoadAutoplay()
        {
            String context = _state.GetContextUri();
            if (context == null)
            {
                LOGGER.Error("Cannot load autoplay with null context!");
                PanicState(null);
                return;
            }

            String contextDesc = _state.GetContextMetadata("context_description");

            try
            {
                MercuryClient.Response resp = _session.GetMercury().SendSync(MercuryRequests.AutoplayQuery(context));
                if (resp.StatusCode == 200)
                {
                    String newContext = resp.Payload.ReadIntoString(0);
                    String sessionId = _state.LoadContext(newContext);
                    _state.SetContextMetadata("context_description", contextDesc);
                    
                    _events.ContextChanged();
                    LoadSession(sessionId, true, false);
                    
                    LOGGER.Debug("Loading context for autoplay, uri: " + newContext);
                } else if (resp.StatusCode == 204)
                {
                    StationsWrapper station = _session.GetMercury().SendSync(MercuryRequests.GetStationFor(context));
                    String sessionId = _state.LoadContextWithTracks(station.Uri(), station.Tracks());
                    _state.SetContextMetadata("context_description", contextDesc);
                    
                    _events.ContextChanged();
                    LoadSession(sessionId, true, false);
                    
                    LOGGER.Debug("Loading context for autoplay (using radio-apollo), uri: " + _state.GetContextUri());
                }
                else
                {
                    LOGGER.Error("Failed retrieving autoplay context, code: " + resp.StatusCode);
                    
                    _state.SetPosition(0);
                    _state.SetState(true, false, false);
                    _state.Updated();
                }
            }
            catch (Exception ex)
            {
                if (ex is MercuryClient.MercuryException && ((MercuryClient.MercuryException) ex).Code == 400)
                {
                    LOGGER.Info("Cannot load autoplay search context: " + context);
                    
                    _state.SetPosition(0);
                    _state.SetState(true, true, false);
                    _state.Updated();
                } else if (ex is AbsSpotifyContext.UnsupportedContextException)
                {
                    LOGGER.ErrorExt("Cannot play context!", ex);
                    PanicState(null);
                }
                else
                {
                    LOGGER.ErrorExt("Failed loading autoplay station!", ex);
                    PanicState(null);
                }
            }
        }
        
        // ================================ //
        // =========== Metrics ============ //
        // ================================ //

        private void StartMetrics(String playbackId, PlaybackMetrics.Reason reason, int pos)
        {
            PlaybackMetrics pm = new PlaybackMetrics(_state.GetCurrentPlayableOrThrow(), playbackId, _state);
            pm.StartedHow(reason, _state.GetPlayOrigin().FeatureIdentifier);
            pm.StartInterval(pos);
            _metrics.Add(playbackId, pm);
        }

        private void EndMetrics(String playbackId, PlaybackMetrics.Reason reason, PlayerMetrics playerMetrics, int when)
        {
            if (playbackId == null) return;

            PlaybackMetrics pm = _metrics.TryGetValue(playbackId, out PlaybackMetrics mp) ? mp : null;
            if (mp == null) return;
            _metrics.Remove(playbackId);
            
            pm.EndedHow(reason, _state.GetPlayOrigin().FeatureIdentifier);
            pm.EndInterval(when);
            pm.Update(playerMetrics);
            pm.SendEvents(_session, _state.GetDevice());
        }
        
        // ================================ //
        // =========== Getters ============ //
        // ================================ //

        public IPlayableId CurrentPlayable()
        {
            return _state.GetCurrentPlayable();
        }

        public bool IsActive()
        {
            return _state != null && _state.IsActive();
        }

        public bool IsReady()
        {
            return _state != null && _state.IsReady();
        }

        public Tracks GetTracks(bool withQueue)
        {
            return new Tracks(_state.GetPrevTracks(), _state.GetCurrentTrack(), _state.GetNextTracks(withQueue));
        }

        public MetadataWrapper CurrentMetadata()
        {
            return _playerSession == null ? null : _playerSession.CurrentMetadata();
        }

        public byte[] CurrentCoverImage()
        {
            MetadataWrapper metadata = CurrentMetadata();
            if (metadata == null) return null;

            ImageId image = null;
            ImageGroup group = metadata.GetCoverImage();
            if (group == null)
            {
                IPlayableId id = _state.GetCurrentPlayable();
                if (id == null) return null;

                Dictionary<String, String> map = _state.MetadataFor(id);
                if (map == null) return null;
                
                foreach (String key in ImageId.ImageSizesUrlsStrings)
                {
                    if (map.ContainsKey(key))
                    {
                        image = (ImageId) ImageId.FromUri(map[key]);
                    }
                }
            }
            else
            {
                image = (ImageId)ImageId.BiggestImage(group);
            }

            if (image == null)
                return null;
            
            HttpResponse resp = _session.GetClient().NewCall(new HttpRequest(_session
                .GetUserAttribute("image-url", "https://i.scdn.co/image/{file_id}")
                .Replace("{file_id}", image.HexId()), HttpMethod.Get));
            byte[] data;
            if (resp.StatusCode == HttpStatusCode.OK && (data = resp.GetResponseBytes()) != null)
                return data;
            else throw new IOException(String.Format("Bad response code. (id: {0}, code: {1})", image.HexId(), resp.StatusCode));
        }

        public int Time()
        {
            return _playerSession == null ? -1 : _playerSession.CurrentTime();
        }
        
        // ================================ //
        // ============ Close! ============ //
        // ================================ //

        public void Dispose()
        {
            if (_playerSession != null)
            {
                EndMetrics(_playerSession.CurrentPlaybackId(), PlaybackMetrics.Reason.Logout,
                    _playerSession.CurrentMetrics(), _state.GetPosition());
                _playerSession.Dispose();
            }
            
            _state.Dispose();
            
            _sink.Dispose();
            if (_state != null && _deviceStateListener != null)
                _state.RemoveListener(_deviceStateListener);
            
            _events.Close();

            LOGGER.Info("Closed player.");
        }

        private class PlayerSessionListenerImpl : PlayerSession.IListener
        {
            private Player _player;

            public PlayerSessionListenerImpl(Player player)
            {
                _player = player;
            }
            
            public IPlayableId CurrentPlayable()
            {
                return _player._state.GetCurrentPlayableOrThrow();
            }

            public IPlayableId NextPlayable()
            {
                StateWrapper.NextPlayable next = _player._state.GetNextPlayable(_player._conf._autoplayEnabled);
                if (next == StateWrapper.NextPlayable.AUTOPLAY)
                {
                    _player.LoadAutoplay();
                    return null;
                }

                if (next.IsOk())
                {
                    if (next != StateWrapper.NextPlayable.OK_PLAY && next != StateWrapper.NextPlayable.OK_REPEAT) 
                        _player._sink.Pause();

                    return _player._state.GetCurrentPlayableOrThrow();
                }
                else
                {
                    LOGGER.Error("Failed loading next song: " + next);
                    _player.PanicState(PlaybackMetrics.Reason.EndPlay);
                    return null;
                }
            }
            
            public IPlayableId NextPlayableDoNotSet()
            {
                return _player._state.NextPlayableDoNotSet();
            }

            public Dictionary<string, string> MetadataFor(IPlayableId playableId)
            {
                return _player._state.MetadataFor(playableId);
            }

            public void PlaybackHalted(int chunk)
            {
                LOGGER.DebugFormat("Playback halted on retrieving chunk {0}.", chunk);
                _player._state.SetBuffering(true);
                _player._state.Updated();
                
                _player._events.PlaybackHaltStateChanged(true);
            }

            public void PlaybackResumedFromHalt(int chunk, long diff)
            {
                LOGGER.DebugFormat("Playback resumed, chunk {0} retrieved, took {1}ms", chunk, diff);
                _player._state.SetPosition(_player._state.GetPosition() - diff);
                _player._state.SetBuffering(false);
                _player._state.Updated();
                
                _player._events.PlaybackHaltStateChanged(false);
            }

            public void StartedLoading()
            {
                if (!_player._state.IsPaused())
                {
                    _player._state.SetBuffering(true);
                    _player._state.Updated();  
                }
                
                _player._events.StartedLoading();
            }

            public void LoadingError(Exception ex)
            {
                _player._events.PlaybackFailed(ex);
                if (ex is PlayableContentFeeder.ContentRestrictedException)
                {
                    LOGGER.ErrorExt("Can't load track (content restricted).", ex);
                }
                else
                {
                    LOGGER.ErrorExt("Failed loading track.", ex);
                    _player.PanicState(PlaybackMetrics.Reason.TrackError);
                }
            }

            public void FinishedLoading(MetadataWrapper metadata)
            {
                _player._state.EnrichWithMetadata(metadata);
                _player._state.SetBuffering(false);
                _player._state.Updated();
                
                _player._events.FinishedLoading();
                _player._events.MetadataAvailable();
            }

            public void PlaybackError(Exception ex)
            {
                if (ex is AbsChunkedInputStream.ChunkException)
                    LOGGER.ErrorExt("Failed retrieving chunk, playback failed!", ex);
                else 
                    LOGGER.ErrorExt("Playback error!", ex);
            }

            public void TrackChanged(string playbackId, MetadataWrapper metadata, int pos, PlaybackMetrics.Reason startedReason)
            {
                if (metadata != null) _player._state.EnrichWithMetadata(metadata);
                _player._state.SetPlaybackId(playbackId);
                _player._state.SetPosition(pos);
                _player._state.Updated();
                
                _player._events.TrackChanged(false);
                _player._events.MetadataAvailable();

                _player._session.GetEventService()
                    .SendEvent(new NewPlaybackIdEvent(_player._state.GetSessionId(), playbackId));
                _player.StartMetrics(playbackId, startedReason, pos);
            }

            public void TrackPlayed(string playbackId, PlaybackMetrics.Reason endReason, PlayerMetrics playerMetrics, int when)
            {
                _player.EndMetrics(playbackId, endReason, playerMetrics, when);
                _player._events.PlaybackEnded();
            }
        }

        private class PlayerFutureImpl : DeviceStateHandler.Listener
        {
            public delegate void OnComplete();
            
            private OnComplete _onComplete;

            public PlayerFutureImpl(OnComplete onComplete)
            {
                _onComplete = onComplete;
            }
            
            public void Ready()
            {
                _onComplete();
            }

            public void Command(DeviceStateHandler.Endpoint endpoint, DeviceStateHandler.CommandBody data)
            {
            }

            public void VolumeChanged()
            {
            }

            public void NotActive()
            {
            }
        }
            
        public interface IEventsListener
        {
            void OnContextChanged(Player player, String newUri);

            void OnTrackChanged(Player player, IPlayableId id, MetadataWrapper metadata);

            void OnPlaybackEnded(Player player);

            void OnPlaybackPaused(Player player, long trackTime);
            
            void OnPlaybackResumed(Player player, long trackTime);
            
            void OnPlaybackFailed(Player player, Exception ex);
            
            void OnTrackSeeked(Player player, long trackTime);
            
            void OnMetadataAvailable(Player player, MetadataWrapper metadata);

            void OnPlaybackHaltStateChanged(Player player, bool halted, long trackTime);

            void OnInactiveSession(Player player, bool timeout);

            void OnVolumeChanged(Player player, float volume);

            void OnPanicState(Player player);

            void OnStartedLoading(Player player);

            void OnFinishedLoading(Player player);
        }

        public class Tracks
        {
            public List<ContextTrack> _previous;
            public ContextTrack _current;
            public List<ContextTrack> _next;

            internal Tracks(List<ContextTrack> previous, ContextTrack current, List<ContextTrack> next)
            {
                _previous = previous;
                _current = current;
                _next = next;
            }
        }

        private class TransitionInfo
        {
            internal PlaybackMetrics.Reason _startedReason;
            internal PlaybackMetrics.Reason _endedReason;
            internal int _endedWhen = -1;

            private TransitionInfo(PlaybackMetrics.Reason endedReason, PlaybackMetrics.Reason startedReason)
            {
                _startedReason = startedReason;
                _endedReason = endedReason;
            }

            internal static TransitionInfo ContextChange(StateWrapper state, bool withSkip)
            {
                TransitionInfo trans = new TransitionInfo(PlaybackMetrics.Reason.EndPlay,
                    withSkip ? PlaybackMetrics.Reason.ClickRow : PlaybackMetrics.Reason.PlayBtn);
                if (state.GetCurrentPlayable() != null) trans._endedWhen = state.GetPosition();
                return trans;
            }

            internal static TransitionInfo SkipTo(StateWrapper state)
            {
                TransitionInfo trans =
                    new TransitionInfo(PlaybackMetrics.Reason.EndPlay, PlaybackMetrics.Reason.ClickRow);
                if (state.GetCurrentPlayable() != null) trans._endedWhen = state.GetPosition();
                return trans;
            }

            internal static TransitionInfo SkippedPrev(StateWrapper state)
            {
                TransitionInfo trans =
                    new TransitionInfo(PlaybackMetrics.Reason.BackBtn, PlaybackMetrics.Reason.BackBtn);
                if (state.GetCurrentPlayable() != null) trans._endedWhen = state.GetPosition();
                return trans;
            }

            internal static TransitionInfo SkippedNext(StateWrapper state)
            {
                TransitionInfo trans =
                    new TransitionInfo(PlaybackMetrics.Reason.ForwardBtn, PlaybackMetrics.Reason.ForwardBtn);
                if (state.GetCurrentPlayable() != null) trans._endedWhen = state.GetPosition();
                return trans;
            }
        }

        private class EventsDispatcher
        {
            private ScheduledExecutorService _executorService;
            internal List<IEventsListener> _listeners = new List<IEventsListener>();
            private Player _player;

            internal EventsDispatcher(PlayerConfiguration conf, ScheduledExecutorService executorService, Player player)
            {
                _player = player;
                _executorService = executorService; 
                if (conf._metadataPipe != null)
                {
                    _listeners.Add(new DacpListener(new DacpMetadataPipe(conf._metadataPipe)));
                }
            }

            private class DacpListener : IEventsListener
            {
                private DacpMetadataPipe _dacpPipe;

                public DacpListener(DacpMetadataPipe dacpPipe)
                {
                    _dacpPipe = dacpPipe;
                }
                
                public void OnContextChanged(Player player, string newUri)
                {
                }

                public void OnTrackChanged(Player player, IPlayableId id, MetadataWrapper metadata)
                {
                }

                public void OnPlaybackEnded(Player player)
                {
                }

                public void OnPlaybackPaused(Player player, long trackTime)
                {
                    _dacpPipe.SendPipeFlush();
                }

                public void OnPlaybackResumed(Player player, long trackTime)
                {
                    MetadataWrapper metadata = player.CurrentMetadata();
                    if (metadata == null) return;
                    
                    OnMetadataAvailable(player, metadata);
                }

                public void OnPlaybackFailed(Player player, Exception ex)
                {
                }

                public void OnTrackSeeked(Player player, long trackTime)
                {
                    _dacpPipe.SendPipeFlush();

                    MetadataWrapper metadata = player.CurrentMetadata();
                    if (metadata == null) return;

                    PlayerMetrics playerMetrics = player._playerSession.CurrentMetrics();
                    if (playerMetrics == null) return;
                    
                    _dacpPipe.SendProgress(player.Time(), metadata.Duration(), playerMetrics._sampleRate);
                }
                
                public void OnMetadataAvailable(Player player, MetadataWrapper metadata)
                {
                    _dacpPipe.SendTrackInfo(metadata.GetName(), metadata.GetAlbumName(), metadata.GetArtist());
                    
                    PlayerMetrics playerMetrics = player._playerSession.CurrentMetrics();
                    if (playerMetrics != null) 
                        _dacpPipe.SendProgress(player.Time(), metadata.Duration(), playerMetrics._sampleRate);

                    try
                    {
                        _dacpPipe.SendImage(player.CurrentCoverImage());
                    }
                    catch (IOException ex)
                    {
                        LOGGER.ErrorExt("Failed getting cover image.", ex);
                    }
                }

                public void OnPlaybackHaltStateChanged(Player player, bool halted, long trackTime)
                {
                }

                public void OnInactiveSession(Player player, bool timeout)
                {
                }

                public void OnVolumeChanged(Player player, float volume)
                {
                    _dacpPipe.SendVolume(volume);
                }

                public void OnPanicState(Player player)
                {
                }

                public void OnStartedLoading(Player player)
                {
                }

                public void OnFinishedLoading(Player player)
                {
                    _dacpPipe.SendPipeFlush();
                }
            }

            private delegate void Run();
            
            private void Schedule(Run run) {
                _executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    run();
                    return 0;
                }, 50, ScheduledExecutorService.TimeUnit.MILLISECONDS));
            }

            internal void PlaybackEnded()
            {
                _listeners.ForEach(l => Schedule(() => l.OnPlaybackEnded(_player)));
            }

            internal void PlaybackPaused()
            {
                _listeners.ForEach(l => Schedule(() => l.OnPlaybackPaused(_player, _player._state.GetPosition())));
            }

            internal void PlaybackResumed()
            {
                _listeners.ForEach(l => Schedule(() => l.OnPlaybackResumed(_player, _player._state.GetPosition())));
            }

            internal void PlaybackFailed(Exception ex)
            {
                _listeners.ForEach(l => Schedule(() => l.OnPlaybackFailed(_player, ex)));
            }

            internal void ContextChanged()
            {
                String uri = _player._state.GetContextUri();
                if (uri == null) return;
                
                _listeners.ForEach(l => Schedule(() => l.OnContextChanged(_player, uri)));
            }

            internal void StartedLoading()
            {
                _listeners.ForEach(l => Schedule(() => l.OnStartedLoading(_player)));
            }

            internal void FinishedLoading()
            {
                _listeners.ForEach(l => Schedule(() => l.OnFinishedLoading(_player)));
            }

            internal void TrackChanged(bool userInitiated)
            {
                IPlayableId id = _player._state.GetCurrentPlayable();
                if (id == null) return;

                MetadataWrapper metadata = _player.CurrentMetadata();
                _listeners.ForEach(l => Schedule(() => l.OnTrackChanged(_player, id, metadata)));
            }

            internal void Seeked(int pos)
            {
                _listeners.ForEach(l => Schedule(() => l.OnTrackSeeked(_player, pos)));
            }

            internal void VolumeChanged(int value)
            {
                float volume = (float)value / VOLUME_MAX;
                
                _listeners.ForEach(l => Schedule(() => l.OnVolumeChanged(_player, volume)));
            }

            internal void MetadataAvailable()
            {
                MetadataWrapper metadata = _player.CurrentMetadata();
                if (metadata == null) return;
                
                _listeners.ForEach(l => Schedule(() => l.OnMetadataAvailable(_player, metadata)));
            }

            internal void PlaybackHaltStateChanged(bool halted)
            {
                long trackTime = _player._state.GetPosition();
                _listeners.ForEach(l => Schedule(() => l.OnPlaybackHaltStateChanged(_player, halted, trackTime)));
            }

            internal void InactiveSession(bool timeout)
            {
                _listeners.ForEach(l => Schedule(() => l.OnInactiveSession(_player, timeout)));
            }

            internal void PanicState()
            {
                _listeners.ForEach(l => Schedule(() => l.OnPanicState(_player)));
            }

            public void Close()
            {
                _listeners.ForEach(l =>
                {
                    try
                    {
                        l.GetType().GetMethod("Close").Invoke(l, null);
                    }
                    catch (Exception ignored)
                    {
                    }
                });
                
                _listeners.Clear();
            }
        }
    }
}
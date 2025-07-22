using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using decoder_api;
using lib.audio;
using lib.audio.cdn;
using lib.audio.decoders;
using lib.common;
using lib.core;
using lib.mercury;
using lib.metadata;
using log4net;
using player.crossfade;
using player.metrics;
using player.mixing;

namespace player.playback
{
    public sealed class PlayerQueueEntry : PlayerQueue.Entry, IDisposable, IHaltListener
    {
        public const int INSTANT_PRELOAD = 1;
        public const int INSTANT_START_NEXT = 2;
        public const int INSTANT_END = 3;
        private static ILog LOGGER = LogManager.GetLogger(typeof(PlayerQueueEntry));
        internal IPlayableId Playable;
        internal string PlaybackId;
        private PlayerConfiguration _conf;
        private bool _preloaded;
        private IListener _listener;
        private object _playbackLock = new object();
        private SortedDictionary<int, int> _notifyInstants = new SortedDictionary<int, int>();
        private AudioSink _sink;
        private Session _session;
        internal CrossfadeController Crossfade;
        internal PlaybackMetrics.Reason EndReason = PlaybackMetrics.Reason.EndPlay;
        private Decoder _decoder;
        private IDecodedAudioStream _audioStream;
        private MetadataWrapper _metadata;
        private volatile bool _closed = false;
        private volatile MixingLine.MixingOutput _output;
        private long _playbackHaltedAt = 0;
        private volatile int _seekTime = -1;
        private bool _retried = false;
        private PlayableContentFeeder.Metrics _contentMetrics;

        internal PlayerQueueEntry(AudioSink sink, Session session, PlayerConfiguration conf, IPlayableId playable,
            bool preloaded, IListener listener)
        {
            _sink = sink;
            _session = session;
            _conf = conf;
            Playable = playable;
            _preloaded = preloaded;
            _listener = listener;
        }

        public PlayerQueueEntry RetrySelf(bool preloaded)
        {
            if (_retried) throw new InvalidOperationException();

            var retry = new PlayerQueueEntry(_sink, _session, _conf, Playable, preloaded, _listener) { _retried = true };
            return retry;
        }

        private void Load(bool preload)
        {
            PlayableContentFeeder.LoadedStream stream;
            if (Playable is LocalId localId)
                stream = PlayableContentFeeder.LoadedStream.ForLocalFile(localId,
                    Path.Combine(_conf._localFilesPath, localId.Name()),
                    _session.GetScheduledExecutorService());
            else
                stream = _session.GetContentFeeder().Load(Playable, new VorbisOnlyAudioQuality(_conf._preferredQuality),
                    preload, this);

            _metadata = stream.Metadata;
            _contentMetrics = stream.Metrics;
            _audioStream = stream.In;

            if (_metadata.IsEpisode() && _metadata._episode != null)
            {
                LOGGER.InfoFormat("Loaded episode. (name: '{0}', duration: {1}, uri: {2}, id: {3})", _metadata._episode.Name,
                    _metadata._episode.Duration, Playable.ToSpotifyUri(), PlaybackId);
            } else if (_metadata.IsTrack() && _metadata._track != null)
            {
                LOGGER.InfoFormat("Loaded track. (name: '{0}', artists: '{1}', duration: {2}, uri: {3}, id: {4})", _metadata._track.Name,
                    Utils.ArtistsToString(_metadata._track.Artists), _metadata._track.Duration, Playable.ToSpotifyUri(), PlaybackId);
            } else if (Playable is LocalId)
            {
                LocalId vLocalId = Playable as LocalId;
                LOGGER.InfoFormat("Loaded local file. (filename: '{0}', duration: '{1}', uri: '{2}', id: '{3}'",
                    vLocalId.Name(), vLocalId.Duration(), Playable.ToSpotifyUri(), PlaybackId);
            }

            Crossfade = new CrossfadeController(PlaybackId, _metadata.Duration(), _listener.MetadataFor(Playable),
                _conf);
            if (Crossfade.HasAnyFadeOut() || _conf._preloadEnabled)
                NotifyInstant(INSTANT_PRELOAD, Crossfade.FadeOutStartTimeMin() - 20000);

            float normalizationFactor;
            if (stream.NormalizationData == null || !_conf._enableNormalisation) normalizationFactor = 1;
            else
                normalizationFactor =
                    stream.NormalizationData.GetFactor(_conf._normalisationPregain, _conf._useAlbumGain);

            _decoder = Decoders.InitDecoder(stream.In.Codec(), stream.In.Stream(), normalizationFactor,
                _metadata.Duration());

            LOGGER.DebugFormat("Loaded {0} decoder: {1} (of: {2}, format: {3}, playbackId: {4}",
                stream.In.Codec(), _decoder.GetType().Name, stream.In.Describe(), _decoder.GetAudioFormat(),
                PlaybackId);
        }

        public MetadataWrapper Metadata() => _metadata;

        public PlayerMetrics Metrics() => new PlayerMetrics(_contentMetrics, Crossfade, _audioStream, _decoder);

        public int GetTime() => _decoder == null ? -1 : _decoder.Time();

        public int GetTimeNoThrow()
        { 
            return GetTime();
        }

        public void Seek(int pos)
        {
            _seekTime = pos;
            _output?.EmptyBuffer();
        }

        public void SetOutput(MixingLine.MixingOutput output)
        {
            if (_closed || HasOutput())
            {
                output.Clear();
                throw new InvalidOperationException($"Cannot set output for {this}");
            }

            lock (_playbackLock)
            {
                _output = output;
                Monitor.PulseAll(_playbackLock);
            }
        }

        private void ClearOutput()
        {
            if (_output != null)
            {
                MixingLine.MixingOutput tmp = _output;
                _output = null;

                tmp.Toggle(false, null);
                tmp.Clear();
            }

            lock (_playbackLock)
            {
                Monitor.PulseAll(_playbackLock);
            }
        }

        public bool HasOutput() => _output != null;

        public void NotifyInstant(int callbackId, int when)
        {
            if (_decoder != null)
            {
                int time = _decoder.Time(); 
                if (time >= when) 
                { 
                    _listener.InstantReached(this, callbackId, time); 
                    return;
                }
            }

            _notifyInstants[when] = callbackId;
        }

        public void Run()
        {
            _listener.StartedLoading(this);

            try
            {
                Load(_preloaded);
            }
            catch (Exception ex) when (
                ex is IOException || ex is PlayableContentFeeder.ContentRestrictedException ||
                ex is CdnManager.CdnException || ex is MercuryClient.MercuryException || ex is Decoder.DecoderException)
            {
                Dispose();
                _listener.LoadingError(this, ex, _retried);
                return;
            }

            if (_seekTime != -1)
            {
                _decoder.Seek(_seekTime);
                _seekTime = -1;
            }

            _listener.FinishedLoading(this, _metadata);

            bool canGetTime = true;
            while (!_closed)
            {
                if (_output == null)
                {
                    lock (_playbackLock)
                    {
                        try
                        {
                            Monitor.Wait(_playbackLock);
                        }
                        catch (ThreadInterruptedException)
                        {
                            break;
                        }
                    }

                    if (_output == null) continue;
                }

                if (_closed) break;
                _output.Toggle(true, _decoder.GetAudioFormat());

                if (_seekTime != -1)
                {
                    _decoder.Seek(_seekTime);
                    _seekTime = -1;
                }

                if (canGetTime)
                {
                    int time = _decoder.Time();
                    if (_notifyInstants.Any()) CheckInstants(time); 
                    if (_output == null) continue; 
                    _output.SetGain(Crossfade.GetGain(time));
                }

                try
                {
                    if (_decoder.WriteSomeTo(_output) == -1)
                    {
                        Dispose();
                        break;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is Decoder.DecoderException)
                {
                    if (!_closed)
                    {
                        Dispose();
                        _listener.PlaybackError(this, ex);
                        return;
                    }

                    break;
                }
            }

            _output?.Toggle(false, null);
            _listener.PlaybackEnded(this);
        }

        private void CheckInstants(int time)
        {
            while (_notifyInstants.Any() && time >= _notifyInstants.Keys.First())
            {
                int key = _notifyInstants.Keys.First();
                int callbackId = _notifyInstants[key];
                _notifyInstants.Remove(key);
                _listener.InstantReached(this, callbackId, time);
            }
        }

        public bool CloseIfUseless()
        {
            if (!HasOutput())
            {
                Dispose();
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            _closed = true;
            ClearOutput();
            _decoder?.Dispose();
        }

        public void StreamReadHalted(int chunk, long time)
        {
            _playbackHaltedAt = time;
            _listener.PlaybackHalted(this, chunk);
        }

        public void StreamReadResumed(int chunk, long time)
        {
            if (_playbackHaltedAt == 0) return;

            int duration = (int)(time - _playbackHaltedAt);
            _listener.PlaybackResumed(this, chunk, duration);
        }

        public override string ToString() => $"PlayerQueueEntry{{{PlaybackId}}}";
        
        internal interface IListener
        {
            void PlaybackError(PlayerQueueEntry entry, Exception ex);
            
            void PlaybackEnded(PlayerQueueEntry entry);

            void PlaybackHalted(PlayerQueueEntry entry, int chunk);

            void PlaybackResumed(PlayerQueueEntry entry, int chunk, int diff);

            void InstantReached(PlayerQueueEntry entry, int callbackId, int exactTime);
            
            void StartedLoading(PlayerQueueEntry entry);

            void LoadingError(PlayerQueueEntry entry, Exception ex, bool retried);

            void FinishedLoading(PlayerQueueEntry entry, MetadataWrapper metadata);
            
            Dictionary<String, String> MetadataFor(IPlayableId playable);
        }
    }
}
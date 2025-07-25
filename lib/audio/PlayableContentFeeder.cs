using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using deps.HttpSharp;
using lib.audio.cdn;
using lib.audio.format;
using lib.audio.storage;
using lib.common;
using lib.core;
using lib.dealer;
using lib.metadata;
using log4net;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using spotify.download.proto;
using spotify.metadata.proto;
using AudioFile = spotify.metadata.proto.AudioFile;

namespace lib.audio
{
    public class PlayableContentFeeder
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(PlayableContentFeeder));
        private static String STORAGE_RESOLVE_INTERACTIVE = "/storage-resolve/files/audio/interactive/{0}";
        private static String STORAGE_RESOLVE_INTERACTIVE_PREFETCH = "/storage-resolve/files/audio/interactive_prefetch/{0}";
        private Session _session;

        public PlayableContentFeeder(Session session)
        {
            _session = session;
        }

        private static Track PickAlternativeIfNecessary(Track track)
        {
            if (track.Files.Count > 0) return track;
            
            foreach (Track alt in track.Alternatives) {
                if (alt.Files.Count > 0) {
                    track.Files.AddRange(alt.Files);
                    return track;
                }
            }

            return null;
        }

        public LoadedStream Load(IPlayableId id, AudioQualityPicker audioQualityPicker, bool preload,
            IHaltListener haltListener)
        {
            if (id is TrackId)
            {
                return LoadTrack((TrackId) id, audioQualityPicker, preload, haltListener);
            } 
            if (id is EpisodeId)
            {
                return LoadEpisode((EpisodeId) id, audioQualityPicker, preload, haltListener);
            }

            throw new Exception("Unknown content: " + id);
        }

        private StorageResolveResponse ResolveStorageInteractive(byte[] fileId, bool preload)
        {
            HttpResponse resp = _session.GetApi().Send(ApiClient.RequestMethod.GET,
                String.Format(preload ? STORAGE_RESOLVE_INTERACTIVE_PREFETCH : STORAGE_RESOLVE_INTERACTIVE,
                    Utils.bytesToHex(fileId)));

            if (resp.StatusCode != HttpStatusCode.OK)
                throw new IOException(resp.StatusCode + ": " + resp.StatusDescription);
            
            Stream body = resp.GetResponseStream();
            return Serializer.Deserialize<StorageResolveResponse>(body);
        }

        private LoadedStream LoadTrack(TrackId id, AudioQualityPicker audioQualityPicker, bool preload,
            IHaltListener haltListener)
        {
            Track original = _session.GetApi().GetMetadata4Track(id);
            Track track = PickAlternativeIfNecessary(original);
            
            if (track == null)
            {
                String country = _session.GetCountryCode();
                if (country != null) ContentRestrictedException.CheckRestrictions(country, original.Restrictions);
                
                LOGGER.Error("Couldn't find playable track: " + id.ToSpotifyUri());
                throw new FileAudioStream.FeederException();
            }
            
            return LoadTrack(track, audioQualityPicker, preload, haltListener);
        }

        private LoadedStream LoadCdnStream(AudioFile file, Track track, Episode episode, String urlStr, bool preload,
            IHaltListener haltListener)
        {
            if (track == null && episode == null)
                throw new Exception();
            
            Uri url = new Uri(urlStr);
            if (track != null) return CdnFeedHelper.LoadTrack(_session, track, file, url, preload, haltListener);
            return CdnFeedHelper.LoadEpisode(_session, episode, file, url, haltListener);
        }

        private LoadedStream LoadStream(AudioFile file, Track track, Episode episode, bool preload,
            IHaltListener haltListener)
        {
            if (track == null && episode == null)
                throw new Exception();
            
            StorageResolveResponse resp = ResolveStorageInteractive(file.FileId, preload);
            switch (resp.result)
            {
                case StorageResolveResponse.Result.Cdn:
                    if (track != null)
                        return CdnFeedHelper.LoadTrack(_session, track, file, resp, preload, haltListener);
                    return CdnFeedHelper.LoadEpisode(_session, episode, file, resp, haltListener);
                case StorageResolveResponse.Result.Storage:
                    try
                    {
                        if (track != null)
                            return StorageFeedHelper.LoadTrack(_session, track, file, preload, haltListener);
                        return StorageFeedHelper.LoadEpisode(_session, episode, file, preload, haltListener);
                    }
                    catch (AudioFileFetch.StorageNotAvailable ex)
                    {
                        LOGGER.Info("Storage is not available. Going CDN: " + ex._cdnUrl);
                        return LoadCdnStream(file, track, episode, ex._cdnUrl, preload, haltListener);
                    }
                case StorageResolveResponse.Result.Restricted:
                    throw new Exception("Content is restricted!");
                default:
                    throw new Exception("Unknown result: " + resp.result);
            }
        }

        private LoadedStream LoadTrack(Track track, AudioQualityPicker audioQualityPicker, bool preload,
            IHaltListener haltListener)
        {
            AudioFile file = audioQualityPicker.getFile(track.Files);
            if (file == null)
            {
                LOGGER.Error("Couldn't find any suitable audio file, available " + Utils.formatsToString(track.Files));
                throw new FileAudioStream.FeederException();
            }
            
            return LoadStream(file, track, null, preload, haltListener);
        }

        private LoadedStream LoadEpisode(EpisodeId id, AudioQualityPicker audioQualityPicker, bool preload,
            IHaltListener haltListener)
        {
            Episode episode = _session.GetApi().GetMetadata4Episode(id);
            
            if(!episode.ExternalUrl.Equals("")) 
                return CdnFeedHelper.LoadEpisodeExternal(_session, episode, haltListener);
            AudioFile file = audioQualityPicker.getFile(episode.Audioes);
            if (file == null)
            {
                LOGGER.Error("Couldn't find any suitable audio file, available: " + Utils.formatsToString(episode.Audioes));
                throw new FileAudioStream.FeederException();
            }

            return LoadStream(file, null, episode, preload, haltListener);
        }

        private class FileAudioStream : IDecodedAudioStream
        {
            private static ILog LOGGER = LogManager.GetLogger(typeof(FileAudioStream));
            private String _file;
            private FileStream _fileStream;
            private byte[][] _buffer;
            private int _chunks;
            private int _size;
            private bool[] _available;
            private bool[] _requested;
            private ScheduledExecutorService _executorService;

            public FileAudioStream(String file, ScheduledExecutorService executorService)
            {
                _file = file;
                _executorService = executorService;
                _fileStream = File.OpenRead(file);
                
                _size = (int)_fileStream.Length;
                _chunks = (_size * ChannelManager.CHUNK_SIZE - 1) / ChannelManager.CHUNK_SIZE;
                _buffer = new byte[_chunks][];
                _available = new bool[_chunks];
                _requested = new bool[_chunks];
            }

            private class AbsChunkedInputStreamImpl : AbsChunkedInputStream
            {
                private FileAudioStream _fileAudioStream;
                
                public AbsChunkedInputStreamImpl(bool retryOnChunkError, FileAudioStream fileAudioStream) : base(retryOnChunkError)
                {
                    _fileAudioStream = fileAudioStream;
                }

                protected override byte[][] Buffer()
                {
                    return _fileAudioStream._buffer;
                }

                public override int Size()
                {
                    return _fileAudioStream._size;
                }

                protected override bool[] RequestedChunks()
                {
                    return _fileAudioStream._requested;
                }

                protected override bool[] AvailableChunks()
                {
                    return _fileAudioStream._available;
                }

                protected override int Chunks()
                {
                    return _fileAudioStream._chunks;
                }

                protected override void RequestChunkFromStream(int index)
                {
                    _fileAudioStream._executorService.schedule(new ScheduledExecutorService.ScheduledFuture<bool>(() =>
                    {
                        try
                        {
                            _fileAudioStream._fileStream.Seek(index * ChannelManager.CHUNK_SIZE, SeekOrigin.Begin);
                            byte[] buffer = _fileAudioStream._buffer[index];
                            _fileAudioStream._fileStream.Read(buffer, 0, buffer.Length);
                            NotifyChunkAvailable(index);
                        }
                        catch (IOException ex)
                        {
                            NotifyChunkError(index, new ChunkException(ex.Message));
                        }
                        return false;
                    }, 0));
                }

                public void StreamReadHalted(int chunk, long time)
                {
                    LOGGER.WarnFormat("Not dispatching stream read halted event (chunk: {0})", chunk);
                }

                public void StreamReadResumed(int chunk, long time)
                {
                    LOGGER.WarnFormat("Not dispatching stream read resumed event (chunk: {0})", chunk);
                }

                public override void Flush()
                {
                    throw new InvalidOperationException();
                }

                public override void SetLength(long value)
                {
                    throw new InvalidOperationException();
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                    throw new InvalidOperationException();
                }

                public override int Read()
                {
                    return -1;
                }

                public override bool CanRead { get => true; }
                public override bool CanSeek { get => true; }
                public override bool CanWrite { get => false; }
            }
            
            public AbsChunkedInputStream Stream()
            {
                AbsChunkedInputStream stream = new AbsChunkedInputStreamImpl(true, this);
                stream.Initialize();
                return stream;
            }

            public SuperAudioFormat Codec()
            {
                return SuperAudioFormat.MP3; // FIXME: Detect codec
            }

            public string Describe()
            {
                return "{file: " + _file + "}";
            }

            public int DecryptTimeMS()
            {
                return 0;
            }

            public class FeederException : IOException
            {
                public FeederException()
                {
                }
            }
        }
        
        public class ContentRestrictedException : Exception
        {
            public static void CheckRestrictions(String country, List<Restriction> restrictions)
            {
                foreach (Restriction restriction in restrictions)
                    if (IsRestricted(country, restriction))
                        throw new ContentRestrictedException();
            }

            private static bool IsInList(String list, String match)
            {
                for (int i = 0; i < list.Length; i += 2)
                    if (list.Substring(i, i + 2).Equals(match))
                        return true;
                    
                return false;
            }

            private static bool IsRestricted(String countryCode, Restriction restriction)
            {
                if (restriction.CountriesAllowed != null)
                {
                    String allowed = restriction.CountriesAllowed;
                    if (allowed.Equals("")) return true;

                    if (!IsInList(restriction.CountriesForbidden, countryCode))
                        return true;
                }

                if (restriction.CountriesForbidden != null)
                    return IsInList(restriction.CountriesForbidden, countryCode);

                return false;
            }
        }
        
        public class Metrics
        {
            public String FileId;
            public bool PreloadedAudioKey;
            public int AudioKeyTime;

            public Metrics(byte[] fileId, bool preloadedAudioKey, int audioKeyTime)
            {
                FileId = fileId == null ? null : Utils.bytesToHex(fileId).ToLower();
                PreloadedAudioKey = preloadedAudioKey;
                AudioKeyTime = audioKeyTime;

                if (preloadedAudioKey && audioKeyTime != -1)
                    throw new Exception("Illegal state!");
            }
        }
        
        public class LoadedStream
        {
            public MetadataWrapper Metadata;
            public IDecodedAudioStream In;
            public NormalizationData NormalizationData;
            public Metrics Metrics;

            public LoadedStream(Track track, IDecodedAudioStream inStream, NormalizationData normalizationData,
                Metrics metrics)
            {
                Metadata = new MetadataWrapper(track, null, null);
                In = inStream;
                NormalizationData = normalizationData;
                Metrics = metrics;
            }
                
            public LoadedStream(Episode episode, IDecodedAudioStream inStream, NormalizationData normalizationData, Metrics metrics)
            {
                Metadata = new MetadataWrapper(null, episode, null);
                In = inStream;
                NormalizationData = normalizationData;
                Metrics = metrics;
            }

            private LoadedStream(LocalId id, IDecodedAudioStream inStream) {
                Metadata = new MetadataWrapper(null, null, id);
                In = inStream;
                NormalizationData = null;
                Metrics = new Metrics(null, false, 0);
            }

            public static LoadedStream ForLocalFile(LocalId id, String file, ScheduledExecutorService scheduledExecutorService)
            {
                return new LoadedStream(id, new FileAudioStream(file, scheduledExecutorService));
            }
        }
    }
}
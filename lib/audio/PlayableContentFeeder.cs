using System;
using System.Collections.Generic;
using System.IO;
using lib.audio.format;
using lib.audio.storage;
using lib.common;
using lib.core;
using lib.metadata;
using log4net;
using spotify.metadata.proto;

namespace lib.audio
{
    public class PlayableContentFeeder
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(PlayableContentFeeder));
        private static String STORAGE_RESOLVE_INTERACTIVE = "/storage-resolve/files/audio/interactive/%s";
        private static String STORAGE_RESOLVE_INTERACTIVE_PREFETCH = "/storage-resolve/files/audio/interactive_prefetch/%s";
        protected Session Session;

        public PlayableContentFeeder(Session session)
        {
            Session = session;
        }

        private static Track PickAlternativeIfNecessary(Track track)
        {
            if (track.Files.Count > 0) return track;
            
            foreach (Track alt in track.Alternatives) {
                if (alt.Files.Count > 0) {
                    Track trackBuilder = new Track();
                    trackBuilder.Files.RemoveRange(0, alt.Files.Count);
                    trackBuilder.Files.AddRange(alt.Files);
                    return track;
                }
            }

            return null;
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
            private ScheduledExecutorService _executorService = new ScheduledExecutorService();

            public FileAudioStream(String file)
            {
                _file = file;
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
                
                public AbsChunkedInputStreamImpl(bool retryOnChunkError, FileAudioStream fileAudioStream) : base(retryOnChunkError, null)
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
                            NotifyCunkError(index, new ChunkException(ex.Message));
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

                public override bool CanRead { get => true; }
                public override bool CanSeek { get => true; }
                public override bool CanWrite { get => false; }
            }
            
            public AbsChunkedInputStream Stream()
            {
                return new AbsChunkedInputStreamImpl(true, this);
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

                if (preloadedAudioKey && audioKeyTime != 0)
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

            public static LoadedStream ForLocalFile(LocalId id, String file)
            {
                return new LoadedStream(id, new FileAudioStream(file));
            }
        }
    }
}
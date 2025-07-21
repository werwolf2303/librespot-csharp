using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using lib.audio.cdn;
using lib.audio.decrypt;
using lib.audio.format;
using lib.cache;
using lib.common;
using lib.core;
using log4net;

namespace lib.audio.storage
{
    public class AudioFileStreaming : AudioFile, IDecodedAudioStream
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(AudioFileStreaming));
        private CacheManager.Handler _cacheHandler;
        private spotify.metadata.proto.AudioFile _file;
        private byte[] _key;
        private Session _session;
        private IHaltListener _haltListener;
        private ScheduledExecutorService _executorService;
        private int _chunks = -1;
        private ChunksBuffer _chunksBuffer;

        internal AudioFileStreaming(Session session, spotify.metadata.proto.AudioFile file, byte[] key, IHaltListener haltListener)
        {
            _session = session;
            _executorService = _session.GetScheduledExecutorService();
            _haltListener = haltListener;
            _cacheHandler = session.GetCache().GetHandler(Utils.bytesToHex(file.FileId));
            _file = file;
            _key = key;
        }

        public SuperAudioFormat Codec()
        {
            return SuperAudioFormat.get(_file.format);
        }

        public string Describe()
        {
            return "(fileId: " + Utils.bytesToHex(_file.FileId) + ")";
        }

        public int DecryptTimeMS()
        {
            return _chunksBuffer == null ? 0 : _chunksBuffer._audioDecrypt.decryptTimeMs();
        }

        public AbsChunkedInputStream Stream()
        {
            if (_chunksBuffer == null)
                throw new Exception("Stream not open!");
            return _chunksBuffer.Stream();
        }

        internal void RequestChunk(byte[] fileId, int index, AudioFile file)
        {
            if (_cacheHandler == null || !TryCacheChunk(index))
            {
                try
                {
                    _session.GetChannel().requestChunk(fileId, index, file);
                }
                catch (IOException ex)
                {
                    LOGGER.Error("Failed requesting chunk from network, index: " + index, ex);
                    _chunksBuffer._internalStream.NotifyChunkError(index, new AbsChunkedInputStream.ChunkException(ex.Message));
                }
            }
        }

        private bool TryCacheChunk(int index)
        {
            try
            {
                if (!_cacheHandler.HasChunk(index)) return false;
                _cacheHandler.ReadChunk(index, this);
                return true;
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is CacheManager.BadChunkHashException)
                {
                    LOGGER.Error("Failed requesting chunk from cache, index: " + index, ex);
                    return false;
                } 
                throw;
            }
        }

        private bool TryCacheHeaders(AudioFileFetch fetch)
        {
            List<JournalHeader> headers = _cacheHandler.GetAllHeaders();
            if (headers.Count == 0)
                return false;

            JournalHeader cdnHeader;
            if ((cdnHeader = JournalHeader.find(headers, AudioFileFetch.HEADER_CON)) != null)
                throw new AudioFileFetch.StorageNotAvailable(Encoding.UTF8.GetString(cdnHeader.value));

            foreach (JournalHeader header in headers)
                fetch.WriteHeader(header.id, header.value, true);

            return true;
        }

        private AudioFileFetch RequestHeaders()
        {
            AudioFileFetch fetch = new AudioFileFetch(_cacheHandler);
            if (_cacheHandler == null || !TryCacheHeaders(fetch)) 
                RequestChunk(_file.FileId, 0, fetch);
            
            fetch.WaitChunk();
            return fetch;
        }

        internal void Open()
        {
            AudioFileFetch fetch = RequestHeaders();
            int size = fetch.GetSize();
            _chunks = fetch.GetChunks();
            _chunksBuffer = new ChunksBuffer(size, _chunks, this);
        }

        private void RequestChunk(int index)
        {
            RequestChunk(_file.FileId, index, this);
            _chunksBuffer._requested[index] = true;
        }

        public void WriteChunk(byte[] buffer, int chunkIndex, bool cached)
        {
            if (!cached && _cacheHandler != null)
            {
                try
                {
                    _cacheHandler.WriteChunk(buffer, chunkIndex);
                }
                catch (IOException ex)
                {
                    LOGGER.Warn("Failed writing to cache! (index: " + chunkIndex + ")", ex);
                }
            }
            
            _chunksBuffer.WriteChunk(buffer, chunkIndex);
            LOGGER.DebugFormat("Chunk {0}/{1} completed, cached: {2}, fileId: {3}", chunkIndex, _chunks, cached, Utils.bytesToHex(_file.FileId));
        }

        public void WriteHeader(int id, byte[] bytes, bool cached)
        {
            // Not interested
        }

        public void StreamError(int chunkIndex, short code)
        { 
            LOGGER.ErrorFormat("Stream error, index: {0}, code: {1}", chunkIndex, code);
            _chunksBuffer._internalStream.NotifyChunkError(chunkIndex, AbsChunkedInputStream.ChunkException.FromStreamError(code));
        }

        public void Dispose()
        {
            if (_chunksBuffer != null) 
                _chunksBuffer.Dispose();

            if (_cacheHandler != null)
            {
                try
                {
                    _cacheHandler.Dispose();
                }
                catch (IOException ignored)
                {
                }
            }
        }

        private class ChunksBuffer : IDisposable
        {
            private int _size;
            private byte[][] _buffer;
            private bool[] _available;
            internal bool[] _requested;
            internal AudioDecrypt _audioDecrypt;
            internal InternalStream _internalStream;
            private AudioFileStreaming _audioFileStreaming;
            
            internal ChunksBuffer(int size, int chunks, AudioFileStreaming audioFileStreaming)
            {
                _audioFileStreaming = audioFileStreaming;
                _size = size;
                _buffer = new byte[chunks][];
                _available = new bool[chunks];
                _requested = new bool[chunks];
                _audioDecrypt = new AesAudioDecrypt(_audioFileStreaming._key);
                _internalStream =
                    new InternalStream(_audioFileStreaming._session.GetConfiguration()
                        .RetryOnChunkError, this);
                _internalStream.Initialize();
            }

            internal void WriteChunk(byte[] chunk, int chunkIndex)
            {
                if (_internalStream.IsClosed()) return;

                if (chunk.Length != _buffer[chunkIndex].Length)
                    throw new Exception(String.Format("Buffer size mismatch, required: {0}, received: {1}, index: {2}", _buffer[chunkIndex].Length, chunk.Length, chunkIndex));
                
                _buffer[chunkIndex] = chunk;
                _audioDecrypt.decryptChunk(chunkIndex, chunk);
                _internalStream.NotifyChunkAvailable(chunkIndex);
            }

            internal AbsChunkedInputStream Stream()
            {
                return _internalStream;
            }

            public void Dispose()
            {
                _internalStream.Dispose();
                _audioFileStreaming.Dispose();
            }

            internal class InternalStream : AbsChunkedInputStream
            {
                private ChunksBuffer _chunksBuffer;
                
                internal InternalStream(bool retryOnChunkError, ChunksBuffer chunksBuffer) : base(retryOnChunkError)
                {
                    _chunksBuffer = chunksBuffer;
                }

                public override void Flush()
                {
                }

                public override void SetLength(long value)
                {
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                }

                public override bool CanRead
                {
                    get => true;
                }
                public override bool CanSeek
                {
                    get => true;
                }
                public override bool CanWrite
                {
                    get => false;
                }
                public override int Size()
                {
                    return _chunksBuffer._size;
                }

                protected override byte[][] Buffer()
                {
                    return _chunksBuffer._buffer;
                }

                protected override bool[] RequestedChunks()
                {
                    return _chunksBuffer._requested;
                }

                protected override bool[] AvailableChunks()
                {
                    return _chunksBuffer._available;
                }

                protected override int Chunks()
                {
                    return _chunksBuffer._audioFileStreaming._chunks;
                }

                protected override void RequestChunkFromStream(int index)
                {
                    _chunksBuffer._audioFileStreaming._executorService.schedule(
                        new ScheduledExecutorService.ScheduledFuture<int>(
                            () =>
                            {
                                _chunksBuffer._audioFileStreaming.RequestChunk(index);
                                return 0;
                            }, 0));
                }

                public void StreamReadHalted(int chunk, long time)
                {
                    if (_chunksBuffer._audioFileStreaming._haltListener != null)
                        _chunksBuffer._audioFileStreaming._executorService.schedule(
                            new ScheduledExecutorService.ScheduledFuture<int>(
                                () =>
                                {
                                    _chunksBuffer._audioFileStreaming._haltListener.StreamReadHalted(chunk, time);
                                    return 0;
                                }, 0));
                }
                
                public void StreamReadResumed(int chunk, long time)
                {
                    if (_chunksBuffer._audioFileStreaming._haltListener != null)
                        _chunksBuffer._audioFileStreaming._executorService.schedule(
                            new ScheduledExecutorService.ScheduledFuture<int>(
                                () =>
                                {
                                    _chunksBuffer._audioFileStreaming._haltListener.StreamReadResumed(chunk, time);
                                    return 0;
                                }, 0));
                }
            }
        }
    }
}
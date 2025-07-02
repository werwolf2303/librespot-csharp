using System;
using System.IO;
using System.Text;
using System.Threading;
using lib.cache;
using lib.common;
using log4net;

namespace lib.audio.storage
{
    public class AudioFileFetch : AudioFile
    {
        public static byte HEADER_SIZE = 0x3;
        public static byte HEADER_CON = 0x4;
        private static ILog LOGGER = LogManager.GetLogger(typeof(AudioFileFetch));
        private CacheManager.Handler _cache;
        private int _size = -1;
        private int _chunks = -1;
        private volatile bool _closed = false;
        private AbsChunkedInputStream.ChunkException _exception = null;

        internal AudioFileFetch(CacheManager.Handler cache)
        {
            _cache = cache;
        }

        public void WriteChunk(byte[] chunk, int chunkIndex, bool cached)
        {
            if (chunkIndex != 0)
                throw new Exception("Illegal state! chunkIndex not zero: " + chunkIndex);
        }

        public void WriteHeader(int id, byte[] bytes, bool cached)
        {
            lock (this)
            {
                if (_closed) return;

                if (!cached && _cache != null)
                {
                    try
                    {
                        _cache.SetHeader(id, bytes);
                    }
                    catch (IOException ex)
                    {
                        if (id == HEADER_SIZE) throw new IOException(ex.Message, ex);
                        LOGGER.WarnFormat("Failed writing header to cache! (id: {0}}", Utils.ByteToHex((byte)id));
                    }
                }

                if (id == HEADER_SIZE)
                {
                    _size = new BinaryReader(new MemoryStream(bytes)).ReadInt32();
                    _size *= 4;
                    _chunks = (_size + ChannelManager.CHUNK_SIZE - 1) / ChannelManager.CHUNK_SIZE;

                    _exception = null;
                    Monitor.PulseAll(this);
                }
                else
                {
                    _exception = new StorageNotAvailable(Encoding.UTF8.GetString(bytes));
                }
            }
        }

        public void StreamError(int chunkIndex, short code)
        {
            lock (this)
            {
                LOGGER.ErrorFormat("Stream error, index: {0}, code: {1}", chunkIndex, code);
                
                _exception = AbsChunkedInputStream.ChunkException.FromStreamError(code);
                Monitor.PulseAll(this);
            }
        }

        void WaitChunk()
        {
            lock (this)
            {
                if (_size != -1) return;

                _exception = null;
                Monitor.Wait(this);

                if (_exception != null)
                    throw _exception;
            }
        }

        public class StorageNotAvailable : AbsChunkedInputStream.ChunkException
        {
            public String _cdnUrl;

            internal StorageNotAvailable(String cdnUrl) : base("")
            {
                _cdnUrl = cdnUrl;
            }
        }

        public int GetSize()
        {
            if (_size == -1) throw new Exception("Illegal state! Headers not received yet!");
            return _size;
        }

        public int GetChunks()
        {
            if (_chunks == -1) throw new Exception("Illegal state! Headers not received yet!");
            return _chunks;
        }

        public void Dispose()
        {
            _closed = true;
        }
    }
}
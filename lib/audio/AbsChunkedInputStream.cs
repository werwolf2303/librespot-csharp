using System;
using System.IO;
using System.Threading;
using decoder_api;
using lib.audio.storage;
using lib.common;

namespace lib.audio
{
    public abstract class AbsChunkedInputStream : SeekableInputStream, IHaltListener
    {
        private static int PRELOAD_AHEAD = 3;
        private static int PRELOAD_CHUNK_RETRIES = 2;
        private static int MAX_CHUNK_TRIES = 128;
        private Object _waitLock = new Object();
        private int[] _retries;
        private bool _retryOnChunkError;
        private volatile int _waitForChunk = -1;
        private volatile ChunkException _chunkException;
        private long _pos = 0;
        private long _mark = 0;
        private volatile bool _closed = false;
        private int _decodedLength = 0;
        private IHaltListener _haltListenerImplementation;

        internal AbsChunkedInputStream(bool retryOnChunkError, Stream baseStream) : base(baseStream)
        {
            _retries = new int[Chunks()];
            _retryOnChunkError = retryOnChunkError;
        }

        public bool IsClosed()
        {
            return _closed;
        }

        protected abstract byte[][] Buffer();

        public override void Close()
        {
            _closed = true;

            lock (_waitLock)
            {
                Monitor.PulseAll(_waitLock);
            }
        }

        public override long Length
        {
            get => Size() - _pos;
        }

        public void Mark(int readAheadLimit)
        {
            _mark = _pos;
        }

        public void Reset()
        {
            _pos = _mark;
        }

        public override long Position
        {
            get => _pos;
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset < 0) throw new ArgumentException("offset must be non-negative");
            if (_closed) throw new IOException("Stream is closed!");
            _pos = offset;

            CheckAvailability((int) _pos / ChannelManager.CHUNK_SIZE, false, false);

            return _pos;
        }

        public override long Skip(long skip)
        {
            if (skip < 0) throw new ArgumentException("skip must be non-negative");
            if (_closed) throw new IOException("Stream is closed!");

            long k = Size() - _pos;
            if (skip < k) k = skip;
            _pos += k;

            int chunk = (int) _pos / ChannelManager.CHUNK_SIZE;
            CheckAvailability(chunk, false, false);

            return k;
        }

        protected abstract bool[] RequestedChunks();
        protected abstract bool[] AvailableChunks();

        protected abstract int Chunks();

        protected abstract void RequestChunkFromStream(int index);

        private bool ShouldRetry(int chunk)
        {
            if (_retries[chunk] < 1) return true;
            if (_retries[chunk] > MAX_CHUNK_TRIES) return false;
            return !_retryOnChunkError;
        }
       
        /// <summary>
        /// Chunk if 'chunk' param is available or wait until it becomes, also handles the retry mechanism.
        /// </summary>
        /// <param name="chunk">The chunk index</param>
        /// <param name="wait">Whether we should wait for 'chunk' to be available</param>
        /// <param name="halted">Whether we have already notified that the retrieving of this chunk is halted</param>
        /// <exception cref="IOException">If we fail to retrieve this chunk and no more retries are available</exception>
        private void CheckAvailability(int chunk, bool wait, bool halted)
        {
            if (halted && !wait) throw new ArgumentException();

            if (!RequestedChunks()[chunk])
            {
                RequestChunkFromStream(chunk);
                RequestedChunks()[chunk] = true;
            }

            for (int i = chunk + 1; i <= Math.Min(Chunks() - 1, chunk + PRELOAD_AHEAD); i++)
            {
                if (!RequestedChunks()[i] && _retries[i] < PRELOAD_CHUNK_RETRIES)
                {
                    RequestChunkFromStream(i);
                    RequestedChunks()[i] = true;
                }
            }

            if (wait)
            {
                if (AvailableChunks()[chunk]) return;

                bool retry = false;
                lock(_waitLock) {
                    if (!halted) StreamReadHalted(chunk, Utils.getUnixTimeStampInMilliseconds());
                    
                    _chunkException = null;
                    _waitForChunk = chunk; 
                    Monitor.Wait(_waitLock);
                    
                    if (_closed) return;

                    if (_chunkException != null)
                    {
                        if (ShouldRetry(chunk)) retry = true;
                        else throw _chunkException;
                    }

                    if (!retry) StreamReadResumed(chunk, Utils.getUnixTimeStampInMilliseconds());
                }

                if (retry)
                {
                    Thread.Sleep((int) Math.Log10(_retries[chunk]) * 1000);

                    CheckAvailability(chunk, true, true); // We must exit the synchronized block!
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_closed) throw new IOException("Stream is closed!");

            if (offset < 0 || count < 0 || count > buffer.Length - offset)
            {
                throw new IndexOutOfRangeException();
            } 
            
            if (count == 0)
            {
                return 0;
            }

            if (_pos >= Size())
                return -1;

            int i = 0;
            while (true)
            {
                long chunk = _pos / ChannelManager.CHUNK_SIZE;
                long chunkOff = _pos % ChannelManager.CHUNK_SIZE;
                
                CheckAvailability((int) chunk, true, false);

                int copy = (int) Math.Min(Buffer()[chunk].Length - chunkOff, count - i);
                byte[] buff = Buffer()[chunk];
                Array.Copy(buff, chunkOff, buffer, offset + i, copy);
                i += copy;
                _pos += copy;

                if (i == count || _pos >= Size())
                    return i;
            }
        }

        public override int Read()
        {
            if (_closed) throw new IOException("Stream is closed!");

            if (_pos >= Size())
                return -1;
            
            long chunk = _pos / ChannelManager.CHUNK_SIZE;
            CheckAvailability((int) chunk, true, false);
            
            return Buffer()[chunk][_pos++ % ChannelManager.CHUNK_SIZE];
        }

        public void NotifyChunkAvailable(int index)
        {
            AvailableChunks()[index] = true;
            _decodedLength += Buffer()[index].Length;

            lock (_waitLock)
            {
                if (index == _waitForChunk && !_closed)
                {
                    _waitForChunk = -1;
                    Monitor.PulseAll(_waitLock);
                }
            }
        }

        public void NotifyChunkError(int index, ChunkException ex)
        {
            AvailableChunks()[index] = false;
            RequestedChunks()[index] = false;
            _retries[index] += 1;

            lock (_waitLock)
            {
                if (index == _waitForChunk && !_closed)
                {
                    _chunkException = ex;
                    _waitForChunk = -1;
                    Monitor.PulseAll(_waitLock);
                }
            }
        }

        public override int DecodedLength()
        {
            return _decodedLength;
        }

        public void StreamReadHalted(int chunk, long time)
        {
        }

        public void StreamReadResumed(int chunk, long time)
        {
        }
        
        
        
        public class ChunkException : IOException {
        
            public ChunkException(String message): base(message) {} 
            public static ChunkException FromStreamError(short streamError) { 
                return new ChunkException("Failed due to stream error, code: " + streamError); 
            }
        }
    }
}
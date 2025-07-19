using System;
using System.IO;
using System.Threading;
using lib.audio.storage; 
using lib.common; 

public abstract class AbsChunkedInputStream : Stream, IDisposable
{
    private static readonly int PRELOAD_AHEAD = 3;
    private static readonly int PRELOAD_CHUNK_RETRIES = 2;
    private static readonly int MAX_CHUNK_TRIES = 128;

    private readonly object _waitLock = new object();
    private readonly bool _retryOnChunkError;
    private int[] _retries;

    private volatile int _waitForChunk = -1;
    private volatile ChunkException _chunkException;
    private long _pos = 0;
    private long _mark = 0;
    private volatile bool _closed = false;
    private int _decodedLength = 0;

    protected AbsChunkedInputStream(bool retryOnChunkError)
    {
        _retryOnChunkError = retryOnChunkError;
    }

    public void Initialize()
    {
        _retries = new int[Chunks()];
    }
    
    protected abstract byte[][] Buffer();
    public abstract int Size();
    protected abstract bool[] RequestedChunks();
    protected abstract bool[] AvailableChunks();
    protected abstract int Chunks();
    protected abstract void RequestChunkFromStream(int index);
    
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => Size();

    public override long Position
    {
        get => _pos;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush() { } 
    
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_closed) throw new ObjectDisposedException(nameof(AbsChunkedInputStream));

        long newPos;
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPos = offset;
                break;
            case SeekOrigin.Current:
                newPos = _pos + offset;
                break;

            case SeekOrigin.End:
                newPos = Length + offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin));
        }

        if (newPos < 0) throw new IOException("An attempt was made to move the position before the beginning of the stream.");

        _pos = newPos;
        
        if (_pos < Length)
        {
            CheckAvailability((int)(_pos / ChannelManager.CHUNK_SIZE), false, false);
        }

        return _pos;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_closed) throw new ObjectDisposedException(nameof(AbsChunkedInputStream));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count > buffer.Length - offset) throw new ArgumentException("Invalid offset and length.");
        if (count == 0) return 0;
        if (_pos >= Size()) return -1;

        int totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            if (_pos >= Size()) break;

            long chunkIndex = _pos / ChannelManager.CHUNK_SIZE;
            long chunkOffset = _pos % ChannelManager.CHUNK_SIZE;

            CheckAvailability((int)chunkIndex, true, false);
            if (_closed) return totalBytesRead;

            byte[] chunkBuffer = Buffer()[(int)chunkIndex];

            long remaining = Size() - _pos;
            int bytesAvailableInChunk = chunkBuffer.Length - (int)chunkOffset;
            int bytesRequested = count - totalBytesRead;
            int bytesToCopy = (int)Math.Min(Math.Min(bytesAvailableInChunk, bytesRequested), remaining);

            Array.Copy(chunkBuffer, chunkOffset, buffer, offset + totalBytesRead, bytesToCopy);

            totalBytesRead += bytesToCopy;
            _pos += bytesToCopy;
        }

        return totalBytesRead;
    }

    public override int ReadByte()
    {
        if (_closed) throw new ObjectDisposedException(nameof(AbsChunkedInputStream));
        if (_pos >= Size()) return -1;

        long chunkIndex = _pos / ChannelManager.CHUNK_SIZE;
        CheckAvailability((int)chunkIndex, true, false);
        if (_closed) return -1;

        return Buffer()[chunkIndex][_pos++ % ChannelManager.CHUNK_SIZE];
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    
    public void Mark()
    {
        _mark = _pos;
    }
    
    public void Reset()
    {
        _pos = _mark;
    }
    
    public bool IsClosed() => _closed;

    private bool ShouldRetry(int chunk)
    {
        if (_retries[chunk] < 1) return true;
        if (_retries[chunk] > MAX_CHUNK_TRIES) return false;
        return !_retryOnChunkError;
    }

    private void CheckAvailability(int chunk, bool wait, bool halted)
    {
        if (halted && !wait) throw new ArgumentException();
        if (_closed) return;

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

        if (wait && !AvailableChunks()[chunk])
        {
            bool retry = false;
            lock (_waitLock)
            {
                if (AvailableChunks()[chunk]) return;

                if (!halted) StreamReadHalted(chunk, Utils.getUnixTimeStampInMilliseconds());

                try
                {
                    _chunkException = null;
                    _waitForChunk = chunk;
                    Monitor.Wait(_waitLock);

                    if (_closed) return;

                    if (_chunkException != null)
                    {
                        if (ShouldRetry(chunk)) retry = true;
                        else throw _chunkException;
                    }
                }
                catch (ThreadInterruptedException ex)
                {
                    throw new IOException("Thread was interrupted.", ex);
                }

                if (!retry) StreamReadResumed(chunk, Utils.getUnixTimeStampInMilliseconds());
            }

            if (retry)
            {
                try
                {
                    Thread.Sleep((int)(Math.Pow(2, _retries[chunk])) * 100);
                }
                catch (ThreadInterruptedException)
                {
                    Thread.CurrentThread.Interrupt();
                }

                CheckAvailability(chunk, true, true);
            }
        }
    }

    public void NotifyChunkAvailable(int index)
    {
        if (index < 0 || index >= AvailableChunks().Length) return;

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
        if (index < 0 || index >= AvailableChunks().Length) return;

        AvailableChunks()[index] = false;
        RequestedChunks()[index] = false;
        _retries[index]++;

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
    
    public int DecodedLength() => _decodedLength;

    public virtual void StreamReadHalted(int chunk, long time) { }
    public virtual void StreamReadResumed(int chunk, long time) { }

    #region IDisposable
    protected override void Dispose(bool disposing)
    {
        if (_closed) return;

        if (disposing)
        {
            
        }

        _closed = true;
        lock (_waitLock)
        {
            Monitor.PulseAll(_waitLock);
        }

        base.Dispose(disposing);
    }
    #endregion

    #region Nested Exception Class
    public class ChunkException : IOException
    {
        public ChunkException(string message) : base(message) { }
        public ChunkException(string message, Exception inner) : base(message, inner) { }

        public static ChunkException FromStreamError(short streamError)
        {
            return new ChunkException("Failed due to stream error, code: " + streamError);
        }
    }
    #endregion
}
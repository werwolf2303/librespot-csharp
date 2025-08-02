using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace lib.audio.playback
{
    public class BlockingStream : Stream
    {
        private readonly Queue<byte> _buffer = new Queue<byte>();
        private readonly object _lock = new object();
        private bool _isCompleted;

        public BlockingStream(int capacity)
        {
            _buffer = new Queue<byte>(capacity);
        }

        public BlockingStream()
        {
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException(); 
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;

            lock (_lock)
            {
                while (true)
                {
                    while (_buffer.Count > 0 && bytesRead < count)
                    {
                        buffer[offset + bytesRead] = _buffer.Dequeue();
                        bytesRead++;
                    }

                    if (bytesRead > 0)
                        return bytesRead;

                    if (_isCompleted)
                        return 0;

                    Monitor.Wait(_lock); 
                }
            }
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                    _buffer.Enqueue(buffer[offset + i]);

                _isCompleted = true;
                Monitor.PulseAll(_lock);
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
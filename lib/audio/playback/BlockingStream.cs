using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using lib.audio;

namespace lib.audio.playback
{
    public class BlockingStream : Stream
    {
        private readonly Queue<byte> _buffer = new Queue<byte>();
        private readonly object _lock = new object();
        private bool _isCompleted;
        private Thread _streamingThread;

        public BlockingStream()
        {
        }

        public BlockingStream(IDecodedAudioStream decodedAudioStream)
        {
            if (decodedAudioStream == null)
                throw new ArgumentNullException(nameof(decodedAudioStream));

            _streamingThread = new Thread(() => StreamFromSource(decodedAudioStream.Stream()))
            {
                IsBackground = true,
                Name = "BlockingStream-Feeder"
            };
            _streamingThread.Start();
        }

        private void StreamFromSource(Stream sourceStream)
        {
            try
            {
                if (sourceStream is AbsChunkedInputStream chunkedStream)
                {
                    chunkedStream.Initialize();
                }

                byte[] tempBuffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = sourceStream.Read(tempBuffer, 0, tempBuffer.Length)) > 0)
                {
                    Write(tempBuffer, 0, bytesRead);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in BlockingStream feeder thread: {ex.Message}");
            }
            finally
            {
                Complete();
            }
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;

            lock (_lock)
            {
                while (bytesRead == 0)
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

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                    _buffer.Enqueue(buffer[offset + i]);

                Monitor.PulseAll(_lock);
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                _isCompleted = true;
                Monitor.PulseAll(_lock);
            }
        }

        public override void Close()
        {
            Complete();
            base.Close();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
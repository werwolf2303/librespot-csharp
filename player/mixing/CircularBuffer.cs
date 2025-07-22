using System;
using System.Threading;
using lib.common;

namespace player.mixing
{
     public class CircularBuffer : IDisposable
    {
        internal readonly object _lock = new object();
        private readonly byte[] _data;
        internal volatile bool _closed = false;
        private int _head;
        private int _tail;

        public CircularBuffer(int bufferSize)
        {
            _data = new byte[bufferSize + 1];
            _head = 0;
            _tail = 0;
        }

        private void AwaitSpace(int count)
        {
            while (Free() < count && !_closed)
            {
                Monitor.Wait(_lock, TimeSpan.FromMilliseconds(100));
            }
        }

        protected void AwaitData(int count)
        {
            while (Available() < count && !_closed)
            {
                Monitor.Wait(_lock, TimeSpan.FromMilliseconds(100));
            }
        }

        public void Write(byte[] b, int off, int len)
        {
            if (_closed) return;
            
            lock (_lock)
            {
                AwaitSpace(len);
                if (_closed) return;

                for (int i = 0; i < len; i++)
                {
                    _data[_tail] = b[off + i];
                    _tail = (_tail + 1) % _data.Length;
                }
                
                Monitor.PulseAll(_lock);
            }
        }

        public void Write(byte value)
        {
            if (_closed) return;

            lock (_lock)
            {
                AwaitSpace(1);
                if (_closed) return;

                _data[_tail] = value;
                _tail = (_tail + 1) % _data.Length;
                
                Monitor.PulseAll(_lock);
            }
        }

        public int Read(byte[] b, int off, int len)
        {
            if (_closed && Available() == 0) return 0;

            lock (_lock)
            {
                AwaitData(1);
                if (_closed && Available() == 0) return 0;
                
                int bytesToRead = Math.Min(len, Available());
                for (int i = 0; i < bytesToRead; i++)
                {
                    b[off + i] = (byte)ReadInternal();
                }
                
                Monitor.PulseAll(_lock);
                return bytesToRead;
            }
        }

        protected int ReadInternal()
        {
            int value = _data[_head];
            _head = (_head + 1) % _data.Length;
            return value;
        }

        public int Read()
        {
            if (_closed && Available() == 0) return -1;

            lock (_lock)
            {
                AwaitData(1);
                if (_closed && Available() == 0) return -1;
                
                int value = ReadInternal();
                Monitor.PulseAll(_lock);
                return value;
            }
        }

        public int Available()
        {
            return (_tail - _head + _data.Length) % _data.Length;
        }

        public int Free()
        {
            return (_data.Length - 1) - Available();
        }

        public bool Full()
        {
            return Available() == _data.Length - 1;
        }

        public void Empty()
        {
            lock (_lock)
            {
                _head = 0;
                _tail = 0;
                Monitor.PulseAll(_lock);
            }
        }

        public void Dispose()
        {
            _closed = true;
            lock (_lock)
            {
                Monitor.PulseAll(_lock);
            }
        }

        public override string ToString()
        {
            return $"CircularBuffer (head: {_head}, tail: {_tail}, data: {Utils.bytesToHex(_data)})";
        }
    }
}
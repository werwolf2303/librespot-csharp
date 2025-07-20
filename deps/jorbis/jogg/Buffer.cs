using System;

namespace deps.jorbis.jogg
{
    public class Buffer
    {
        private const int BufferIncrement = 256;

        private static readonly int[] Mask =
        {
            0x00000000, 0x00000001, 0x00000003,
            0x00000007, 0x0000000f, 0x0000001f, 0x0000003f, 0x0000007f, 0x000000ff,
            0x000001ff, 0x000003ff, 0x000007ff, 0x00000fff, 0x00001fff, 0x00003fff,
            0x00007fff, 0x0000ffff, 0x0001ffff, 0x0003ffff, 0x0007ffff, 0x000fffff,
            0x001fffff, 0x003fffff, 0x007fffff, 0x00ffffff, 0x01ffffff, 0x03ffffff,
            0x07ffffff, 0x0fffffff, 0x1fffffff, 0x3fffffff, 0x7fffffff, -1
        };

        private int _ptr = 0;
        private byte[] _buffer = null;
        private int _endbit = 0;
        private int _endbyte = 0;
        private int _storage = 0;

        public byte[] BufferData => _buffer;

        public void WriteInit()
        {
            _buffer = new byte[BufferIncrement];
            _ptr = 0;
            _buffer[0] = (byte)'\0';
            _storage = BufferIncrement;
        }

        public void Write(byte[] s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == 0)
                    break;
                Write(s[i], 8);
            }
        }

        public void Read(byte[] s, int bytes)
        {
            int i = 0;
            while (bytes-- != 0)
            {
                s[i++] = (byte)Read(8);
            }
        }

        void Reset()
        {
            _ptr = 0;
            _buffer[0] = (byte)'\0';
            _endbit = _endbyte = 0;
        }

        public void WriteClear()
        {
            _buffer = null;
        }

        public void ReadInit(byte[] buf, int bytes)
        {
            ReadInit(buf, 0, bytes);
        }

        public void ReadInit(byte[] buf, int start, int bytes)
        {
            _ptr = start;
            _buffer = buf;
            _endbit = _endbyte = 0;
            _storage = bytes;
        }

        public void Write(int value, int bits)
        {
            if (_endbyte + 4 >= _storage)
            {
                byte[] foo = new byte[_storage + BufferIncrement];
                Array.Copy(_buffer, 0, foo, 0, _storage);
                _buffer = foo;
                _storage += BufferIncrement;
            }

            value &= Mask[bits];
            bits += _endbit;
            _buffer[_ptr] |= (byte)(value << _endbit);

            if (bits >= 8)
            {
                _buffer[_ptr + 1] = (byte)((uint)value >> (8 - _endbit));
                if (bits >= 16)
                {
                    _buffer[_ptr + 2] = (byte)((uint)value >> (16 - _endbit));
                    if (bits >= 24)
                    {
                        _buffer[_ptr + 3] = (byte)((uint)value >> (24 - _endbit));
                        if (bits >= 32)
                        {
                            if (_endbit > 0)
                                _buffer[_ptr + 4] = (byte)((uint)value >> (32 - _endbit));
                            else
                                _buffer[_ptr + 4] = 0;
                        }
                    }
                }
            }

            _endbyte += bits / 8;
            _ptr += bits / 8;
            _endbit = bits & 7;
        }

        public int Look(int bits)
        {
            int ret;
            int m = Mask[bits];

            bits += _endbit;

            if (_endbyte + 4 >= _storage)
            {
                if (_endbyte + (bits - 1) / 8 >= _storage)
                    return -1;
            }

            ret = (int)((uint)_buffer[_ptr] >> _endbit);
            if (bits > 8)
            {
                ret |= (_buffer[_ptr + 1]) << (8 - _endbit);
                if (bits > 16)
                {
                    ret |= (_buffer[_ptr + 2]) << (16 - _endbit);
                    if (bits > 24)
                    {
                        ret |= (_buffer[_ptr + 3]) << (24 - _endbit);
                        if (bits > 32 && _endbit != 0)
                        {
                            ret |= (_buffer[_ptr + 4]) << (32 - _endbit);
                        }
                    }
                }
            }

            return m & ret;
        }

        public int Look1()
        {
            if (_endbyte >= _storage)
                return -1;
            return ((_buffer[_ptr] >> _endbit) & 1);
        }

        public void Adv(int bits)
        {
            bits += _endbit;
            _ptr += bits / 8;
            _endbyte += bits / 8;
            _endbit = bits & 7;
        }

        public void Adv1()
        {
            ++_endbit;
            if (_endbit > 7)
            {
                _endbit = 0;
                _ptr++;
                _endbyte++;
            }
        }

        public int Read(int bits)
        {
            int ret;
            int m = Mask[bits];

            bits += _endbit;

            if (_endbyte + 4 >= _storage)
            {
                ret = -1;
                if (_endbyte + (bits - 1) / 8 >= _storage)
                {
                    _ptr += bits / 8;
                    _endbyte += bits / 8;
                    _endbit = bits & 7;
                    return ret;
                }
            }

            ret = (int)((uint)_buffer[_ptr] >> _endbit);
            if (bits > 8)
            {
                ret |= _buffer[_ptr + 1] << (8 - _endbit);
                if (bits > 16)
                {
                    ret |= _buffer[_ptr + 2] << (16 - _endbit);
                    if (bits > 24)
                    {
                        ret |= _buffer[_ptr + 3] << (24 - _endbit);
                        if (bits > 32 && _endbit != 0)
                        {
                            ret |= _buffer[_ptr + 4] << (32 - _endbit);
                        }
                    }
                }
            }

            ret &= m;

            _ptr += bits / 8;
            _endbyte += bits / 8;
            _endbit = bits & 7;
            return ret;
        }

        public int ReadB(int bits)
        {
            int ret;
            int m = 32 - bits;

            bits += _endbit;

            if (_endbyte + 4 >= _storage)
            {
                ret = -1;
                if (_endbyte * 8 + bits > _storage * 8)
                {
                    _ptr += bits / 8;
                    _endbyte += bits / 8;
                    _endbit = bits & 7;
                    return ret;
                }
            }

            ret = _buffer[_ptr] << (24 + _endbit);
            if (bits > 8)
            {
                ret |= _buffer[_ptr + 1] << (16 + _endbit);
                if (bits > 16)
                {
                    ret |= _buffer[_ptr + 2] << (8 + _endbit);
                    if (bits > 24)
                    {
                        ret |= _buffer[_ptr + 3] << _endbit;
                        if (bits > 32 && (_endbit != 0))
                            ret |= (int)((uint)_buffer[_ptr + 4] >> (8 - _endbit));
                    }
                }
            }

            ret = (int)(((uint)ret >> (m >> 1)) >> ((m + 1) >> 1));

            _ptr += bits / 8;
            _endbyte += bits / 8;
            _endbit = bits & 7;
            return ret;
        }

        public int Read1()
        {
            int ret;
            if (_endbyte >= _storage)
            {
                ret = -1;
                _endbit++;
                if (_endbit > 7)
                {
                    _endbit = 0;
                    _ptr++;
                    _endbyte++;
                }

                return ret;
            }

            ret = (_buffer[_ptr] >> _endbit) & 1;

            _endbit++;
            if (_endbit > 7)
            {
                _endbit = 0;
                _ptr++;
                _endbyte++;
            }

            return ret;
        }

        public int GetBytes()
        {
            return _endbyte + (_endbit + 7) / 8;
        }

        public int GetBits()
        {
            return _endbyte * 8 + _endbit;
        }

        public static int ILog(int v)
        {
            int ret = 0;
            while (v > 0)
            {
                ret++;
                v = (int)((uint)v >> 1);
            }

            return ret;
        }

        public static void Report(string message)
        {
            Console.Error.WriteLine(message);
            Environment.Exit(1);
        }
    }
}

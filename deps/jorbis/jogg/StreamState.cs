using System;
using System.Text;

namespace deps.jorbis.jogg
{
    public class StreamState
    {
        private byte[] _bodyData;
        private int _bodyStorage;
        private int _bodyFill;
        private int _bodyReturned;

        private int[] _lacingVals;
        private long[] _granuleVals;
        private int _lacingStorage;
        private int _lacingFill;
        private int _lacingPacket;
        private int _lacingReturned;

        private readonly byte[] _header = new byte[282];
        private int _headerFill;

        public int IsEndOfStream { get; private set; }
        private bool _isBeginningOfStream;
        private int _serialNo;
        private int _pageNo;
        private long _packetNo;
        private long _granulePos;

        public StreamState()
        {
            Init();
        }

        public StreamState(int serialNo) : this()
        {
            Init(serialNo);
        }

        private void Init()
        {
            _bodyStorage = 16 * 1024;
            _bodyData = new byte[_bodyStorage];
            _lacingStorage = 1024;
            _lacingVals = new int[_lacingStorage];
            _granuleVals = new long[_lacingStorage];
        }

        public void Init(int serialNo)
        {
            if (_bodyData == null)
            {
                Init();
            }
            else
            {
                Array.Clear(_bodyData, 0, _bodyData.Length);
                Array.Clear(_lacingVals, 0, _lacingVals.Length);
                Array.Clear(_granuleVals, 0, _granuleVals.Length);
            }

            _serialNo = serialNo;
        }

        public void Clear()
        {
            _bodyData = null;
            _lacingVals = null;
            _granuleVals = null;
        }

        public void Destroy()
        {
            Clear();
        }

        private void BodyExpand(int needed)
        {
            if (_bodyStorage <= _bodyFill + needed)
            {
                _bodyStorage += (needed + 1024);
                byte[] foo = new byte[_bodyStorage];
                Array.Copy(_bodyData, 0, foo, 0, _bodyData.Length);
                _bodyData = foo;
            }
        }

        private void LacingExpand(int needed)
        {
            if (_lacingStorage <= _lacingFill + needed)
            {
                _lacingStorage += (needed + 32);
                int[] foo = new int[_lacingStorage];
                Array.Copy(_lacingVals, 0, foo, 0, _lacingVals.Length);
                _lacingVals = foo;

                long[] bar = new long[_lacingStorage];
                Array.Copy(_granuleVals, 0, bar, 0, _granuleVals.Length);
                _granuleVals = bar;
            }
        }

        public int PacketIn(Packet op)
        {
            int lacingVal = op.Bytes / 255 + 1;

            if (_bodyReturned != 0)
            {
                _bodyFill -= _bodyReturned;
                if (_bodyFill != 0)
                {
                    Array.Copy(_bodyData, _bodyReturned, _bodyData, 0, _bodyFill);
                }

                _bodyReturned = 0;
            }

            BodyExpand(op.Bytes);
            LacingExpand(lacingVal);

            Array.Copy(op.PacketBase, op.TPacket, _bodyData, _bodyFill, op.Bytes);
            _bodyFill += op.Bytes;

            int j;
            for (j = 0; j < lacingVal - 1; j++)
            {
                _lacingVals[_lacingFill + j] = 255;
                _granuleVals[_lacingFill + j] = _granulePos;
            }

            _lacingVals[_lacingFill + j] = (op.Bytes) % 255;
            _granulePos = _granuleVals[_lacingFill + j] = op.GranulePos;

            _lacingVals[_lacingFill] |= 0x100;

            _lacingFill += lacingVal;

            _packetNo++;

            if (op.EndOfStream != 0)
                IsEndOfStream = 1;
            return 0;
        }

        public int PacketOut(Packet op)
        {
            int ptr = _lacingReturned;

            if (_lacingPacket <= ptr)
            {
                return 0;
            }

            if ((_lacingVals[ptr] & 0x400) != 0)
            {
                _lacingReturned++;
                _packetNo++;
                return -1;
            }

            int size = _lacingVals[ptr] & 0xff;
            int bytes = 0;

            op.PacketBase = _bodyData;
            op.TPacket = _bodyReturned;
            op.EndOfStream = _lacingVals[ptr] & 0x200;
            op.BeginningOfStream = _lacingVals[ptr] & 0x100;
            bytes += size;

            while (size == 255)
            {
                int val = _lacingVals[++ptr];
                size = val & 0xff;
                if ((val & 0x200) != 0)
                    op.EndOfStream = 1;
                bytes += size;
            }

            op.PacketNo = _packetNo;
            op.GranulePos = _granuleVals[ptr];
            op.Bytes = bytes;

            _bodyReturned += bytes;
            _lacingReturned = ptr + 1;

            _packetNo++;
            return 1;
        }

        public int PageIn(Page og)
        {
            byte[] headerBase = og.HeaderBase;
            int header = og.Header;
            byte[] bodyBase = og.BodyBase;
            int body = og.Body;
            int bodySize = og.BodyLength;
            int segPtr = 0;

            int version = og.Version();
            int continued = og.Continued();
            int bos = og.Bos();
            int eos = og.Eos();
            long granulepos = og.GranulePos();
            int serialno = og.Serialno();
            int pageno = og.PageNo();
            int segments = headerBase[header + 26] & 0xff;

            int lr = _lacingReturned;
            int br = _bodyReturned;

            if (br != 0)
            {
                _bodyFill -= br;
                if (_bodyFill != 0)
                {
                    Array.Copy(_bodyData, br, _bodyData, 0, _bodyFill);
                }

                _bodyReturned = 0;
            }

            if (lr != 0)
            {
                if ((_lacingFill - lr) != 0)
                {
                    Array.Copy(_lacingVals, lr, _lacingVals, 0, _lacingFill - lr);
                    Array.Copy(_granuleVals, lr, _granuleVals, 0, _lacingFill - lr);
                }

                _lacingFill -= lr;
                _lacingPacket -= lr;
                _lacingReturned = 0;
            }

            if (serialno != _serialNo) return -1;
            if (version > 0) return -1;

            LacingExpand(segments + 1);

            if (pageno != _pageNo)
            {
                for (int i = _lacingPacket; i < _lacingFill; i++)
                {
                    _bodyFill -= _lacingVals[i] & 0xff;
                }

                _lacingFill = _lacingPacket;

                if (_pageNo != -1)
                {
                    _lacingVals[_lacingFill++] = 0x400;
                    _lacingPacket++;
                }

                if (continued != 0)
                {
                    bos = 0;
                    for (; segPtr < segments; segPtr++)
                    {
                        int val = headerBase[header + 27 + segPtr] & 0xff;
                        body += val;
                        bodySize -= val;
                        if (val < 255)
                        {
                            segPtr++;
                            break;
                        }
                    }
                }
            }

            if (bodySize != 0)
            {
                BodyExpand(bodySize);
                Array.Copy(bodyBase, body, _bodyData, _bodyFill, bodySize);
                _bodyFill += bodySize;
            }

            int saved = -1;
            while (segPtr < segments)
            {
                int val = headerBase[header + 27 + segPtr] & 0xff;
                _lacingVals[_lacingFill] = val;
                _granuleVals[_lacingFill] = -1;

                if (bos != 0)
                {
                    _lacingVals[_lacingFill] |= 0x100;
                    bos = 0;
                }

                if (val < 255)
                    saved = _lacingFill;

                _lacingFill++;
                segPtr++;

                if (val < 255)
                    _lacingPacket = _lacingFill;
            }

            if (saved != -1)
            {
                _granuleVals[saved] = granulepos;
            }

            if (eos != 0)
            {
                IsEndOfStream = 1;
                if (_lacingFill > 0)
                    _lacingVals[_lacingFill - 1] |= 0x200;
            }

            _pageNo = pageno + 1;
            return 0;
        }

        public int Flush(Page og)
        {
            int vals = 0;
            int maxVals = (_lacingFill > 255 ? 255 : _lacingFill);
            int bytes = 0;
            int acc = 0;
            long granulePos = _granuleVals[0];

            if (maxVals == 0) return 0;

            if (!_isBeginningOfStream)
            {
                granulePos = 0;
                for (vals = 0; vals < maxVals; vals++)
                {
                    if ((_lacingVals[vals] & 0x0ff) < 255)
                    {
                        vals++;
                        break;
                    }
                }
            }
            else
            {
                for (vals = 0; vals < maxVals; vals++)
                {
                    if (acc > 4096) break;
                    acc += _lacingVals[vals] & 0x0ff;
                    granulePos = _granuleVals[vals];
                }
            }

            byte[] oggsBytes = Encoding.UTF8.GetBytes("OggS");
            Array.Copy(oggsBytes, 0, _header, 0, 4);

            _header[4] = 0x00;
            _header[5] = 0x00;
            if ((_lacingVals[0] & 0x100) == 0) _header[5] |= 0x01;
            if (!_isBeginningOfStream) _header[5] |= 0x02;
            if (IsEndOfStream != 0 && _lacingFill == vals) _header[5] |= 0x04;
            _isBeginningOfStream = true;

            for (int i = 6; i < 14; i++)
            {
                _header[i] = (byte)granulePos;
                granulePos = (long)((ulong)granulePos >> 8);
            }

            int serialno = _serialNo;
            for (int i = 14; i < 18; i++)
            {
                _header[i] = (byte)serialno;
                serialno = (int)((uint)serialno >> 8);
            }

            if (_pageNo == -1) _pageNo = 0;
            int pageno = _pageNo++;
            for (int i = 18; i < 22; i++)
            {
                _header[i] = (byte)pageno;
                pageno = (int)((uint)pageno >> 8);
            }

            _header[22] = 0;
            _header[23] = 0;
            _header[24] = 0;
            _header[25] = 0;

            _header[26] = (byte)vals;
            for (int i = 0; i < vals; i++)
            {
                _header[i + 27] = (byte)_lacingVals[i];
                bytes += (_header[i + 27] & 0xff);
            }

            og.HeaderBase = _header;
            og.Header = 0;
            og.HeaderLength = _headerFill = vals + 27;
            og.BodyBase = _bodyData;
            og.Body = _bodyReturned;
            og.BodyLength = bytes;

            _lacingFill -= vals;
            Array.Copy(_lacingVals, vals, _lacingVals, 0, _lacingFill);
            Array.Copy(_granuleVals, vals, _granuleVals, 0, _lacingFill);
            _bodyReturned += bytes;

            og.Checksum();

            return 1;
        }

        public int PageOut(Page og)
        {
            if ((IsEndOfStream != 0 && _lacingFill != 0) ||
                _bodyFill - _bodyReturned > 4096 ||
                _lacingFill >= 255 ||
                (_lacingFill != 0 && !_isBeginningOfStream))
            {
                return Flush(og);
            }

            return 0;
        }

        public int Eof()
        {
            return IsEndOfStream;
        }

        public int Reset()
        {
            _bodyFill = 0;
            _bodyReturned = 0;

            _lacingFill = 0;
            _lacingPacket = 0;
            _lacingReturned = 0;

            _headerFill = 0;

            IsEndOfStream = 0;
            _isBeginningOfStream = false;
            _pageNo = -1;
            _packetNo = 0;
            _granulePos = 0;
            return 0;
        }
    }
}
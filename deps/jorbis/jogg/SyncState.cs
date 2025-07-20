using System;

namespace deps.jorbis.jogg
{
    public class SyncState
    {
        public byte[] Data { get; private set; }
        private int _storage;
        private int _fill;
        private int _returned;

        private int _unsynced;
        private int _headerbytes;
        private int _bodybytes;

        private readonly Page _pageseek = new Page();
        private readonly byte[] _checksum = new byte[4];

        public int DataOffset => _returned;
        public int BufferOffset => _fill;

        public void Init()
        {
        }

        public int Clear()
        {
            Data = null;
            return 0;
        }

        public int GetBuffer(int size)
        {
            if (_returned != 0)
            {
                _fill -= _returned;
                if (_fill > 0)
                {
                    Array.Copy(Data, _returned, Data, 0, _fill);
                }

                _returned = 0;
            }

            if (size > _storage - _fill)
            {
                int newsize = size + _fill + 4096;
                if (Data != null)
                {
                    byte[] foo = new byte[newsize];
                    Array.Copy(Data, 0, foo, 0, Data.Length);
                    Data = foo;
                }
                else
                {
                    Data = new byte[newsize];
                }

                _storage = newsize;
            }

            return _fill;
        }

        public int Wrote(int bytes)
        {
            if (_fill + bytes > _storage)
                return -1;
            _fill += bytes;
            return 0;
        }

        public int PageSeek(Page og)
        {
            int page = _returned;
            int next;
            int bytes = _fill - _returned;

            if (_headerbytes == 0)
            {
                if (bytes < 27)
                    return 0;

                if (Data[page] != 'O' || Data[page + 1] != 'g' || Data[page + 2] != 'g' || Data[page + 3] != 'S')
                {
                    _headerbytes = 0;
                    _bodybytes = 0;

                    next = 0;
                    for (int ii = 0; ii < bytes - 1; ii++)
                    {
                        if (Data[page + 1 + ii] == 'O')
                        {
                            next = page + 1 + ii;
                            break;
                        }
                    }

                    if (next == 0)
                        next = _fill;

                    _returned = next;
                    return -(next - page);
                }

                int headerBytesLocal = (Data[page + 26] & 0xff) + 27;
                if (bytes < headerBytesLocal)
                    return 0;

                for (int i = 0; i < (Data[page + 26] & 0xff); i++)
                {
                    _bodybytes += (Data[page + 27 + i] & 0xff);
                }

                _headerbytes = headerBytesLocal;
            }

            if (_bodybytes + _headerbytes > bytes)
                return 0;

            lock (_checksum)
            {
                Array.Copy(Data, page + 22, _checksum, 0, 4);
                Data[page + 22] = 0;
                Data[page + 23] = 0;
                Data[page + 24] = 0;
                Data[page + 25] = 0;

                Page log = _pageseek;
                log.HeaderBase = Data;
                log.Header = page;
                log.HeaderLength = _headerbytes;
                log.BodyBase = Data;
                log.Body = page + _headerbytes;
                log.BodyLength = _bodybytes;
                log.Checksum();

                if (_checksum[0] != Data[page + 22] || _checksum[1] != Data[page + 23] ||
                    _checksum[2] != Data[page + 24] || _checksum[3] != Data[page + 25])
                {
                    Array.Copy(_checksum, 0, Data, page + 22, 4);

                    _headerbytes = 0;
                    _bodybytes = 0;
                    next = 0;
                    for (int ii = 0; ii < bytes - 1; ii++)
                    {
                        if (Data[page + 1 + ii] == 'O')
                        {
                            next = page + 1 + ii;
                            break;
                        }
                    }

                    if (next == 0)
                        next = _fill;
                    _returned = next;
                    return -(next - page);
                }
            }

            page = _returned;
            if (og != null)
            {
                og.HeaderBase = Data;
                og.Header = page;
                og.HeaderLength = _headerbytes;
                og.BodyBase = Data;
                og.Body = page + _headerbytes;
                og.BodyLength = _bodybytes;
            }

            _unsynced = 0;
            _returned += (bytes = _headerbytes + _bodybytes);
            _headerbytes = 0;
            _bodybytes = 0;
            return bytes;
        }

        public int PageOut(Page og)
        {
            while (true)
            {
                int ret = PageSeek(og);
                if (ret > 0)
                {
                    return 1;
                }

                if (ret == 0)
                {
                    return 0;
                }

                if (_unsynced == 0)
                {
                    _unsynced = 1;
                    return -1;
                }
            }
        }

        public int Reset()
        {
            _fill = 0;
            _returned = 0;
            _unsynced = 0;
            _headerbytes = 0;
            _bodybytes = 0;
            return 0;
        }
    }
}
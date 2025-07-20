using System;

namespace deps.jorbis.jogg
{

    public class Page
    {
        private static readonly int[] CrcLookup;

        static Page()
        {
            CrcLookup = new int[256];
            for (int i = 0; i < CrcLookup.Length; i++)
            {
                CrcLookup[i] = CrcEntry(i);
            }
        }

        private static int CrcEntry(int index)
        {
            int r = index << 24;
            for (int i = 0; i < 8; i++)
            {
                if ((r & 0x80000000) != 0)
                {
                    r = (r << 1) ^ 0x04c11db7;
                }
                else
                {
                    r <<= 1;
                }
            }
            
            return (int)((uint)r & 0xffffffff);
        }

        public byte[] HeaderBase { get; set; }
        public int Header { get; set; }
        public int HeaderLength { get; set; }
        public byte[] BodyBase { get; set; }
        public int Body { get; set; }
        public int BodyLength { get; set; }

        public int Version()
        {
            return HeaderBase[Header + 4] & 0xff;
        }

        public int Continued()
        {
            return HeaderBase[Header + 5] & 0x01;
        }

        public int Bos()
        {
            return HeaderBase[Header + 5] & 0x02;
        }

        public int Eos()
        {
            return HeaderBase[Header + 5] & 0x04;
        }

        public long GranulePos()
        {
            long foo = HeaderBase[Header + 13] & 0xff;
            foo = (foo << 8) | (HeaderBase[Header + 12] & 0xff);
            foo = (foo << 8) | (HeaderBase[Header + 11] & 0xff);
            foo = (foo << 8) | (HeaderBase[Header + 10] & 0xff);
            foo = (foo << 8) | (HeaderBase[Header + 9] & 0xff);
            foo = (foo << 8) | (HeaderBase[Header + 8] & 0xff);
            foo = (foo << 8) | (HeaderBase[Header + 7] & 0xff);
            foo = (foo << 8) | (HeaderBase[Header + 6] & 0xff);
            return foo;
        }

        public int Serialno()
        {
            return (HeaderBase[Header + 14] & 0xff) |
                   ((HeaderBase[Header + 15] & 0xff) << 8) |
                   ((HeaderBase[Header + 16] & 0xff) << 16) |
                   ((HeaderBase[Header + 17] & 0xff) << 24);
        }

        public int PageNo()
        {
            return (HeaderBase[Header + 18] & 0xff) |
                   ((HeaderBase[Header + 19] & 0xff) << 8) |
                   ((HeaderBase[Header + 20] & 0xff) << 16) |
                   ((HeaderBase[Header + 21] & 0xff) << 24);
        }

        public void Checksum()
        {
            int crc_reg = 0;

            for (int i = 0; i < HeaderLength; i++)
            {
                crc_reg = (crc_reg << 8) ^
                          CrcLookup[(int)(((uint)crc_reg >> 24) & 0xff) ^ (HeaderBase[Header + i] & 0xff)];
            }

            for (int i = 0; i < BodyLength; i++)
            {
                crc_reg = (crc_reg << 8) ^
                          CrcLookup[(int)(((uint)crc_reg >> 24) & 0xff) ^ (BodyBase[Body + i] & 0xff)];
            }

            HeaderBase[Header + 22] = (byte)crc_reg;
            HeaderBase[Header + 23] = (byte)((uint)crc_reg >> 8);
            HeaderBase[Header + 24] = (byte)((uint)crc_reg >> 16);
            HeaderBase[Header + 25] = (byte)((uint)crc_reg >> 24);
        }

        public Page Copy()
        {
            return Copy(new Page());
        }

        public Page Copy(Page p)
        {
            byte[] tmpHeader = new byte[HeaderLength];
            Array.Copy(HeaderBase, Header, tmpHeader, 0, HeaderLength);
            p.HeaderLength = HeaderLength;
            p.HeaderBase = tmpHeader;
            p.Header = 0;

            byte[] tmpBody = new byte[BodyLength];
            Array.Copy(BodyBase, Body, tmpBody, 0, BodyLength);
            p.BodyLength = BodyLength;
            p.BodyBase = tmpBody;
            p.Body = 0;

            return p;
        }
    }
}
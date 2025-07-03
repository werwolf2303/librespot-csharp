using System;
using System.IO;

namespace lib.common
{
    /// <summary>
    /// The ByteBuffer reads in big endian by default and converts everything to little endian
    /// </summary>
    public class ByteBuffer : BinaryReader
    {
        private ByteBuffer(byte[] buffer) : base(new MemoryStream(buffer))
        {
        }

        public short GetShort()
        {
            byte[] bytes = ReadBytes(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        public int GetInt()
        {
            byte[] bytes = ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public long GetLong()
        {
            byte[] bytes = ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }

        public static ByteBuffer Wrap(byte[] buffer)
        {
            return new ByteBuffer(buffer);
        }
    }
}
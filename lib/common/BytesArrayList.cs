using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.Encoders;

namespace lib.common
{
    public class BytesArrayList : IEnumerable<byte[]>
    {
        private byte[][] _elementData;
        private int _size;

        public BytesArrayList()
        {
            _size = 0;
            _elementData = new byte[5][];
        }

        private BytesArrayList(byte[][] buffer)
        {
            _elementData = buffer;
            _size = buffer.Length;
        }

        public static MemoryStream StreamBase64(string[] payloads)
        {
            byte[][] decoded = new byte[payloads.Length][];
            for (int i = 0; i < decoded.Length; i++)
                decoded[i] = Base64.Decode(payloads[i]);
            return new BytesArrayList(decoded).Stream();
        }

        public static MemoryStream Stream(string[] payloads)
        {
            byte[][] bytes = new byte[payloads.Length][];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Encoding.UTF8.GetBytes(payloads[i]);
            return new BytesArrayList(bytes).Stream();
        }

        private void EnsureExplicitCapacity(int minCapacity)
        {
            if (minCapacity - _elementData.Length > 0)
                Grow(minCapacity);
        }

        public void Add(byte[] e)
        {
            EnsureExplicitCapacity(_size + 1);
            _elementData[_size++] = e;
        }

        public byte[] Get(int index)
        {
            if (index >= _size)
                throw new IndexOutOfRangeException(string.Format("size: {0}, index: {1}", _size, index));
            return _elementData[index];
        }

        public byte[][] ToArray()
        {
            byte[][] result = new byte[_size][];
            Array.Copy(_elementData, result, _size);
            return result;
        }

        private void Grow(int minCapacity)
        {
            int oldCapacity = _elementData.Length;
            int newCapacity = oldCapacity + (oldCapacity >> 1);
            if (newCapacity - minCapacity < 0)
                newCapacity = minCapacity;
            byte[][] newData = new byte[newCapacity][];
            Array.Copy(_elementData, newData, _size);
            _elementData = newData;
        }

        public BytesArrayList CopyOfRange(int from, int to)
        {
            int length = to - from;
            byte[][] newArray = new byte[length][];
            Array.Copy(_elementData, from, newArray, 0, length);
            return new BytesArrayList(newArray);
        }

        public int Size()
        {
            return _size;
        }

        public override string ToString()
        {
            return ToHex();
        }

        public string ToHex()
        {
            string[] array = new string[_size];
            byte[][] copy = ToArray();
            for (int i = 0; i < copy.Length; i++)
                array[i] = Utils.bytesToHex(copy[i]);
            return Arrays.ToString(array);
        }

        public MemoryStream Stream()
        {
            return new InternalStream(this);
        }

        public string ReadIntoString(int index)
        {
            byte[] b = Get(index);
            return Encoding.UTF8.GetString(b, 0, b.Length);
        }

        private class InternalStream : MemoryStream
        {
            private int _offset = 0;
            private int _sub = 0;
            private readonly BytesArrayList _parent;

            public InternalStream(BytesArrayList parent)
            {
                _parent = parent;
            }

            public override int Read(byte[] b, int off, int len)
            {
                if (off < 0 || len < 0 || len > b.Length - off)
                    throw new IndexOutOfRangeException();
                if (len == 0)
                    return 0;
                if (_sub >= _parent._size)
                    return -1;

                int i = 0;
                while (true)
                {
                    int copy = Math.Min(len - i, _parent._elementData[_sub].Length - _offset);
                    Array.Copy(_parent._elementData[_sub], _offset, b, off + i, copy);
                    i += copy;
                    _offset += copy;

                    if (i == len)
                        return i;

                    if (_offset >= _parent._elementData[_sub].Length)
                    {
                        _offset = 0;
                        if (++_sub >= _parent._size)
                            return i == 0 ? -1 : i;
                    }
                }
            }

            public override int ReadByte()
            {
                if (_sub >= _parent._size)
                    return -1;

                if (_offset >= _parent._elementData[_sub].Length)
                {
                    _offset = 0;
                    if (++_sub >= _parent._size)
                        return -1;
                }

                return _parent._elementData[_sub][_offset++] & 0xff;
            }
        }

        public IEnumerator<byte[]> GetEnumerator()
        {
            for (int i = 0; i < _size; i++)
                yield return _elementData[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using lib.common;
using lib.core;
using lib.crypto;

namespace zeroconf.zeroconf
{
    public class Record
    {
        internal static int TYPE_A = 0x01;
        internal static int TYPE_PTR = 0x0C;
        internal static int TYPE_CNAME = 0x05;
        internal static int TYPE_TXT = 0x10;
        internal static int TYPE_AAAA = 0x1C;
        internal static int TYPE_SRV = 0x21;
        internal static int TYPE_NSEC = 0x2F;
        internal static int TYPE_ANY = 0xFF;
        private static Dictionary<int, Type> _recordsMap = new Dictionary<int, Type>()
        {
            {TYPE_A, typeof(RecordAORAAAA)},
            {TYPE_AAAA, typeof(RecordAORAAAA)},
            {TYPE_SRV, typeof(RecordSRV)},
            {TYPE_PTR, typeof(RecordPTR)},
            {TYPE_TXT, typeof(RecordTXT)},
            {TYPE_ANY, typeof(RecordANY)}
        };
        private long _timestamp;
        private int _type;
        protected internal int TTL;
        private String _name;
        private int _clazz;
        private byte[] data;

        internal Record(int type)
        {
            _timestamp = TimeProvider.currentTimeMillis();
            _type = type & 0xFFFF;
            SetTTL(4500);
            _clazz = 1;
        }

        protected static int WriteName(String name, BinaryWriter writer, Packet packet)
        {
            int len = name.Length;
            int start = 0;
            for (int i = 0; i <= len; i++)
            {
                char c = i == len ? '.' : name.ToCharArray()[i];
                if (c == '.') {
                    writer.Write((byte) (i - start));
                    for (int j = start; j < i; j++)
                        writer.Write((byte) name.ToCharArray()[j]);

                    start = i + 1;
                }
            }

            writer.Write((byte) 0);
            return name.Length + 2;
        }
        
        protected static String ReadName(BinaryReader reader) {
            StringBuilder sb = new StringBuilder();
            int len;
            while ((len = (reader.ReadByte() & 0xFF)) > 0) {
                if (len >= 0x40) {
                    int off = ((len & 0x3F) << 8) | (reader.ReadByte() & 0xFF); // Offset from start of packet
                    long oldPos = reader.BaseStream.Position;
                    reader.BaseStream.Position = off;
                    if (sb.Length > 0) sb.Append('.');
                    sb.Append(ReadName(reader));
                    reader.BaseStream.Position = oldPos;
                    break;
                } else {
                    if (sb.Length > 0) sb.Append('.');
                    while (len-- > 0) sb.Append((char) (reader.ReadByte() & 0xFF));
                }
            }
            return sb.ToString();
        }
        
        internal static Record ReadAnswer(BinaryReader reader) {
            String name = ReadName(reader);
            int type = reader.ReadUInt16() & 0xFFFF;
            Record record = GetInstance(type);
            record.SetName(name);
            record._clazz = reader.ReadUInt16() & 0xFFFF;
            record.TTL = reader.ReadInt32();
            int len = reader.ReadUInt16() & 0xFFFF;
            record.ReadData(len, reader);
            return record;
        }

        internal static Record ReadQuestion(BinaryReader reader) {
            String name = ReadName(reader);
            int type = reader.ReadUInt16() & 0xFFFF;
            Record record = GetInstance(type);
            record.SetName(name);
            record._clazz = reader.ReadUInt16() & 0xFFFF;
            return record;
        }

        private static Record GetInstance(int type)
        {
            if (_recordsMap.ContainsKey(type))
            {
                return _recordsMap[type].GetConstructor(new Type[] { }).Invoke(new object[] { }) as Record;
            }
            else return new Record(type);
        }

        public String GetName() {
            return _name;
        }

        internal Record SetName(String name) {
            _name = name;
            return this;
        }

        public int GetType() {
            return _type;
        }

        internal Record SetTTL(int ttl) {
            TTL = ttl;
            return this;
        }

        protected void ReadData(int len, BinaryReader reader) {
            data = new byte[len];
            reader.ReadFully(data);
        }

        protected int WriteData(BinaryWriter writer, Packet packet) {
            if (data != null) {
                writer.Write(data);
                return data.Length;
            } else {
                return -1;
            }
        }

        public bool IsUnicastQuery() {
            return (_clazz & 0x80) != 0;
        }

        public bool IsExpired() {
            return TimeProvider.currentTimeMillis() > _timestamp + TTL * 1000L;
        }

        public override string ToString()
        {
            return String.Format("Record(type={0}, ttl={1}, name={2}', clazz={3}, data={4})",
                _type, TTL, _name, _clazz, BitConverter.ToString(data));
        }

        internal void Write(BinaryWriter writer, Packet packet)
        {
            WriteName(_name, writer, packet);
            writer.WriteBigEndian((short) _type);
            writer.WriteBigEndian((short) _clazz);
            writer.WriteBigEndian(TTL);
            long pos = writer.BaseStream.Position;
            writer.WriteBigEndian((short) 0);
            int len = WriteData(writer, packet);
            long oldPos = writer.BaseStream.Position;
            writer.BaseStream.Position = pos;
            if (len > 0)
            {
                writer.WriteBigEndian((short)len);
                writer.BaseStream.Position = oldPos;
            }
            else writer.BaseStream.Position = pos;
        }
    }
}
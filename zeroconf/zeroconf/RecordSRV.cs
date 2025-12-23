using System;
using System.IO;
using System.Text;
using lib.crypto;

namespace zeroconf.zeroconf
{
    public class RecordSRV : Record
    {
        private int _priority;
        private int _weight;
        private int _port;
        private String _target;

        internal RecordSRV() : base(TYPE_SRV)
        {
        }

        internal RecordSRV(String name, String target, int port) : base(TYPE_SRV)
        {
            SetName(name);
            _target = target;
            _port = port;
        }

        protected new void ReadData(int len, BinaryReader reader)
        {
            _priority = reader.ReadUInt16() & 0xFFFF;
            _weight = reader.ReadUInt16() & 0xFFFF;
            _port = reader.ReadUInt16() & 0xFFFF;
            _target = ReadName(reader);
        }

        protected new int WriteData(BinaryWriter writer, Packet packet)
        {
            if (_target != null)
            {
                writer.Write((short) _priority);
                writer.Write((short) _weight);
                writer.Write((short) _port);
                return 6 + WriteName(_target, writer, packet);
            }
            else
            {
                return -1;
            }
        }

        public int GetPriority()
        {
            return _priority;
        }

        public int GetWeight()
        {
            return _weight;
        }

        public int GetPort()
        {
            return _port;
        }

        public String GetTarget()
        {
            return _target;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(type:srv, name:\"");
            sb.Append(GetName());
            sb.Append('\"');
            if (_target != null) {
                sb.Append(", priority:");
                sb.Append(GetPriority());
                sb.Append(", weight:");
                sb.Append(GetWeight());
                sb.Append(", port:");
                sb.Append(GetPort());
                sb.Append(", target:\"");
                sb.Append(GetTarget());
                sb.Append('\"');
            }
            sb.Append(')');
            return sb.ToString();
        }
    }
}
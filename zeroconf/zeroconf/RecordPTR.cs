using System;
using System.IO;
using System.Text;
using lib.crypto;

namespace zeroconf.zeroconf
{
    public class RecordPTR : Record
    {
        private String _value;
        
        internal RecordPTR() : base(TYPE_PTR)
        {
        }

        internal RecordPTR(String name, String value) : base(TYPE_PTR)
        {
            SetName(name);
            _value = value;
        }

        /**
         * For queries
         */
        internal RecordPTR(String name) : base(TYPE_PTR)
        {
            SetName(name);
        }

        protected new void ReadData(int len, BinaryReader reader)
        {
            _value = ReadName(reader);
        }

        protected new int WriteData(BinaryWriter writer, Packet packet)
        {
            return _value != null ? WriteName(_value, writer, packet) : -1;
        }

        public String GetValue()
        {
            return _value;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(type:ptr, name\"");
            sb.Append(GetName());
            sb.Append("\"");
            if (_value != null)
            {
                sb.Append(", value:\"");
                sb.Append(GetValue());
                sb.Append("\"");
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
}
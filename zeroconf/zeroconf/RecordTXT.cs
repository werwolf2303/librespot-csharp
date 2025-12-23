using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using lib.crypto;

namespace zeroconf.zeroconf
{
    public class RecordTXT : Record
    {
        private Dictionary<String, String> _values;

        internal RecordTXT() : base(TYPE_TXT)
        {
        }

        internal RecordTXT(String name, Dictionary<String, String> values) : base(TYPE_TXT)
        {
            SetName(name);
            _values = values;
        }

        internal RecordTXT(String name, String map) : base(TYPE_TXT)
        {
            SetName(name);
            _values = new Dictionary<String, String>();
            String[] q = Regex.Split(map, ", *");
            foreach (String s in q)
            {
                String[] kv = s.Split('=');
                if (kv.Length == 2) _values[kv[0]] = kv[1];
            }
        }

        protected new void ReadData(int len, BinaryReader reader)
        { 
            long end = reader.BaseStream.Position + len;
            _values = new Dictionary<String, String>();
            while (reader.BaseStream.Position < end)
            {
                int slen = reader.ReadByte() & 0xFF;
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < slen; i++) sb.Append((char) reader.ReadByte());
                
                String value = sb.ToString();
                int ix = value.IndexOf('=');
                if (ix > 0) _values[value.Substring(0, ix)] = value.Substring(ix + 1);
            }
        }

        protected new int WriteData(BinaryWriter writer, Packet packet)
        {
            if (_values != null)
            {
                int len = 0;
                foreach (KeyValuePair<String, String> pair in _values)
                {
                    String value = pair.Key + "=" + pair.Value;
                    writer.Write((byte) value.Length);
                    writer.Write(Encoding.UTF8.GetBytes(value));
                    len += value.Length + 1;
                }

                return len;
            }
            else
            {
                return -1;
            }
        }

        public Dictionary<String, String> GetValues()
        {
            return _values;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(type:text, name:\"");
            sb.Append(GetName());
            sb.Append("\"");
            if (_values != null) {
                sb.Append(", values:");
                sb.Append(GetValues());
            }
            sb.Append(')');
            return sb.ToString();
        }
    }
}
using System;
using System.Collections.Generic;
using lib.common;

namespace lib.cache
{
    public class JournalHeader
    {
        public int id;
        public byte[] value;

        public JournalHeader(int id, String value)
        {
            this.id = id;
            this.value = Utils.hexToBytes(value);
        }
        
        public static JournalHeader find(List<JournalHeader> headers, byte id)
        {
            foreach (JournalHeader header in headers)
            {
                if (header.id == id)
                {
                    return header;
                }
            }
            return null;
        }

        public override string ToString()
        {
            return "JournalHeader{" + "id=" + id + ", value=" + Utils.bytesToHex(value) + "}";
        }
    }
}
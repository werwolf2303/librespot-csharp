using System;
using System.IO;
using System.Net;
using lib.crypto;

namespace zeroconf.zeroconf
{
    public class RecordANY : Record
    {
        internal RecordANY() : base(TYPE_ANY)
        {
        }

        internal RecordANY(String name) : base(TYPE_ANY)
        {
            SetName(name);
        }
        
        protected new void ReadData(int len, BinaryReader reader)
        {
            throw new InvalidOperationException();
        }

        protected new int WriteData(BinaryWriter writer, Packet packet)
        {
            return -1;
        }

        public override string ToString()
        {
            return String.Format("(type:any, name:\"{0}\")", GetName());
        }
    }
}
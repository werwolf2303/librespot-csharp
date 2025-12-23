using System;
using System.IO;
using System.Net;
using lib.crypto;

namespace zeroconf.zeroconf
{
    public class RecordAORAAAA : Record
    {
        private byte[] _address;

        internal RecordAORAAAA() : base(TYPE_A)
        {
        }

        internal RecordAORAAAA(String name, IPAddress address) : base(TYPE_A)
        {
            SetName(name);
            SetTTL(120);
            _address = address.GetAddressBytes();
        }

        protected new void ReadData(int len, BinaryReader reader)
        {
            _address = new byte[len];
            reader.ReadFully(_address);
        }

        protected new int WriteData(BinaryWriter writer, Packet packet)
        {
            if (_address != null)
            {
                writer.Write(_address);
                return _address.Length;
            }
            else
            {
                return -1;
            }
        }

        public IPAddress GetAddress()
        {
            return _address == null ? null : new IPAddress(_address);
        }

        public override string ToString()
        {
            return String.Format("(type:a/aaaa, name:\"{0}\", address:\"{2}\"", GetName(), GetAddress());
        }
    }
}
using System;
using lib.common;
using Spotify;

namespace lib.mercury
{
    public class RawMercuryRequest
    {
        internal Header _header;
        internal byte[][] _payload;

        public RawMercuryRequest(Header header, byte[][] payload)
        {
            _header = header;
            _payload = payload;
        }

        public static RawMercuryRequest Sub(String uri)
        {
            return new Builder
            {
                Uri = uri,
                Method = "SUB",
            }.Build();
        }

        public static RawMercuryRequest Unsub(String uri)
        {
            return new Builder
            {
                Uri = uri,
                Method = "UNSUB",
            }.Build();
        }

        public static RawMercuryRequest Get(String uri)
        {
            return new Builder
            {
                Uri = uri,
                Method = "GET",
            }.Build();
        }

        public static RawMercuryRequest Send(String uri, byte[] part)
        {
            return new Builder
            {
                Uri = uri,
                Method = "SEND",
            }.AddPayloadPart(part).Build();
        }

        public static RawMercuryRequest Post(String uri, byte[] part)
        {
            return new Builder
            {
                Uri = uri,
                Method = "POST",
                
            }.AddPayloadPart(part).Build();
        }
        
        public static Builder NewBuilder()
        {
            return new Builder();
        }

        public class Builder : Header
        {
            private readonly BytesArrayList _payload = new BytesArrayList();

            public Builder AddPayloadPart(byte[] part) {
                _payload.Add(part);
                return this;
            }

            public RawMercuryRequest Build()
            {
                return new RawMercuryRequest(this, _payload.ToArray());
            }
        }
    }
}
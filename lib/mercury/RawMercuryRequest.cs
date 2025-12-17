using System;
using System.Collections.Generic;
using System.Text;
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

        public class Builder
        {
            private readonly BytesArrayList _payload = new BytesArrayList();
            private List<UserField> _userFields = new List<UserField>();
            
            public String Uri { get; set; }
            public String Method { get; set; }
            public int StatusCode { get; set; }
            public String ContentType { get; set; }

            public Builder AddUserField(String name, String value)
            {
                UserField userField = new UserField();
                userField.Key = name;
                userField.Value = Encoding.UTF8.GetBytes(value);
                
                _userFields.Add(userField);
                
                return this;
            }

            public Builder AddPayloadPart(byte[] part) {
                _payload.Add(part);
                return this;
            }

            public RawMercuryRequest Build()
            {
                Header header = new Header();
                if (ContentType != null) header.ContentType = ContentType;
                if (Method != null) header.Method = Method;
                if (Uri != null) header.Uri = Uri;
                if (StatusCode == 0) header.StatusCode = StatusCode;
                header.UserFields.AddRange(_userFields);
                return new RawMercuryRequest(header, _payload.ToArray());
            }
        }
    }
}
using System;
using csharp;

namespace lib.dealer
{
    public class MessageType : Enumeration
    {
        public static readonly MessageType Ping = new MessageType("ping");
        public static readonly MessageType Pong = new MessageType("pong");
        public static readonly MessageType Message = new MessageType("message");
        public static readonly MessageType Request = new MessageType("request");

        private String _val;
        
        private MessageType(String val)
        {
            _val = val;
        }

        public MessageType()
        {
        }

        public static MessageType Parse(String type)
        {
            foreach (MessageType msg in Enum.GetValues(typeof(MessageType)))
                if (msg._val.Equals(type))
                    return msg;

            throw new Exception("Invalid MessageType: " + type);
        }
        
        public String Value { get => _val; }
    }
}
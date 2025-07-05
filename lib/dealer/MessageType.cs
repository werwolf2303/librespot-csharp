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
            if (type.Equals("ping"))
                return Ping;
            if (type.Equals("pong"))
                return Pong;
            if (type.Equals("message")) 
                return Message;
            if (type.Equals("request"))
                return Request;
            throw new Exception("Invalid MessageType: " + type);
        }
        
        public String Value { get => _val; }
    }
}
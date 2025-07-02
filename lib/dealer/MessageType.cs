using System;
using csharp;

namespace lib.dealer
{
    public class MessageType : Enumeration
    {
        public readonly MessageType Ping = new MessageType("ping");
        public readonly MessageType Pong = new MessageType("pong");
        public readonly MessageType Message = new MessageType("message");
        public readonly MessageType Request = new MessageType("request");

        private String _val;
        
        private MessageType(String val)
        {
            _val = val;
        }

        public MessageType()
        {
        }
        
        public String Value { get => _val; }
    }
}
using System;

namespace lib.crypto
{
    /**
     * @author Gianlu
     */
    public class Packet {
        public byte cmd;
        public byte[] payload;
        private Type type = Type.NULL;

        public Packet(byte cmd, byte[] payload) {
            this.cmd = cmd;
            this.payload = payload;
        }
        
        public Type getType() {
            if (type == Type.NULL) type = parse(cmd);
            return type;
        }

        public bool Is(Type type) {
            return getType() == type;
        }

        public enum Type {
            NULL = 0,
            SecretBlock=0x02,
            Ping=0x04,
            StreamChunk=0x08,
            StreamChunkRes=0x09,
            ChannelError=0x0a,
            ChannelAbort=0x0b,
            RequestKey=0x0c,
            AesKey=0x0d,
            AesKeyError=0x0e,
            Image=0x19,
            CountryCode=0x1b,
            Pong=0x49,
            PongAck=0x4a,
            Pause=0x4b,
            ProductInfo=0x50,
            LegacyWelcome=0x69,
            LicenseVersion=0x76,
            Login=0xab,
            APWelcome=0xac,
            AuthFailure=0xad,
            MercuryReq=0xb2,
            MercurySub=0xb3,
            MercuryUnsub=0xb4,
            MercuryEvent=0xb5,
            TrackEndedTime=0x82,
            UnknownData_AllZeros=0x1f,
            PreferredLocale=0x74,
            Unknown_0x4f=0x4f,
            Unknown_0x0f=0x0f,
            Unknown_0x10=0x10
        }

        public static Type parse(byte val)
        {
            foreach (Type cmd in Enum.GetValues(typeof(Type)))
                if (cmd.Equals(val))
                    return cmd;
            return Type.NULL;
        }

        public static Type forMethod(String method) {
            switch (method) {
                case "SUB":
                    return Type.MercurySub;
                case "UNSUB":
                    return Type.MercuryUnsub;
                default:
                    return Type.MercuryReq;
            }
        }
    }
}
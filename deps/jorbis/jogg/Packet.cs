namespace deps.jorbis.jogg
{
    public class Packet
    {
        public byte[] PacketBase;
        public int TPacket;
        public int Bytes;
        public int BeginningOfStream;
        public int EndOfStream;

        public long GranulePos;
        
        
        /**
         * sequence number for decode; the framing
         * knows where there's a hole in the data,
         * but we need coupling so that the codec
         * (which is in a seperate abstraction
         * layer) also knows about the gap
         */
        public long PacketNo;
    }
}
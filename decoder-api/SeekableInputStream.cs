using System.IO;

namespace decoder_api
{
    public abstract class SeekableInputStream : Stream
    {
        private Stream _baseStream;

        public SeekableInputStream(Stream baseStream)
        {
            _baseStream = baseStream;
        }
        
        public abstract int Size();
        public abstract long Skip(long skip);
        public abstract int DecodedLength();
        public abstract int Read();
    }
}
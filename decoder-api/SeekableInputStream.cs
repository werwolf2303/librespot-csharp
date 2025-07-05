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
        
        public override abstract bool CanRead { get; }
        public override abstract bool CanSeek { get; }
        public override abstract bool CanWrite { get; }
        public override abstract long Length { get; }
        public override abstract long Position { get; set; }

        public override abstract void Flush();
        public override abstract int Read(byte[] buffer, int offset, int count);
        public override abstract long Seek(long offset, SeekOrigin origin);
        public override abstract void SetLength(long value);
        public override abstract void Write(byte[] buffer, int offset, int count);
    }
}
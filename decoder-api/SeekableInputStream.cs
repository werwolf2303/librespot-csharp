using System.IO;

namespace decoder_api
{
    public abstract class SeekableInputStream : Stream
    {
        public abstract int size();

        public abstract int position();

        public abstract int seek(int seekZero);

        public abstract long skip(long skip);
        
        public abstract int read(byte[] buffer, int offset, int length);

        public abstract int decodedLength();
    }
}
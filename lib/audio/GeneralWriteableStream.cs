using System.IO;

namespace lib.audio
{
    public interface GeneralWriteableStream
    {
        /// <exception cref="IOException"></exception>
        void writeChunk(byte[] buffer, int chunkIndex, bool cached);
    }
}
using System.IO;

namespace lib.audio
{
    public interface IGeneralWriteableStream
    {
        /// <exception cref="IOException"></exception>
        void WriteChunk(byte[] buffer, int chunkIndex, bool cached);
    }
}
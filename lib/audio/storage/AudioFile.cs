using System;
using System.IO;

namespace lib.audio.storage
{
    public interface AudioFile : IDisposable, GeneralWriteableStream
    {
        /// <exception cref="IOException"></exception>
        void writeChunk(byte[] chunk, int chunkIndex, bool cached);

        /// <exception cref="IOException"></exception>
        void writeHeader(int id, byte[] bytes, bool cached);

        void streamError(int chunkIndex, short code);
    }
}
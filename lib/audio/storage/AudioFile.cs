using System;
using System.IO;

namespace lib.audio.storage
{
    public interface AudioFile : IDisposable, IGeneralWriteableStream
    {
        /// <exception cref="IOException"></exception>
        void WriteChunk(byte[] chunk, int chunkIndex, bool cached);

        /// <exception cref="IOException"></exception>
        void WriteHeader(int id, byte[] bytes, bool cached);

        void StreamError(int chunkIndex, short code);
    }
}
using System.IO;

namespace lib.audio.decrypt
{
    public interface AudioDecrypt
    {
        /// <exception cref="IOException"></exception>
        void decryptChunk(int chunkIndex, byte[] buffer);

        int decryptTimeMs();
    }
}
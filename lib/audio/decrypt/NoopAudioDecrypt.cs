namespace lib.audio.decrypt
{
    public class NoopAudioDecrypt : AudioDecrypt
    {
        public void decryptChunk(int chunkIndex, byte[] buffer)
        {
        }

        public int decryptTimeMs()
        {
            return 0;
        }
    }
}
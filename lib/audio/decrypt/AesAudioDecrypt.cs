using System;
using System.IO;
using System.Numerics;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace lib.audio.decrypt
{
    public class AesAudioDecrypt : AudioDecrypt
    {
        private static readonly byte[] AUDIO_AES_IV_BYTES =
        {
            0x72, 0xe0, 0x67, 0xfb, 0xdd, 0xcb, 0xcf, 0x77,
            0xeb, 0xe8, 0xbc, 0x64, 0x3f, 0x63, 0x0d, 0x93
        };

        private static readonly BigInteger INITIAL_IV_INT;

        private static readonly BigInteger IV_INCREMENT_VALUE = new BigInteger(0x100);

        private readonly byte[] _secretKey;
        private readonly BufferedBlockCipher _bufferedCipher; 
        private int _decryptCount = 0;
        private long _decryptTotalTime = 0;
        
        static AesAudioDecrypt()
        {
            byte[] ivCopyForBigInt = (byte[])AUDIO_AES_IV_BYTES.Clone();
            Array.Reverse(ivCopyForBigInt);
            INITIAL_IV_INT = new BigInteger(ivCopyForBigInt);
        }

        public AesAudioDecrypt(byte[] key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            this._secretKey = key;

            try
            {
                _bufferedCipher = new BufferedBlockCipher(new SicBlockCipher(new AesEngine()));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize AES CTR cipher.", ex);
            }
        }

        public void decryptChunk(int chunkIndex, byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            
            const int CHUNK_SIZE = 409600;
            const int AES_BLOCK_SIZE_BYTES = 16;

            BigInteger currentCounter =
                INITIAL_IV_INT + new BigInteger((long)CHUNK_SIZE * chunkIndex / AES_BLOCK_SIZE_BYTES);

            try
            {
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                for (int i = 0; i < buffer.Length; i += 4096)
                {
                    byte[] ivBytes = currentCounter.ToByteArray(); 
                    Array.Reverse(ivBytes); 

                    byte[] paddedIvBytes = new byte[AES_BLOCK_SIZE_BYTES];
                    int copyLen = Math.Min(ivBytes.Length, AES_BLOCK_SIZE_BYTES);
                    Buffer.BlockCopy(ivBytes, 0, paddedIvBytes, AES_BLOCK_SIZE_BYTES - copyLen, copyLen);

                    ParametersWithIV keyAndIV = new ParametersWithIV(new KeyParameter(_secretKey), paddedIvBytes);

                    _bufferedCipher.Init(true, keyAndIV);

                    int count = Math.Min(4096, buffer.Length - i);

                    int bytesProcessed = _bufferedCipher.ProcessBytes(buffer, i, count, buffer, i);

                    if (count != bytesProcessed)
                        throw new IOException(string.Format(
                            "Bouncy Castle ProcessBytes did not process all data. Actual: {0}, Expected: {1}",
                            bytesProcessed, count));

                    currentCounter = currentCounter + IV_INCREMENT_VALUE;
                }

                stopwatch.Stop();
                _decryptTotalTime += stopwatch.ElapsedTicks;
                _decryptCount++;
            }
            catch (DataLengthException ex)
            {
                throw new IOException("Cryptographic data length error.", ex);
            }
            catch (InvalidCipherTextException ex)
            {
                throw new IOException("Decryption failed due to invalid ciphertext.", ex);
            }
            catch (Exception ex)
            {
                throw new IOException("An unexpected error occurred during chunk decryption.", ex);
            }
        }
        
        public int decryptTimeMs()
        {
            if (_decryptCount == 0)
            {
                return 0;
            }

            double averageTicks = (double)_decryptTotalTime / _decryptCount;
            return (int)(averageTicks / System.Diagnostics.Stopwatch.Frequency * 1000f);
        }
    }
}
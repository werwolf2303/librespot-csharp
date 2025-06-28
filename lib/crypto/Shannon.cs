using System;

namespace lib.crypto
{
    /// <summary>
    /// A complete C# 4.0 compatible implementation of the Shannon stream cipher.
    /// The cryptographic logic is ported from the original Java implementation to produce identical output.
    /// </summary>
    public class Shannon
    {
        #region Constants and Fields

        private const int N = 16;
        private const int FOLD = N;
        private const int INITKONST = 0x6996c53a;
        private const int KEYP = 13;

        private readonly int[] R;
        private readonly int[] CRC;
        private readonly int[] initR;
        private int konst;
        private int sbuf;
        private int mbuf;
        private int nbuf;

        #endregion

        /// <summary>
        /// Initializes a new instance of the Shannon stream cipher.
        /// </summary>
        public Shannon()
        {
            this.R = new int[N];
            this.CRC = new int[N];
            this.initR = new int[N];
        }

        #region Core Algorithm

        /// <summary>
        /// C# 4.0 Compatibility: Manual implementation of a 32-bit left bitwise rotation,
        /// as System.Numerics.BitOperations is not available in .NET Framework 4.0.
        /// </summary>
        /// <param name="value">The value to rotate.</param>
        /// <param name="bits">The number of bits to rotate.</param>
        /// <returns>The rotated value.</returns>
        private static int RotateLeft(int value, int bits)
        {
            uint u_value = (uint)value;
            return (int)((u_value << bits) | (u_value >> (32 - bits)));
        }

        private int Sbox(int i)
        {
            // C# 4.0 Change: Replaced BitOperations.RotateLeft with our manual implementation.
            i ^= RotateLeft(i, 5) | RotateLeft(i, 7);
            i ^= RotateLeft(i, 19) | RotateLeft(i, 22);
            return i;
        }

        private int Sbox2(int i)
        {
            i ^= RotateLeft(i, 7) | RotateLeft(i, 22);
            i ^= RotateLeft(i, 5) | RotateLeft(i, 19);
            return i;
        }

        private void Cycle()
        {
            int t = this.R[12] ^ this.R[13] ^ this.konst;
            t = this.Sbox(t) ^ RotateLeft(this.R[0], 1);

            for (int i = 1; i < N; i++)
            {
                this.R[i - 1] = this.R[i];
            }
            this.R[N - 1] = t;

            t = Sbox2(this.R[2] ^ this.R[15]);
            this.R[0] ^= t;
            this.sbuf = t ^ this.R[8] ^ this.R[12];
        }

        private void CrcFunc(int i)
        {
            int t = this.CRC[0] ^ this.CRC[2] ^ this.CRC[15] ^ i;
            for (int j = 1; j < N; j++)
            {
                this.CRC[j - 1] = this.CRC[j];
            }
            this.CRC[N - 1] = t;
        }

        private void MacFunc(int i)
        {
            this.CrcFunc(i);
            this.R[KEYP] ^= i;
        }

        #endregion

        #region State Management

        private void InitState()
        {
            this.R[0] = 1;
            this.R[1] = 1;
            for (int i = 2; i < N; i++)
            {
                this.R[i] = this.R[i - 1] + this.R[i - 2];
            }
            this.konst = INITKONST;
        }

        private void SaveState()
        {
            for (int i = 0; i < N; i++)
            {
                this.initR[i] = this.R[i];
            }
        }

        private void ReloadState()
        {
            for (int i = 0; i < N; i++)
            {
                this.R[i] = this.initR[i];
            }
        }

        private void GenKonst()
        {
            this.konst = this.R[0];
        }

        private void AddKey(int k)
        {
            this.R[KEYP] ^= k;
        }

        private void Diffuse()
        {
            for (int i = 0; i < FOLD; i++)
            {
                this.Cycle();
            }
        }

        private void LoadKey(byte[] key)
        {
            byte[] extra = new byte[4];
            int i, j;
            
            for (i = 0; i < (key.Length & ~0x03); i += 4)
            {
                int t = (key[i + 3] << 24) | (key[i + 2] << 16) | (key[i + 1] << 8) | key[i];
                this.AddKey(t);
                this.Cycle();
            }

            if (i < key.Length)
            {
                for (j = 0; i < key.Length; i++)
                {
                    extra[j++] = key[i];
                }
                for (; j < 4; j++)
                {
                    extra[j] = 0;
                }
                int t = (extra[3] << 24) | (extra[2] << 16) | (extra[1] << 8) | extra[0];
                this.AddKey(t);
                this.Cycle();
            }

            this.AddKey(key.Length);
            this.Cycle();

            for (i = 0; i < N; i++)
            {
                this.CRC[i] = this.R[i];
            }
            this.Diffuse();

            for (i = 0; i < N; i++)
            {
                this.R[i] ^= this.CRC[i];
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the encryption key. This must be the first method called.
        /// </summary>
        public void Key(byte[] key)
        {
            this.InitState();
            this.LoadKey(key);
            this.GenKonst();
            this.SaveState();
            this.nbuf = 0;
        }

        /// <summary>
        /// Sets the nonce (or Initialization Vector). This should be called after setting the key and
        /// before encrypting/decrypting a new stream.
        /// </summary>
        public void Nonce(byte[] nonce)
        {
            this.ReloadState();
            this.konst = INITKONST;
            this.LoadKey(nonce);
            this.GenKonst();
            this.nbuf = 0;
        }

        /// <summary>
        /// XORs the input buffer with the keystream. Does not perform MAC operations.
        /// </summary>
        public void Stream(byte[] buffer)
        {
            int i = 0, j, n = buffer.Length;
            while (this.nbuf != 0 && n != 0)
            {
                buffer[i++] ^= (byte)(this.sbuf & 0xFF);
                this.sbuf >>= 8;
                this.nbuf -= 8;
                n--;
            }

            j = n & ~0x03;
            while (i < j)
            {
                this.Cycle();
                int word = this.sbuf;
                buffer[i + 3] ^= (byte)(word >> 24);
                buffer[i + 2] ^= (byte)(word >> 16);
                buffer[i + 1] ^= (byte)(word >> 8);
                buffer[i] ^= (byte)word;
                i += 4;
            }

            n &= 0x03;
            if (n != 0)
            {
                this.Cycle();
                this.nbuf = 32;
                while (this.nbuf != 0 && n != 0)
                {
                    buffer[i++] ^= (byte)(this.sbuf & 0xFF);
                    this.sbuf >>= 8;
                    this.nbuf -= 8;
                    n--;
                }
            }
        }

        /// <summary>
        /// Accumulates the buffer into the MAC state without encryption.
        /// </summary>
        public void MacOnly(byte[] buffer)
        {
            int i = 0, j, n = buffer.Length;

            if (this.nbuf != 0)
            {
                while (this.nbuf != 0 && n != 0)
                {
                    this.mbuf ^= buffer[i++] << (32 - this.nbuf);
                    this.nbuf -= 8;
                    n--;
                }
                if (this.nbuf != 0) return;
                this.MacFunc(this.mbuf);
            }

            j = n & ~0x03;
            while (i < j)
            {
                this.Cycle();
                int t = (buffer[i + 3] << 24) | (buffer[i + 2] << 16) | (buffer[i + 1] << 8) | buffer[i];
                this.MacFunc(t);
                i += 4;
            }

            n &= 0x03;
            if (n != 0)
            {
                this.Cycle();
                this.mbuf = 0;
                this.nbuf = 32;
                while (n != 0)
                {
                    this.mbuf ^= buffer[i++] << (32 - this.nbuf);
                    this.nbuf -= 8;
                    n--;
                }
            }
        }
        
        /// <summary>
        /// Encrypts the buffer in-place and accumulates the plaintext into the MAC state.
        /// </summary>
        public void Encrypt(byte[] buffer)
        {
            // C# 4.0 Change: Replaced expression-body with a full method body.
            this.Encrypt(buffer, buffer.Length);
        }
        
        /// <summary>
        /// Encrypts a specified length of the buffer in-place and accumulates the plaintext into the MAC state.
        /// </summary>
        public void Encrypt(byte[] buffer, int n)
        {
            int i = 0, j;
            if (this.nbuf != 0)
            {
                while (this.nbuf != 0 && n != 0)
                {
                    this.mbuf ^= buffer[i] << (32 - this.nbuf);
                    buffer[i] ^= (byte)(this.sbuf >> (32 - this.nbuf));
                    i++;
                    this.nbuf -= 8;
                    n--;
                }
                if (this.nbuf != 0) return;
                this.MacFunc(this.mbuf);
            }

            j = n & ~0x03;
            while (i < j)
            {
                this.Cycle();
                int t = (buffer[i + 3] << 24) | (buffer[i + 2] << 16) | (buffer[i + 1] << 8) | buffer[i];
                this.MacFunc(t);
                t ^= this.sbuf;
                buffer[i + 3] = (byte)(t >> 24);
                buffer[i + 2] = (byte)(t >> 16);
                buffer[i + 1] = (byte)(t >> 8);
                buffer[i] = (byte)t;
                i += 4;
            }

            n &= 0x03;
            if (n != 0)
            {
                this.Cycle();
                this.mbuf = 0;
                this.nbuf = 32;
                while (n != 0)
                {
                    this.mbuf ^= buffer[i] << (32 - this.nbuf);
                    buffer[i] ^= (byte)(this.sbuf >> (32 - this.nbuf));
                    i++;
                    this.nbuf -= 8;
                    n--;
                }
            }
        }
        
        /// <summary>
        /// Decrypts the buffer in-place and accumulates the plaintext into the MAC state.
        /// </summary>
        public void Decrypt(byte[] buffer)
        {
            this.Decrypt(buffer, buffer.Length);
        }
        
        /// <summary>
        /// Decrypts a specified length of the buffer in-place and accumulates the plaintext into the MAC state.
        /// </summary>
        public void Decrypt(byte[] buffer, int n)
        {
            int i = 0, j;
            if (this.nbuf != 0)
            {
                while (this.nbuf != 0 && n != 0)
                {
                    buffer[i] ^= (byte)(this.sbuf >> (32 - this.nbuf));
                    this.mbuf ^= buffer[i] << (32 - this.nbuf);
                    i++;
                    this.nbuf -= 8;
                    n--;
                }
                if (this.nbuf != 0) return;
                this.MacFunc(this.mbuf);
            }

            j = n & ~0x03;
            while (i < j)
            {
                this.Cycle();
                int t = (buffer[i + 3] << 24) | (buffer[i + 2] << 16) | (buffer[i + 1] << 8) | buffer[i];
                t ^= this.sbuf;
                this.MacFunc(t);
                buffer[i + 3] = (byte)(t >> 24);
                buffer[i + 2] = (byte)(t >> 16);
                buffer[i + 1] = (byte)(t >> 8);
                buffer[i] = (byte)t;
                i += 4;
            }

            n &= 0x03;
            if (n != 0)
            {
                this.Cycle();
                this.mbuf = 0;
                this.nbuf = 32;
                while (n != 0)
                {
                    buffer[i] ^= (byte)(this.sbuf >> (32 - this.nbuf));
                    this.mbuf ^= buffer[i] << (32 - this.nbuf);
                    i++;
                    this.nbuf -= 8;
                    n--;
                }
            }
        }
        
        /// <summary>
        /// Finalizes the MAC calculation and writes the MAC tag to the provided buffer.
        /// </summary>
        public void Finish(byte[] buffer)
        {
            this.Finish(buffer, buffer.Length);
        }
        
        /// <summary>
        /// Finalizes the MAC calculation and writes the MAC tag to the provided buffer up to a specified length.
        /// </summary>
        public void Finish(byte[] buffer, int n)
        {
            int i = 0, j;

            if (this.nbuf != 0)
            {
                this.MacFunc(this.mbuf);
            }

            this.Cycle();
            this.AddKey(INITKONST ^ (this.nbuf << 3));
            this.nbuf = 0;

            for (j = 0; j < N; j++)
            {
                this.R[j] ^= this.CRC[j];
            }
            this.Diffuse();

            while (n > 0)
            {
                this.Cycle();
                if (n >= 4)
                {
                    int word = this.sbuf;
                    buffer[i + 3] = (byte)(word >> 24);
                    buffer[i + 2] = (byte)(word >> 16);
                    buffer[i + 1] = (byte)(word >> 8);
                    buffer[i] = (byte)word;
                    n -= 4;
                    i += 4;
                }
                else
                {
                    for (j = 0; j < n; j++)
                    {
                        buffer[i + j] = (byte)(this.sbuf >> (j * 8));
                    }
                    break;
                }
            }
        }

        #endregion
    }
}
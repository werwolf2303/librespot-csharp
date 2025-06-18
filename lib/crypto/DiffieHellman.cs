using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace lib.crypto
{
    public class DiffieHellman
    {
        private static readonly BigInteger GENERATOR = new BigInteger(2);

        private static readonly byte[] PRIME_BYTES_JAVA_BIG_ENDIAN =
        {
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xc9,
            0x0f, 0xda, 0xa2, 0x21, 0x68, 0xc2, 0x34, 0xc4, 0xc6,
            0x62, 0x8b, 0x80, 0xdc, 0x1c, 0xd1, 0x29, 0x02, 0x4e,
            0x08, 0x8a, 0x67, 0xcc, 0x74, 0x02, 0x0b, 0xbe, 0xa6,
            0x3b, 0x13, 0x9b, 0x22, 0x51, 0x4a, 0x08, 0x79, 0x8e,
            0x34, 0x04, 0xdd, 0xef, 0x95, 0x19, 0xb3, 0xcd, 0x3a,
            0x43, 0x1b, 0x30, 0x2b, 0x0a, 0x6d, 0xf2, 0x5f, 0x14,
            0x37, 0x4f, 0xe1, 0x35, 0x6d, 0x6d, 0x51, 0xc2, 0x45,
            0xe4, 0x85, 0xb5, 0x76, 0x62, 0x5e, 0x7e, 0xc6, 0xf4,
            0x4c, 0x42, 0xe9, 0xa6, 0x3a, 0x36, 0x20, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff
        };
        
        public static BigInteger CreatePositiveBigIntegerFromBigEndianBytes(byte[] bigEndianMagnitude)
        {
            if (bigEndianMagnitude == null || bigEndianMagnitude.Length == 0)
            {
                return BigInteger.Zero;
            }
            byte[] littleEndianBytes = bigEndianMagnitude.Reverse().ToArray();
            {
                Array.Resize(ref littleEndianBytes, littleEndianBytes.Length + 1);
                littleEndianBytes[littleEndianBytes.Length - 1] = 0x00; // Append 0x00
            }
            return new BigInteger(littleEndianBytes);
        }

        private static readonly BigInteger PRIME =
            CreatePositiveBigIntegerFromBigEndianBytes(PRIME_BYTES_JAVA_BIG_ENDIAN);

        private readonly BigInteger privateKey;
        private readonly BigInteger publicKey;

        public DiffieHellman(RandomNumberGenerator random) 
        {
            byte[] keyData = new byte[95];
            random.GetBytes(keyData);
            privateKey = CreatePositiveBigIntegerFromBigEndianBytes(keyData);
            publicKey = BigInteger.ModPow(GENERATOR, privateKey, PRIME);
        }

        public BigInteger ComputeSharedKey(byte[] remoteKeyBytes)
        {
            BigInteger remoteKey = CreatePositiveBigIntegerFromBigEndianBytes(remoteKeyBytes);
            return BigInteger.ModPow(remoteKey, privateKey, PRIME);
        }

        public BigInteger PrivateKey => privateKey;

        public BigInteger PublicKey => publicKey;

        public byte[] PublicKeyArray()
        {
            byte[] publicKeyLittleEndian = publicKey.ToByteArray();
            if (publicKeyLittleEndian.Length > 1 && publicKeyLittleEndian[publicKeyLittleEndian.Length - 1] == 0x00 &&
                publicKey.Sign > 0)
            {
                publicKeyLittleEndian = publicKeyLittleEndian.Take(publicKeyLittleEndian.Length - 1).ToArray();
            }
            return publicKeyLittleEndian.Reverse().ToArray();
        }
    }
}
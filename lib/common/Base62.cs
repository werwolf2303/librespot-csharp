using System;
using System.IO;
using System.Linq;
using Org.BouncyCastle.Utilities;

namespace lib.common
{
    /**
     * A Base62 encoder/decoder.
     *
     * @author Sebastian Ruhleder, sebastian@seruco.io
     */
    public class Base62
    {
        private static int STANDARD_BASE = 256;
        private static int TARGET_BASE = 62;
        private byte[] alphabet;
        private byte[] lookup;

        private Base62(byte[] alphabet)
        {
            this.alphabet = alphabet;
            createLookupTable();
        }

        public static Base62 createInstance()
        {
            return createInstanceWithGmpCharacterSet();
        }

        public static Base62 createInstanceWithGmpCharacterSet()
        {
            return new Base62(CharacterSets.GMP);
        }

        public static Base62 createInstanceWithInvertedCharacterSet()
        {
            return new Base62(CharacterSets.INVERTED);
        }

        public byte[] encode(byte[] message, int length)
        {
            byte[] indices = convert(message, STANDARD_BASE, TARGET_BASE, length);
            return translate(indices, alphabet);
        }

        public byte[] encode(byte[] message)
        {
            return encode(message, -1);
        }

        public byte[] decode(byte[] encoded, int length)
        {
            byte[] prepared = translate(encoded, lookup);
            return convert(prepared, TARGET_BASE, STANDARD_BASE, length);
        }

        public byte[] decode(byte[] encoded)
        {
            return decode(encoded, -1);
        }

        private byte[] translate(byte[] indices, byte[] dictionary)
        {
            byte[] translation = new byte[indices.Length];
            for (int i = 0; i < indices.Length; i++)
                translation[i] = dictionary[indices[i]];

            return translation;
        }

        private byte[] convert(byte[] message, int sourceBase, int targetBase, int length)
        {
            int estimatedLength = length == -1 ? estimateOutputLength(message.Length, sourceBase, targetBase) : length;

            using (MemoryStream stream = new MemoryStream(estimatedLength))
            using (BinaryWriter streamWriter = new BinaryWriter(stream))
            {
                byte[] source = message;
                while (source.Length > 0)
                {
                    using (MemoryStream quotient = new MemoryStream(source.Length))
                    using (BinaryWriter quotientWriter = new BinaryWriter(quotient))
                    {
                        int remainder = 0;
                        foreach (byte b in source)
                        {
                            int accumulator = b + remainder * sourceBase;
                            int digit = (accumulator - (accumulator % targetBase)) / targetBase;
                            remainder = accumulator % targetBase;

                            if (quotient.Length > 0 || digit > 0)
                                quotientWriter.Write((byte)digit);
                        }

                        streamWriter.Write((byte)remainder);
                        source = quotient.ToArray();
                    }
                }

                byte[] resultBytes;
                if (stream.Length < estimatedLength)
                {
                    long currentSize = stream.Length;
                    for (int i = 0; i < estimatedLength - currentSize; i++)
                        streamWriter.Write((byte)0);
                    resultBytes = stream.ToArray();
                }
                else if (stream.Length > estimatedLength)
                {
                    resultBytes = stream.ToArray().Take(estimatedLength).ToArray();
                }
                else
                {
                    resultBytes = stream.ToArray();
                }

                Array.Reverse(resultBytes);
                return resultBytes;
            }
        }

        private int estimateOutputLength(int inputLength, int sourceBase, int targetBase)
        {
            return (int)Math.Ceiling(Math.Log(sourceBase) / Math.Log(targetBase) * inputLength);
        }

        private byte[] reverse(byte[] arr)
        {
            Array.Reverse(arr);
            return arr;
        }

        private void createLookupTable()
        {
            lookup = new byte[256];
            for (int i = 0; i < alphabet.Length; i++)
                lookup[alphabet[i]] = (byte)i;
        }

        private static class CharacterSets
        {
            public static byte[] GMP =
            {
                (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
                (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F',
                (byte)'G', (byte)'H', (byte)'I', (byte)'J', (byte)'K', (byte)'L', (byte)'M', (byte)'N',
                (byte)'O', (byte)'P', (byte)'Q', (byte)'R', (byte)'S', (byte)'T', (byte)'U', (byte)'V',
                (byte)'W', (byte)'X', (byte)'Y', (byte)'Z', (byte)'a', (byte)'b', (byte)'c', (byte)'d',
                (byte)'e', (byte)'f', (byte)'g', (byte)'h', (byte)'i', (byte)'j', (byte)'k', (byte)'l',
                (byte)'m', (byte)'n', (byte)'o', (byte)'p', (byte)'q', (byte)'r', (byte)'s', (byte)'t',
                (byte)'u', (byte)'v', (byte)'w', (byte)'x', (byte)'y', (byte)'z'
            };

            public static byte[] INVERTED =
            {
                (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
                (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f',
                (byte)'g', (byte)'h', (byte)'i', (byte)'j', (byte)'k', (byte)'l', (byte)'m', (byte)'n',
                (byte)'o', (byte)'p', (byte)'q', (byte)'r', (byte)'s', (byte)'t', (byte)'u', (byte)'v',
                (byte)'w', (byte)'x', (byte)'y', (byte)'z', (byte)'A', (byte)'B', (byte)'C', (byte)'D',
                (byte)'E', (byte)'F', (byte)'G', (byte)'H', (byte)'I', (byte)'J', (byte)'K', (byte)'L',
                (byte)'M', (byte)'N', (byte)'O', (byte)'P', (byte)'Q', (byte)'R', (byte)'S', (byte)'T',
                (byte)'U', (byte)'V', (byte)'W', (byte)'X', (byte)'Y', (byte)'Z'
            };
        }
    }
}
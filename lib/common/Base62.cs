using System;
using System.Collections.Generic;
using System.Linq;

namespace lib.common
{
    /**
     * A Base62 encoder/decoder.
     *
     * This is a C# port of the original Java implementation by Sebastian Ruhleder.
     * @author Sebastian Ruhleder, sebastian@seruco.io
     */
    public class Base62
    {
        private const int STANDARD_BASE = 256;
        private const int TARGET_BASE = 62;
        private readonly byte[] _alphabet;
        private readonly byte[] _lookup;

        private Base62(byte[] alphabet)
        {
            _alphabet = alphabet;
            _lookup = CreateLookupTable(alphabet);
        }
        
        /// <summary>
        /// Creates a Base62 instance. Defaults to the GMP-style character set.
        /// </summary>
        /// <returns>A Base62 instance.</returns>
        public static Base62 CreateInstance()
        {
            return CreateInstanceWithGmpCharacterSet();
        }

        /// <summary>
        /// Creates a Base62 instance using the GMP-style character set.
        /// </summary>
        /// <returns>A Base62 instance.</returns>
        public static Base62 CreateInstanceWithGmpCharacterSet()
        {
            return new Base62(CharacterSets.Gmp);
        }

        /// <summary>
        /// Creates a Base62 instance using the inverted character set.
        /// </summary>
        /// <returns>A Base62 instance.</returns>
        public static Base62 CreateInstanceWithInvertedCharacterSet()
        {
            return new Base62(CharacterSets.Inverted);
        }

        /// <summary>
        /// Encodes a sequence of bytes in Base62 encoding and pads it accordingly.
        /// </summary>
        /// <param name="message">A byte sequence.</param>
        /// <param name="length">The expected length of the output. If -1, length is estimated.</param>
        /// <returns>A sequence of Base62-encoded bytes.</returns>
        public byte[] Encode(byte[] message, int length)
        {
            byte[] indices = Convert(message, STANDARD_BASE, TARGET_BASE, length);
            return Translate(indices, _alphabet);
        }

        /// <summary>
        /// Encodes a sequence of bytes in Base62 encoding.
        /// </summary>
        /// <param name="message">A byte sequence.</param>
        /// <returns>A sequence of Base62-encoded bytes.</returns>
        public byte[] Encode(byte[] message)
        {
            return Encode(message, -1);
        }

        /// <summary>
        /// Decodes a sequence of Base62-encoded bytes and pads it accordingly.
        /// </summary>
        /// <param name="encoded">A sequence of Base62-encoded bytes.</param>
        /// <param name="length">The expected length of the output. If -1, length is estimated.</param>
        /// <returns>A byte sequence.</returns>
        public byte[] Decode(byte[] encoded, int length)
        {
            byte[] prepared = Translate(encoded, _lookup);
            return Convert(prepared, TARGET_BASE, STANDARD_BASE, length);
        }

        /// <summary>
        /// Decodes a sequence of Base62-encoded bytes.
        /// </summary>
        /// <param name="encoded">A sequence of Base62-encoded bytes.</param>
        /// <returns>A byte sequence.</returns>
        public byte[] Decode(byte[] encoded)
        {
            return Decode(encoded, -1);
        }

        /// <summary>
        /// Uses the elements of a byte array as indices to a dictionary and returns the corresponding values
        /// in form of a byte array.
        /// </summary>
        private byte[] Translate(byte[] indices, byte[] dictionary)
        {
            byte[] translation = new byte[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                translation[i] = dictionary[indices[i]];
            }
            return translation;
        }

        /// <summary>
        /// Converts a byte array from a source base to a target base.
        /// </summary>
        private byte[] Convert(byte[] message, int sourceBase, int targetBase, int length)
        {
            int estimatedLength = length == -1 ? EstimateOutputLength(message.Length, sourceBase, targetBase) : length;

            var source = message.ToList();
            var result = new List<byte>(estimatedLength);

            while (source.Count > 0)
            {
                var quotient = new List<byte>();
                int remainder = 0;

                foreach (byte b in source)
                {
                    int accumulator = b + remainder * sourceBase;
                    
                    int digit = accumulator / targetBase;
                    remainder = accumulator % targetBase;

                    if (quotient.Count > 0 || digit > 0)
                    {
                        quotient.Add((byte)digit);
                    }
                }

                result.Add((byte)remainder);
                source = quotient;
            }
            
            int resultCount = result.Count;
            if (estimatedLength > resultCount)
            {
                for (int i = 0; i < estimatedLength - resultCount; i++)
                {
                    result.Add(0);
                }
            }

            if (estimatedLength < result.Count)
            {
                result = result.Take(estimatedLength).ToList();
            }

            result.Reverse();
            return result.ToArray();
        }

        /// <summary>
        /// Estimates the length of the output in bytes.
        /// </summary>
        private int EstimateOutputLength(int inputLength, int sourceBase, int targetBase)
        {
            return (int)Math.Ceiling(Math.Log(sourceBase) / Math.Log(targetBase) * inputLength);
        }

        /// <summary>
        /// Creates the lookup table from character to index of character in character set.
        /// </summary>
        private byte[] CreateLookupTable(byte[] alphabet)
        {
            var lookup = new byte[256];
            for (int i = 0; i < alphabet.Length; i++)
            {
                lookup[alphabet[i]] = (byte) i;
            }
            return lookup;
        }

        private static class CharacterSets
        {
            public static readonly byte[] Gmp =
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

            public static readonly byte[] Inverted =
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
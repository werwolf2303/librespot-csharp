using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Utilities;
using spotify.metadata.proto;

namespace lib.common
{
    public class Utils
    {
        private static char[] hexArray = "0123456789ABCDEF".ToCharArray();
        
        public static byte[] hexToBytes(string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even length.", nameof(hexString));
            }
            byte[] data = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
            {
                int highByte = Uri.FromHex(hexString[i]);
                int lowByte = Uri.FromHex(hexString[i + 1]);
                data[i / 2] = (byte)((highByte << 4) | lowByte);
            }
            return data;
        }

        public static String bytesToHex(byte[] bytes) {
            return bytesToHex(bytes, 0, bytes.Length, false, -1);
        }
        
        public static String bytesToHex(byte[] bytes, int off, int len) {
            return bytesToHex(bytes, off, len, false, -1);
        }
        
        public static String bytesToHex(byte[] bytes, int offset, int length, bool trim, int minLength) {
            if (bytes == null) return "";

            int newOffset = 0;
            bool trimming = trim;
            char[] hexChars = new char[length * 2];
            for (int j = 0; j < length; j++) {
                int v = bytes[offset + j] & 0xFF;
                if (trimming) {
                    if (v == 0) {
                        newOffset = j + 1;

                        if (minLength != -1 && length - newOffset == minLength)
                            trimming = false;

                        continue;
                    }
                    else
                    {
                        trimming = false;
                    }
                }

                hexChars[j * 2] = hexArray[(uint)v >> 4];
                hexChars[j * 2 + 1] = hexArray[v & 0x0F];
            }

            return new String(hexChars, newOffset * 2, hexChars.Length - newOffset * 2);
        }
        
        public static string ConvertApsToString(Dictionary<string, List<string>> dictionary)
        {
            if (dictionary == null)
            {
                return "null";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("{");

            foreach (var entry in dictionary)
            {
                string key = entry.Key ?? "null"; 
                List<string> values = entry.Value;
                sb.Append($"  \"{key}\": [");
                if (values != null)
                {
                    sb.Append(string.Join(", ", values.Select(v => $"\"{v}\"")));
                }
                else
                {
                    sb.Append("null");
                }
                sb.Append("],");
            }
            if (dictionary.Any())
            {
                sb.Length--; 
            }
            sb.Append("}");
            return sb.ToString();
        }
        
        public static String randomHexString(RandomNumberGenerator random, int length) {
            byte[] bytes = new byte[length / 2];
            random.GetBytes(bytes);
            return bytesToHex(bytes, 0, bytes.Length, false, length);
        }

        public static byte[] toByteArray(int i) {
            return new [] 
            {
                (byte)((i >> 24) & 0xFF),
                (byte)((i >> 16) & 0xFF),
                (byte)((i >> 8) & 0xFF),
                (byte)(i & 0xFF)
            };
        }

        public static byte[] toByteArray(BigInteger i)
        {
            byte[] array = i.ToByteArray();
            if (array[array.Length - 1] == 0)
                array = array.Take(array.Length - 1).ToArray();
            Array.Reverse(array);
            return array;
        }

        public static long getUnixTimeStampInMilliseconds()
        {
            DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime currentUtcTime = DateTime.UtcNow;
            TimeSpan diff = currentUtcTime - unixEpoch;
            return (long)diff.TotalMilliseconds;
        }
        
        public static List<AudioFile.Format> formatsToString(List<AudioFile> files) {
            List<AudioFile.Format> list = new List<AudioFile.Format>(files.Count);
            foreach (AudioFile file in files) list.Add(file.format);
            return list;
        }
        
        public static byte[] ToBigEndian(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }
    }
}
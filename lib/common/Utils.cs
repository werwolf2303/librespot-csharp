using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            for (int j = offset; j < length; j++) {
                int v = bytes[j] & 0xFF;
                if (trimming) {
                    if (v == 0) {
                        newOffset = j + 1;

                        if (minLength != -1 && length - newOffset == minLength)
                            trimming = false;

                        continue;
                    } else {
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
                sb.Length -= Environment.NewLine.Length + 1; 
            }
            sb.Append("}");
            return sb.ToString();
        }
        
        public enum TimeUnit
        {
            NANOSECONDS,
            MICROSECONDS,
            MILLISECONDS,
            SECONDS,
            MINUTES,
            HOURS,
            DAYS
        }
        
        // https://stackoverflow.com/a/78491060
        public class SecureRandom : IDisposable
        {
            private RandomNumberGenerator rng = RandomNumberGenerator.Create();

            public int GenerateRandom(int minValue, int maxValue)
            {
                if (minValue >= maxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must be less than maxValue");
                }

                int range = maxValue - minValue + 1;
                byte[] uint32Buffer = new byte[4];

                int result;
                do
                {
                    rng.GetBytes(uint32Buffer);
                    uint randomUint = BitConverter.ToUInt32(uint32Buffer, 0);
                    result = (int)(randomUint % range);
                } while (result < 0 || result >= range);

                return minValue + result;
            }

            public void Dispose()
            {
                rng.Dispose();
            }
        }
        
        public static String randomHexString(Random random, int length) {
            byte[] bytes = new byte[length / 2];
            random.NextBytes(bytes);
            return bytesToHex(bytes, 0, bytes.Length, false, length);
        }

        public static byte[] toByteArray(int i) {
            var buffer = new byte[4];
            buffer[0] = Convert.ToByte(i);
            return buffer;
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
    }
}
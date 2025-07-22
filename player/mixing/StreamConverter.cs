using System;
using System.IO;
using sink_api;

namespace player.mixing
{
public sealed class StreamConverter : Stream
    {
        private readonly bool _monoToStereo;
        private readonly int _sampleSizeFrom;
        private readonly int _sampleSizeTo;
        private byte[] _buffer;

        private StreamConverter(OutputAudioFormat from, OutputAudioFormat to)
        {
            _monoToStereo = from.getChannels() == 1 && to.getChannels() == 2;
            _sampleSizeFrom = from.getSampleSizeInBits();
            _sampleSizeTo = to.getSampleSizeInBits();
        }

        public static bool CanConvert(OutputAudioFormat from, OutputAudioFormat to)
        {
            if (from.isBigEndian() || to.isBigEndian()) return false;
            if (from.matches(to)) return true;
            if (!Equals(from.getEncoding(), to.getEncoding())) return false;
            return from.getSampleRate() == to.getSampleRate();
        }

        public static StreamConverter Converter(OutputAudioFormat from, OutputAudioFormat to)
        {
            if (!CanConvert(from, to))
                throw new NotSupportedException($"Cannot convert from '{from}' to '{to}'");

            return new StreamConverter(from, to);
        }

        private static byte[] MonoToStereo(byte[] src, int sampleSizeBits)
        {
            if (sampleSizeBits != 16) throw new NotSupportedException();

            byte[] result = new byte[src.Length * 2];
            for (int i = 0; i < src.Length - 1; i += 2)
            {
                result[i * 2] = src[i];
                result[i * 2 + 1] = src[i + 1];
                result[i * 2 + 2] = src[i];
                result[i * 2 + 3] = src[i + 1];
            }

            return result;
        }

        private static byte[] SampleSizeConversion(byte[] src, int fromSampleSize, int toSampleSize)
        {
            int sampleConversionRatio = toSampleSize / fromSampleSize;
            if (sampleConversionRatio == 1) return src;
            
            int fromSampleSizeByte = fromSampleSize / 8;
            int toSampleSizeByte = toSampleSize / 8;

            byte[] result = new byte[src.Length * sampleConversionRatio];
            for (int i = 0, j = 0; i < src.Length; i += fromSampleSizeByte, j += toSampleSizeByte)
            {
                float val;
                if (fromSampleSize == 8)
                {
                    val = (sbyte)src[i];
                    val /= 128f;
                }
                else if (fromSampleSize == 16)
                {
                    val = (short)((src[i] & 0xFF) | ((src[i + 1] & 0xFF) << 8));
                    val /= 32768f;
                }
                else
                {
                    throw new NotSupportedException("Sample size: " + fromSampleSize);
                }

                if (toSampleSize == 8)
                {
                    sbyte s = (sbyte)(val * 127); 
                    result[j] = (byte)s;
                }
                else if (toSampleSize == 16)
                {
                    short s = (short)(val * 32767);
                    result[j] = (byte)s;
                    result[j + 1] = (byte)(((uint)s) >> 8);
                }
                else
                {
                    throw new NotSupportedException("Sample size: " + toSampleSize);
                }
            }

            return result;
        }

        public byte[] Convert()
        {
            byte[] result = SampleSizeConversion(_buffer, _sampleSizeFrom, _sampleSizeTo);
            if (_monoToStereo) result = MonoToStereo(result, _sampleSizeTo);
            return result;
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_buffer == null || _buffer.Length != count) _buffer = new byte[count];
            Array.Copy(buffer, offset, _buffer, 0, count);
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
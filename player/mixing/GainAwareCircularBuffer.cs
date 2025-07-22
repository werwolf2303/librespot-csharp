using System.Threading;

namespace player.mixing
{
    public class GainAwareCircularBuffer : CircularBuffer
    {
        public GainAwareCircularBuffer(int bufferSize) : base(bufferSize)
        {
        }

        private static void WriteToArray(int val, byte[] b, int dest)
        {
            if (val > 32767) val = 32767;
            else if (val < -32768) val = -32768;
            else if (val < 0) val |= 32768;

            b[dest] = (byte)val;
            b[dest + 1] = (byte)(((uint)val) >> 8);
        }

        public void ReadGain(byte[] b, int off, int len, float gain)
        {
            if (_closed) return;

            lock (_lock)
            {
                AwaitData(len);
                if (_closed) return;

                int dest = off;
                for (int i = 0; i < len; i += 2, dest += 2)
                {
                    int val = (short)((ReadInternal() & 0xFF) | ((ReadInternal() & 0xFF) << 8));
                    val = (int)(val * gain);
                    WriteToArray(val, b, dest);
                }

                Monitor.PulseAll(_lock);
            }
        }

        public void ReadMergeGain(byte[] b, int off, int len, float globalGain, float firstGain, float secondGain)
        {
            if (_closed) return;

            lock (_lock)
            {
                AwaitData(len);
                if (_closed) return;

                int dest = off;
                for (int i = 0; i < len; i += 2, dest += 2)
                {
                    short first = (short)((b[dest] & 0xFF) | ((b[dest + 1] & 0xFF) << 8));
                    first = (short)(first * firstGain);

                    short second = (short)((ReadInternal() & 0xFF) | ((ReadInternal() & 0xFF) << 8));
                    second = (short)(second * secondGain);

                    int result = (int)((first + second) * globalGain);
                    WriteToArray(result, b, dest);
                }

                Monitor.PulseAll(_lock);
            }
        }
    }
}
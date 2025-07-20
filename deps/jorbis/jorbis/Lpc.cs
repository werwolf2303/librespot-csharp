using System;

namespace deps.jorbis.jorbis
{
    public class Lpc
    {
        private readonly Drft _fft = new Drft();
        private int _ln;
        private int _m;

        public static float LpcFromData(float[] data, float[] lpc, int n, int m)
        {
            float[] aut = new float[m + 1];
            int i, j;

            j = m + 1;
            while (j-- != 0)
            {
                float d = 0;
                for (i = j; i < n; i++)
                    d += data[i] * data[i - j];
                aut[j] = d;
            }

            float error = aut[0];
            if (Math.Abs(error) < 1.0e-9) // A small tolerance check for floating point zero
            {
                Array.Clear(lpc, 0, m);
                return 0;
            }

            for (i = 0; i < m; i++)
            {
                float r = -aut[i + 1];

                if (Math.Abs(error) < 1.0e-9)
                {
                    Array.Clear(lpc, 0, m);
                    return 0;
                }

                for (j = 0; j < i; j++)
                    r -= lpc[j] * aut[i - j];
                r /= error;

                lpc[i] = r;
                for (j = 0; j < i / 2; j++)
                {
                    float tmp = lpc[j];
                    lpc[j] += r * lpc[i - 1 - j];
                    lpc[i - 1 - j] += r * tmp;
                }

                if ((i % 2) != 0)
                    lpc[j] += lpc[j] * r;

                error *= 1.0f - r * r;
            }

            return error;
        }

        public float LpcFromCurve(float[] curve, float[] lpc)
        {
            int n = _ln;
            float[] work = new float[n + n];
            float fscale = 0.5f / n;

            for (int i = 0; i < n; i++)
            {
                work[i * 2] = curve[i] * fscale;
                work[i * 2 + 1] = 0;
            }

            work[n * 2 - 1] = curve[n - 1] * fscale;

            n *= 2;
            _fft.Backward(work);

            for (int i = 0, j = n / 2; i < n / 2;)
            {
                float temp = work[i];
                work[i++] = work[j];
                work[j++] = temp;
            }

            return LpcFromData(work, lpc, n, _m);
        }

        public void Init(int mapped, int m)
        {
            _ln = mapped;
            this._m = m;
            _fft.Init(mapped * 2);
        }

        public void Clear()
        {
            _fft.Clear();
        }

        private static float FastHypot(float a, float b)
        {
            return (float)Math.Sqrt(a * a + b * b);
        }

        public void LpcToCurve(float[] curve, float[] lpc, float amp)
        {
            Array.Clear(curve, 0, _ln * 2);

            if (amp == 0)
                return;

            for (int i = 0; i < _m; i++)
            {
                curve[i * 2 + 1] = lpc[i] / (4 * amp);
                curve[i * 2 + 2] = -lpc[i] / (4 * amp);
            }

            _fft.Backward(curve);

            int l2 = _ln * 2;
            float unit = 1.0f / amp;
            curve[0] = 1.0f / (curve[0] * 2 + unit);
            for (int i = 1; i < _ln; i++)
            {
                float real = curve[i] + curve[l2 - i];
                float imag = curve[i] - curve[l2 - i];
                float a = real + unit;
                curve[i] = 1.0f / FastHypot(a, imag);
            }
        }
    }
}
using System;

namespace deps.jorbis.jorbis
{
    public class Mdct
    {
        private int _n;
        private int _log2n;
        private float[] _trig;
        private int[] _bitrev;
        private float _scale;

        private float[] _x = new float[1024];
        private float[] _w = new float[1024];
        private readonly object _backwardLock = new object();

        public void Init(int n)
        {
            _bitrev = new int[n / 4];
            _trig = new float[n + n / 4];
            _log2n = (int)Math.Round(Math.Log(n) / Math.Log(2));
            this._n = n;

            const int ae = 0;
            const int ao = 1;
            int be = ae + n / 2;
            int bo = be + 1;
            int ce = be + n / 2;
            int co = ce + 1;

            for (int i = 0; i < n / 4; i++)
            {
                _trig[ae + i * 2] = (float)Math.Cos((Math.PI / n) * (4 * i));
                _trig[ao + i * 2] = (float)-Math.Sin((Math.PI / n) * (4 * i));
                _trig[be + i * 2] = (float)Math.Cos((Math.PI / (2 * n)) * (2 * i + 1));
                _trig[bo + i * 2] = (float)Math.Sin((Math.PI / (2 * n)) * (2 * i + 1));
            }

            for (int i = 0; i < n / 8; i++)
            {
                _trig[ce + i * 2] = (float)Math.Cos((Math.PI / n) * (4 * i + 2));
                _trig[co + i * 2] = (float)-Math.Sin((Math.PI / n) * (4 * i + 2));
            }

            int mask = (1 << (_log2n - 1)) - 1;
            int msb = 1 << (_log2n - 2);
            for (int i = 0; i < n / 8; i++)
            {
                int acc = 0;
                for (int j = 0; (uint)msb >> j != 0; j++)
                    if ((((uint)msb >> j) & i) != 0)
                        acc |= 1 << j;
                _bitrev[i * 2] = (~acc) & mask;
                _bitrev[i * 2 + 1] = acc;
            }

            _scale = 4.0f / n;
        }

        public void Clear()
        {
        }

        public void Forward(float[] input, float[] output)
        {
        }

        public void Backward(float[] input, float[] output)
        {
            lock (_backwardLock)
            {
                if (_x.Length < _n / 2) _x = new float[_n / 2];
                if (_w.Length < _n / 2) _w = new float[_n / 2];

                float[] x = _x;
                float[] w = _w;
                int n2 = (int) ((uint) _n >> 1);
                int n4 = (int) ((uint) _n >> 2);
                int n8 = (int) ((uint) _n >> 3);

                // rotate + step 1
                int inO = 1;
                int xO = 0;
                int A = n2;
                for (int i = 0; i < n8; i++)
                {
                    A -= 2;
                    x[xO++] = -input[inO + 2] * _trig[A + 1] - input[inO] * _trig[A];
                    x[xO++] = input[inO] * _trig[A + 1] - input[inO + 2] * _trig[A];
                    inO += 4;
                }

                inO = n2 - 4;
                for (int i = 0; i < n8; i++)
                {
                    A -= 2;
                    x[xO++] = input[inO] * _trig[A + 1] + input[inO + 2] * _trig[A];
                    x[xO++] = input[inO] * _trig[A] - input[inO + 2] * _trig[A + 1];
                    inO -= 4;
                }

                float[] xxx = MdctKernel(x, w, _n, n2, n4, n8);
                int xx = 0;

                // step 8
                int B = n2;
                int o1 = n4, o2 = o1 - 1;
                int o3 = n4 + n2, o4 = o3 - 1;
                for (int i = 0; i < n4; i++)
                {
                    float temp1 = (xxx[xx] * _trig[B + 1] - xxx[xx + 1] * _trig[B]);
                    float temp2 = -(xxx[xx] * _trig[B] + xxx[xx + 1] * _trig[B + 1]);

                    output[o1] = -temp1;
                    output[o2] = temp1;
                    output[o3] = temp2;
                    output[o4] = temp2;

                    o1++;
                    o2--;
                    o3++;
                    o4--;
                    xx += 2;
                    B += 2;
                }
            }
        }

        private float[] MdctKernel(float[] x, float[] w, int n, int n2, int n4, int n8)
        {
            // step 2
            int xA = n4;
            int xB = 0;
            int w2 = n4;
            int A = n2;
            for (int i = 0; i < n4;)
            {
                float x0 = x[xA] - x[xB];
                float x1;
                w[w2 + i] = x[xA++] + x[xB++];
                
                x1 = x[xA] - x[xB];
                A -= 4;
                
                w[i++] = x0 * _trig[A] + x1 * _trig[A + 1];
                w[i] = x1 * _trig[A] - x0 * _trig[A + 1];

                w[w2 + i] = x[xA++] + x[xB++];
                i++;
            }

            // step 3
            for (int i = 0; i < _log2n - 3; i++)
            {
                int k0 = (int) ((uint) n >> (i + 2));
                int k1 = 1 << (i + 3);
                int wbase = n2 - 2;
                A = 0;
                float[] temp;
                for (int r = 0; r < (k0 >> 2); r++)
                {
                    int w1 = wbase;
                    w2 = w1 - (k0 >> 1);
                    float AEv = _trig[A], wA;
                    float AOv = _trig[A + 1], wB;
                    wbase -= 2;

                    k0++;
                    for (int s = 0; s < (2 << i); s++)
                    {
                        wB = w[w1] - w[w2];
                        x[w1] = w[w1] + w[w2]; 
                        wA = w[++w1] - w[++w2];
                        x[w1] = w[w1] + w[w2];
                        x[w2] = wA * AEv - wB * AOv;
                        x[w2 - 1] = wB * AEv + wA * AOv;
                        w1 -= k0;
                        w2 -= k0;
                    }

                    k0--;
                    A += k1;
                }

                temp = w;
                w = x;
                x = temp;
            }

            // step 4, 5, 6, 7
            int C = n;
            int bit = 0;
            int x1_ = 0;
            int x2_ = n2 - 1;
            for (int i = 0; i < n8; i++)
            {
                int t1 = _bitrev[bit++];
                int t2 = _bitrev[bit++];

                float wA = w[t1] - w[t2 + 1];
                float wB = w[t1 - 1] + w[t2];
                float wC = w[t1] + w[t2 + 1];
                float wD = w[t1 - 1] - w[t2];

                float wACE = wA * _trig[C];
                float wBCE = wB * _trig[C++];
                float wACO = wA * _trig[C];
                float wBCO = wB * _trig[C++];

                x[x1_++] = (wC + wACO + wBCE) * 0.5f;
                x[x2_--] = (-wD + wBCO - wACE) * 0.5f;
                x[x1_++] = (wD + wBCO - wACE) * 0.5f;
                x[x2_--] = (wC - wACO - wBCE) * 0.5f;
            }

            return x;
        }
    }
}
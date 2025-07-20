using System.Runtime.InteropServices;

namespace deps.jorbis.jorbis
{
    public static class Lsp
    {
        private const float M_PI = 3.1415926539f;

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [FieldOffset(0)] public float f;

            [FieldOffset(0)] public int i;
        }

        public static void LspToCurve(float[] curve, int[] map, int n, int ln, float[] lsp, int m, float amp,
            float ampoffset)
        {
            float wdel = M_PI / ln;
            for (int i = 0; i < m; i++)
                lsp[i] = Lookup.CosLook(lsp[i]);

            int m2 = (m / 2) * 2;
            int i_ptr = 0;

            while (i_ptr < n)
            {
                int k = map[i_ptr];
                float p = 0.7071067812f;
                float q = 0.7071067812f;
                float w = Lookup.CosLook(wdel * k);

                for (int j = 0; j < m2; j += 2)
                {
                    q *= lsp[j] - w;
                    p *= lsp[j + 1] - w;
                }

                if ((m & 1) != 0)
                {
                    q *= lsp[m - 1] - w;
                    q *= q;
                    p *= p * (1.0f - w * w);
                }
                else
                {
                    q *= q * (1.0f + w);
                    p *= p * (1.0f - w);
                }

                q = p + q;

                FloatIntUnion union = new FloatIntUnion { f = q };
                int hx = union.i;
                int ix = 0x7fffffff & hx;
                int qexp = 0;

                if (ix >= 0x7f800000 || ix == 0)
                {
                    // 0, inf, nan
                }
                else
                {
                    if (ix < 0x00800000) // subnormal
                    {
                        q *= 3.3554432000e+07f; // 0x4c000000
                        union.f = q;
                        hx = union.i;
                        ix = 0x7fffffff & hx;
                        qexp = -25;
                    }

                    qexp += (int)((uint)ix >> 23) - 126;
                    hx = (int)(((uint)hx & 0x807FFFFFu) | 0x3F000000u);
                    union.i = hx;
                    q = union.f;
                }

                q = Lookup.FromDbLook(amp * Lookup.InvSqLook(q) * Lookup.InvSq2ExpLook(qexp + m) - ampoffset);

                do
                {
                    curve[i_ptr++] *= q;
                } while (i_ptr < n && map[i_ptr] == k);
            }
        }
    }
}
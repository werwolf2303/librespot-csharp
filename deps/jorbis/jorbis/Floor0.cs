using System;
using System.Runtime.CompilerServices;
using Buffer = deps.jorbis.jogg.Buffer;

namespace deps.jorbis.jorbis
{
    public class Floor0 : FuncFloor
    {
        private float[] _lsp;

        public override void Pack(object i, Buffer opb)
        {
            InfoFloor0 info = (InfoFloor0)i;
            opb.Write(info.Order, 8);
            opb.Write(info.Rate, 16);
            opb.Write(info.BarkMap, 16);
            opb.Write(info.AmpBits, 6);
            opb.Write(info.AmpdB, 8);
            opb.Write(info.NumBooks - 1, 4);
            for (int j = 0; j < info.NumBooks; j++)
                opb.Write(info.Books[j], 8);
        }

        public override object Unpack(Info vi, Buffer opb)
        {
            InfoFloor0 info = new InfoFloor0();
            info.Order = opb.Read(8);
            info.Rate = opb.Read(16);
            info.BarkMap = opb.Read(16);
            info.AmpBits = opb.Read(6);
            info.AmpdB = opb.Read(8);
            info.NumBooks = opb.Read(4) + 1;

            if ((info.Order < 1) || (info.Rate < 1) || (info.BarkMap < 1) || (info.NumBooks < 1))
            {
                return null;
            }

            for (int j = 0; j < info.NumBooks; j++)
            {
                info.Books[j] = opb.Read(8);
                if (info.Books[j] < 0 || info.Books[j] >= vi.Books)
                {
                    return null;
                }
            }

            return info;
        }

        public override object Look(DspState vd, InfoMode mi, object i)
        {
            float scale;
            Info vi = vd.Vi;
            InfoFloor0 info = (InfoFloor0)i;
            LookFloor0 look = new LookFloor0();
            look.M = info.Order;
            look.N = vi.BlockSizes[mi.BlockFlag] / 2;
            look.Ln = info.BarkMap;
            look.Vi = info;
            look.LpcLook.Init(look.Ln, look.M);

            scale = look.Ln / ToBark((float)(info.Rate / 2.0));

            look.LinearMap = new int[look.N];
            for (int j = 0; j < look.N; j++)
            {
                int val = (int)Math.Floor(ToBark((float)((info.Rate / 2.0) / look.N * j)) * scale);
                if (val >= look.Ln)
                    val = look.Ln; // Guard against approximation
                look.LinearMap[j] = val;
            }

            return look;
        }

        private static float ToBark(float f)
        {
            return (float)(13.1 * Math.Atan(.00074 * (f)) + 2.24 * Math.Atan((f) * (f) * 1.85e-8) + 1e-4 * (f));
        }

        public override object State(object i)
        {
            EchstateFloor0 state = new EchstateFloor0();
            InfoFloor0 info = (InfoFloor0)i;
            state.Codewords = new int[info.Order];
            state.Curve = new float[info.BarkMap];
            state.FrameNo = -1;
            return state;
        }

        public override void FreeInfo(object i)
        {
        }

        public override void FreeLook(object i)
        {
        }

        public override void FreeState(object vs)
        {
        }

        public override int Forward(Block vb, object i, float[] @in, float[] @out, object vs)
        {
            return 0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override int Inverse(Block vb, object i, float[] @out)
        {
            LookFloor0 look = (LookFloor0)i;
            InfoFloor0 info = look.Vi;
            int ampraw = vb.Opb.Read(info.AmpBits);

            if (ampraw > 0)
            {
                int maxval = (1 << info.AmpBits) - 1;
                float amp = (float)ampraw / maxval * info.AmpdB;
                int booknum = vb.Opb.Read(Util.ILog(info.NumBooks));

                if (booknum != -1 && booknum < info.NumBooks)
                {
                    if (_lsp == null || _lsp.Length < look.M)
                    {
                        _lsp = new float[look.M];
                    }
                    else
                    {
                        for (int j = 0; j < look.M; j++)
                            _lsp[j] = 0.0f;
                    }

                    CodeBook b = vb.Vd.FullBooks[info.Books[booknum]];
                    float last = 0.0f;

                    for (int j = 0; j < look.M; j++)
                        @out[j] = 0.0f;

                    for (int j = 0; j < look.M; j += b.Dim)
                    {
                        if (b.DecodeVs(_lsp, j, vb.Opb, 1, -1) == -1)
                        {
                            for (int k = 0; k < look.N; k++)
                                @out[k] = 0.0f;
                            return 0;
                        }
                    }

                    for (int j = 0; j < look.M;)
                    {
                        for (int k = 0; k < b.Dim; k++, j++)
                            _lsp[j] += last;
                        last = _lsp[j - 1];
                    }

                    Lsp.LspToCurve(@out, look.LinearMap, look.N, look.Ln, _lsp, look.M, amp, info.AmpdB);
                    return 1;
                }
            }

            return 0;
        }

        public override object Inverse1(Block vb, object i, object memo)
        {
            LookFloor0 look = (LookFloor0)i;
            InfoFloor0 info = look.Vi;
            float[] lsp = null;
            if (memo is float[])
            {
                lsp = (float[])memo;
            }

            int ampraw = vb.Opb.Read(info.AmpBits);
            if (ampraw > 0)
            {
                int maxval = (1 << info.AmpBits) - 1;
                float amp = (float)ampraw / maxval * info.AmpdB;
                int booknum = vb.Opb.Read(Util.ILog(info.NumBooks));

                if (booknum != -1 && booknum < info.NumBooks)
                {
                    CodeBook b = vb.Vd.FullBooks[info.Books[booknum]];
                    float last = 0.0f;

                    if (lsp == null || lsp.Length < look.M + 1)
                    {
                        lsp = new float[look.M + 1];
                    }
                    else
                    {
                        for (int j = 0; j < lsp.Length; j++)
                            lsp[j] = 0.0f;
                    }

                    for (int j = 0; j < look.M; j += b.Dim)
                    {
                        if (b.DecodeVSet(lsp, j, vb.Opb, b.Dim) == -1)
                        {
                            return null;
                        }
                    }

                    for (int j = 0; j < look.M;)
                    {
                        for (int k = 0; k < b.Dim; k++, j++)
                            lsp[j] += last;
                        last = lsp[j - 1];
                    }

                    lsp[look.M] = amp;
                    return lsp;
                }
            }

            return null;
        }

        public override int Inverse2(Block vb, object i, object memo, float[] @out)
        {
            LookFloor0 look = (LookFloor0)i;
            InfoFloor0 info = look.Vi;

            if (memo != null)
            {
                float[] lsp = (float[])memo;
                float amp = lsp[look.M];
                Lsp.LspToCurve(@out, look.LinearMap, look.N, look.Ln, lsp, look.M, amp, info.AmpdB);
                return 1;
            }

            for (int j = 0; j < look.N; j++)
                @out[j] = 0.0f;
            return 0;
        }

        public class InfoFloor0
        {
            public int Order { get; set; }
            public int Rate { get; set; }
            public int BarkMap { get; set; }
            public int AmpBits { get; set; }
            public int AmpdB { get; set; }
            public int NumBooks { get; set; }
            public int[] Books { get; set; } = new int[16];
        }

        public class LookFloor0
        {
            public int N { get; set; }
            public int Ln { get; set; }
            public int M { get; set; }
            public int[] LinearMap { get; set; }
            public InfoFloor0 Vi { get; set; }
            public Lpc LpcLook { get; set; } = new Lpc();
        }

        public class EchstateFloor0
        {
            public int[] Codewords { get; set; }
            public float[] Curve { get; set; }
            public long FrameNo { get; set; }
            public long Codes { get; set; }
        }
    }
}
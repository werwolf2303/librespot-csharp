using System;

namespace deps.jorbis.jorbis
{
    public class DspState
    {
        private const float Pi = 3.1415926539f;
        private const int ViTransformB = 1;
        private const int ViWindowB = 1;

        public int ModeBits;
        private float[][] _pcm;
        private int _pcmStorage;
        private int _pcmCurrent;
        private int _pcmReturned;
        private float[] _multipliers;
        private int _envelopeStorage;
        private int _envelopeCurrent;
        private long _glueBits;
        private long _timeBits;
        private long _floorBits;
        private long _resBits;
        public float[][][][][] Window;
        public object[][] Transform;
        public object[] Mode;

        public int AnalysisP { get; set; }
        public Info Vi { get; set; }
        public int EofFlag { get; set; }
        public int LW { get; set; }
        public int W { get; set; }
        public int NW { get; set; }
        public int CenterW { get; set; }
        public long GranulePos { get; set; }
        public long Sequence { get; set; }
        public CodeBook[] FullBooks { get; set; }
        public byte[] Header { get; set; }
        public byte[] Header1 { get; set; }
        public byte[] Header2 { get; set; }

        public DspState()
        {
            Transform = new object[2][];
            Window = new float[2][][][][];
            Window[0] = new float[2][][][];
            Window[0][0] = new float[2][][];
            Window[0][1] = new float[2][][];
            Window[0][0][0] = new float[2][];
            Window[0][0][1] = new float[2][];
            Window[0][1][0] = new float[2][];
            Window[0][1][1] = new float[2][];
            Window[1] = new float[2][][][];
            Window[1][0] = new float[2][][];
            Window[1][1] = new float[2][][];
            Window[1][0][0] = new float[2][];
            Window[1][0][1] = new float[2][];
            Window[1][1][0] = new float[2][];
            Window[1][1][1] = new float[2][];
        }

        private static float[] CreateWindow(int type, int window, int left, int right)
        {
            float[] ret = new float[window];
            switch (type)
            {
                case 0:
                    int leftbegin = window / 4 - left / 2;
                    int rightbegin = window - window / 4 - right / 2;

                    for (int i = 0; i < left; i++)
                    {
                        float x = (float)((i + .5) / left * Pi / 2.0);
                        x = (float)Math.Sin(x);
                        x *= x;
                        x *= (float)(Pi / 2.0);
                        x = (float)Math.Sin(x);
                        ret[i + leftbegin] = x;
                    }

                    for (int i = leftbegin + left; i < rightbegin; i++)
                    {
                        ret[i] = 1.0f;
                    }

                    for (int i = 0; i < right; i++)
                    {
                        float x = (float)((right - i - .5) / right * Pi / 2.0);
                        x = (float)Math.Sin(x);
                        x *= x;
                        x *= (float)(Pi / 2.0);
                        x = (float)Math.Sin(x);
                        ret[i + rightbegin] = x;
                    }

                    break;
                default:
                    return null;
            }

            return ret;
        }

        public int Init(Info vi, bool isEncoder)
        {
            this.Vi = vi;
            ModeBits = Util.ILog2(vi.Modes);

            Transform[0] = new object[ViTransformB];
            Transform[1] = new object[ViTransformB];

            Transform[0][0] = new Mdct();
            Transform[1][0] = new Mdct();
            ((Mdct)Transform[0][0]).Init(vi.BlockSizes[0]);
            ((Mdct)Transform[1][0]).Init(vi.BlockSizes[1]);

            Window[0][0][0] = new float[ViWindowB][];
            Window[0][0][1] = Window[0][0][0];
            Window[0][1][0] = Window[0][0][0];
            Window[0][1][1] = Window[0][0][0];
            Window[1][0][0] = new float[ViWindowB][];
            Window[1][0][1] = new float[ViWindowB][];
            Window[1][1][0] = new float[ViWindowB][];
            Window[1][1][1] = new float[ViWindowB][];

            for (int i = 0; i < ViWindowB; i++)
            {
                Window[0][0][0][i] = CreateWindow(i, vi.BlockSizes[0], vi.BlockSizes[0] / 2, vi.BlockSizes[0] / 2);
                Window[1][0][0][i] = CreateWindow(i, vi.BlockSizes[1], vi.BlockSizes[0] / 2, vi.BlockSizes[0] / 2);
                Window[1][0][1][i] = CreateWindow(i, vi.BlockSizes[1], vi.BlockSizes[0] / 2, vi.BlockSizes[1] / 2);
                Window[1][1][0][i] = CreateWindow(i, vi.BlockSizes[1], vi.BlockSizes[1] / 2, vi.BlockSizes[0] / 2);
                Window[1][1][1][i] = CreateWindow(i, vi.BlockSizes[1], vi.BlockSizes[1] / 2, vi.BlockSizes[1] / 2);
            }

            FullBooks = new CodeBook[vi.Books];
            for (int i = 0; i < vi.Books; i++)
            {
                FullBooks[i] = new CodeBook();
                FullBooks[i].InitDecode(vi.BookParam[i]);
            }

            _pcmStorage = 8192;
            _pcm = new float[vi.Channels][];
            for (int i = 0; i < vi.Channels; i++)
            {
                _pcm[i] = new float[_pcmStorage];
            }

            LW = 0;
            W = 0;

            CenterW = vi.BlockSizes[1] / 2;
            _pcmCurrent = CenterW;

            Mode = new object[vi.Modes];
            for (int i = 0; i < vi.Modes; i++)
            {
                int mapnum = vi.ModeParam[i].Mapping;
                int maptype = vi.MapType[mapnum];
                Mode[i] = FuncMapping.MappingP[maptype].Look(this, vi.ModeParam[i], vi.MapParam[mapnum]);
            }

            return 0;
        }

        public int SynthesisInit(Info vi)
        {
            Init(vi, false);
            _pcmReturned = CenterW;
            CenterW -= vi.BlockSizes[W] / 4 + vi.BlockSizes[LW] / 4;
            GranulePos = -1;
            Sequence = -1;
            return 0;
        }

        public DspState(Info vi) : base()
        {
            Init(vi, false);
            _pcmReturned = CenterW;
            CenterW = vi.BlockSizes[W] / 4 + vi.BlockSizes[LW] / 4;
            GranulePos = -1;
            Sequence = -1;
        }

        public int SynthesisBlockIn(Block vb)
        {
            if (CenterW > Vi.BlockSizes[1] / 2 && _pcmReturned > 8192)
            {
                int shiftPCM = CenterW - Vi.BlockSizes[1] / 2;
                shiftPCM = (_pcmReturned < shiftPCM ? _pcmReturned : shiftPCM);

                _pcmCurrent -= shiftPCM;
                CenterW -= shiftPCM;
                _pcmReturned -= shiftPCM;
                if (shiftPCM != 0)
                {
                    for (int i = 0; i < Vi.Channels; i++)
                    {
                        Array.Copy(_pcm[i], shiftPCM, _pcm[i], 0, _pcmCurrent);
                    }
                }
            }

            LW = W;
            W = vb.W;
            NW = -1;

            _glueBits += vb.GlueBits;
            _timeBits += vb.TimeBits;
            _floorBits += vb.FloorBits;
            _resBits += vb.ResBits;

            if (Sequence + 1 != vb.Sequence)
                GranulePos = -1;

            Sequence = vb.Sequence;

            int sizeW = Vi.BlockSizes[W];
            int centerW = CenterW + Vi.BlockSizes[LW] / 4 + sizeW / 4;
            int beginW = centerW - sizeW / 2;
            int endW = beginW + sizeW;
            int beginSl = 0;
            int endSl = 0;

            if (endW > _pcmStorage)
            {
                _pcmStorage = endW + Vi.BlockSizes[1];
                for (int i = 0; i < Vi.Channels; i++)
                {
                    float[] foo = new float[_pcmStorage];
                    Array.Copy(_pcm[i], 0, foo, 0, _pcm[i].Length);
                    _pcm[i] = foo;
                }
            }

            switch (W)
            {
                case 0:
                    beginSl = 0;
                    endSl = Vi.BlockSizes[0] / 2;
                    break;
                case 1:
                    beginSl = Vi.BlockSizes[1] / 4 - Vi.BlockSizes[LW] / 4;
                    endSl = beginSl + Vi.BlockSizes[LW] / 2;
                    break;
            }

            for (int j = 0; j < Vi.Channels; j++)
            {
                int pcmOffset = beginW;
                int i;
                for (i = beginSl; i < endSl; i++)
                {
                    _pcm[j][pcmOffset + i] += vb.Pcm[j][i];
                }

                for (; i < sizeW; i++)
                {
                    _pcm[j][pcmOffset + i] = vb.Pcm[j][i];
                }
            }

            if (GranulePos == -1)
            {
                GranulePos = vb.GranulePos;
            }
            else
            {
                GranulePos += (centerW - CenterW);
                if (vb.GranulePos != -1 && GranulePos != vb.GranulePos)
                {
                    if (GranulePos > vb.GranulePos && vb.EofFlag != 0)
                    {
                        centerW -= (int)(GranulePos - vb.GranulePos);
                    }

                    GranulePos = vb.GranulePos;
                }
            }

            CenterW = centerW;
            _pcmCurrent = endW;
            if (vb.EofFlag != 0)
                EofFlag = 1;

            return 0;
        }

        public int SynthesisPcmOut(float[][][] pcmOut, int[] index)
        {
            if (_pcmReturned < CenterW)
            {
                if (pcmOut != null)
                {
                    for (int i = 0; i < Vi.Channels; i++)
                    {
                        index[i] = _pcmReturned;
                    }
                    
                    pcmOut[0] = _pcm;
                }

                return CenterW - _pcmReturned;
            }

            return 0;
        }

        public int SynthesisRead(int bytes)
        {
            if (bytes != 0 && _pcmReturned + bytes > CenterW)
                return -1;
            _pcmReturned += bytes;
            return 0;
        }

        public void Clear()
        {
        }
    }
}
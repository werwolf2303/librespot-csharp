using System;
using Buffer = deps.jorbis.jogg.Buffer;

namespace deps.jorbis.jorbis
{
    public class Floor1 : FuncFloor
    {
        private const int Floor1RangeDb = 140;
        private const int VifPosit = 63;
        
        private static float[] FloorFromdBLookup = {1.0649863e-07F, 1.1341951e-07F,
      1.2079015e-07F, 1.2863978e-07F, 1.3699951e-07F, 1.4590251e-07F,
      1.5538408e-07F, 1.6548181e-07F, 1.7623575e-07F, 1.8768855e-07F,
      1.9988561e-07F, 2.128753e-07F, 2.2670913e-07F, 2.4144197e-07F,
      2.5713223e-07F, 2.7384213e-07F, 2.9163793e-07F, 3.1059021e-07F,
      3.3077411e-07F, 3.5226968e-07F, 3.7516214e-07F, 3.9954229e-07F,
      4.2550680e-07F, 4.5315863e-07F, 4.8260743e-07F, 5.1396998e-07F,
      5.4737065e-07F, 5.8294187e-07F, 6.2082472e-07F, 6.6116941e-07F,
      7.0413592e-07F, 7.4989464e-07F, 7.9862701e-07F, 8.5052630e-07F,
      9.0579828e-07F, 9.6466216e-07F, 1.0273513e-06F, 1.0941144e-06F,
      1.1652161e-06F, 1.2409384e-06F, 1.3215816e-06F, 1.4074654e-06F,
      1.4989305e-06F, 1.5963394e-06F, 1.7000785e-06F, 1.8105592e-06F,
      1.9282195e-06F, 2.0535261e-06F, 2.1869758e-06F, 2.3290978e-06F,
      2.4804557e-06F, 2.6416497e-06F, 2.8133190e-06F, 2.9961443e-06F,
      3.1908506e-06F, 3.3982101e-06F, 3.6190449e-06F, 3.8542308e-06F,
      4.1047004e-06F, 4.3714470e-06F, 4.6555282e-06F, 4.9580707e-06F,
      5.2802740e-06F, 5.6234160e-06F, 5.9888572e-06F, 6.3780469e-06F,
      6.7925283e-06F, 7.2339451e-06F, 7.7040476e-06F, 8.2047000e-06F,
      8.7378876e-06F, 9.3057248e-06F, 9.9104632e-06F, 1.0554501e-05F,
      1.1240392e-05F, 1.1970856e-05F, 1.2748789e-05F, 1.3577278e-05F,
      1.4459606e-05F, 1.5399272e-05F, 1.6400004e-05F, 1.7465768e-05F,
      1.8600792e-05F, 1.9809576e-05F, 2.1096914e-05F, 2.2467911e-05F,
      2.3928002e-05F, 2.5482978e-05F, 2.7139006e-05F, 2.8902651e-05F,
      3.0780908e-05F, 3.2781225e-05F, 3.4911534e-05F, 3.7180282e-05F,
      3.9596466e-05F, 4.2169667e-05F, 4.4910090e-05F, 4.7828601e-05F,
      5.0936773e-05F, 5.4246931e-05F, 5.7772202e-05F, 6.1526565e-05F,
      6.5524908e-05F, 6.9783085e-05F, 7.4317983e-05F, 7.9147585e-05F,
      8.4291040e-05F, 8.9768747e-05F, 9.5602426e-05F, 0.00010181521F,
      0.00010843174F, 0.00011547824F, 0.00012298267F, 0.00013097477F,
      0.00013948625F, 0.00014855085F, 0.00015820453F, 0.00016848555F,
      0.00017943469F, 0.00019109536F, 0.00020351382F, 0.00021673929F,
      0.00023082423F, 0.00024582449F, 0.00026179955F, 0.00027881276F,
      0.00029693158F, 0.00031622787F, 0.00033677814F, 0.00035866388F,
      0.00038197188F, 0.00040679456F, 0.00043323036F, 0.00046138411F,
      0.00049136745F, 0.00052329927F, 0.00055730621F, 0.00059352311F,
      0.00063209358F, 0.00067317058F, 0.00071691700F, 0.00076350630F,
      0.00081312324F, 0.00086596457F, 0.00092223983F, 0.00098217216F,
      0.0010459992F, 0.0011139742F, 0.0011863665F, 0.0012634633F,
      0.0013455702F, 0.0014330129F, 0.0015261382F, 0.0016253153F,
      0.0017309374F, 0.0018434235F, 0.0019632195F, 0.0020908006F,
      0.0022266726F, 0.0023713743F, 0.0025254795F, 0.0026895994F,
      0.0028643847F, 0.0030505286F, 0.0032487691F, 0.0034598925F,
      0.0036847358F, 0.0039241906F, 0.0041792066F, 0.0044507950F,
      0.0047400328F, 0.0050480668F, 0.0053761186F, 0.0057254891F,
      0.0060975636F, 0.0064938176F, 0.0069158225F, 0.0073652516F,
      0.0078438871F, 0.0083536271F, 0.0088964928F, 0.009474637F, 0.010090352F,
      0.010746080F, 0.011444421F, 0.012188144F, 0.012980198F, 0.013823725F,
      0.014722068F, 0.015678791F, 0.016697687F, 0.017782797F, 0.018938423F,
      0.020169149F, 0.021479854F, 0.022875735F, 0.024362330F, 0.025945531F,
      0.027631618F, 0.029427276F, 0.031339626F, 0.033376252F, 0.035545228F,
      0.037855157F, 0.040315199F, 0.042935108F, 0.045725273F, 0.048696758F,
      0.051861348F, 0.055231591F, 0.058820850F, 0.062643361F, 0.066714279F,
      0.071049749F, 0.075666962F, 0.080584227F, 0.085821044F, 0.091398179F,
      0.097337747F, 0.10366330F, 0.11039993F, 0.11757434F, 0.12521498F,
      0.13335215F, 0.14201813F, 0.15124727F, 0.16107617F, 0.17154380F,
      0.18269168F, 0.19456402F, 0.20720788F, 0.22067342F, 0.23501402F,
      0.25028656F, 0.26655159F, 0.28387361F, 0.30232132F, 0.32196786F,
      0.34289114F, 0.36517414F, 0.38890521F, 0.41417847F, 0.44109412F,
      0.46975890F, 0.50028648F, 0.53279791F, 0.56742212F, 0.60429640F,
      0.64356699F, 0.68538959F, 0.72993007F, 0.77736504F, 0.82788260F,
      0.88168307F, 0.9389798F, 1.0F};

        public override void Pack(object i, Buffer opb)
        {
            InfoFloor1 info = (InfoFloor1)i;

            int count = 0;
            int rangebits;
            int maxposit = info.PostList[1];
            int maxclass = -1;

            opb.Write(info.Partitions, 5);
            for (int j = 0; j < info.Partitions; j++)
            {
                opb.Write(info.PartitionClass[j], 4);
                if (maxclass < info.PartitionClass[j])
                    maxclass = info.PartitionClass[j];
            }

            for (int j = 0; j < maxclass + 1; j++)
            {
                opb.Write(info.ClassDim[j] - 1, 3);
                opb.Write(info.ClassSubs[j], 2);
                if (info.ClassSubs[j] != 0)
                {
                    opb.Write(info.ClassBook[j], 8);
                }

                for (int k = 0; k < (1 << info.ClassSubs[j]); k++)
                {
                    opb.Write(info.ClassSubBook[j][k] + 1, 8);
                }
            }

            opb.Write(info.Mult - 1, 2);
            opb.Write(Util.ILog2(maxposit), 4);
            rangebits = Util.ILog2(maxposit);
            
            for (int j = 0, k = 0; j < info.Partitions; j++)
            {
                count += info.ClassDim[info.PartitionClass[j]];
                for (; k < count; k++)
                {
                    opb.Write(info.PostList[k + 2], rangebits);
                }
            }
        }

        public override object Unpack(Info vi, Buffer opb)
        {
            int count = 0, maxclass = -1, rangebits;
            InfoFloor1 info = new InfoFloor1();

            info.Partitions = opb.Read(5);
            for (int j = 0; j < info.Partitions; j++)
            {
                info.PartitionClass[j] = opb.Read(4);
                if (maxclass < info.PartitionClass[j])
                    maxclass = info.PartitionClass[j];
            }

            for (int j = 0; j < maxclass + 1; j++)
            {
                info.ClassDim[j] = opb.Read(3) + 1;
                info.ClassSubs[j] = opb.Read(2);
                if (info.ClassSubs[j] < 0)
                {
                    info.Free();
                    return null;
                }

                if (info.ClassSubs[j] != 0)
                {
                    info.ClassBook[j] = opb.Read(8);
                }

                if (info.ClassBook[j] < 0 || info.ClassBook[j] >= vi.Books)
                {
                    info.Free();
                    return null;
                }

                for (int k = 0; k < (1 << info.ClassSubs[j]); k++)
                {
                    info.ClassSubBook[j][k] = opb.Read(8) - 1;
                    if (info.ClassSubBook[j][k] < -1 || info.ClassSubBook[j][k] >= vi.Books)
                    {
                        info.Free();
                        return null;
                    }
                }
            }

            info.Mult = opb.Read(2) + 1;
            rangebits = opb.Read(4);

            for (int j = 0, k = 0; j < info.Partitions; j++)
            {
                count += info.ClassDim[info.PartitionClass[j]];
                for (; k < count; k++)
                {
                    int t = info.PostList[k + 2] = opb.Read(rangebits);
                    if (t < 0 || t >= (1 << rangebits))
                    {
                        info.Free();
                        return null;
                    }
                }
            }

            info.PostList[0] = 0;
            info.PostList[1] = 1 << rangebits;

            return info;
        }

        public override object Look(DspState vd, InfoMode mi, object i)
        {
            int n = 0;
            int[] sortpointer = new int[VifPosit + 2];
            InfoFloor1 info = (InfoFloor1)i;
            LookFloor1 look = new LookFloor1();
            look.Vi = info;
            look.N = info.PostList[1];

            for (int j = 0; j < info.Partitions; j++)
            {
                n += info.ClassDim[info.PartitionClass[j]];
            }

            n += 2;
            look.Posts = n;

            for (int j = 0; j < n; j++)
            {
                sortpointer[j] = j;
            }

            int foo;
            for (int j = 0; j < n - 1; j++)
            {
                for (int k = j; k < n; k++)
                {
                    if (info.PostList[sortpointer[j]] > info.PostList[sortpointer[k]])
                    {
                        foo = sortpointer[k];
                        sortpointer[k] = sortpointer[j];
                        sortpointer[j] = foo;
                    }
                }
            }

            for (int j = 0; j < n; j++)
            {
                look.ForwardIndex[j] = sortpointer[j];
            }

            for (int j = 0; j < n; j++)
            {
                look.ReverseIndex[look.ForwardIndex[j]] = j;
            }

            for (int j = 0; j < n; j++)
            {
                look.SortedIndex[j] = info.PostList[look.ForwardIndex[j]];
            }

            switch (info.Mult)
            {
                case 1: look.QuantQ = 256; break;
                case 2: look.QuantQ = 128; break;
                case 3: look.QuantQ = 86; break;
                case 4: look.QuantQ = 64; break;
                default: look.QuantQ = -1; break;
            }

            for (int j = 0; j < n - 2; j++)
            {
                int lo = 0;
                int hi = 1;
                int lx = 0;
                int hx = look.N;
                int currentx = info.PostList[j + 2];
                for (int k = 0; k < j + 2; k++)
                {
                    int x = info.PostList[k];
                    if (x > lx && x < currentx)
                    {
                        lo = k;
                        lx = x;
                    }

                    if (x < hx && x > currentx)
                    {
                        hi = k;
                        hx = x;
                    }
                }

                look.LoNeighbor[j] = lo;
                look.HiNeighbor[j] = hi;
            }

            return look;
        }

        public override object State(object i)
        {
            throw new NotImplementedException();
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

        public override int Forward(Block vb, object i, float[] @in, float[] @out, object vs) => 0;
        public override int Inverse(Block vb, object i, float[] @out)
        {
            throw new NotImplementedException();
        }

        public override object Inverse1(Block vb, object ii, object memo)
        {
            LookFloor1 look = (LookFloor1)ii;
            InfoFloor1 info = look.Vi;
            CodeBook[] books = vb.Vd.FullBooks;

            if (vb.Opb.Read(1) == 1)
            {
                int[] fit_value = null;
                if (memo is int[])
                {
                    fit_value = (int[])memo;
                }
                if (fit_value == null || fit_value.Length < look.Posts)
                {
                    fit_value = new int[look.Posts];
                }
                else
                {
                    for(int i=0; i<fit_value.Length; i++)
                        fit_value[i]=0;
                }

                fit_value[0] = vb.Opb.Read(Util.ILog(look.QuantQ - 1));
                fit_value[1] = vb.Opb.Read(Util.ILog(look.QuantQ - 1));

                for (int i = 0, j = 2; i < info.Partitions; i++)
                {
                    int clss = info.PartitionClass[i];
                    int cdim = info.ClassDim[clss];
                    int csubbits = info.ClassSubs[clss];
                    int csub = 1 << csubbits;
                    int cval = 0;

                    if (csubbits != 0)
                    {
                        cval = books[info.ClassBook[clss]].Decode(vb.Opb);
                        if (cval == -1) return null;
                    }

                    for (int k = 0; k < cdim; k++)
                    {
                        int book = info.ClassSubBook[clss][cval & (csub - 1)];
                        cval = (int)((uint)cval >> csubbits);
                        if (book >= 0)
                        {
                            if ((fit_value[j + k] = books[book].Decode(vb.Opb)) == -1)
                            {
                                return null;
                            }
                        }
                        else
                        {
                            fit_value[j + k] = 0;
                        }
                    }

                    j += cdim;
                }

                for (int i = 2; i < look.Posts; i++)
                {
                    int predicted = RenderPoint(info.PostList[look.LoNeighbor[i - 2]],
                        info.PostList[look.HiNeighbor[i - 2]],
                        fit_value[look.LoNeighbor[i - 2]], fit_value[look.HiNeighbor[i - 2]],
                        info.PostList[i]);
                    int hiroom = look.QuantQ - predicted;
                    int loroom = predicted;
                    int room = (hiroom < loroom ? hiroom : loroom) << 1;
                    int val = fit_value[i];

                    if (val != 0)
                    {
                        if (val >= room)
                        {
                            if (hiroom > loroom)
                            {
                                val = val - loroom;
                            }
                            else val = -1 - (val - hiroom);
                        }
                        else
                        {
                            if ((val & 1) != 0)
                            {
                                val = -(int)((uint)(val + 1) >> 1);
                            }
                            else val >>= 1;
                        }

                        fit_value[i] = val + predicted;
                        fit_value[look.LoNeighbor[i - 2]] &= 0x7fff;
                        fit_value[look.HiNeighbor[i - 2]] &= 0x7fff;
                    }
                    else
                    {
                        fit_value[i] = predicted | 0x8000;
                    }
                }

                return fit_value;
            }

            return null;
        }

        private static int RenderPoint(int x0, int x1, int y0, int y1, int x)
        {
            y0 &= 0x7fff;
            y1 &= 0x7fff;
            int dy = y1 - y0;
            int adx = x1 - x0;
            int ady = Math.Abs(dy);
            int err = ady * (x - x0);
            int off = err / adx;
            return (dy < 0) ? (y0 - off) : (y0 + off);
        }

        public override int Inverse2(Block vb, object i, object memo, float[] @out)
        {
            LookFloor1 look = (LookFloor1)i;
            InfoFloor1 info = look.Vi;
            int n = vb.Vd.Vi.BlockSizes[vb.Mode] / 2;

            if (memo != null)
            {
                int[] fit_value = (int[])memo;
                int hx = 0;
                int lx = 0;
                int ly = fit_value[0] * info.Mult;
                for (int j = 1; j < look.Posts; j++)
                {
                    int current = look.ForwardIndex[j];
                    int hy = fit_value[current] & 0x7fff;
                    if (hy == fit_value[current])
                    {
                        hy *= info.Mult;
                        hx = info.PostList[current];
                        RenderLine(lx, hx, ly, hy, @out);
                        lx = hx;
                        ly = hy;
                    }
                }

                for (int j = hx; j < n; j++)
                {
                    @out[j] *= @out[j - 1];
                }

                return 1;
            }

            for (int j = 0; j < n; j++)
            {
                @out[j] = 0.0f;
            }

            return 0;
        }

        private static void RenderLine(int x0, int x1, int y0, int y1, float[] d)
        {
            int dy = y1 - y0;
            int adx = x1 - x0;
            int ady = Math.Abs(dy);
            int @base = dy / adx;
            int sy = (dy < 0 ? @base - 1 : @base + 1);
            int x = x0;
            int y = y0;
            int err = 0;

            ady -= Math.Abs(@base * adx);

            if (x < d.Length) d[x] *= FloorFromdBLookup[y];

            while (++x < x1)
            {
                err += ady;
                if (err >= adx)
                {
                    err -= adx;
                    y += sy;
                }
                else
                {
                    y += @base;
                }

                if (x < d.Length) d[x] *= FloorFromdBLookup[y];
            }
        }

        public class InfoFloor1
        {
            public const int VifPosit = 63;
            public const int VifClass = 16;
            public const int VifParts = 31;

            public int Partitions { get; set; }
            public int[] PartitionClass { get; set; } = new int[VifParts];
            public int[] ClassDim { get; set; } = new int[VifClass];
            public int[] ClassSubs { get; set; } = new int[VifClass];
            public int[] ClassBook { get; set; } = new int[VifClass];
            public int[][] ClassSubBook { get; set; } = new int[VifClass][];
            public int Mult { get; set; }
            public int[] PostList { get; set; } = new int[VifPosit + 2];
            public float MaxOver { get; set; }
            public float MaxUnder { get; set; }
            public float MaxErr { get; set; }
            public int TwoFitMinSize { get; set; }
            public int TwoFitMinUsed { get; set; }
            public int TwoFitWeight { get; set; }
            public float TwoFitAtten { get; set; }
            public int UnusedMinSize { get; set; }
            public int UnusedMinN { get; set; }
            public int N { get; set; }

            public InfoFloor1()
            {
                for (int i = 0; i < ClassSubBook.Length; i++)
                {
                    ClassSubBook[i] = new int[8];
                }
            }

            public void Free()
            {
                PartitionClass = null;
                ClassDim = null;
                ClassSubs = null;
                ClassBook = null;
                ClassSubBook = null;
                PostList = null;
            }
        }

        public class LookFloor1
        {
            public const int VifPosit = 63;
            public int[] SortedIndex { get; set; } = new int[VifPosit + 2];
            public int[] ForwardIndex { get; set; } = new int[VifPosit + 2];
            public int[] ReverseIndex { get; set; } = new int[VifPosit + 2];
            public int[] HiNeighbor { get; set; } = new int[VifPosit];
            public int[] LoNeighbor { get; set; } = new int[VifPosit];
            public int Posts { get; set; }
            public int N { get; set; }
            public int QuantQ { get; set; }
            public InfoFloor1 Vi { get; set; }
            public int PhraseBits { get; set; }
            public int PostBits { get; set; }
            public int Frames { get; set; }
        }
    }
}
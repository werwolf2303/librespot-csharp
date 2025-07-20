using System;
using Buffer = deps.jorbis.jogg.Buffer;

namespace deps.jorbis.jorbis
{
    public class Residue0 : FuncResidue
    {
        private static int[][][] _01InversePartword = new int[2][][];
        private static int[][] _2InversePartword;
        private static readonly object _inverseLock = new object();

        public override void Pack(object vr, Buffer opb)
        {
            InfoResidue0 info = (InfoResidue0)vr;
            int acc = 0;
            opb.Write(info.Begin, 24);
            opb.Write(info.End, 24);
            opb.Write(info.Grouping - 1, 24);
            opb.Write(info.Partitions - 1, 6);
            opb.Write(info.GroupBook, 8);

            for (int j = 0; j < info.Partitions; j++)
            {
                int i = info.SecondStages[j];
                if (Util.ILog(i) > 3)
                {
                    opb.Write(i, 3);
                    opb.Write(1, 1);
                    opb.Write((int)((uint)i >> 3), 5);
                }
                else
                {
                    opb.Write(i, 4);
                }

                acc += Util.ICount(i);
            }

            for (int j = 0; j < acc; j++)
            {
                opb.Write(info.BookList[j], 8);
            }
        }

        public override object Unpack(Info vi, Buffer opb)
        {
            int acc = 0;
            InfoResidue0 info = new InfoResidue0();
            info.Begin = opb.Read(24);
            info.End = opb.Read(24);
            info.Grouping = opb.Read(24) + 1;
            info.Partitions = opb.Read(6) + 1;
            info.GroupBook = opb.Read(8);

            for (int j = 0; j < info.Partitions; j++)
            {
                int cascade = opb.Read(3);
                if (opb.Read(1) != 0)
                {
                    cascade |= (opb.Read(5) << 3);
                }

                info.SecondStages[j] = cascade;
                acc += Util.ICount(cascade);
            }

            for (int j = 0; j < acc; j++)
            {
                info.BookList[j] = opb.Read(8);
            }

            if (info.GroupBook >= vi.Books)
            {
                FreeInfo(info);
                return null;
            }

            for (int j = 0; j < acc; j++)
            {
                if (info.BookList[j] >= vi.Books)
                {
                    FreeInfo(info);
                    return null;
                }
            }

            return info;
        }

        public override object Look(DspState vd, InfoMode vm, object vr)
        {
            InfoResidue0 info = (InfoResidue0)vr;
            LookResidue0 look = new LookResidue0();
            int acc = 0;
            int maxstage = 0;
            look.Info = info;
            look.Map = vm.Mapping;
            look.Parts = info.Partitions;
            look.FullBooks = vd.FullBooks;
            look.PhraseBook = vd.FullBooks[info.GroupBook];
            int dim = look.PhraseBook.Dim;

            look.PartBooks = new int[look.Parts][];
            for (int j = 0; j < look.Parts; j++)
            {
                int i = info.SecondStages[j];
                int stages = Util.ILog(i);
                if (stages > 0)
                {
                    if (stages > maxstage)
                        maxstage = stages;
                    look.PartBooks[j] = new int[stages];
                    for (int k = 0; k < stages; k++)
                    {
                        if ((i & (1 << k)) != 0)
                        {
                            look.PartBooks[j][k] = info.BookList[acc++];
                        }
                    }
                }
            }

            look.Partvals = (int)Math.Round(Math.Pow(look.Parts, dim));
            look.Stages = maxstage;
            look.DecodeMap = new int[look.Partvals][];
            for (int j = 0; j < look.Partvals; j++)
            {
                int val = j;
                int mult = look.Partvals / look.Parts;
                look.DecodeMap[j] = new int[dim];
                for (int k = 0; k < dim; k++)
                {
                    int deco = val / mult;
                    val -= deco * mult;
                    mult /= look.Parts;
                    look.DecodeMap[j][k] = deco;
                }
            }

            return look;
        }

        public override void FreeInfo(object i)
        {
        }

        public override void FreeLook(object i)
        {
        }

        internal static int _01Inverse(Block vb, object vl, float[][] @in, int ch, int decodepart)
        {
            lock (_inverseLock)
            {
                LookResidue0 look = (LookResidue0)vl;
                InfoResidue0 info = look.Info;

                int samplesPerPartition = info.Grouping;
                int partitionsPerWord = look.PhraseBook.Dim;
                int n = info.End - info.Begin;
                int partvals = n / samplesPerPartition;
                int partwords = (partvals + partitionsPerWord - 1) / partitionsPerWord;

                if (_01InversePartword.Length < ch)
                {
                    _01InversePartword = new int[ch][][];
                }

                for (int j = 0; j < ch; j++)
                {
                    if (_01InversePartword[j] == null || _01InversePartword[j].Length < partwords)
                    {
                        _01InversePartword[j] = new int[partwords][];
                    }
                }

                for (int s = 0; s < look.Stages; s++)
                {
                    for (int i = 0, l = 0; i < partvals; l++)
                    {
                        if (s == 0)
                        {
                            for (int j = 0; j < ch; j++)
                            {
                                int temp = look.PhraseBook.Decode(vb.Opb);
                                if (temp == -1) return 0;
                                _01InversePartword[j][l] = look.DecodeMap[temp];
                                if (_01InversePartword[j][l] == null) return 0;
                            }
                        }

                        for (int k = 0; k < partitionsPerWord && i < partvals; k++, i++)
                        {
                            for (int j = 0; j < ch; j++)
                            {
                                int offset = info.Begin + i * samplesPerPartition;
                                int index = _01InversePartword[j][l][k];
                                if ((info.SecondStages[index] & (1 << s)) != 0)
                                {
                                    CodeBook stagebook = look.FullBooks[look.PartBooks[index][s]];
                                    if (stagebook != null)
                                    {
                                        if (decodepart == 0)
                                        {
                                            if (stagebook.DecodeVsAdd(@in[j], offset, vb.Opb, samplesPerPartition) ==
                                                -1)
                                                return 0;
                                        }
                                        else
                                        {
                                            if (stagebook.DecodeVAdd(@in[j], offset, vb.Opb, samplesPerPartition) == -1)
                                                return 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return 0;
            }
        }

        internal static int _2Inverse(Block vb, object vl, float[][] @in, int ch)
        {
            lock (_inverseLock)
            {
                LookResidue0 look = (LookResidue0)vl;
                InfoResidue0 info = look.Info;

                int samplesPerPartition = info.Grouping;
                int partitionsPerWord = look.PhraseBook.Dim;
                int n = info.End - info.Begin;
                int partvals = n / samplesPerPartition;
                int partwords = (partvals + partitionsPerWord - 1) / partitionsPerWord;

                if (_2InversePartword == null || _2InversePartword.Length < partwords)
                {
                    _2InversePartword = new int[partwords][];
                }

                for (int s = 0; s < look.Stages; s++)
                {
                    for (int i = 0, l = 0; i < partvals; l++)
                    {
                        if (s == 0)
                        {
                            int temp = look.PhraseBook.Decode(vb.Opb);
                            if (temp == -1) return 0;
                            _2InversePartword[l] = look.DecodeMap[temp];
                            if (_2InversePartword[l] == null) return 0;
                        }

                        for (int k = 0; k < partitionsPerWord && i < partvals; k++, i++)
                        {
                            int offset = info.Begin + i * samplesPerPartition;
                            int index = _2InversePartword[l][k];
                            if ((info.SecondStages[index] & (1 << s)) != 0)
                            {
                                CodeBook stagebook = look.FullBooks[look.PartBooks[index][s]];
                                if (stagebook != null)
                                {
                                    if (stagebook.DecodeVvAdd(@in, offset, ch, vb.Opb, samplesPerPartition) == -1)
                                        return 0;
                                }
                            }
                        }
                    }
                }

                return 0;
            }
        }

        public override int Inverse(Block vb, object vl, float[][] @in, int[] nonzero, int ch)
        {
            int used = 0;
            for (int i = 0; i < ch; i++)
            {
                if (nonzero[i] != 0)
                {
                    @in[used++] = @in[i];
                }
            }

            return used != 0 ? _01Inverse(vb, vl, @in, used, 0) : 0;
        }

        public class LookResidue0
        {
            public InfoResidue0 Info { get; set; }
            public int Map { get; set; }
            public int Parts { get; set; }
            public int Stages { get; set; }
            public CodeBook[] FullBooks { get; set; }
            public CodeBook PhraseBook { get; set; }
            public int[][] PartBooks { get; set; }
            public int Partvals { get; set; }
            public int[][] DecodeMap { get; set; }
            public int PostBits { get; set; }
            public int PhraseBits { get; set; }
            public int Frames { get; set; }
        }

        public class InfoResidue0
        {
            public int Begin { get; set; }
            public int End { get; set; }
            public int Grouping { get; set; }
            public int Partitions { get; set; }
            public int GroupBook { get; set; }
            public int[] SecondStages { get; set; } = new int[64];
            public int[] BookList { get; set; } = new int[256];
            public float[] EntMax { get; set; } = new float[64];
            public float[] AmpMax { get; set; } = new float[64];
            public int[] SubGrp { get; set; } = new int[64];
            public int[] BLimit { get; set; } = new int[64];
        }
    }
}
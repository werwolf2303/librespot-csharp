using System;
using Buffer = deps.jorbis.jogg.Buffer;

namespace deps.jorbis.jorbis
{
    public class StaticCodeBook
    {
        public int Dim { get; private set; }
        public int Entries { get; private set; }
        public int[] LengthList { get; set; }
        public int MapType { get; private set; }
        public int QMin { get; private set; }
        public int QDelta { get; private set; }
        public int QQuant { get; private set; }
        public int QSequencep { get; private set; }
        public int[] QuantList { get; private set; }

        private const int VqFexp = 10;
        private const int VqFman = 21;
        private const int VqFexpBias = 768;

        public int Pack(Buffer opb)
        {
            bool ordered = false;
            opb.Write(0x564342, 24);
            opb.Write(Dim, 16);
            opb.Write(Entries, 24);

            int i;
            for (i = 1; i < Entries; i++)
            {
                if (LengthList[i] < LengthList[i - 1])
                    break;
            }

            if (i == Entries)
                ordered = true;

            if (ordered)
            {
                int count = 0;
                opb.Write(1, 1);
                opb.Write(LengthList[0] - 1, 5);

                for (i = 1; i < Entries; i++)
                {
                    int current = LengthList[i];
                    int last = LengthList[i - 1];
                    if (current > last)
                    {
                        for (int j = last; j < current; j++)
                        {
                            opb.Write(i - count, Util.ILog(Entries - count));
                            count = i;
                        }
                    }
                }

                opb.Write(i - count, Util.ILog(Entries - count));
            }
            else
            {
                opb.Write(0, 1);
                for (i = 0; i < Entries; i++)
                {
                    if (LengthList[i] == 0)
                        break;
                }

                if (i == Entries)
                {
                    opb.Write(0, 1);
                    for (i = 0; i < Entries; i++)
                    {
                        opb.Write(LengthList[i] - 1, 5);
                    }
                }
                else
                {
                    opb.Write(1, 1);
                    for (i = 0; i < Entries; i++)
                    {
                        if (LengthList[i] == 0)
                        {
                            opb.Write(0, 1);
                        }
                        else
                        {
                            opb.Write(1, 1);
                            opb.Write(LengthList[i] - 1, 5);
                        }
                    }
                }
            }

            opb.Write(MapType, 4);
            switch (MapType)
            {
                case 0:
                    break;
                case 1:
                case 2:
                    if (QuantList == null) return -1;
                    opb.Write(QMin, 32);
                    opb.Write(QDelta, 32);
                    opb.Write(QQuant - 1, 4);
                    opb.Write(QSequencep, 1);

                    int quantvals = 0;
                    switch (MapType)
                    {
                        case 1:
                            quantvals = Maptype1Quantvals();
                            break;
                        case 2:
                            quantvals = Entries * Dim;
                            break;
                    }

                    for (i = 0; i < quantvals; i++)
                    {
                        opb.Write(Math.Abs(QuantList[i]), QQuant);
                    }

                    break;
                default:
                    return -1;
            }

            return 0;
        }

        public int Unpack(Buffer opb)
        {
            if (opb.Read(24) != 0x564342)
            {
                Clear();
                return -1;
            }

            Dim = opb.Read(16);
            Entries = opb.Read(24);
            if (Entries == -1)
            {
                Clear();
                return -1;
            }

            switch (opb.Read(1))
            {
                case 0:
                    LengthList = new int[Entries];
                    if (opb.Read(1) != 0)
                    {
                        for (int i = 0; i < Entries; i++)
                        {
                            if (opb.Read(1) != 0)
                            {
                                int num = opb.Read(5);
                                if (num == -1)
                                {
                                    Clear();
                                    return -1;
                                }

                                LengthList[i] = num + 1;
                            }
                            else
                            {
                                LengthList[i] = 0;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < Entries; i++)
                        {
                            int num = opb.Read(5);
                            if (num == -1)
                            {
                                Clear();
                                return -1;
                            }

                            LengthList[i] = num + 1;
                        }
                    }

                    break;
                case 1:
                    int length = opb.Read(5) + 1;
                    LengthList = new int[Entries];
                    for (int i = 0; i < Entries;)
                    {
                        int num = opb.Read(Util.ILog(Entries - i));
                        if (num == -1)
                        {
                            Clear();
                            return -1;
                        }

                        for (int j = 0; j < num; j++, i++)
                        {
                            LengthList[i] = length;
                        }

                        length++;
                    }

                    break;
                default:
                    return -1;
            }

            MapType = opb.Read(4);
            switch (MapType)
            {
                case 0:
                    break;
                case 1:
                case 2:
                    QMin = opb.Read(32);
                    QDelta = opb.Read(32);
                    QQuant = opb.Read(4) + 1;
                    QSequencep = opb.Read(1);

                    int quantvals = 0;
                    switch (MapType)
                    {
                        case 1:
                            quantvals = Maptype1Quantvals();
                            break;
                        case 2:
                            quantvals = Entries * Dim;
                            break;
                    }

                    QuantList = new int[quantvals];
                    for (int i = 0; i < quantvals; i++)
                    {
                        QuantList[i] = opb.Read(QQuant);
                    }

                    if (quantvals > 0 && QuantList[quantvals - 1] == -1)
                    {
                        Clear();
                        return -1;
                    }

                    break;
                default:
                    Clear();
                    return -1;
            }

            return 0;
        }

        private int Maptype1Quantvals()
        {
            int vals = (int)Math.Floor(Math.Pow(Entries, 1.0 / Dim));
            while (true)
            {
                long acc = 1;
                long acc1 = 1;
                for (int i = 0; i < Dim; i++)
                {
                    acc *= vals;
                    acc1 *= vals + 1;
                }

                if (acc <= Entries && acc1 > Entries)
                {
                    return vals;
                }
                else
                {
                    if (acc > Entries)
                    {
                        vals--;
                    }
                    else
                    {
                        vals++;
                    }
                }
            }
        }

        public void Clear()
        {
        }

        public float[] Unquantize()
        {
            if (MapType == 1 || MapType == 2)
            {
                float mindel = Float32Unpack(QMin);
                float delta = Float32Unpack(QDelta);
                float[] r = new float[Entries * Dim];

                switch (MapType)
                {
                    case 1:
                        int quantvals = Maptype1Quantvals();
                        for (int j = 0; j < Entries; j++)
                        {
                            float last = 0.0f;
                            int indexdiv = 1;
                            for (int k = 0; k < Dim; k++)
                            {
                                int index = (j / indexdiv) % quantvals;
                                float val = QuantList[index];
                                val = Math.Abs(val) * delta + mindel + last;
                                if (QSequencep != 0)
                                    last = val;
                                r[j * Dim + k] = val;
                                indexdiv *= quantvals;
                            }
                        }

                        break;
                    case 2:
                        for (int j = 0; j < Entries; j++)
                        {
                            float last = 0.0f;
                            for (int k = 0; k < Dim; k++)
                            {
                                float val = QuantList[j * Dim + k];
                                val = Math.Abs(val) * delta + mindel + last;
                                if (QSequencep != 0)
                                    last = val;
                                r[j * Dim + k] = val;
                            }
                        }

                        break;
                }

                return r;
            }

            return null;
        }

        public static long Float32Pack(float val)
        {
            uint sign = 0;
            if (val < 0)
            {
                sign = 0x80000000;
                val = -val;
            }

            int exp = (int)Math.Floor(Math.Log(val) / Math.Log(2));
            int mant = (int)Math.Round(Math.Pow(val, (VqFman - 1) - exp));
            exp = (exp + VqFexpBias) << VqFman;
            return sign | (uint)exp | (uint)mant;
        }

        public static float Float32Unpack(int val)
        {
            float mant = val & 0x1fffff;
            float exp = (val & 0x7fe00000) >> VqFman;
            if ((val & 0x80000000) != 0)
                mant = -mant;
            return Ldexp(mant, (int)exp - (VqFman - 1) - VqFexpBias);
        }

        private static float Ldexp(float foo, int e)
        {
            return (float)(foo * Math.Pow(2, e));
        }
    }
}
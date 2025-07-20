using System.Runtime.CompilerServices;
using deps.jorbis.jogg;

namespace deps.jorbis.jorbis
{
    public class CodeBook
    {
        private StaticCodeBook _c = new StaticCodeBook();
        private DecodeAux _decodeTree;
        private int[] _t = new int[15];

        public int Dim { get; private set; }
        public int Entries { get; private set; }
        public float[] ValueList { get; private set; }
        public int[] CodeList { get; private set; }

        public int Encode(int a, Buffer b)
        {
            b.Write(CodeList[a], _c.LengthList[a]);
            return _c.LengthList[a];
        }

        public int ErrorV(float[] a)
        {
            int best = Best(a, 1);
            for (int k = 0; k < Dim; k++)
            {
                a[k] = ValueList[best * Dim + k];
            }

            return best;
        }

        public int EncodeV(int best, float[] a, Buffer b)
        {
            for (int k = 0; k < Dim; k++)
            {
                a[k] = ValueList[best * Dim + k];
            }

            return Encode(best, b);
        }

        public int EncodeVs(float[] a, Buffer b, int step, int addmul)
        {
            int best = BestError(a, step, addmul);
            return Encode(best, b);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public int DecodeVsAdd(float[] a, int offset, Buffer b, int n)
        {
            int step = n / Dim;
            int entry;
            int i, j, o;

            if (_t.Length < step)
            {
                _t = new int[step];
            }

            for (i = 0; i < step; i++)
            {
                entry = Decode(b);
                if (entry == -1)
                    return -1;
                _t[i] = entry * Dim;
            }

            for (i = 0, o = 0; i < Dim; i++, o += step)
            {
                for (j = 0; j < step; j++)
                {
                    a[offset + o + j] += ValueList[_t[j] + i];
                }
            }

            return 0;
        }

        public int DecodeVAdd(float[] a, int offset, Buffer b, int n)
        {
            int i, j, entry;
            int t;
            
            if (Dim > 8)
            {
                for (i = 0; i < n;)
                {
                    entry = Decode(b);
                    if (entry == -1) 
                        return -1;
                    t = entry * Dim;
                    for (j = 0; j < Dim;)
                    {
                        a[offset + (i++)] += ValueList[t + (j++)];
                    }
                }
            }
            else
            {
                for (i = 0; i < n;)
                {
                    entry = Decode(b);
                    if (entry == -1) return -1;
                    t = entry * Dim;
                    j = 0;
                    switch (Dim)
                    {
                        case 8:
                            a[offset + (i++)] += ValueList[t + (j++)];
                            goto case 7;
                        case 7:
                            a[offset + (i++)] += ValueList[t + (j++)];
                            goto case 6;
                        case 6:
                            a[offset + (i++)] += ValueList[t + (j++)];
                            goto case 5;
                        case 5:
                            a[offset + (i++)] += ValueList[t + (j++)];
                            goto case 4;
                        case 4:
                            a[offset + (i++)] += ValueList[t + (j++)];
                            goto case 3;
                        case 3:
                            a[offset + (i++)] += ValueList[t + (j++)];
                            goto case 2;
                        case 2:
                            a[offset + (i++)] += ValueList[t + (j++)];
                            goto case 1;
                        case 1:
                            a[offset + (i++)] += ValueList[t + (j++)];
                            goto case 0;
                        case 0:
                            break;
                    }
                }
            }

            return 0;
        }

        public int DecodeVSet(float[] a, int offset, Buffer b, int n)
        {
            int i, j, entry;
            int t;
            
            for (i = 0; i < n;)
            {
                entry = Decode(b);
                if (entry == -1) return -1;
                t = entry * Dim;
                for (j = 0; j < Dim;)
                {
                    a[offset + i++] = ValueList[t + (j++)];
                }
            }

            return 0;
        }

        public int DecodeVvAdd(float[][] a, int offset, int ch, Buffer b, int n)
        {
            int i, j, entry;
            int chptr = 0;
            
            for (i = offset / ch; i < (offset + n) / ch;)
            {
                entry = Decode(b);
                if (entry == -1) return -1;

                int t = entry * Dim;
                for (j = 0; j < Dim; j++)
                {
                    a[chptr++][i] += ValueList[t + j];
                    if (chptr == ch)
                    {
                        chptr = 0;
                        i++;
                    }
                }
            }

            return 0;
        }

        public int Decode(Buffer b)
        {
            int ptr = 0;
            DecodeAux t = _decodeTree;
            int lok = b.Look(t.Tabn);

            if (lok >= 0)
            {
                ptr = t.Tab[lok];
                b.Adv(t.Tabl[lok]);
                if (ptr <= 0)
                {
                    return -ptr;
                }
            }

            do
            {
                switch (b.Read1())
                {
                    case 0:
                        ptr = t.Ptr0[ptr];
                        break;
                    case 1:
                        ptr = t.Ptr1[ptr];
                        break;
                    default:
                        return -1;
                }
            } while (ptr > 0);

            return -ptr;
        }

        public int DecodeVs(float[] a, int index, Buffer b, int step, int addmul)
        {
            int entry = Decode(b);
            if (entry == -1) return -1;

            switch (addmul)
            {
                case -1: 
                    for (int i = 0, o = 0; i < Dim; i++, o += step)
                        a[index + o] = ValueList[entry * Dim + i];
                    break;
                case 0: 
                    for (int i = 0, o = 0; i < Dim; i++, o += step)
                        a[index + o] += ValueList[entry * Dim + i];
                    break;
                case 1: 
                    for (int i = 0, o = 0; i < Dim; i++, o += step)
                        a[index + o] *= ValueList[entry * Dim + i];
                    break;
            }

            return entry;
        }

        public int Best(float[] a, int step)
        {
            // brute force it!
            int besti = -1;
            float best = 0.0f;
            int e = 0;
            for (int i = 0; i < Entries; i++)
            {
                if (_c.LengthList[i] > 0)
                {
                    float _this = Dist(Dim, ValueList, e, a, step);
                    if (besti == -1 || _this < best)
                    {
                        best = _this;
                        besti = i;
                    }
                }

                e += Dim;
            }

            return besti;
        }

        public int BestError(float[] a, int step, int addmul)
        {
            int best = Best(a, step);
            switch (addmul)
            {
                case 0:
                    for (int i = 0, o = 0; i < Dim; i++, o += step)
                        a[o] -= ValueList[best * Dim + i];
                    break;
                case 1:
                    for (int i = 0, o = 0; i < Dim; i++, o += step)
                    {
                        float val = ValueList[best * Dim + i];
                        if (val == 0)
                        {
                            a[o] = 0;
                        }
                        else
                        {
                            a[o] /= val;
                        }
                    }

                    break;
            }

            return best;
        }

        public void Clear()
        {
        }

        private static float Dist(int el, float[] reference, int index, float[] b, int step)
        {
            float acc = 0.0f;
            for (int i = 0; i < el; i++)
            {
                float val = (reference[index + i] - b[i * step]);
                acc += val * val;
            }

            return acc;
        }

        public int InitDecode(StaticCodeBook s)
        {
            _c = s;
            Entries = s.Entries;
            Dim = s.Dim;
            ValueList = s.Unquantize();

            _decodeTree = MakeDecodeTree();
            if (_decodeTree == null)
            {
                Clear();
                return -1;
            }

            return 0;
        }

        private static int[] MakeWords(int[] l, int n)
        {
            int[] marker = new int[33];
            int[] r = new int[n];

            for (int i = 0; i < n; i++)
            {
                int length = l[i];
                if (length > 0)
                {
                    int entry = marker[length];
                    if (length < 32 && ((uint)entry >> length) != 0)
                    {
                        return null;
                    }

                    r[i] = entry;

                    for (int j = length; j > 0; j--)
                    {
                        if ((marker[j] & 1) != 0)
                        {
                            if (j == 1)
                                marker[1]++;
                            else
                                marker[j] = marker[j - 1] << 1;
                            break;
                        }

                        marker[j]++;
                    }

                    for (int j = length + 1; j < 33; j++)
                    {
                        if (((uint)marker[j] >> 1) == entry)
                        {
                            entry = marker[j];
                            marker[j] = marker[j - 1] << 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                int temp = 0;
                for (int j = 0; j < l[i]; j++)
                {
                    temp <<= 1;
                    temp |= (int)(((uint)r[i] >> j) & 1);
                }

                r[i] = temp;
            }

            return r;
        }

        private DecodeAux MakeDecodeTree()
        {
            int top = 0;
            DecodeAux t = new DecodeAux();
            int[] ptr0 = t.Ptr0 = new int[Entries * 2];
            int[] ptr1 = t.Ptr1 = new int[Entries * 2];
            int[] codelist = MakeWords(_c.LengthList, _c.Entries);

            if (codelist == null) return null;
            t.Aux = Entries * 2;

            for (int i = 0; i < Entries; i++)
            {
                if (_c.LengthList[i] > 0)
                {
                    int ptr = 0;
                    int j;
                    for (j = 0; j < _c.LengthList[i] - 1; j++)
                    {
                        int bit = (int)(((uint)codelist[i] >> j) & 1);
                        if (bit == 0)
                        {
                            if (ptr0[ptr] == 0)
                            {
                                ptr0[ptr] = ++top;
                            }
                            ptr = ptr0[ptr];
                        }
                        else
                        {
                            if (ptr1[ptr] == 0)
                            {
                                ptr1[ptr] = ++top;
                            }
                            ptr = ptr1[ptr];
                        }
                    }

                    if ((((uint)codelist[i] >> j) & 1) == 0)
                    {
                        ptr0[ptr] = -i;
                    }
                    else
                    {
                        ptr1[ptr] = -i;
                    }
                }
            }

            t.Tabn = Util.ILog(Entries) - 4;
            if (t.Tabn < 5) t.Tabn = 5;

            int n = 1 << t.Tabn;
            t.Tab = new int[n];
            t.Tabl = new int[n];

            for (int i = 0; i < n; i++)
            {
                int p = 0;
                int j = 0;
                for (j = 0; j < t.Tabn && (p > 0 || j == 0); j++)
                {
                    if ((i & (1 << j)) != 0)
                    {
                        p = ptr1[p];
                    }
                    else
                    {
                        p = ptr0[p];
                    }
                }

                t.Tab[i] = p;
                t.Tabl[i] = j;
            }

            return t;
        }

        public class DecodeAux
        {
            public int[] Tab { get; set; }
            public int[] Tabl { get; set; }
            public int Tabn { get; set; }
            public int[] Ptr0 { get; set; }
            public int[] Ptr1 { get; set; }
            public int Aux { get; set; }
        }
    }
}
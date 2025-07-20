namespace deps.jorbis.jorbis
{
    public static class Util
    {
        public static int ILog(int v)
        {
            int ret = 0;
            uint val = (uint)v;
            while (val != 0)
            {
                ret++;
                val >>= 1;
            }
            return ret;
        }

        public static int ILog2(int v)
        {
            int ret = 0;
            uint val = (uint)v;
            while (val > 1)
            {
                ret++;
                val >>= 1;
            }
            return ret;
        }

        public static int ICount(int v)
        {
            int ret = 0;
            uint val = (uint)v;
            while (val != 0)
            {
                ret += (int)(val & 1);
                val >>= 1;
            }
            return ret;
        }
    }
}
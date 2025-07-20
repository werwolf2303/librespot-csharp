namespace deps.jorbis.jorbis
{
    public class Residue2 : Residue0
    {
        public override int Inverse(Block vb, object vl, float[][] @in, int[] nonzero, int ch)
        {
            int i = 0;
            for (i = 0; i < ch; i++)
            {
                if (nonzero[i] != 0)
                    break;
            }

            if (i == ch)
            {
                return 0; // no nonzero vectors
            }

            return _2Inverse(vb, vl, @in, ch);
        }
    }
}
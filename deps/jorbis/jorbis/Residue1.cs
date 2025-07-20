namespace deps.jorbis.jorbis
{
    public class Residue1 : Residue0
    {
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

            if (used != 0)
            {
                return _01Inverse(vb, vl, @in, used, 1);
            }
        
            return 0;
        }
    }
}
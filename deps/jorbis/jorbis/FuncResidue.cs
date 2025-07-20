using deps.jorbis.jogg;

namespace deps.jorbis.jorbis
{
    public abstract class FuncResidue
    {
        public static readonly FuncResidue[] ResidueP = { new Residue0(), new Residue1(), new Residue2() };

        public abstract void Pack(object vr, Buffer opb);

        public abstract object Unpack(Info vi, Buffer opb);

        public abstract object Look(DspState vd, InfoMode vm, object vr);

        public abstract void FreeInfo(object i);

        public abstract void FreeLook(object i);

        public abstract int Inverse(Block vb, object vl, float[][] @in, int[] nonzero, int ch);
    }
}
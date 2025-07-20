using deps.jorbis.jogg;

namespace deps.jorbis.jorbis
{
    public abstract class FuncFloor
    {
        public static FuncFloor[] FloorP = { new Floor0(), new Floor1() };
        public abstract void Pack(object i, Buffer opb);
        public abstract object Unpack(Info vi, Buffer opb);
        public abstract object Look(DspState vd, InfoMode mi, object i);
        public abstract object State(object i);
        public abstract void FreeInfo(object i);
        public abstract void FreeLook(object i);
        public abstract void FreeState(object vs);
        public abstract int Forward(Block vb, object i, float[] @in, float[] @out, object vs);
        public abstract int Inverse(Block vb, object i, float[] @out);
        public abstract object Inverse1(Block vb, object i, object memo);
        public abstract int Inverse2(Block vb, object i, object memo, float[] @out);
    }
}
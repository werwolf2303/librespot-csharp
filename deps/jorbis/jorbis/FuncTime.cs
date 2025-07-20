using deps.jorbis.jogg;

namespace deps.jorbis.jorbis
{
    public abstract class FuncTime
    {
        public static readonly FuncTime[] TimeP = { new Time0() };

        public abstract void Pack(object i, Buffer opb);

        public abstract object Unpack(Info vi, Buffer opb);

        public abstract object Look(DspState vd, InfoMode vm, object i);

        public abstract void FreeInfo(object i);

        public abstract void FreeLook(object i);

        public abstract int Inverse(Block vb, object i, float[] @in, float[] @out);
    }
}
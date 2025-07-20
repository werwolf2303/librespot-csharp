using deps.jorbis.jogg;

namespace deps.jorbis.jorbis
{
    public class Time0 : FuncTime
    {
        public override void Pack(object i, Buffer opb)
        {
        }

        public override object Unpack(Info vi, Buffer opb)
        {
            return string.Empty;
        }

        public override object Look(DspState vd, InfoMode vm, object i)
        {
            return string.Empty;
        }

        public override void FreeInfo(object i)
        {
        }

        public override void FreeLook(object i)
        {
        }

        public override int Inverse(Block vb, object i, float[] @in, float[] @out)
        {
            return 0;
        }
    }
}
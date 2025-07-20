using deps.jorbis.jogg;

namespace deps.jorbis.jorbis
{
    public abstract class FuncMapping
    {
        public static readonly FuncMapping[] MappingP = { new Mapping0() };

        public abstract void Pack(Info info, object imap, Buffer buffer);
        public abstract object Unpack(Info info, Buffer buffer);
        public abstract object Look(DspState vd, InfoMode vm, object m);
        public abstract void FreeInfo(object imap);
        public abstract void FreeLook(object imap);
        public abstract int Inverse(Block vd, object lm);
    }
}
using lib.crypto;

namespace lib.core
{
    public interface IPacketsReceiver
    {
        void Dispatch(Packet packet);
    }
}
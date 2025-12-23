namespace zeroconf.zeroconf
{
    public interface IPacketListener
    {
        void PacketEvent(Packet packet);
    }
}
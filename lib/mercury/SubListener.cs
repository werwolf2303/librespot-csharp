namespace lib.mercury
{
    public interface ISubListener
    {
        void Event(MercuryClient.Response resp);
    }
}
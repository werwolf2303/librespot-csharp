namespace lib.audio
{
    public interface HaltListener
    {
        void streamReadHalted(int chunk, long time);

        void streamReadResumed(int chunk, long time);
    }
}
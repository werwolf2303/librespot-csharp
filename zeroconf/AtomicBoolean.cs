using System.Threading;

namespace zeroconf
{
 
    public sealed class AtomicBoolean
    {
        private int value;

        public AtomicBoolean(bool initial = false)
        {
            value = initial ? 1 : 0;
        }

        public bool Get() => value != 0;

        public void Set(bool v)
        {
            Interlocked.Exchange(ref value, v ? 1 : 0);
        }

        public bool CompareAndSet(bool expected, bool update)
        {
            int e = expected ? 1 : 0;
            int u = update ? 1 : 0;
            return Interlocked.CompareExchange(ref value, u, e) == e;
        }
    }
}
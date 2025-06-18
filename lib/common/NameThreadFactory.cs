using System;
using System.Threading;

namespace lib.common
{
    public delegate string Func<T>(T arg);
    
    public class NameThreadFactory
    {
        private readonly Func<Action, string> nameProvider;

        public NameThreadFactory(Func<Action, string> nameProvider)
        {
            if (nameProvider == null)
                throw new ArgumentNullException(nameof(nameProvider));

            this.nameProvider = nameProvider;
        }
        
        /// <exception cref="System.ArgumentNullException">Thrown if the runnable (Action) is null.</exception>
        public Thread newThread(Action r)
        {
            if (r == null)
                throw new ArgumentNullException(nameof(r));
            Thread t = new Thread(() => r.Invoke());
            t.Name = nameProvider.Invoke(r);
            if (t.Priority != ThreadPriority.Normal)
            {
                t.Priority = ThreadPriority.Normal;
            }
            return t;
        }
    }
}
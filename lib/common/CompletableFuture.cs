using System;
using System.Threading;

namespace lib.common
{
    public class CompletableFuture<V>
    {
        private readonly object _lock = new object();
        private bool _isDone = false;
        private V _value;
        private Exception _exception;

        public bool Complete(V value)
        {
            lock (_lock)
            {
                if (_isDone) return false;
            
                _value = value;
                _isDone = true;
                Monitor.PulseAll(_lock);
            }
            return true;
        }

        public bool Cancel()
        {
            lock (_lock)
            {
                if (_isDone) return false;

                _exception = new OperationCanceledException();
                _isDone = true;
                Monitor.PulseAll(_lock);
            }
            return true;
        }

        public V Get()
        {
            lock (_lock)
            {
                while (!_isDone)
                {
                    Monitor.Wait(_lock);
                }

                if (_exception != null)
                    throw _exception;

                return _value;
            }
        }
    }
}
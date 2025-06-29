using System;
using System.Collections.Generic;
using System.Threading;

namespace lib.common
{
    public class ScheduledExecutorService : IDisposable
    {
        private List<IScheduledFuture> _scheduledFutures = new List<IScheduledFuture>();
        private Object _scheduledFuturesLock = new Object();
        private Timer _timer;
        
        public ScheduledExecutorService()
        {
            _timer = new Timer(state =>
            {
                lock (_scheduledFuturesLock)
                {
                    List<int> removals = new List<int>();
                    for (int i = 0; i < _scheduledFutures.Count; i++)
                    {
                        _scheduledFutures[i].ExecuteIfNeeded(Utils.getUnixTimeStampInMilliseconds());
                        if (_scheduledFutures[i].WasExecuted() || _scheduledFutures[i].IsExecuting())
                        {
                            removals.Add(i);
                        }
                    }

                    for (int i = 0; i < removals.Count; i++)
                    {
                        _scheduledFutures.RemoveAt(removals[i]);
                    }
                }
            }, null, 0, 500);
        }

        public enum TimeUnit
        {
            MILLISECONDS,
            SECONDS,
            MINUTES,
            HOURS
        }

        public interface IScheduledFuture
        {
            void ExecuteIfNeeded(long currentTimeMilliseconds);
            void Cancel(bool interruptIfRunning);
            bool WasExecuted(); 
            bool IsExecuting();
            void Reset();
        }

        public class ScheduledFuture<T> : IScheduledFuture
        {
            private long _executionTime;
            private T _value;
            private object _executionLock = new object();
            private Function _function;
            private Thread _thread;
            private object _valueLock = new object();
            private bool _executed;
            private bool _executing;
            private TimeUnit _unit;

            public ScheduledFuture(Function func, long delay, TimeUnit unit = TimeUnit.SECONDS)
            {
                init(func, delay, unit);
            }
            
            private void init(Function func, long delay, TimeUnit unit = TimeUnit.SECONDS)
            {
                _function = func;
                _unit = unit;
                
                long systemMilliseconds = Utils.getUnixTimeStampInMilliseconds();
                
                switch (unit) 
                {
                    case TimeUnit.HOURS:
                        _executionTime = systemMilliseconds + (long)TimeSpan.FromHours(delay).TotalMilliseconds;
                        break;
                    case TimeUnit.MINUTES:
                        _executionTime = systemMilliseconds + (long)TimeSpan.FromMinutes(delay).TotalMilliseconds;
                        break;
                    case TimeUnit.MILLISECONDS:
                        _executionTime = systemMilliseconds + delay;
                        break;
                    case TimeUnit.SECONDS:
                        _executionTime = systemMilliseconds + (long)TimeSpan.FromSeconds(delay).TotalMilliseconds;
                        break;
                }

                _thread = new Thread(o =>
                {
                    lock (_executionLock)
                    {
                        _value = _function();
                        lock (_valueLock) 
                        {
                            Monitor.PulseAll(_valueLock);
                        }
                    }
                });
                _thread.Name = "ScheduledFuture";
            }

            public void ExecuteIfNeeded(long currentTimeMilliseconds)
            {
                if (_executed || _executing) return;
                if (currentTimeMilliseconds >= _executionTime)
                {
                    _value = Execute();
                }
            }
            
            public T Execute()
            {
                _thread.Start();
                _thread.Join();
                _executing = false;
                _executed = true;
                return _value;
            }

            public void Cancel(bool interruptIfRunning = true)
            {
                if (!_executing) return;
                if (!interruptIfRunning && _executing) return;
                _thread.Interrupt();
            }

            public bool WasExecuted()
            {
                return _executed;
            }

            public bool IsExecuting()
            {
                return _executing;
            }

            public void Reset()
            {
                _executed = false;
                _executing = false;
                init(_function, _executionTime, _unit);
            }
            
            public T get()
            {
                if (_executed) return _value;
                lock (_valueLock)
                {
                    Monitor.Wait(_valueLock);
                }
                return _value;
            }
            
            public delegate T Function();
        }
        
        public void schedule(IScheduledFuture future)
        {
            lock (_scheduledFuturesLock)
            {
                if (_scheduledFutures.Contains(future)) return;
                Monitor.Enter(_scheduledFuturesLock);
                if(future.WasExecuted()) future.Reset();
                _scheduledFutures.Add(future);
                Monitor.Exit(_scheduledFuturesLock);
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            lock (_scheduledFuturesLock)
            {
                Monitor.Enter(_scheduledFuturesLock);
                for (int i = 0; i < _scheduledFutures.Count; i++)
                {
                    _scheduledFutures[i].Cancel(false);
                }
                _scheduledFutures = null;
                Monitor.Exit(_scheduledFuturesLock);
            }
            _timer = null;
            _scheduledFuturesLock = null;
        }
    }
}
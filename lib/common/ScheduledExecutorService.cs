using System;
using System.Collections.Generic;
using System.Threading;
using log4net;
using log4net.Util;

namespace lib.common
{
    public class ScheduledExecutorService : IDisposable
    {
        private List<IScheduledFuture> _scheduledFutures = new List<IScheduledFuture>();
        private Object _scheduledFuturesLock = new Object();
        private bool _isRunning;
        private Thread _workerThread;
        private readonly AutoResetEvent _wakeup = new AutoResetEvent(false);
        private static ILog LOGGER = LogManager.GetLogger(typeof(ScheduledExecutorService));

        private bool _shutdownInitiated = false;
        private readonly object _shutdownLock = new object();

        public ScheduledExecutorService()
        {
            _isRunning = true;
            _workerThread = new Thread(() =>
            {
                while (_isRunning)
                {
                    long waitTime = -1;
                    long now = Utils.getUnixTimeStampInMilliseconds();

                    lock (_scheduledFuturesLock)
                    {
                        _scheduledFutures.RemoveAll(f => (f.WasExecuted() && !f.IsInfinite()) || f.IsCancelled());
                        
                        List<Thread> threads = new List<Thread>();
                        
                        foreach (var f in _scheduledFutures)
                        {
                            Thread optThread = f.ExecuteIfNeeded(now);

                            // Waiting for the task thread is only necessary for infinite tasks
                            // This is because of the waitTime that gets calculated
                            if (optThread != null && f.IsInfinite())
                            {
                                threads.Add(optThread);
                            }
                        }

                        threads.ForEach(t => t.Join());
                        
                        _scheduledFutures.RemoveAll(f => (f.WasExecuted() && !f.IsInfinite()) || f.IsCancelled());

                        foreach (var f in _scheduledFutures)
                        {
                            if (!f.WasExecuted() || f.IsInfinite())
                            {
                                long timeUntilExecution = f.GetExecutionTime() - now;
                                if (waitTime == Timeout.Infinite) waitTime = timeUntilExecution;
                                if (timeUntilExecution < waitTime)
                                {
                                    waitTime = Math.Max(0, timeUntilExecution);
                                }
                            }
                        }
                    }

                    if (!_isRunning) break;

                    if (waitTime <= -1) 
                    { 
                        _wakeup.WaitOne();
                    }
                    else _wakeup.WaitOne((int)Math.Min(waitTime, int.MaxValue));
                }
            });
            _workerThread.Name = "ScheduledExecutorService-worker";
            _workerThread.Start();
        }

        public List<IScheduledFuture> ScheduledFutures
        {
            get => _scheduledFutures;
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
            Thread ExecuteIfNeeded(long currentTimeMilliseconds);
            void Cancel(bool interruptIfRunning);
            bool WasExecuted();
            bool IsExecuting();
            void Reset();
            void SetInfinite(bool value);
            bool IsInfinite();
            long GetExecutionTime();
            bool IsCancelled();
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
            private bool _infinite;
            private long _delay;
            private bool _isCancelled;
            private Action _onReschedule;

            public ScheduledFuture(Function func, long delay, TimeUnit unit = TimeUnit.SECONDS, Action onReschedule = null)
            {
                _onReschedule = onReschedule;
                Init(func, delay, unit);
            }

            private void Reschedule()
            {
                long systemMilliseconds = Utils.getUnixTimeStampInMilliseconds();

                switch (_unit)
                {
                    case TimeUnit.HOURS:
                        _executionTime = systemMilliseconds + (long)TimeSpan.FromHours(_delay).TotalMilliseconds;
                        break;
                    case TimeUnit.MINUTES:
                        _executionTime = systemMilliseconds + (long)TimeSpan.FromMinutes(_delay).TotalMilliseconds;
                        break;
                    case TimeUnit.MILLISECONDS:
                        _executionTime = systemMilliseconds + _delay;
                        break;
                    case TimeUnit.SECONDS:
                        _executionTime = systemMilliseconds + (long)TimeSpan.FromSeconds(_delay).TotalMilliseconds;
                        break;
                }

                _executing = false;
                _executed = false;

                _onReschedule?.Invoke();
            }

            public long GetExecutionTime()
            {
                return _executionTime;
            }

            public bool IsInfinite()
            {
                return _infinite;
            }

            private void Init(Function func, long delay, TimeUnit unit = TimeUnit.SECONDS)
            {
                _delay = delay;
                _function = func;
                _unit = unit;
                Reschedule();
            }

            public Thread ExecuteIfNeeded(long currentTimeMilliseconds)
            {
                if (_executed || _executing) return null;
                if (currentTimeMilliseconds >= _executionTime)
                {
                    _executing = true;
                    _thread = new Thread(_ =>
                    {
                        try
                        {
                            T result = _function();

                            lock (_valueLock)
                            {
                                _value = result;
                                Monitor.PulseAll(_valueLock);
                            }
                        }
                        catch (Exception ex)
                        {
                            LOGGER.ErrorExt("Unexpected exception in worker function", ex);
                        }
                        finally
                        {
                            _executing = false;
                            _executed = true;
                            if (_infinite) Reschedule();
                        }
                    });
                    _thread.Name = "ScheduledExecutorService-worker";
                    _thread.Start();
                    
                    return _thread;
                }

                return null;
            }

            public void Cancel(bool interruptIfRunning = true)
            {
                if (!_executing) return;
                if (!interruptIfRunning && _executing) return;
                if (_thread != null) _thread.Interrupt();
                _isCancelled = true;
            }

            public bool IsCancelled()
            {
                return _isCancelled;
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
                Init(_function, _delay, _unit);
            }

            public void SetInfinite(bool value)
            {
                _infinite = value;
            }

            public T Get()
            {
                lock (_valueLock)
                {
                    Monitor.Wait(_valueLock);
                }

                return _value;
            }

            public delegate T Function();
        }

        private void scheduleInternal(IScheduledFuture future)
        {
            if (!_isRunning || _shutdownInitiated)
                throw new InvalidOperationException("Scheduler is shut down. Cannot schedule new tasks");
            
            lock (_scheduledFuturesLock)
            {
                if (_scheduledFutures.Contains(future)) return; 
                if (future.WasExecuted()) future.Reset(); 
                _scheduledFutures.Add(future); 
                _wakeup.Set();
            }
        }

        public void schedule(IScheduledFuture future)
        {
            if (future == null) throw new ArgumentNullException(nameof(future));
            if (!_isRunning || _shutdownInitiated)
                throw new InvalidOperationException("Cannot schedule task: executor is shutting down or already shut down");
            
            ThreadPool.QueueUserWorkItem(state => { scheduleInternal(future); });
        }

        public void scheduleAtFixedRate(IScheduledFuture future)
        {
            future.SetInfinite(true);
            schedule(future);
        }

        public bool IsShutdown
        {
            get => !_isRunning;
        }

        public void Dispose()
        {
            lock (_shutdownLock)
            {
                if (!_isRunning) return;
                _shutdownInitiated = true;
                _isRunning = false;
            }

            try
            {
                _wakeup.Set();
            }
            catch (ObjectDisposedException) {}
            
            _workerThread?.Join(5000);
            
            lock (_scheduledFuturesLock)
            {
                foreach (var future in _scheduledFutures)
                {
                    try
                    {
                        future.Cancel(interruptIfRunning: false);
                    }
                    catch { }
                }
                _scheduledFutures.Clear();
            }
            
            try
            {
                _wakeup.Dispose();
            }
            catch {  }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;

namespace lib.common
{
    public class AsyncWorker<T> : IDisposable
    {
        private readonly Queue<T> _workQueue = new Queue<T>();
        private Thread _workerThread;
        private readonly object _lock = new object();
        private bool _shutDown;
        private readonly string _threadName;
        private readonly Handler _handler;

        public AsyncWorker(string threadName, Handler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _threadName = threadName ?? "AsyncWorkerThread";
        }

        private void Run()
        {
            while (true)
            {
                T workItem;
                lock (_lock)
                {
                    while (_workQueue.Count == 0 && !_shutDown)
                    {
                        Monitor.Wait(_lock);
                    }
                    if (_shutDown) return;
                    workItem = _workQueue.Dequeue();
                }
                try
                {
                    _handler(workItem);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"AsyncWorker handler error: {ex}");
                }
            }
        }

        public void Submit(T toWorkOn)
        {
            if (_shutDown)
                throw new ObjectDisposedException(nameof(AsyncWorker<T>));
            lock (_lock)
            {
                _workQueue.Enqueue(toWorkOn);
                Monitor.Pulse(_lock);
                if (_workerThread == null || !_workerThread.IsAlive)
                {
                    _workerThread = new Thread(Run)
                    {
                        IsBackground = true,
                        Name = _threadName
                    };
                    _workerThread.Start();
                }
            }
        }

        private void AwaitTermination()
        {
            Thread workerToJoin;
            lock (_lock)
            {
                workerToJoin = _workerThread;
            }
            workerToJoin?.Join();
        }

        public delegate void Handler(T workItem);

        public void Dispose()
        {
            lock (_lock)
            {
                _shutDown = true;
                _workQueue.Clear();
            }
            AwaitTermination();
        }
    }
}
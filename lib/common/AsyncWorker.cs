using System;
using System.Collections.Generic;
using System.Threading;

namespace lib.common
{
    public class AsyncWorker<T> : IDisposable
    {
        private Queue<T> _workQueue = new Queue<T>();
        private Thread _workerThread;
        private bool _shutDown = false;
        private Handler _handler;
        private String _threadName;
        
        public AsyncWorker(String threadName, Handler handler)
        {
            _handler = handler;
            _threadName = threadName;
        }

        private void Run()
        {
            while (!_shutDown)
            {
                lock (_workQueue)
                {
                    _handler(_workQueue.Dequeue());
                }
            }
        }

        public void Submit(T toWorkOn)
        {
            lock (_workQueue)
            {
                _workQueue.Enqueue(toWorkOn);
            }

            if (!_workerThread.IsAlive)
            {
                _workerThread = new Thread(Run);
                _workerThread.IsBackground = true;
                _workerThread.Name = _threadName;
                _workerThread.Start();
            }
        }

        public void AwaitTermination()
        {
            if (_workerThread.IsAlive) _workerThread.Join();
        }
        
        public delegate void Handler(T workItem);

        public void Dispose()
        {
            _shutDown = true;
            if (_workerThread.IsAlive) _workerThread.Join();
            _workQueue.Clear();
        }
    }
}
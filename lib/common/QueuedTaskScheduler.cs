using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace lib.common
{
 internal class QueuedTaskScheduler : TaskScheduler, IDisposable
    {
        private readonly BlockingCollection<Action> _tasks = new BlockingCollection<Action>();
        private readonly Thread _thread;
        private volatile bool _isShutdown = false;
        private readonly ManualResetEvent _idleEvent = new ManualResetEvent(true);

        public bool IsShutdown => _isShutdown;

        public QueuedTaskScheduler(NameThreadFactory nameThreadFactory, string threadNameForLogger)
        {
            if (nameThreadFactory == null)
                throw new ArgumentNullException(nameof(nameThreadFactory));

            _thread = nameThreadFactory.newThread(() =>
            {
                foreach (var action in _tasks.GetConsumingEnumerable())
                {
                    _idleEvent.Reset();
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        LogManager.GetLogger(typeof(QueuedTaskScheduler)).Error($"Unhandled exception in '{threadNameForLogger}' task.", ex);
                    }
                    finally
                    {
                        if (_tasks.Count == 0 && !_tasks.IsAddingCompleted)
                        {
                             _idleEvent.Set();
                        }
                    }
                }
                _idleEvent.Set();
            });
            _thread.IsBackground = true;
            _thread.Start();
        }

        protected override void QueueTask(Task task)
        {
            if (_isShutdown)
            {
                throw new InvalidOperationException("Scheduler is shut down and cannot accept new tasks.");
            }
            _tasks.Add(() => TryExecuteTask(task));
        }

        public void PQueueTask(Task task)
        {
            QueueTask(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Enumerable.Empty<Task>();
        }

        public void Shutdown()
        {
            if (_isShutdown) return;
            _isShutdown = true;
            _tasks.CompleteAdding();
        }

        public bool awaitTermination(TimeSpan timeout)
        {
            if (!_isShutdown)
                return false;

            return _thread.Join(timeout);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Shutdown();
                if (_thread.IsAlive)
                {
                    _thread.Join(TimeSpan.FromMilliseconds(500));
                }
                _tasks.Dispose();
                _idleEvent.Dispose();
            }
        }
    }
}
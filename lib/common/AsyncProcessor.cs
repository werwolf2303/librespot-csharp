using System;
using System.Threading.Tasks;
using log4net;

namespace lib.common
{
    /*
public delegate TResult Func<T, TResult>(T arg);

    /// <summary>
    /// Simple worker thread that processes tasks sequentially
    /// </summary>
    /// <typeparam name="REQ">The type of task/input that AsyncProcessor handles.</typeparam>
    /// <typeparam name="RES">Return type of our processor implementation</typeparam>
    public class AsyncProcessor<REQ, RES> : IDisposable
    {
        private static readonly ILog LOGGER = LogManager.GetLogger(typeof(AsyncProcessor<REQ, RES>));
        private readonly string name;
        private readonly Func<REQ, RES> processor;
        private readonly QueuedTaskScheduler executor;
        private readonly TaskFactory _taskFactory;

        /// <summary>
        /// Initializes a new instance of the AsyncProcessor.
        /// </summary>
        /// <param name="name">Name of async processor - used for thread name and logging.</param>
        /// <param name="processor">Actual processing implementation ran on background thread.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if name or processor is null.</exception>
        public AsyncProcessor(string name, Func<REQ, RES> processor)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));

            this.name = name;
            this.processor = processor;

            NameThreadFactory threadFactory = new NameThreadFactory(r => $"AsyncProcessor-{this.name}-Thread");
            this.executor = new QueuedTaskScheduler(threadFactory, this.name);
            this._taskFactory = new TaskFactory(this.executor);

            LOGGER.Debug("AsyncProcessor{{{0}}} has started: " + this.name);
        }
        
        public Task<RES> submit(REQ task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            return this._taskFactory.StartNew(() => this.processor.Invoke(task));
        }
        
        public bool awaitTermination(long timeout, Utils.TimeUnit unit)
        {
            if (!executor.IsShutdown)
                throw new InvalidOperationException(string.Format("AsyncProcessor{{{0}}} hasn't been shut down yet", name));

            TimeSpan timeoutSpan = ConvertTimeUnitToTimeSpan(timeout, unit);

            if (executor.awaitTermination(timeoutSpan))
            {
                LOGGER.Debug("AsyncProcessor{{{0}}} is shut down: " + name);
                return true;
            }
            else
            {
                return false;
            }
        }
        
        public void Dispose()
        {
            LOGGER.Debug("AsyncProcessor{{{0}}} is shutting down: " + name);
            executor.Shutdown();
        }

        private TimeSpan ConvertTimeUnitToTimeSpan(long timeout, Utils.TimeUnit unit)
        {
            switch (unit)
            {
                case Utils.TimeUnit.DAYS: return TimeSpan.FromDays(timeout);
                case Utils.TimeUnit.HOURS: return TimeSpan.FromHours(timeout);
                case Utils.TimeUnit.MINUTES: return TimeSpan.FromMinutes(timeout);
                case Utils.TimeUnit.SECONDS: return TimeSpan.FromSeconds(timeout);
                case Utils.TimeUnit.MILLISECONDS: return TimeSpan.FromMilliseconds(timeout);
                case Utils.TimeUnit.MICROSECONDS: return TimeSpan.FromTicks(timeout * 10);
                case Utils.TimeUnit.NANOSECONDS: return TimeSpan.FromTicks(timeout / 100);
                default: throw new ArgumentOutOfRangeException(nameof(unit), "Unsupported TimeUnit.");
            }
        }
    }*/
}
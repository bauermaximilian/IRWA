using System;
using System.Threading;

#nullable enable

namespace IRWA
{
    public class ThreadPool
    {
        private class ThreadPoolTask : Task
        {
            public ThreadPoolTask(Action action, CancellationToken cancellationToken) 
                : base(action, cancellationToken)
            {
            }

            public void RunTask()
            {
                Run();
            }
        }

        public const int ThreadPoolShutdownTimeout = 3000;

        private readonly AutoResetEvent jobAdded = new AutoResetEvent(false);
        private readonly ConcurrentQueue jobs = new ConcurrentQueue();
        private readonly Thread[] threads;

        private CancellationTokenSource? threadPoolShutdownCts;

        public ThreadPool(int size)
        {
            if (size < 0 || size > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            threads = new Thread[size];
        }

        public void Start()
        {
            Stop();

            threadPoolShutdownCts = new CancellationTokenSource();
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(RunThreadPoolThread);
                threads[i].Start();
            }
        }

        public void Stop(int timeout = ThreadPoolShutdownTimeout)
        {
            threadPoolShutdownCts?.Cancel();
            for (int i = 0; i < threads.Length; i++)
            {
                var thread = threads[i];
                if (thread != null && thread.IsAlive)
                {
                    if (!thread.Join(timeout))
                    {
                        thread.Abort();
                    }
                }
            }
        }

        public Task RunAsync(Action action, CancellationToken cancellationToken = default)
        {
            var task = new ThreadPoolTask(action, cancellationToken);
            jobs.Enqueue(task);
            jobAdded.Set();
            return task;
        }

        private void RunThreadPoolThread()
        {
            if (threadPoolShutdownCts == null)
            {
                return;
            }

            var cancellationToken = threadPoolShutdownCts.Token;
            var waitHandles = new WaitHandle[]
            {
                jobAdded,
                cancellationToken.WaitHandle
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                int handleIndex = WaitHandle.WaitAny(waitHandles);
                if (handleIndex == 0)
                {
                    if (jobs.TryDequeue(out var job) && job is ThreadPoolTask task)
                    {
                        task.RunTask();
                    }
                }
            }
        }
    }
}

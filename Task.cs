using System;
using System.Threading;

#nullable enable

namespace IRWA
{
    public enum TaskState
    {
        Initial,
        Running,
        Cancelled,
        Failed,
        Finished
    }

    public abstract class Task
    {
        public CancellationToken CancellationToken { get; }
        public TaskState State { get; private set; } = TaskState.Initial;
        public Exception? Error { get; private set; }

        public WaitHandle TaskCompleted => taskCompletedEvent;

        private readonly ManualResetEvent taskCompletedEvent = new ManualResetEvent(false);
        private readonly Action action;

        protected Task(Action action, CancellationToken cancellationToken)
        {
            this.action = action;
            CancellationToken = cancellationToken;
        }

        protected void Run()
        {
            State = TaskState.Running;
            try
            {
                if (!CancellationToken.IsCancellationRequested)
                {
                    action();
                    State = TaskState.Finished;
                }
                else
                {
                    State = TaskState.Cancelled;
                }
            }
            catch (Exception exc)
            {
                Error = exc;
                State = TaskState.Failed;
            }
            finally
            {
                taskCompletedEvent.Set();
            }
        }
    }
}

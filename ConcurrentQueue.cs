using System.Collections;

#nullable enable

namespace IRWA
{
    public class ConcurrentQueue
    {
        private readonly Queue queue = new Queue();
        private readonly object queueLock = new object();

        public void Enqueue(object item)
        {
            lock (queueLock)
            {
                queue.Enqueue(item);
            }
        }

        public bool TryDequeue(out object item)
        {
            lock (queueLock)
            {
                if (queue.Count > 0)
                {
                    item = queue.Dequeue();
                    return true;
                }
                else
                {
                    item = null!;
                    return false;
                }
            }
        }
    }
}

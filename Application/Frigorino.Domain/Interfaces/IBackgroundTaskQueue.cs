namespace Frigorino.Domain.Interfaces
{
    public interface IBackgroundTaskQueue
    {
        /// <summary>
        /// Enqueues fire-and-forget work to run shortly after — off the request thread, in a fresh DI scope.
        /// Returns false (and logs a warning) if the queue is full; never blocks the caller.
        /// </summary>
        bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work);
    }
}

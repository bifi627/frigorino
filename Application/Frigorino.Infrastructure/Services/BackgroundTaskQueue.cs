using System.Threading.Channels;
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // In-memory, bounded, lossy-by-design queue of fire-and-forget work items.
    // Single consumer (QueuedHostedService) drains it; many producers may enqueue.
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        public const int Capacity = 1000;

        private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel;
        private readonly ILogger<BackgroundTaskQueue> _logger;

        public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger)
        {
            _logger = logger;
            _channel = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(
                new BoundedChannelOptions(Capacity)
                {
                    // One consumer, many producers — lets the channel optimize.
                    SingleReader = true,
                    SingleWriter = false,
                    // FullMode defaults to Wait, so TryWrite returns false when full
                    // (we never call WriteAsync, so it never actually blocks).
                });
        }

        // Consumer-only. Deliberately kept off IBackgroundTaskQueue so producers can't dequeue.
        public ChannelReader<Func<IServiceProvider, CancellationToken, Task>> Reader => _channel.Reader;

        public bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work)
        {
            ArgumentNullException.ThrowIfNull(work);

            if (_channel.Writer.TryWrite(work))
            {
                return true;
            }

            _logger.LogWarning(
                "Background task queue is full (capacity {Capacity}); dropping work item.", Capacity);
            return false;
        }
    }
}

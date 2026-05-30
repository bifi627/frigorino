using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class BackgroundTaskQueueTests
    {
        private static BackgroundTaskQueue NewQueue() =>
            new BackgroundTaskQueue(NullLogger<BackgroundTaskQueue>.Instance);

        [Fact]
        public void TryEnqueue_WithCapacity_ReturnsTrue()
        {
            var queue = NewQueue();

            var enqueued = queue.TryEnqueue((_, _) => Task.CompletedTask);

            Assert.True(enqueued);
        }

        [Fact]
        public void TryEnqueue_Null_Throws()
        {
            var queue = NewQueue();

            Assert.Throws<ArgumentNullException>(() => queue.TryEnqueue(null!));
        }

        [Fact]
        public void TryEnqueue_WhenFull_ReturnsFalse()
        {
            var queue = NewQueue();

            // No consumer is running, so nothing drains the channel.
            // Fill it exactly to capacity — every write must succeed.
            for (var i = 0; i < BackgroundTaskQueue.Capacity; i++)
            {
                Assert.True(queue.TryEnqueue((_, _) => Task.CompletedTask));
            }

            // The next write has nowhere to go and must be rejected (not block).
            var overflow = queue.TryEnqueue((_, _) => Task.CompletedTask);

            Assert.False(overflow);
        }
    }
}

using FakeItEasy;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class ProductClassificationTriggerTests
    {
        [Fact]
        public void Null_OnProductReferenced_DoesNotEnqueue()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var trigger = new NullProductClassificationTrigger();

            trigger.OnProductReferenced(42, "Milk");

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public void Queueing_OnProductReferenced_Enqueues()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var trigger = new QueueingProductClassificationTrigger(queue);

            trigger.OnProductReferenced(42, "Milk");

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustHaveHappenedOnceExactly();
        }
    }
}

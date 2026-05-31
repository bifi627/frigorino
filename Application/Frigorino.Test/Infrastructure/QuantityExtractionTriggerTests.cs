using FakeItEasy;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class QuantityExtractionTriggerTests
    {
        [Fact]
        public void Queueing_DigitText_EnqueuesAndDoesNotClassifyDirectly()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemEntered(42, 7, 100, "20 apples");

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [Fact]
        public void Queueing_NoDigitText_ClassifiesRawAndDoesNotEnqueue()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemEntered(42, 7, 100, "milk");

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
            A.CallTo(() => classification.OnProductReferenced(42, "milk")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Null_ClassifiesRawAndDoesNotEnqueue()
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemEntered(42, 7, 100, "20 apples");

            A.CallTo(() => classification.OnProductReferenced(42, "20 apples")).MustHaveHappenedOnceExactly();
        }
    }
}

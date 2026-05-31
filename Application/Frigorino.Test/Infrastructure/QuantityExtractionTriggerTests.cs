using FakeItEasy;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class QuantityExtractionTriggerTests
    {
        private static ItemTextAnalysis Resolved(string name) =>
            new(ItemTextRoute.Resolved, name, Quantity.Create(2m, QuantityUnit.Kilogram).Value);

        [Fact]
        public void Queueing_NeedsExtraction_EnqueuesAndDoesNotClassifyDirectly()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.NeedsExtraction, "20 apples", null));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [Fact]
        public void Queueing_Resolved_ClassifiesCleanNameAndDoesNotEnqueue()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemRouted(42, 7, 100, Resolved("flour"));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
            A.CallTo(() => classification.OnProductReferenced(42, "flour")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Queueing_ClassifyOnly_ClassifiesRawAndDoesNotEnqueue()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.ClassifyOnly, "milk", null));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
            A.CallTo(() => classification.OnProductReferenced(42, "milk")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Queueing_SkipAi_DoesNothing()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.SkipAi, "https://x.com", null));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [Theory]
        [InlineData(ItemTextRoute.NeedsExtraction, "20 apples")] // extraction off -> classify raw instead
        [InlineData(ItemTextRoute.ClassifyOnly, "milk")]
        public void Null_NonSkipRoutes_ClassifyCleanName(ItemTextRoute route, string cleanName)
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(route, cleanName, null));

            A.CallTo(() => classification.OnProductReferenced(42, cleanName)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Null_Resolved_ClassifiesCleanName()
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemRouted(42, 7, 100, Resolved("flour"));

            A.CallTo(() => classification.OnProductReferenced(42, "flour")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Null_SkipAi_DoesNothing()
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.SkipAi, "https://x.com", null));

            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }
    }
}

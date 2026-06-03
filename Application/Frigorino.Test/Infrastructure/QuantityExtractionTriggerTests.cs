using FakeItEasy;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class QuantityExtractionTriggerTests
    {
        [Fact]
        public void Queueing_NeedsExtraction_EnqueuesExtractJob()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var trigger = new QueueingQuantityExtractionTrigger(queue);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.NeedsExtraction, "20 apples"));

            // The enabled path only enqueues the extract job; classification is chained inside that
            // job (on the extractor's clean name), not triggered directly here.
            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Queueing_SkipAi_DoesNothing()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var trigger = new QueueingQuantityExtractionTrigger(queue);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.SkipAi, "https://x.com"));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public void Null_NeedsExtraction_ClassifiesCleanNameDirectly()
        {
            // Extraction off: nothing was stripped, so the clean name is the raw text. Classify it
            // directly since there is no extract job to chain from.
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.NeedsExtraction, "two cups of coffee"));

            A.CallTo(() => classification.OnProductReferenced(42, "two cups of coffee")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Null_SkipAi_DoesNothing()
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.SkipAi, "https://x.com"));

            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }
    }
}

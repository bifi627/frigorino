using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    // Enabled path: NeedsExtraction enqueues the recipe extract job. No classification ever.
    public class QueueingRecipeQuantityExtractionTrigger : IRecipeQuantityExtractionTrigger
    {
        private readonly IBackgroundTaskQueue _queue;

        public QueueingRecipeQuantityExtractionTrigger(IBackgroundTaskQueue queue)
        {
            _queue = queue;
        }

        public void OnItemRouted(int householdId, int recipeId, int itemId, ItemTextAnalysis analysis)
        {
            if (analysis.Route == ItemTextRoute.NeedsExtraction)
            {
                _queue.TryEnqueue((sp, ct) =>
                    sp.GetRequiredService<IExtractRecipeQuantityJob>()
                      .Run(householdId, recipeId, itemId, analysis.CleanName, ct));
            }
        }
    }

    // Disabled path: extraction off. PURE no-op — unlike the list NullQuantityExtractionTrigger,
    // recipes never classify, so there is nothing to fall through to.
    public class NullRecipeQuantityExtractionTrigger : IRecipeQuantityExtractionTrigger
    {
        public void OnItemRouted(int householdId, int recipeId, int itemId, ItemTextAnalysis analysis)
        {
        }
    }
}

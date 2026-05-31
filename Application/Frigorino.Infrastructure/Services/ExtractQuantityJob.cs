using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Runs in a fresh DI scope on the background runner. Lossy by design: any failure drops the
    // work item. On success it rewrites the item's text to the clean name + sets the structured
    // quantity, then chains to classification on the clean name (the catalog key — cache intact).
    public class ExtractQuantityJob : IExtractQuantityJob
    {
        private readonly ApplicationDbContext _db;
        private readonly IQuantityExtractor _extractor;
        private readonly IProductClassificationTrigger _classificationTrigger;
        private readonly ILogger<ExtractQuantityJob> _logger;

        public ExtractQuantityJob(
            ApplicationDbContext db,
            IQuantityExtractor extractor,
            IProductClassificationTrigger classificationTrigger,
            ILogger<ExtractQuantityJob> logger)
        {
            _db = db;
            _extractor = extractor;
            _classificationTrigger = classificationTrigger;
            _logger = logger;
        }

        public async Task Run(int householdId, int listId, int itemId, string rawText, CancellationToken ct)
        {
            // Load the aggregate so the write-back goes through the domain method. If the list/item
            // is gone (deleted between enqueue and run), no-op without calling the extractor.
            var list = await _db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            var item = list?.ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (list is null || item is null)
            {
                return;
            }

            var result = await _extractor.ExtractAsync(rawText, ct);
            if (result.IsFailed)
            {
                _logger.LogWarning(
                    "Quantity extraction failed for item {ItemId} (household {HouseholdId}); dropping.",
                    itemId, householdId);
                return;
            }

            var extraction = result.Value;
            var applied = list.ApplyExtractedQuantity(itemId, extraction.CleanName, extraction.Quantity);
            if (applied.IsFailed)
            {
                return;
            }

            await _db.SaveChangesAsync(ct);

            // Chain: classify on the clean name so the Product-catalog cache keys on "apples",
            // not "20 apples". Enabled trigger enqueues classify; disabled trigger is a no-op.
            _classificationTrigger.OnProductReferenced(householdId, extraction.CleanName);
        }
    }
}

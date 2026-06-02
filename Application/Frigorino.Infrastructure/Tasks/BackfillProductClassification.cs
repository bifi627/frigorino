using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Tasks
{
    // Startup backfill: enqueue classification for ListItem product names that have no up-to-date
    // Product (never classified, or below the current ClassifierVersion). Idempotent and
    // version-aware via the existing classify job; capped per run, with the remainder picked up on
    // the next cold start (the queue is lossy, but loss here is recoverable by re-scanning).
    public class BackfillProductClassification : IMaintenanceTask
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IProductClassificationTrigger _trigger;
        private readonly IItemClassifier _classifier;
        private readonly ILogger<BackfillProductClassification> _logger;

        public BackfillProductClassification(
            ApplicationDbContext dbContext,
            IProductClassificationTrigger trigger,
            IItemClassifier classifier,
            ILogger<BackfillProductClassification> logger)
        {
            _dbContext = dbContext;
            _trigger = trigger;
            _classifier = classifier;
            _logger = logger;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var candidates = await _dbContext.ListItems
                .Where(li => li.IsActive)
                .Select(li => new ListItemNameCandidate(li.List.HouseholdId, li.Text))
                .Distinct()
                .ToListAsync(cancellationToken);

            var existing = await _dbContext.Products
                .Select(p => new ExistingProduct(p.HouseholdId, p.NormalizedName, p.ClassifierVersion))
                .ToListAsync(cancellationToken);

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, _classifier.Version);
            if (gaps.Count == 0)
            {
                return;
            }

            // Cap per run to the queue capacity so a large first backfill cannot overflow (and
            // silently drop) the lossy queue; the remainder is enqueued on the next cold start.
            var toEnqueue = gaps.Take(BackgroundTaskQueue.Capacity).ToList();
            foreach (var gap in toEnqueue)
            {
                _trigger.OnProductReferenced(gap.HouseholdId, gap.RawName);
            }

            var deferred = gaps.Count - toEnqueue.Count;
            _logger.LogInformation(
                "Backfill classification: {Total} gap(s) found, {Enqueued} enqueued, {Deferred} deferred to next cold start.",
                gaps.Count, toEnqueue.Count, deferred);
        }
    }
}

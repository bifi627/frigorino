using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Idempotent, cache-aware classify job. Runs in a fresh DI scope created by the background
    // runner. Lossy by design: any failure drops the work item; the next reference re-triggers it.
    public class ClassifyProductJob : IClassifyProductJob
    {
        private readonly ApplicationDbContext _db;
        private readonly IItemClassifier _classifier;
        private readonly ILogger<ClassifyProductJob> _logger;

        public ClassifyProductJob(
            ApplicationDbContext db, IItemClassifier classifier, ILogger<ClassifyProductJob> logger)
        {
            _db = db;
            _classifier = classifier;
            _logger = logger;
        }

        public async Task Run(int householdId, string rawName, CancellationToken ct)
        {
            var normalized = ProductName.Normalize(rawName);
            if (normalized.Length == 0)
            {
                return;
            }

            var existing = await _db.Products
                .FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.NormalizedName == normalized, ct);

            if (existing is not null && existing.ClassifierVersion >= _classifier.Version)
            {
                // Cache hit — already classified at the current version.
                return;
            }

            var result = await _classifier.ClassifyAsync(normalized, ct);
            if (result.IsFailed)
            {
                _logger.LogWarning(
                    "Classification failed for product '{NormalizedName}' (household {HouseholdId}); dropping.",
                    normalized, householdId);
                return;
            }

            if (existing is null)
            {
                var created = Product.Create(householdId, normalized, result.Value, _classifier.Version);
                if (created.IsFailed)
                {
                    return;
                }
                _db.Products.Add(created.Value);
            }
            else
            {
                existing.ApplyClassification(result.Value, _classifier.Version);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Benign unique-index race: another work item classified the same new name first.
                // The work is done; nothing more to do.
                _logger.LogDebug(
                    "Concurrent insert race for product '{NormalizedName}' (household {HouseholdId}); ignoring.",
                    normalized, householdId);
            }
        }
    }
}

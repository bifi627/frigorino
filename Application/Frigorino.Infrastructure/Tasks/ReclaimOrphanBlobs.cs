using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frigorino.Infrastructure.Tasks
{
    // Mark-and-sweep reclamation of orphaned blobs: deletes stored blobs that no ListItems row
    // references (active or soft-deleted) and that are older than the grace period. Robust to every
    // leak path — the bulk purge, DB cascade deletes, and a crash between blob save and DB commit —
    // without tracing FK relationships. Order-independent with DeleteInactiveItems.
    public sealed class ReclaimOrphanBlobs : IMaintenanceTask
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IFileStorage _storage;
        private readonly IFileStorageMaintenance _maintenance;
        private readonly TimeSpan _gracePeriod;
        private readonly ILogger<ReclaimOrphanBlobs> _logger;

        public ReclaimOrphanBlobs(
            ApplicationDbContext dbContext,
            IFileStorage storage,
            IFileStorageMaintenance maintenance,
            IOptions<MaintenanceSettings> settings,
            ILogger<ReclaimOrphanBlobs> logger)
        {
            _dbContext = dbContext;
            _storage = storage;
            _maintenance = maintenance;
            _gracePeriod = TimeSpan.FromHours(settings.Value.OrphanBlobGraceHours);
            _logger = logger;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            // Referenced set: every full-res + thumbnail key across ALL ListItems (active AND
            // soft-deleted — soft-deleted items keep their blob for undo until they are purged).
            var keyPairs = await _dbContext.ListItems
                .Where(li => li.StorageKey != null)
                .Select(li => new { li.StorageKey, li.ThumbnailStorageKey })
                .ToListAsync(cancellationToken);

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in keyPairs)
            {
                if (pair.StorageKey is not null)
                {
                    referenced.Add(pair.StorageKey);
                }

                if (pair.ThumbnailStorageKey is not null)
                {
                    referenced.Add(pair.ThumbnailStorageKey);
                }
            }

            var blobs = new List<StoredBlob>();
            await foreach (var blob in _maintenance.ListAsync(cancellationToken))
            {
                blobs.Add(blob);
            }

            var reclaimable = OrphanBlobSweep.SelectReclaimableKeys(
                blobs, referenced, DateTimeOffset.UtcNow, _gracePeriod);

            var deleted = 0;
            foreach (var key in reclaimable)
            {
                try
                {
                    await _storage.DeleteAsync(key, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reclaim orphan blob {Key}", key);
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation("Reclaimed {Count} orphan blob(s).", deleted);
            }
        }
    }
}

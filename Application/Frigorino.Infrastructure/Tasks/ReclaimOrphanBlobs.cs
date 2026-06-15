using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Notifications;
using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frigorino.Infrastructure.Tasks
{
    // Mark-and-sweep reclamation of orphaned blobs, per blob area. For each registered
    // IBlobReferenceSource it deletes blobs in that area's folder that no row references (active or
    // soft-deleted) and that are older than the grace period. Robust to every leak path — the bulk
    // purge, DB cascade deletes, and a crash between blob save and DB commit — without tracing FK
    // relationships. Order-independent with DeleteInactiveItems. Adding a blob area means adding a
    // reference source + its keyed storage; this sweep needs no edits.
    public sealed class ReclaimOrphanBlobs : IMaintenanceTask
    {
        private readonly IServiceProvider _services;
        private readonly IEnumerable<IBlobReferenceSource> _referenceSources;
        private readonly TimeSpan _gracePeriod;
        private readonly ILogger<ReclaimOrphanBlobs> _logger;

        public ReclaimOrphanBlobs(
            IServiceProvider services,
            IEnumerable<IBlobReferenceSource> referenceSources,
            IOptions<MaintenanceSettings> settings,
            ILogger<ReclaimOrphanBlobs> logger)
        {
            _services = services;
            _referenceSources = referenceSources;
            _gracePeriod = TimeSpan.FromHours(settings.Value.OrphanBlobGraceHours);
            _logger = logger;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            foreach (var source in _referenceSources)
            {
                await ReclaimAreaAsync(source, cancellationToken);
            }
        }

        private async Task ReclaimAreaAsync(IBlobReferenceSource source, CancellationToken cancellationToken)
        {
            var referenced = await source.GetReferencedKeysAsync(cancellationToken);

            // Resolve this area's storage by its DI key — keeps the sweep scoped to one feature's folder.
            var storage = _services.GetRequiredKeyedService<IFileStorage>(source.AreaName);
            var maintenance = _services.GetRequiredKeyedService<IFileStorageMaintenance>(source.AreaName);

            var blobs = new List<StoredBlob>();
            await foreach (var blob in maintenance.ListAsync(cancellationToken))
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
                    await storage.DeleteAsync(key, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reclaim orphan blob {Key} in area {Area}", key, source.AreaName);
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Reclaimed {Count} orphan blob(s) in area {Area}.", deleted, source.AreaName);
            }
        }
    }
}

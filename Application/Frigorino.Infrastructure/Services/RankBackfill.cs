using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // One-time expand-phase backfill: assign fractional-index Rank to rows created before the
    // Rank column existed, derived from the legacy SortOrder ordering. Does NOT bump UpdatedAt
    // (ExecuteUpdateAsync bypasses SaveChangesAsync stamping) — deliberate, mirrors the retention
    // concern that retired the old RecalculateSortOrderTask sweep. Runs at startup before serving;
    // a no-op once every row has a Rank. Removed in the deferred cleanup once stage+prod are filled.
    //
    // The Rank column is nullable at the DB level (the model marks it required), so the "needs
    // backfill" guard compares via EF.Property<string?> to read the real DB nullability.
    public static class RankBackfill
    {
        public static async Task RunAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct = default)
        {
            await BackfillListItemsAsync(db, logger, ct);
            await BackfillInventoryItemsAsync(db, logger, ct);
        }

        private static async Task BackfillListItemsAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)
        {
            if (!await db.ListItems.AnyAsync(i => EF.Property<string?>(i, nameof(ListItem.Rank)) == null, ct))
            {
                return;
            }

            // Group by (list, status) section; order within section by the legacy SortOrder.
            var rows = await db.ListItems
                .OrderBy(i => i.ListId).ThenBy(i => i.Status).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new { i.Id, i.ListId, i.Status })
                .ToListAsync(ct);

            var total = 0;
            foreach (var section in rows.GroupBy(r => new { r.ListId, r.Status }))
            {
                var ordered = section.ToList();
                var keys = FractionalIndex.GenerateKeysBetween(null, null, ordered.Count);
                for (var k = 0; k < ordered.Count; k++)
                {
                    var id = ordered[k].Id;
                    var rank = keys[k];
                    await db.ListItems.Where(i => i.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.Rank, rank), ct);
                    total++;
                }
            }
            logger.LogInformation("RankBackfill: assigned Rank to {Count} list items.", total);
        }

        private static async Task BackfillInventoryItemsAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)
        {
            if (!await db.InventoryItems.AnyAsync(i => EF.Property<string?>(i, nameof(InventoryItem.Rank)) == null, ct))
            {
                return;
            }

            var rows = await db.InventoryItems
                .OrderBy(i => i.InventoryId).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new { i.Id, i.InventoryId })
                .ToListAsync(ct);

            var total = 0;
            foreach (var section in rows.GroupBy(r => r.InventoryId))
            {
                var ordered = section.ToList();
                var keys = FractionalIndex.GenerateKeysBetween(null, null, ordered.Count);
                for (var k = 0; k < ordered.Count; k++)
                {
                    var id = ordered[k].Id;
                    var rank = keys[k];
                    await db.InventoryItems.Where(i => i.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.Rank, rank), ct);
                    total++;
                }
            }
            logger.LogInformation("RankBackfill: assigned Rank to {Count} inventory items.", total);
        }
    }
}

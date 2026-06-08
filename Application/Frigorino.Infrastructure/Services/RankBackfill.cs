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
    // The "needs backfill" guard MUST use raw SQL: the Rank column is nullable at the DB level
    // during the expand phase, but the EF model maps it required (.IsRequired()), so a LINQ
    // `Rank == null` predicate is optimized by SqlNullabilityProcessor to `WHERE FALSE` (it prunes
    // IS NULL on a column the model declares non-nullable) — which would make the guard always skip
    // and the backfill never run. Raw SQL bypasses that and asks the actual column.
    public static class RankBackfill
    {
        public static async Task RunAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct = default)
        {
            await BackfillListItemsAsync(db, logger, ct);
            await BackfillInventoryItemsAsync(db, logger, ct);
        }

        // True when at least one active-or-inactive row in `table` still has a NULL Rank.
        // Table names are internal constants (EF default mappings), never user input.
        private static async Task<bool> HasNullRankAsync(ApplicationDbContext db, string table, CancellationToken ct)
        {
            var count = await db.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"{table}\" WHERE \"Rank\" IS NULL")
                .SingleAsync(ct);
            return count > 0;
        }

        private static async Task BackfillListItemsAsync(ApplicationDbContext db, ILogger logger, CancellationToken ct)
        {
            if (!await HasNullRankAsync(db, "ListItems", ct))
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
            if (!await HasNullRankAsync(db, "InventoryItems", ct))
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

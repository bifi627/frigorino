using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Items
{
    // Concurrent reorders/appends into the same slot can mint the same Rank; the partial unique
    // index (ListId/InventoryId, [Status,] Rank WHERE IsActive) rejects the duplicate with SQLSTATE
    // 23505. The aggregate re-mints from current neighbours, so on conflict we reload fresh state
    // and re-apply. Bounded — a true unresolved race surfaces as a thrown exception (500/conflict).
    //
    // `applyAndSave` must, on EACH invocation: clear the change tracker, (re)load the aggregate,
    // invoke the aggregate method, SaveChangesAsync, and return the projected response. It is
    // re-invoked from scratch on each attempt so the re-mint sees the committed neighbour.
    public static class RankRetry
    {
        public const int MaxAttempts = 3;

        public static async Task<T> SaveWithRetryAsync<T>(Func<Task<T>> applyAndSave)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await applyAndSave();
                }
                catch (DbUpdateException ex)
                    when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation }
                          && attempt < MaxAttempts)
                {
                    // Unique-violation on Rank: a concurrent request committed the same key first.
                    // The next iteration reloads fresh state and re-mints. Any other DbUpdateException
                    // (transient fault / deadlock / timeout) propagates.
                }
            }
        }
    }
}

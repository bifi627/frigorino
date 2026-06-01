using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Notifications
{
    // Invoked by the secured /internal/expiry-scan endpoint. Claim-slot-first ordering: the
    // NotificationDispatch ledger row is inserted and committed BEFORE the send is enqueued, so
    // concurrent/double fires are safe — only the run that wins the unique-index race on
    // (UserId, HouseholdId, SentOn) enqueues. Trade-off: a slot may be claimed whose send is later
    // dropped (acceptable for a daily digest — better to occasionally miss than double-notify).
    public class ExpiryNotificationScan
    {
        private readonly ApplicationDbContext _db;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<ExpiryNotificationScan> _logger;

        public ExpiryNotificationScan(
            ApplicationDbContext db,
            IBackgroundTaskQueue queue,
            ILogger<ExpiryNotificationScan> logger)
        {
            _db = db;
            _queue = queue;
            _logger = logger;
        }

        public async Task RunAsync(DateOnly today, CancellationToken ct)
        {
            // 1. Candidate items: active, with an expiry date.
            var candidates = await _db.InventoryItems
                .Where(i => i.IsActive && i.ExpiryDate != null && i.Inventory.IsActive)
                .Select(i => new ExpiryCandidate(
                    i.InventoryId, i.Inventory.HouseholdId, i.Text, i.ExpiryDate!.Value))
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                _logger.LogInformation("Expiry scan: no candidate items.");
                return;
            }

            var householdIds = candidates.Select(c => c.HouseholdId).Distinct().ToList();
            var inventoryIds = candidates.Select(c => c.InventoryId).Distinct().ToList();

            // 2. Inventory settings for those inventories.
            var inventorySettings = await _db.InventorySettings
                .Where(s => inventoryIds.Contains(s.InventoryId))
                .ToDictionaryAsync(
                    s => s.InventoryId,
                    s => new InventoryNotificationSetting(s.ExpiryNotificationsEnabled, s.ExpiryLeadDays),
                    ct);

            // 3. Recipients: active members of those households who opted in AND have >=1 token.
            var recipients = await (
                from uh in _db.UserHouseholds
                where uh.IsActive && householdIds.Contains(uh.HouseholdId)
                join us in _db.UserSettings on uh.UserId equals us.UserId
                where us.ExpiryNotificationsEnabled
                where _db.FcmTokens.Any(t => t.UserId == uh.UserId)
                select new DigestRecipient(uh.UserId, uh.HouseholdId, us.ExpiryLeadDays, us.Language))
                .ToListAsync(ct);

            if (recipients.Count == 0)
            {
                _logger.LogInformation("Expiry scan: no eligible recipients.");
                return;
            }

            // 4. Already-dispatched keys for today.
            var dispatchedToday = await _db.NotificationDispatches
                .Where(d => d.SentOn == today)
                .Select(d => new { d.UserId, d.HouseholdId })
                .ToListAsync(ct);
            var alreadyDispatched = dispatchedToday
                .Select(d => (d.UserId, d.HouseholdId))
                .ToHashSet();

            // 5. Plan + dispatch. Claim each slot (insert + commit the ledger row) BEFORE enqueuing,
            // so a concurrent scan that lost the unique-index race never enqueues a duplicate send.
            var plans = ExpiryDigestPlanner.Plan(candidates, inventorySettings, recipients, alreadyDispatched, today);
            var claimed = 0;
            foreach (var plan in plans)
            {
                var notification = DigestMessageComposer.Compose(plan);
                var userId = plan.UserId;

                // Claim the slot first. The unique index on (UserId, HouseholdId, SentOn) lets only one
                // concurrent scan win; the loser hits DbUpdateException and skips enqueuing.
                var dispatch = NotificationDispatch.Create(plan.UserId, plan.HouseholdId, today);
                _db.NotificationDispatches.Add(dispatch);
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // A concurrent scan already claimed this (user, household, day). Detach so the failed
                    // row is not re-attempted on the next iteration's save, then skip the enqueue.
                    _db.Entry(dispatch).State = EntityState.Detached;
                    _logger.LogInformation(
                        "Expiry scan: slot already claimed for user {UserId} household {HouseholdId}; skipping enqueue.",
                        plan.UserId, plan.HouseholdId);
                    continue;
                }

                // Slot is claimed. Enqueue the send — a rejected enqueue just drops the send (the
                // accepted trade-off); the ledger row already prevents a re-fire.
                _queue.TryEnqueue((sp, token) =>
                    sp.GetRequiredService<INotificationSender>().SendDigestAsync(userId, notification, token));
                claimed++;
            }

            _logger.LogInformation("Expiry scan: claimed {Count} digest slot(s).", claimed);
        }
    }
}

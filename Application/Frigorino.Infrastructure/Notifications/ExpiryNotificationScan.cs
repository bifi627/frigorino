using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Notifications
{
    // Invoked by the secured /internal/expiry-scan endpoint (and re-runnable idempotently).
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

            // 5. Plan + dispatch.
            var plans = ExpiryDigestPlanner.Plan(candidates, inventorySettings, recipients, alreadyDispatched, today);
            var enqueued = 0;
            foreach (var plan in plans)
            {
                var notification = DigestMessageComposer.Compose(plan);
                var userId = plan.UserId;

                var accepted = _queue.TryEnqueue((sp, token) =>
                    sp.GetRequiredService<INotificationSender>().SendDigestAsync(userId, notification, token));

                // Write the ledger row only on a successful enqueue (lossy-by-design ordering).
                if (accepted)
                {
                    _db.NotificationDispatches.Add(
                        NotificationDispatch.Create(plan.UserId, plan.HouseholdId, today));
                    enqueued++;
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Expiry scan: enqueued {Count} digest(s).", enqueued);
        }
    }
}

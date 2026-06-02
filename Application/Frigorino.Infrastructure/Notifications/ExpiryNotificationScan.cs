using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frigorino.Infrastructure.Notifications
{
    // Invoked by the secured /internal/expiry-scan endpoint. Claim-slot-first ordering: the
    // NotificationDispatch ledger row is inserted and committed BEFORE the notification is sent, so
    // concurrent/double fires are safe — only the run that wins the unique-index race on
    // (UserId, InventoryId, SentOn) sends. The send runs SYNCHRONOUSLY inside the request rather than
    // via the in-memory queue: the queue gives no durability guarantee (work lost on restart/crash),
    // and a once-daily cron+ledger loss is unrecoverable, so the outcome must be known in-request.
    // Trade-off: a slot may be claimed whose send then fails (rare, accepted for a daily digest —
    // better to occasionally miss than double-notify, since the ledger already committed).
    public class ExpiryNotificationScan
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationSender _sender;
        private readonly MaintenanceSettings _settings;
        private readonly ILogger<ExpiryNotificationScan> _logger;

        public ExpiryNotificationScan(
            ApplicationDbContext db,
            INotificationSender sender,
            IOptions<MaintenanceSettings> settings,
            ILogger<ExpiryNotificationScan> logger)
        {
            _db = db;
            _sender = sender;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task RunAsync(DateOnly today, CancellationToken ct)
        {
            // 1. Candidate items: active, with an expiry date, from active inventories.
            var candidates = await _db.InventoryItems
                .Where(i => i.IsActive && i.ExpiryDate != null && i.Inventory.IsActive)
                .Select(i => new ExpiryCandidate(
                    i.InventoryId, i.Inventory.HouseholdId, i.Inventory.Name, i.Text, i.ExpiryDate!.Value))
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                _logger.LogInformation("Expiry scan: no candidate items.");
                return;
            }

            var householdIds = candidates.Select(c => c.HouseholdId).Distinct().ToList();
            var inventoryIds = candidates.Select(c => c.InventoryId).Distinct().ToList();

            // 2. Per-user, per-inventory preferences (mute + lead override) for those inventories.
            var prefRows = await _db.UserInventoryNotificationSettings
                .Where(s => inventoryIds.Contains(s.InventoryId))
                .Select(s => new { s.UserId, s.InventoryId, s.Enabled, s.LeadDays })
                .ToListAsync(ct);
            var userInventoryPrefs = prefRows.ToDictionary(
                p => (p.UserId, p.InventoryId),
                p => new InventoryNotificationPref(p.Enabled, p.LeadDays));

            // 3. Recipients: active members of those households who are globally opted-in AND have >=1 token.
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

            // 4. Already-dispatched (user, inventory) keys for today.
            var dispatchedToday = await _db.NotificationDispatches
                .Where(d => d.SentOn == today)
                .Select(d => new { d.UserId, d.InventoryId })
                .ToListAsync(ct);
            var alreadyDispatched = dispatchedToday
                .Select(d => (d.UserId, d.InventoryId))
                .ToHashSet();

            // 5. Plan + dispatch. Claim each slot (insert + commit the ledger row) BEFORE sending,
            // so a concurrent scan that lost the unique-index race never sends a duplicate.
            var plans = ExpiryDigestPlanner.Plan(
                candidates, userInventoryPrefs, recipients, alreadyDispatched, today, _settings.OverdueGraceDays);
            var sent = 0;
            foreach (var plan in plans)
            {
                var notification = DigestMessageComposer.Compose(plan);

                // Claim the slot first. The unique index on (UserId, InventoryId, SentOn) lets only one
                // concurrent scan win; the loser hits DbUpdateException and skips sending.
                var dispatch = NotificationDispatch.Create(plan.UserId, plan.InventoryId, today);
                _db.NotificationDispatches.Add(dispatch);
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
                {
                    // A concurrent scan already claimed this (user, inventory, day): the unique index
                    // rejected our insert with SQLSTATE 23505. Detach so the failed row is not retried
                    // on the next iteration's save, then skip the send. Any other DbUpdateException
                    // (transient fault / deadlock / timeout) is NOT caught here — it propagates.
                    _db.Entry(dispatch).State = EntityState.Detached;
                    _logger.LogInformation(
                        "Expiry scan: slot already claimed for user {UserId} inventory {InventoryId}; skipping send.",
                        plan.UserId, plan.InventoryId);
                    continue;
                }

                // Slot is claimed. Send synchronously inside the request. A failed send is the accepted,
                // now-rare lossy-tolerant case — the ledger row already committed, so we log and move on.
                try
                {
                    await _sender.SendDigestAsync(plan.UserId, notification, ct);
                    sent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Expiry scan: notification send failed for user {UserId} inventory {InventoryId} after slot claimed.",
                        plan.UserId, plan.InventoryId);
                    continue;
                }
            }

            _logger.LogInformation("Expiry scan: sent {Count} notification(s).", sent);
        }
    }
}

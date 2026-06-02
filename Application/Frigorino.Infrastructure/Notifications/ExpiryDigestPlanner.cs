namespace Frigorino.Infrastructure.Notifications
{
    // Input rows (plain — no EF types, so this is pure + unit-testable).
    public sealed record ExpiryCandidate(
        int InventoryId, int HouseholdId, string InventoryName, string Text, DateOnly ExpiryDate);

    // Per (user, inventory) override. A MISSING entry ⇒ subscribed, inherit (LeadDays null).
    public sealed record InventoryNotificationPref(bool Enabled, int? LeadDays);

    // A member who is globally opted-in AND has at least one active token, scoped to one household.
    public sealed record DigestRecipient(string UserId, int HouseholdId, int UserLeadDays, string? Language);

    public sealed record DigestLine(string Text, DateOnly ExpiryDate, int DaysUntil);

    // One notification per (user, inventory).
    public sealed record DigestPlan(
        string UserId, int InventoryId, string InventoryName, string? Language, IReadOnlyList<DigestLine> Lines);

    public static class ExpiryDigestPlanner
    {
        public static IReadOnlyList<DigestPlan> Plan(
            IReadOnlyCollection<ExpiryCandidate> candidates,
            IReadOnlyDictionary<(string UserId, int InventoryId), InventoryNotificationPref> userInventoryPrefs,
            IReadOnlyCollection<DigestRecipient> recipients,
            HashSet<(string UserId, int InventoryId)> alreadyDispatched,
            DateOnly today,
            int overdueGraceDays)
        {
            var plans = new List<DigestPlan>();

            // Candidates grouped by inventory, so each (recipient, inventory) is considered once.
            var byInventory = candidates
                .GroupBy(c => c.InventoryId)
                .ToList();

            foreach (var recipient in recipients)
            {
                foreach (var inventoryGroup in byInventory)
                {
                    var first = inventoryGroup.First();
                    if (first.HouseholdId != recipient.HouseholdId)
                    {
                        continue;
                    }

                    var inventoryId = inventoryGroup.Key;

                    if (alreadyDispatched.Contains((recipient.UserId, inventoryId)))
                    {
                        continue;
                    }

                    var hasPref = userInventoryPrefs.TryGetValue((recipient.UserId, inventoryId), out var pref);
                    var subscribed = !hasPref || pref!.Enabled;
                    if (!subscribed)
                    {
                        continue;
                    }

                    var effectiveLeadDays = (hasPref ? pref!.LeadDays : null) ?? recipient.UserLeadDays;

                    var lines = new List<DigestLine>();
                    foreach (var candidate in inventoryGroup)
                    {
                        var daysUntil = candidate.ExpiryDate.DayNumber - today.DayNumber;
                        // Upper bound: within the lead window. Lower bound: not more than the grace
                        // days overdue, so a permanently-overdue item eventually drops off.
                        if (daysUntil <= effectiveLeadDays && daysUntil >= -overdueGraceDays)
                        {
                            lines.Add(new DigestLine(candidate.Text, candidate.ExpiryDate, daysUntil));
                        }
                    }

                    if (lines.Count == 0)
                    {
                        continue;
                    }

                    var ordered = lines
                        .OrderBy(l => l.ExpiryDate)
                        .ThenBy(l => l.Text)
                        .ToList();
                    plans.Add(new DigestPlan(
                        recipient.UserId, inventoryId, first.InventoryName, recipient.Language, ordered));
                }
            }

            return plans;
        }
    }
}

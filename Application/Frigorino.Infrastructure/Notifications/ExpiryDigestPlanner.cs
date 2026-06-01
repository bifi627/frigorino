namespace Frigorino.Infrastructure.Notifications
{
    // Input rows (plain — no EF types, so this is pure + unit-testable).
    public sealed record ExpiryCandidate(int InventoryId, int HouseholdId, string Text, DateOnly ExpiryDate);

    // Missing inventory in the map ⇒ enabled, inherit (LeadDays null).
    public sealed record InventoryNotificationSetting(bool Enabled, int? LeadDays);

    // A member who is opted-in AND has at least one active token, scoped to one household.
    public sealed record DigestRecipient(string UserId, int HouseholdId, int UserLeadDays, string? Language);

    public sealed record DigestLine(string Text, DateOnly ExpiryDate, int DaysUntil);

    public sealed record DigestPlan(string UserId, int HouseholdId, string? Language, IReadOnlyList<DigestLine> Lines);

    public static class ExpiryDigestPlanner
    {
        public static IReadOnlyList<DigestPlan> Plan(
            IReadOnlyCollection<ExpiryCandidate> candidates,
            IReadOnlyDictionary<int, InventoryNotificationSetting> inventorySettings,
            IReadOnlyCollection<DigestRecipient> recipients,
            HashSet<(string UserId, int HouseholdId)> alreadyDispatched,
            DateOnly today,
            int overdueGraceDays)
        {
            var plans = new List<DigestPlan>();

            foreach (var recipient in recipients)
            {
                if (alreadyDispatched.Contains((recipient.UserId, recipient.HouseholdId)))
                {
                    continue;
                }

                var lines = new List<DigestLine>();
                foreach (var candidate in candidates)
                {
                    if (candidate.HouseholdId != recipient.HouseholdId)
                    {
                        continue;
                    }

                    var hasSetting = inventorySettings.TryGetValue(candidate.InventoryId, out var setting);
                    var enabled = !hasSetting || setting!.Enabled;
                    if (!enabled)
                    {
                        continue;
                    }

                    var effectiveLeadDays = (hasSetting ? setting!.LeadDays : null) ?? recipient.UserLeadDays;
                    var daysUntil = candidate.ExpiryDate.DayNumber - today.DayNumber;
                    // Upper bound: within the lead window. Lower bound: not more than the grace days overdue,
                    // so a permanently-overdue item eventually drops off instead of recurring forever.
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
                plans.Add(new DigestPlan(recipient.UserId, recipient.HouseholdId, recipient.Language, ordered));
            }

            return plans;
        }
    }
}

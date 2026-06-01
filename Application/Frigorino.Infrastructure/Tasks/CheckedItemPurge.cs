namespace Frigorino.Infrastructure.Tasks
{
    // A checked list item considered for retention-based purge, tagged with the household it
    // belongs to (resolved via ListItem -> List -> HouseholdId).
    public sealed record CheckedItemCandidate(int ItemId, int HouseholdId, DateTime UpdatedAt);

    // Pure retention decision: which checked items have aged past their household's retention
    // window. Kept free of EF so it is unit-testable without a database (the InMemory provider
    // does not support ExecuteDelete).
    public static class CheckedItemPurge
    {
        public static List<int> SelectExpiredItemIds(
            IReadOnlyCollection<CheckedItemCandidate> candidates,
            IReadOnlyDictionary<int, int> retentionByHousehold,
            DateTime now,
            int defaultRetentionDays)
        {
            var expired = new List<int>();
            foreach (var candidate in candidates)
            {
                var days = retentionByHousehold.TryGetValue(candidate.HouseholdId, out var d)
                    ? d
                    : defaultRetentionDays;

                if (candidate.UpdatedAt < now.AddDays(-days))
                {
                    expired.Add(candidate.ItemId);
                }
            }

            return expired;
        }
    }
}

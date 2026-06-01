using Frigorino.Infrastructure.Tasks;

namespace Frigorino.Test.Infrastructure
{
    public class CheckedItemPurgeTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        private const int DefaultDays = 30;

        [Fact]
        public void UsesHouseholdRetention_WhenPresent()
        {
            var candidates = new[]
            {
                new CheckedItemCandidate(1, HouseholdId: 10, UpdatedAt: Now.AddDays(-8)),  // > 7-day retention => purge
                new CheckedItemCandidate(2, HouseholdId: 10, UpdatedAt: Now.AddDays(-3)),  // < 7-day retention => keep
            };
            var retention = new Dictionary<int, int> { [10] = 7 };

            var ids = CheckedItemPurge.SelectExpiredItemIds(candidates, retention, Now, DefaultDays);

            Assert.Equal(new[] { 1 }, ids);
        }

        [Fact]
        public void FallsBackToDefault_WhenHouseholdHasNoRow()
        {
            var candidates = new[]
            {
                new CheckedItemCandidate(1, HouseholdId: 99, UpdatedAt: Now.AddDays(-31)), // > default 30 => purge
                new CheckedItemCandidate(2, HouseholdId: 99, UpdatedAt: Now.AddDays(-10)), // < default 30 => keep
            };
            var retention = new Dictionary<int, int>();

            var ids = CheckedItemPurge.SelectExpiredItemIds(candidates, retention, Now, DefaultDays);

            Assert.Equal(new[] { 1 }, ids);
        }
    }
}

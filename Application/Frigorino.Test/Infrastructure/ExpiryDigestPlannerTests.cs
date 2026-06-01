using Frigorino.Infrastructure.Notifications;

namespace Frigorino.Test.Infrastructure
{
    public class ExpiryDigestPlannerTests
    {
        private static readonly DateOnly Today = new(2026, 6, 1);

        private static ExpiryCandidate Item(int inventoryId, int householdId, string text, int daysUntil) =>
            new(inventoryId, householdId, text, Today.AddDays(daysUntil));

        [Fact]
        public void IncludesItemsWithinUserDefaultLeadDays_AndExcludesBeyond()
        {
            var candidates = new[]
            {
                Item(1, 10, "Milk", 2),    // within 3 ⇒ include
                Item(1, 10, "Flour", 9),   // beyond 3 ⇒ exclude
            };
            var inventories = new Dictionary<int, InventoryNotificationSetting>(); // no rows ⇒ enabled, inherit
            var recipients = new[] { new DigestRecipient("u1", 10, UserLeadDays: 3, Language: "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates, inventories, recipients,
                alreadyDispatched: new HashSet<(string, int)>(), Today);

            var plan = Assert.Single(plans);
            Assert.Equal("u1", plan.UserId);
            var line = Assert.Single(plan.Lines);
            Assert.Equal("Milk", line.Text);
            Assert.Equal(2, line.DaysUntil);
        }

        [Fact]
        public void InventoryOverrideWidensWindow()
        {
            var candidates = new[] { Item(1, 10, "Frozen peas", 6) };
            var inventories = new Dictionary<int, InventoryNotificationSetting>
            {
                [1] = new InventoryNotificationSetting(Enabled: true, LeadDays: 7),
            };
            var recipients = new[] { new DigestRecipient("u1", 10, UserLeadDays: 3, Language: "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates, inventories, recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Single(Assert.Single(plans).Lines);
        }

        [Fact]
        public void DisabledInventoryIsExcluded()
        {
            var candidates = new[] { Item(1, 10, "Milk", 1) };
            var inventories = new Dictionary<int, InventoryNotificationSetting>
            {
                [1] = new InventoryNotificationSetting(Enabled: false, LeadDays: null),
            };
            var recipients = new[] { new DigestRecipient("u1", 10, UserLeadDays: 3, Language: "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates, inventories, recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Empty(plans);
        }

        [Fact]
        public void OverdueItemsAreIncluded()
        {
            var candidates = new[] { Item(1, 10, "Yogurt", -2) };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Equal(-2, Assert.Single(Assert.Single(plans).Lines).DaysUntil);
        }

        [Fact]
        public void AlreadyDispatchedRecipientIsSkipped()
        {
            var candidates = new[] { Item(1, 10, "Milk", 1) };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };
            var dispatched = new HashSet<(string, int)> { ("u1", 10) };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients, dispatched, Today);

            Assert.Empty(plans);
        }

        [Fact]
        public void RecipientWithNoMatchingItemsGetsNoPlan()
        {
            var candidates = new[] { Item(1, 10, "Flour", 30) };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Empty(plans);
        }

        [Fact]
        public void LinesAreSortedByExpiry()
        {
            var candidates = new[]
            {
                Item(1, 10, "Later", 3),
                Item(1, 10, "Sooner", 1),
            };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients,
                new HashSet<(string, int)>(), Today);

            var lines = Assert.Single(plans).Lines;
            Assert.Equal("Sooner", lines[0].Text);
            Assert.Equal("Later", lines[1].Text);
        }

        [Fact]
        public void LinesWithSameExpiry_AreSortedByText()
        {
            var candidates = new[]
            {
                Item(1, 10, "Zucchini", 2),
                Item(1, 10, "Apples", 2),
            };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients,
                new HashSet<(string, int)>(), Today);

            var lines = Assert.Single(plans).Lines;
            Assert.Equal("Apples", lines[0].Text);
            Assert.Equal("Zucchini", lines[1].Text);
        }

        [Fact]
        public void InventoryOverrideNarrowsWindow()
        {
            var candidates = new[] { Item(1, 10, "Milk", 2) }; // within user default 3...
            var inventories = new Dictionary<int, InventoryNotificationSetting>
            {
                [1] = new InventoryNotificationSetting(Enabled: true, LeadDays: 1), // ...but inventory narrows to 1
            };
            var recipients = new[] { new DigestRecipient("u1", 10, UserLeadDays: 3, Language: "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates, inventories, recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Empty(plans);
        }

        [Fact]
        public void CandidatesAreScopedToRecipientHousehold()
        {
            var candidates = new[]
            {
                Item(1, 10, "House10 Milk", 1),
                Item(2, 20, "House20 Eggs", 1),
            };
            var recipients = new[]
            {
                new DigestRecipient("u1", 10, 3, "en"),
                new DigestRecipient("u2", 20, 3, "en"),
            };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients,
                new HashSet<(string, int)>(), Today);

            var plan10 = Assert.Single(plans, p => p.UserId == "u1");
            Assert.Equal("House10 Milk", Assert.Single(plan10.Lines).Text);

            var plan20 = Assert.Single(plans, p => p.UserId == "u2");
            Assert.Equal("House20 Eggs", Assert.Single(plan20.Lines).Text);
        }
    }
}

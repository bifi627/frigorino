using Frigorino.Infrastructure.Notifications;

namespace Frigorino.Test.Infrastructure
{
    public class ExpiryDigestPlannerTests
    {
        private static readonly DateOnly Today = new(2026, 6, 1);

        private static ExpiryCandidate Item(int inventoryId, int householdId, string inventoryName, string text, int daysUntil) =>
            new(inventoryId, householdId, inventoryName, text, Today.AddDays(daysUntil));

        [Fact]
        public void TwoInventories_OneRecipient_TwoPlans()
        {
            var candidates = new[]
            {
                Item(1, 10, "Fridge", "Milk", 0),
                Item(2, 10, "Pantry", "Eggs", 1),
            };
            var recipients = new[] { new DigestRecipient("user-1", 10, UserLeadDays: 7, Language: "en") };
            var prefs = new Dictionary<(string, int), InventoryNotificationPref>();

            var plans = ExpiryDigestPlanner.Plan(
                candidates,
                prefs,
                recipients,
                alreadyDispatched: new HashSet<(string, int)>(),
                Today,
                overdueGraceDays: 1);

            Assert.Equal(2, plans.Count);
            var plan1 = Assert.Single(plans, p => p.InventoryId == 1);
            Assert.Equal("Fridge", plan1.InventoryName);
            Assert.Equal("Milk", Assert.Single(plan1.Lines).Text);
            var plan2 = Assert.Single(plans, p => p.InventoryId == 2);
            Assert.Equal("Pantry", plan2.InventoryName);
            Assert.Equal("Eggs", Assert.Single(plan2.Lines).Text);
        }

        [Fact]
        public void MutedInventory_IsExcluded()
        {
            var candidates = new[]
            {
                Item(1, 10, "Fridge", "Milk", 0),
                Item(2, 10, "Pantry", "Eggs", 1),
            };
            var recipients = new[] { new DigestRecipient("user-1", 10, UserLeadDays: 7, Language: "en") };
            var prefs = new Dictionary<(string, int), InventoryNotificationPref>
            {
                [("user-1", 1)] = new InventoryNotificationPref(Enabled: false, LeadDays: null),
            };

            var plans = ExpiryDigestPlanner.Plan(
                candidates,
                prefs,
                recipients,
                alreadyDispatched: new HashSet<(string, int)>(),
                Today,
                overdueGraceDays: 1);

            Assert.Single(plans);
            Assert.Equal(2, plans[0].InventoryId);
        }

        [Fact]
        public void PerInventoryLeadOverride_WidensWindow()
        {
            // Global lead 3, pref override 14; item expires in 10 days → within 14, should appear.
            var candidates = new[] { Item(1, 10, "Fridge", "Yogurt", 10) };
            var recipients = new[] { new DigestRecipient("user-1", 10, UserLeadDays: 3, Language: "en") };
            var prefs = new Dictionary<(string, int), InventoryNotificationPref>
            {
                [("user-1", 1)] = new InventoryNotificationPref(Enabled: true, LeadDays: 14),
            };

            var plans = ExpiryDigestPlanner.Plan(
                candidates,
                prefs,
                recipients,
                alreadyDispatched: new HashSet<(string, int)>(),
                Today,
                overdueGraceDays: 1);

            var plan = Assert.Single(plans);
            Assert.Equal("Yogurt", Assert.Single(plan.Lines).Text);
        }

        [Fact]
        public void AlreadyDispatched_UserInventory_Skipped()
        {
            var candidates = new[] { Item(1, 10, "Fridge", "Milk", 1) };
            var recipients = new[] { new DigestRecipient("user-1", 10, UserLeadDays: 7, Language: "en") };
            var prefs = new Dictionary<(string, int), InventoryNotificationPref>();
            var dispatched = new HashSet<(string, int)> { ("user-1", 1) };

            var plans = ExpiryDigestPlanner.Plan(
                candidates,
                prefs,
                recipients,
                alreadyDispatched: dispatched,
                Today,
                overdueGraceDays: 1);

            Assert.Empty(plans);
        }

        [Fact]
        public void OverdueGrace_Lower_Boundary()
        {
            // grace 1: item expired 2 days ago (daysUntil -2) → below floor (-2 < -1) → excluded.
            var candidates_excluded = new[] { Item(1, 10, "Fridge", "Yogurt", -2) };
            var recipients = new[] { new DigestRecipient("user-1", 10, UserLeadDays: 3, Language: "en") };
            var prefs = new Dictionary<(string, int), InventoryNotificationPref>();

            var plans_excluded = ExpiryDigestPlanner.Plan(
                candidates_excluded, prefs, recipients, new HashSet<(string, int)>(), Today, overdueGraceDays: 1);

            Assert.Empty(plans_excluded);

            // grace 3: same item (daysUntil -2) → within floor (-2 >= -3) → included.
            var candidates_included = new[] { Item(1, 10, "Fridge", "Yogurt", -2) };
            var plans_included = ExpiryDigestPlanner.Plan(
                candidates_included, prefs, recipients, new HashSet<(string, int)>(), Today, overdueGraceDays: 3);

            Assert.Single(plans_included);
        }

        [Fact]
        public void Lines_OrderedByExpiryDate_ThenText()
        {
            var candidates = new[]
            {
                Item(1, 10, "Fridge", "Zucchini", 2),
                Item(1, 10, "Fridge", "Apples", 2),
                Item(1, 10, "Fridge", "Milk", 1),
            };
            var recipients = new[] { new DigestRecipient("user-1", 10, UserLeadDays: 7, Language: "en") };
            var prefs = new Dictionary<(string, int), InventoryNotificationPref>();

            var plans = ExpiryDigestPlanner.Plan(
                candidates,
                prefs,
                recipients,
                alreadyDispatched: new HashSet<(string, int)>(),
                Today,
                overdueGraceDays: 1);

            var lines = Assert.Single(plans).Lines;
            Assert.Equal(3, lines.Count);
            Assert.Equal("Milk", lines[0].Text);     // day 1 → first
            Assert.Equal("Apples", lines[1].Text);   // day 2, A before Z
            Assert.Equal("Zucchini", lines[2].Text); // day 2, Z after A
        }

        [Fact]
        public void CrossHousehold_TenantBoundary_NoPlansProduced()
        {
            // Recipient is in household 10; the candidate inventory belongs to household 20.
            // The planner must not cross the household boundary — no plan should be produced.
            var candidates = new[] { Item(inventoryId: 5, householdId: 20, inventoryName: "Pantry", text: "Milk", daysUntil: 1) };
            var recipients = new[] { new DigestRecipient("user-1", HouseholdId: 10, UserLeadDays: 7, Language: "en") };
            var prefs = new Dictionary<(string, int), InventoryNotificationPref>();

            var plans = ExpiryDigestPlanner.Plan(
                candidates,
                prefs,
                recipients,
                alreadyDispatched: new HashSet<(string, int)>(),
                Today,
                overdueGraceDays: 1);

            Assert.Empty(plans);
        }
    }
}

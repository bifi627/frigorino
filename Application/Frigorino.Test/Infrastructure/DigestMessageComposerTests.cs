using Frigorino.Infrastructure.Notifications;

namespace Frigorino.Test.Infrastructure
{
    public class DigestMessageComposerTests
    {
        private static readonly DateOnly Today = new(2026, 6, 1);

        private const int HouseholdId = 7;

        private static DigestPlan PlanWith(int inventoryId, string inventoryName, string? language, params (string text, int days)[] items)
        {
            var lines = items.Select(i => new DigestLine(i.text, Today.AddDays(i.days), i.days)).ToList();
            return new DigestPlan("u1", inventoryId, HouseholdId, inventoryName, language, lines);
        }

        [Fact]
        public void English_TitleIncludesInventoryName_AndCount_AndDeepLink()
        {
            var plan = PlanWith(42, "Fridge", "en", ("Milk", 1), ("Yogurt", 2));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Equal("Fridge: 2 items expiring soon", msg.Title);
            Assert.Equal("/inventories/42/view?householdId=7", msg.DeepLinkPath);
            Assert.Contains("Milk", msg.Body);
            Assert.Contains("Yogurt", msg.Body);
        }

        [Fact]
        public void German_TitleIncludesInventoryName_GermanCopy()
        {
            var plan = PlanWith(42, "Fridge", "de", ("Milch", 1), ("Joghurt", 2));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Equal("Fridge: 2 Artikel laufen bald ab", msg.Title);
            Assert.Equal("/inventories/42/view?householdId=7", msg.DeepLinkPath);
        }

        [Fact]
        public void NullLanguage_FallsBackToEnglish()
        {
            var plan = PlanWith(5, "Pantry", null, ("Milk", 1));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Contains("tomorrow", msg.Body); // 1 day ⇒ "tomorrow"
        }

        [Fact]
        public void OverdueItem_IsPhrasedAsOverdue()
        {
            var plan = PlanWith(5, "Pantry", "en", ("Yogurt", -1));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Contains("overdue", msg.Body);
        }

        [Fact]
        public void ManyItems_AreTruncatedWithMore()
        {
            var plan = PlanWith(5, "Pantry", "en",
                ("A", 1), ("B", 1), ("C", 1), ("D", 1), ("E", 1));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Contains("more", msg.Body); // "+N more"
        }
    }
}

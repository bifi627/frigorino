using Frigorino.Infrastructure.Notifications;

namespace Frigorino.Test.Infrastructure
{
    public class DigestMessageComposerTests
    {
        private static readonly DateOnly Today = new(2026, 6, 1);

        private static DigestPlan PlanWith(string? language, params (string text, int days)[] items)
        {
            var lines = items.Select(i => new DigestLine(i.text, Today.AddDays(i.days), i.days)).ToList();
            return new DigestPlan("u1", 10, language, lines);
        }

        [Fact]
        public void English_TitleCountsItems_BodyListsNames()
        {
            var plan = PlanWith("en", ("Milk", 1), ("Yogurt", 2));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Contains("2", msg.Title);
            Assert.Contains("Milk", msg.Body);
            Assert.Contains("Yogurt", msg.Body);
            Assert.Equal("/inventories", msg.DeepLinkPath);
        }

        [Fact]
        public void German_UsesGermanCopy()
        {
            var plan = PlanWith("de", ("Milch", 0));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Contains("Artikel", msg.Title); // German title template
            Assert.Contains("heute", msg.Body);    // 0 days ⇒ "heute"
        }

        [Fact]
        public void NullLanguage_FallsBackToEnglish()
        {
            var plan = PlanWith(null, ("Milk", 1));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Contains("tomorrow", msg.Body); // 1 day ⇒ "tomorrow"
        }

        [Fact]
        public void OverdueItem_IsPhrasedAsOverdue()
        {
            var plan = PlanWith("en", ("Yogurt", -1));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Contains("overdue", msg.Body);
        }

        [Fact]
        public void ManyItems_AreTruncatedWithMore()
        {
            var plan = PlanWith("en",
                ("A", 1), ("B", 1), ("C", 1), ("D", 1), ("E", 1));

            var msg = DigestMessageComposer.Compose(plan);

            Assert.Contains("more", msg.Body); // "+N more"
        }
    }
}

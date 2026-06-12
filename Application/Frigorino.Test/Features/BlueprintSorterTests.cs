using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Features.Lists.Blueprints;

namespace Frigorino.Test.Features
{
    public class BlueprintSorterTests
    {
        // Items carry Id + Rank only (the sorter reads those two). Rank order here is a < b < c < d.
        private static ListItem Item(int id, string rank) => new() { Id = id, Rank = rank };

        [Fact]
        public void Order_SortsByBlueprintRank_ThenStableByExistingRank()
        {
            var items = new[]
            {
                Item(1, "a0"), // Pantry
                Item(2, "a1"), // Produce
                Item(3, "a2"), // Produce
                Item(4, "a3"), // Bakery
            };
            var categoryByItemId = new Dictionary<int, ProductCategory>
            {
                [1] = ProductCategory.Pantry,
                [2] = ProductCategory.Produce,
                [3] = ProductCategory.Produce,
                [4] = ProductCategory.Bakery,
            };
            var blueprint = new[] { ProductCategory.Produce, ProductCategory.Bakery, ProductCategory.Pantry };

            var ordered = BlueprintSorter.OrderUncheckedItemIds(items, categoryByItemId, blueprint);

            // Produce (2 then 3, stable by rank), then Bakery (4), then Pantry (1).
            Assert.Equal(new[] { 2, 3, 4, 1 }, ordered);
        }

        [Fact]
        public void Order_UncategorizedSinkToBottom_StableAmongThemselves()
        {
            var items = new[]
            {
                Item(1, "a0"), // Snacks — not in blueprint → bottom
                Item(2, "a1"), // Produce
                Item(3, "a2"), // Unknown (unclassified) → bottom
            };
            var categoryByItemId = new Dictionary<int, ProductCategory>
            {
                [1] = ProductCategory.Snacks,
                [2] = ProductCategory.Produce,
                // item 3 deliberately absent → treated as Unknown
            };
            var blueprint = new[] { ProductCategory.Produce };

            var ordered = BlueprintSorter.OrderUncheckedItemIds(items, categoryByItemId, blueprint);

            // Produce first; then the two uncategorized in their original rank order (1 then 3).
            Assert.Equal(new[] { 2, 1, 3 }, ordered);
        }

        [Fact]
        public void Order_SentinelCategories_SinkToBottom()
        {
            var items = new[] { Item(1, "a0"), Item(2, "a1") };
            var categoryByItemId = new Dictionary<int, ProductCategory>
            {
                [1] = ProductCategory.Other,
                [2] = ProductCategory.Produce,
            };
            var blueprint = new[] { ProductCategory.Produce };

            var ordered = BlueprintSorter.OrderUncheckedItemIds(items, categoryByItemId, blueprint);

            Assert.Equal(new[] { 2, 1 }, ordered);
        }
    }
}

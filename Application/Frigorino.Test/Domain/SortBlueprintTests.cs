using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class SortBlueprintTests
    {
        private const int HouseholdId = 42;

        private static readonly ProductCategory[] ValidOrder =
        {
            ProductCategory.Produce, ProductCategory.DairyAndEggs, ProductCategory.Pantry,
        };

        [Fact]
        public void Create_Valid_BuildsOrderedCategories()
        {
            var result = SortBlueprint.Create(HouseholdId, " My Store ", ValidOrder);

            Assert.True(result.IsSuccess);
            var blueprint = result.Value;
            Assert.Equal(HouseholdId, blueprint.HouseholdId);
            Assert.Equal("My Store", blueprint.Name);
            Assert.True(blueprint.IsActive);
            Assert.Equal(
                new[] { ProductCategory.Produce, ProductCategory.DairyAndEggs, ProductCategory.Pantry },
                blueprint.OrderedCategories());
            Assert.Equal(new[] { 0, 1, 2 }, blueprint.Categories.OrderBy(c => c.OrderIndex).Select(c => c.OrderIndex));
        }

        [Fact]
        public void Create_BlankName_Fails()
        {
            var result = SortBlueprint.Create(HouseholdId, "   ", ValidOrder);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_EmptyCategories_Fails()
        {
            var result = SortBlueprint.Create(HouseholdId, "Store", Array.Empty<ProductCategory>());

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_DuplicateCategory_Fails()
        {
            var dupes = new[] { ProductCategory.Produce, ProductCategory.Produce };

            var result = SortBlueprint.Create(HouseholdId, "Store", dupes);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_SentinelCategory_Fails()
        {
            var withSentinel = new[] { ProductCategory.Produce, ProductCategory.Other };

            var result = SortBlueprint.Create(HouseholdId, "Store", withSentinel);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Update_Valid_ReplacesNameAndCategories()
        {
            var blueprint = SortBlueprint.Create(HouseholdId, "Store", ValidOrder).Value;

            var result = blueprint.Update("Renamed", new[] { ProductCategory.Bakery, ProductCategory.Produce });

            Assert.True(result.IsSuccess);
            Assert.Equal("Renamed", blueprint.Name);
            Assert.Equal(new[] { ProductCategory.Bakery, ProductCategory.Produce }, blueprint.OrderedCategories());
        }

        [Fact]
        public void SoftDelete_Deactivates()
        {
            var blueprint = SortBlueprint.Create(HouseholdId, "Store", ValidOrder).Value;

            Assert.True(blueprint.SoftDelete().IsSuccess);
            Assert.False(blueprint.IsActive);
        }

        [Fact]
        public void Restore_Reactivates()
        {
            var blueprint = SortBlueprint.Create(HouseholdId, "Store", ValidOrder).Value;
            blueprint.SoftDelete();

            Assert.True(blueprint.Restore().IsSuccess);
            Assert.True(blueprint.IsActive);
        }

        [Fact]
        public void CreateDefault_CoversAll23AislesInOrder_NoSentinels()
        {
            var blueprint = SortBlueprint.CreateDefault(HouseholdId);

            var categories = blueprint.OrderedCategories();
            Assert.Equal(23, categories.Count);
            Assert.Equal(categories.Count, categories.Distinct().Count());
            Assert.DoesNotContain(ProductCategory.Unknown, categories);
            Assert.DoesNotContain(ProductCategory.Other, categories);
            Assert.Equal(ProductCategory.Produce, categories[0]);
        }
    }
}

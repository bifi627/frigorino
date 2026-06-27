using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class ProductAggregateTests
    {
        private const int HouseholdId = 42;

        private static ProductClassification AiClassification(int days) =>
            new(ProductCategory.DairyAndEggs, ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value);

        [Fact]
        public void Create_Valid_SetsColumnsAndVersion()
        {
            var result = Product.Create(HouseholdId, "milk", AiClassification(7), classifierVersion: 1);

            Assert.True(result.IsSuccess);
            var product = result.Value;
            Assert.Equal(HouseholdId, product.HouseholdId);
            Assert.Equal("milk", product.NormalizedName);
            Assert.Equal(ProductCategory.DairyAndEggs, product.ClassificationProductCategory);
            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product.ClassificationExpiryHandling);
            Assert.Equal(7, product.ClassificationShelfLifeDays);
            Assert.Equal(1, product.ClassifierVersion);
        }

        [Fact]
        public void Create_EmptyNormalizedName_Fails()
        {
            var result = Product.Create(HouseholdId, "  ", AiClassification(7), 1);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_InvalidHousehold_Fails()
        {
            var result = Product.Create(0, "milk", AiClassification(7), 1);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void ApplyClassification_OverwritesLayerAndVersion()
        {
            var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;

            product.ApplyClassification(
                new ProductClassification(ProductCategory.HouseholdAndCleaning, ExpiryProfile.NonPerishable),
                classifierVersion: 2);

            Assert.Equal(ProductCategory.HouseholdAndCleaning, product.ClassificationProductCategory);
            Assert.Equal(ExpiryHandling.NonPerishable, product.ClassificationExpiryHandling);
            Assert.Null(product.ClassificationShelfLifeDays);
            Assert.Equal(2, product.ClassifierVersion);
        }

        [Fact]
        public void EffectiveExpiry_ReconstructsProfileFromColumns()
        {
            var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;

            var effective = product.EffectiveExpiry;

            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, effective.Handling);
            Assert.Equal(7, effective.ShelfLifeDays);
        }

        [Fact]
        public void OverrideClassification_SetsOverrideLayer_AndFlipsEffective()
        {
            var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;

            product.OverrideClassification(
                new ProductClassification(ProductCategory.Pantry,
                    ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 14).Value));

            Assert.True(product.IsOverridden);
            Assert.Equal(ProductCategory.Pantry, product.EffectiveCategory);
            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product.EffectiveExpiry.Handling);
            Assert.Equal(14, product.EffectiveExpiry.ShelfLifeDays);
            // AI layer is preserved underneath.
            Assert.Equal(ProductCategory.DairyAndEggs, product.ClassificationProductCategory);
            Assert.Equal(7, product.ClassificationShelfLifeDays);
        }

        [Fact]
        public void OverrideToNonPerishable_NullsEffectiveShelfLife()
        {
            var product = Product.Create(HouseholdId, "salt", AiClassification(7), 1).Value;

            product.OverrideClassification(
                new ProductClassification(ProductCategory.Pantry, ExpiryProfile.NonPerishable));

            Assert.Equal(ExpiryHandling.NonPerishable, product.EffectiveExpiry.Handling);
            Assert.Null(product.EffectiveExpiry.ShelfLifeDays);
        }

        [Fact]
        public void ResetToAiClassification_RestoresAiVerdict()
        {
            var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;
            product.OverrideClassification(
                new ProductClassification(ProductCategory.Pantry, ExpiryProfile.NonPerishable));

            product.ResetToAiClassification();

            Assert.False(product.IsOverridden);
            Assert.Equal(ProductCategory.DairyAndEggs, product.EffectiveCategory);
            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product.EffectiveExpiry.Handling);
            Assert.Equal(7, product.EffectiveExpiry.ShelfLifeDays);
        }

        [Fact]
        public void IsOverridden_IsFalseUntilOverridden()
        {
            var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;
            Assert.False(product.IsOverridden);
        }
    }
}

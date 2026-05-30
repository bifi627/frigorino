using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class ProductAggregateTests
    {
        private const int HouseholdId = 42;

        private static ProductClassification AiClassification(int days) =>
            new(ProductCategory.Food, ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value);

        [Fact]
        public void Create_Valid_SetsColumnsAndVersion()
        {
            var result = Product.Create(HouseholdId, "milk", AiClassification(7), classifierVersion: 1);

            Assert.True(result.IsSuccess);
            var product = result.Value;
            Assert.Equal(HouseholdId, product.HouseholdId);
            Assert.Equal("milk", product.NormalizedName);
            Assert.Equal(ProductCategory.Food, product.ClassificationProductCategory);
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
                new ProductClassification(ProductCategory.HouseholdSupply, ExpiryProfile.NonPerishable),
                classifierVersion: 2);

            Assert.Equal(ProductCategory.HouseholdSupply, product.ClassificationProductCategory);
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
    }
}

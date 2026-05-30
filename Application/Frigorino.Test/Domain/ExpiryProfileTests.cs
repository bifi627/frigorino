using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class ExpiryProfileTests
    {
        [Fact]
        public void Create_NonPerishable_WithNullShelfLife_Succeeds()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.NonPerishable, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(ExpiryHandling.NonPerishable, result.Value.Handling);
            Assert.Null(result.Value.ShelfLifeDays);
        }

        [Fact]
        public void Create_NonPerishable_WithShelfLife_Fails()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.NonPerishable, 10);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_UserEntersFromPackage_WithShelfLife_Fails()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.UserEntersFromPackage, 10);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_UserEntersFromPackage_WithNullShelfLife_Succeeds()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.UserEntersFromPackage, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(ExpiryHandling.UserEntersFromPackage, result.Value.Handling);
            Assert.Null(result.Value.ShelfLifeDays);
        }

        [Fact]
        public void Create_Failure_CarriesShelfLifeDaysPropertyMetadata()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 366);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors,
                e => e.Metadata.TryGetValue("Property", out var p)
                    && (string)p == nameof(ExpiryProfile.ShelfLifeDays));
        }

        [Fact]
        public void Create_AiRecommends_WithNullShelfLife_Fails()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, null);

            Assert.True(result.IsFailed);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(366)]
        public void Create_AiRecommends_OutOfRange_Fails(int days)
        {
            var result = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days);

            Assert.True(result.IsFailed);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(365)]
        public void Create_AiRecommends_InRange_Succeeds(int days)
        {
            var result = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days);

            Assert.True(result.IsSuccess);
            Assert.Equal(days, result.Value.ShelfLifeDays);
        }

        [Fact]
        public void SuggestedExpiry_AiRecommends_ReturnsTodayPlusDays()
        {
            var profile = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 7).Value;

            var suggested = profile.SuggestedExpiry(new DateOnly(2026, 1, 1));

            Assert.Equal(new DateOnly(2026, 1, 8), suggested);
        }

        [Fact]
        public void SuggestedExpiry_NonPerishable_ReturnsNull()
        {
            Assert.Null(ExpiryProfile.NonPerishable.SuggestedExpiry(new DateOnly(2026, 1, 1)));
        }
    }
}

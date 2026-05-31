using Frigorino.Domain.Quantities;

namespace Frigorino.Test.Domain
{
    public class QuantityTests
    {
        // xUnit InlineData cannot carry decimal literals — pass double and cast.
        [Theory]
        [InlineData(1.0)]
        [InlineData(0.5)]
        [InlineData(500.0)]
        public void Create_PositiveValue_Succeeds(double value)
        {
            var result = Quantity.Create((decimal)value, QuantityUnit.Liter);

            Assert.True(result.IsSuccess);
            Assert.Equal((decimal)value, result.Value.Value);
            Assert.Equal(QuantityUnit.Liter, result.Value.Unit);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Create_NonPositiveValue_FailsKeyedOnValue(double value)
        {
            var result = Quantity.Create((decimal)value, QuantityUnit.Piece);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(Quantity.Value), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void Create_ExceedingPersistedMax_FailsKeyedOnValue()
        {
            var result = Quantity.Create(Quantity.MaxValue + 1m, QuantityUnit.Gram);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(Quantity.Value), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void Create_RoundsToPersistedScale()
        {
            // numeric(12,3): more than 3 decimals are rounded so the VO matches stored precision.
            var result = Quantity.Create(1.5005m, QuantityUnit.Kilogram);

            Assert.True(result.IsSuccess);
            Assert.Equal(1.501m, result.Value.Value);
        }

        [Fact]
        public void Create_ValueRoundingToZero_FailsAsNonPositive()
        {
            var result = Quantity.Create(0.0004m, QuantityUnit.Kilogram);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(Quantity.Value), result.Errors[0].Metadata["Property"]);
        }
    }
}

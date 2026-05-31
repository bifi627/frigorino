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
    }
}

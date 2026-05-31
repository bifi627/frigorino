using Frigorino.Domain.Quantities;

namespace Frigorino.Test.Domain
{
    public class QuantityTryParseTests
    {
        // xUnit InlineData cannot carry decimal literals — compare via double cast.
        [Theory]
        [InlineData("2kg flour", "flour", 2.0, QuantityUnit.Kilogram)]
        [InlineData("500 ml milk", "milk", 500.0, QuantityUnit.Milliliter)]
        [InlineData("1,5 l juice", "juice", 1.5, QuantityUnit.Liter)]
        [InlineData("2 l milk", "milk", 2.0, QuantityUnit.Liter)]
        [InlineData("500g Mehl", "Mehl", 500.0, QuantityUnit.Gram)]
        [InlineData("1.5 kg flour", "flour", 1.5, QuantityUnit.Kilogram)]
        [InlineData("flour 2kg", "flour", 2.0, QuantityUnit.Kilogram)]
        [InlineData("milk 500ml", "milk", 500.0, QuantityUnit.Milliliter)]
        [InlineData("3 milk", "milk", 3.0, QuantityUnit.Piece)]
        [InlineData("2 lemons", "lemons", 2.0, QuantityUnit.Piece)]
        public void TryParse_ConfidentShapes_Resolves(
            string input, string expectedName, double expectedValue, QuantityUnit expectedUnit)
        {
            var ok = Quantity.TryParse(input, out var cleanName, out var quantity);

            Assert.True(ok);
            Assert.Equal(expectedName, cleanName);
            Assert.Equal((decimal)expectedValue, quantity.Value);
            Assert.Equal(expectedUnit, quantity.Unit);
        }

        [Theory]
        [InlineData("7up")]            // brand-digit glued to letters
        [InlineData("WD-40")]          // digit glued via hyphen
        [InlineData("E45 cream")]      // leading letter + glued digit
        [InlineData("Coca Cola 2")]    // trailing bare integer
        [InlineData("milk 2")]         // trailing bare integer
        [InlineData("1,5 milk")]       // bare decimal count (ambiguous)
        [InlineData("2kg")]            // number + unit, no product
        [InlineData("2 kg")]           // number + bare unit token, no product
        [InlineData("500 ml")]         // spaced leading unit, no product name
        [InlineData("2 l")]            // spaced leading unit, no product name
        [InlineData("milk")]           // no digit at all
        [InlineData("")]               // empty
        public void TryParse_AmbiguousOrEmpty_ReturnsFalse(string input)
        {
            var ok = Quantity.TryParse(input, out _, out _);

            Assert.False(ok);
        }
    }
}

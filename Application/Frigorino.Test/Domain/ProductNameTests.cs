using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class ProductNameTests
    {
        [Theory]
        [InlineData("Milk", "milk")]
        [InlineData("  Milk  ", "milk")]
        [InlineData("Whole   Milk", "whole milk")]
        [InlineData("WHOLE\tMILK", "whole milk")]
        [InlineData("Vollmilch", "vollmilch")]
        public void Normalize_LowercasesTrimsAndCollapsesWhitespace(string raw, string expected)
        {
            Assert.Equal(expected, ProductName.Normalize(raw));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Normalize_EmptyOrWhitespace_ReturnsEmpty(string? raw)
        {
            Assert.Equal(string.Empty, ProductName.Normalize(raw!));
        }
    }
}

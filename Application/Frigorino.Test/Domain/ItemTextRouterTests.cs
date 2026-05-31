using Frigorino.Domain.Quantities;

namespace Frigorino.Test.Domain
{
    public class ItemTextRouterTests
    {
        [Theory]
        [InlineData("https://example.com/recipe")]
        [InlineData("check www.shop.com later")]
        [InlineData("   ")]                         // empty after trim
        [InlineData("!!! ??? ...")]                 // punctuation-only
        public void Analyze_Junk_SkipsAi(string input)
        {
            var result = ItemTextRouter.Analyze(input);

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
            Assert.Null(result.Quantity);
        }

        [Fact]
        public void Analyze_TooLong_SkipsAi()
        {
            var longText = new string('x', 121);

            var result = ItemTextRouter.Analyze(longText);

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
        }

        [Fact]
        public void Analyze_TooManyWords_SkipsAi()
        {
            var manyWords = string.Join(' ', Enumerable.Repeat("buy", 16));

            var result = ItemTextRouter.Analyze(manyWords);

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
        }

        [Fact]
        public void Analyze_UrlWithDigits_SkipsBeforeDigitGate()
        {
            var result = ItemTextRouter.Analyze("https://shop.com/item/123");

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
        }

        [Fact]
        public void Analyze_ConfidentQuantity_Resolves()
        {
            var result = ItemTextRouter.Analyze("2kg flour");

            Assert.Equal(ItemTextRoute.Resolved, result.Route);
            Assert.Equal("flour", result.CleanName);
            Assert.NotNull(result.Quantity);
            Assert.Equal(2m, result.Quantity!.Value.Value);
            Assert.Equal(QuantityUnit.Kilogram, result.Quantity!.Value.Unit);
        }

        [Fact]
        public void Analyze_DigitNoConfidentParse_NeedsExtraction()
        {
            var result = ItemTextRouter.Analyze("7up");

            Assert.Equal(ItemTextRoute.NeedsExtraction, result.Route);
            Assert.Equal("7up", result.CleanName);
            Assert.Null(result.Quantity);
        }

        [Fact]
        public void Analyze_NoDigit_ClassifyOnly()
        {
            var result = ItemTextRouter.Analyze("milk");

            Assert.Equal(ItemTextRoute.ClassifyOnly, result.Route);
            Assert.Equal("milk", result.CleanName);
            Assert.Null(result.Quantity);
        }

        [Fact]
        public void Analyze_NonResolvedRoute_KeepsRawTextAsCleanName()
        {
            // The trigger keys off CleanName for every route; non-Resolved must echo raw text.
            // "milk 2" is a trailing bare integer — ambiguous to Quantity.TryParse (no confident
            // parse) but contains a digit, so it routes to NeedsExtraction with raw text intact.
            var result = ItemTextRouter.Analyze("milk 2");

            Assert.Equal(ItemTextRoute.NeedsExtraction, result.Route);
            Assert.Equal("milk 2", result.CleanName);
        }
    }
}

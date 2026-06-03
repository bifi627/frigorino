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
        public void Analyze_UrlWithDigits_SkipsBeforeProcessing()
        {
            var result = ItemTextRouter.Analyze("https://shop.com/item/123");

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
        }

        [Theory]
        [InlineData("2kg flour")]              // digit quantity
        [InlineData("two cups of coffee")]     // spelled-out quantity (EN) — no digit, still extracted
        [InlineData("zwei Liter Cola")]        // spelled-out quantity (DE)
        [InlineData("7up")]                    // brand-digit — LLM decides it is not a quantity
        [InlineData("milk")]                   // no quantity at all — LLM returns it unchanged
        public void Analyze_AnyProductText_NeedsExtraction(string input)
        {
            // Anything that survives the junk guard is handed to the LLM verbatim: there is no
            // reliable cheap way to tell quantity-bearing text from plain text, so the router does
            // not try. The raw text is preserved as CleanName.
            var result = ItemTextRouter.Analyze(input);

            Assert.Equal(ItemTextRoute.NeedsExtraction, result.Route);
            Assert.Equal(input, result.CleanName);
        }
    }
}

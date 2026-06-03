using System.Linq;
using System.Text.RegularExpressions;

namespace Frigorino.Domain.Quantities
{
    // What to do with a new/edited list-item's text. Both routes carry the raw (trimmed) text as
    // CleanName — the router no longer attempts to parse a quantity itself; that is left entirely
    // to the LLM (which alone reliably handles spelled-out amounts like "two cups" / "zwei Liter").
    public enum ItemTextRoute
    {
        SkipAi,
        NeedsExtraction,
    }

    public readonly record struct ItemTextAnalysis(
        ItemTextRoute Route,
        string CleanName);

    // Pure front-door triage for the quantity/classification pipeline. Free to run, so the slices
    // call it unconditionally (extraction enabled or not). It only screens out junk; everything that
    // could be a real product goes to the LLM, because there is no cheap, reliable way to tell text
    // that carries a quantity (digit OR spelled-out, in any language) from text that does not.
    public static class ItemTextRouter
    {
        // Generous ceilings: well above any real product name, well below the 500-char ListItem.Text
        // cap — guards obvious nonsense (e.g. a 300-char paste) without dropping legit multi-word items.
        private const int MaxProductChars = 120;
        private const int MaxProductWords = 15;

        private static readonly Regex Url = new(@"https?://|www\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly char[] WordSeparators = { ' ', '\t', '\n', '\r' };

        public static ItemTextAnalysis Analyze(string? rawText)
        {
            var text = (rawText ?? string.Empty).Trim();

            // Skip guards (terminal): empty / punctuation-only / URL / over the length ceiling.
            if (text.Length == 0
                || !text.Any(char.IsLetterOrDigit)
                || Url.IsMatch(text)
                || text.Length > MaxProductChars
                || text.Split(WordSeparators, System.StringSplitOptions.RemoveEmptyEntries).Length > MaxProductWords)
            {
                return new ItemTextAnalysis(ItemTextRoute.SkipAi, text);
            }

            // Everything else: hand the full text to the LLM for quantity extraction, which chains
            // classification on the stripped clean name.
            return new ItemTextAnalysis(ItemTextRoute.NeedsExtraction, text);
        }
    }
}

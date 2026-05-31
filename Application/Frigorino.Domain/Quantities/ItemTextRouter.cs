using System.Linq;
using System.Text.RegularExpressions;

namespace Frigorino.Domain.Quantities
{
    // What to do with a new/edited list-item's text. SkipAi/NeedsExtraction/ClassifyOnly carry the
    // raw text as CleanName; only Resolved carries the deterministically stripped name + quantity.
    public enum ItemTextRoute
    {
        SkipAi,
        Resolved,
        NeedsExtraction,
        ClassifyOnly,
    }

    public readonly record struct ItemTextAnalysis(
        ItemTextRoute Route,
        string CleanName,
        Quantity? Quantity);

    // Pure front-door triage for the quantity/classification pipeline. Free to run, so the slices
    // call it unconditionally (extraction enabled or not). Guards are evaluated in priority order:
    //   0. skip junk (URL / empty / punctuation-only / over the length ceiling) — terminal,
    //   A. deterministic facet extraction (today: Quantity.TryParse),
    //   B. disposition of the ambiguous remainder (digit -> LLM, else classify).
    public static class ItemTextRouter
    {
        // Generous ceilings: well above any real product name, well below the 500-char ListItem.Text
        // cap — guards obvious nonsense (e.g. a 300-char paste) without dropping legit multi-word items.
        private const int MaxProductChars = 120;
        private const int MaxProductWords = 15;

        private static readonly Regex Url = new(@"https?://|www\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Digit = new(@"\d", RegexOptions.Compiled);
        private static readonly char[] WordSeparators = { ' ', '\t', '\n', '\r' };

        public static ItemTextAnalysis Analyze(string rawText)
        {
            var text = (rawText ?? string.Empty).Trim();

            // Phase 0: skip guards (terminal).
            if (text.Length == 0
                || !text.Any(char.IsLetterOrDigit)
                || Url.IsMatch(text)
                || text.Length > MaxProductChars
                || text.Split(WordSeparators, System.StringSplitOptions.RemoveEmptyEntries).Length > MaxProductWords)
            {
                return new ItemTextAnalysis(ItemTextRoute.SkipAi, rawText ?? string.Empty, null);
            }

            // Phase A: deterministic facet extraction (quantity is the only facet today).
            if (Quantity.TryParse(rawText!, out var cleanName, out var quantity))
            {
                return new ItemTextAnalysis(ItemTextRoute.Resolved, cleanName, quantity);
            }

            // Phase B: disposition of the ambiguous remainder.
            return Digit.IsMatch(rawText!)
                ? new ItemTextAnalysis(ItemTextRoute.NeedsExtraction, rawText!, null)
                : new ItemTextAnalysis(ItemTextRoute.ClassifyOnly, rawText!, null);
        }
    }
}

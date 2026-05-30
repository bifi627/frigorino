using System.Text.RegularExpressions;

namespace Frigorino.Domain.Products
{
    // Normalization v1: lowercase + trim + collapse internal whitespace. Deliberately no
    // stemming / plural-stripping / article-stripping (language-dependent, bilingual en/de).
    public static class ProductName
    {
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return Whitespace.Replace(raw.Trim().ToLowerInvariant(), " ");
        }
    }
}

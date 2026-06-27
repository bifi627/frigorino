using Frigorino.Domain.Products;

namespace Frigorino.Infrastructure.Tasks
{
    // A distinct product name referenced by an active ListItem, tagged with its household
    // (resolved via ListItem -> List -> HouseholdId). RawText is normalized by the helper.
    public sealed record ListItemNameCandidate(int HouseholdId, string RawText);

    // An existing product catalog row, reduced to the fields needed to decide staleness.
    public sealed record ExistingProduct(
        int HouseholdId, string NormalizedName, int ClassifierVersion, bool IsOverridden = false);

    // A name needing (re)classification, carrying one representative raw name so the trigger/job
    // normalizes it consistently with the live path.
    public sealed record ClassificationGap(int HouseholdId, string RawName);

    // Pure gap decision: which referenced names have no up-to-date Product (never classified, or
    // below the current classifier version). Kept free of EF so it is unit-testable without a
    // database (mirrors CheckedItemPurge).
    public static class ProductClassificationGaps
    {
        public static List<ClassificationGap> SelectGaps(
            IReadOnlyCollection<ListItemNameCandidate> candidates,
            IReadOnlyCollection<ExistingProduct> existingProducts,
            int currentClassifierVersion)
        {
            // Highest classifier version per (household, normalized name). The unique index makes
            // duplicates unlikely, but Max keeps the decision well-defined regardless.
            var versionByName = new Dictionary<(int Household, string Name), int>();
            foreach (var product in existingProducts)
            {
                var key = (product.HouseholdId, product.NormalizedName);
                if (!versionByName.TryGetValue(key, out var current) || product.ClassifierVersion > current)
                {
                    versionByName[key] = product.ClassifierVersion;
                }
            }

            var overriddenNames = new HashSet<(int Household, string Name)>();
            foreach (var product in existingProducts)
            {
                if (product.IsOverridden)
                {
                    overriddenNames.Add((product.HouseholdId, product.NormalizedName));
                }
            }

            var gaps = new List<ClassificationGap>();
            var seen = new HashSet<(int Household, string Name)>();
            foreach (var candidate in candidates)
            {
                var normalized = ProductName.Normalize(candidate.RawText);
                if (normalized.Length == 0)
                {
                    continue;
                }

                var key = (candidate.HouseholdId, normalized);
                if (!seen.Add(key))
                {
                    continue;
                }

                var hasOverride = overriddenNames.Contains(key);
                var isUpToDate = hasOverride
                    || (versionByName.TryGetValue(key, out var version) && version >= currentClassifierVersion);
                if (!isUpToDate)
                {
                    gaps.Add(new ClassificationGap(candidate.HouseholdId, candidate.RawText));
                }
            }

            return gaps;
        }
    }
}

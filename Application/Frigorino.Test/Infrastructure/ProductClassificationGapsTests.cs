using Frigorino.Infrastructure.Tasks;

namespace Frigorino.Test.Infrastructure
{
    public class ProductClassificationGapsTests
    {
        private const int CurrentVersion = 2;

        [Fact]
        public void NeverClassifiedName_IsGap()
        {
            var candidates = new[] { new ListItemNameCandidate(HouseholdId: 10, RawText: "Milk") };
            var existing = Array.Empty<ExistingProduct>();

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Equal(new[] { new ClassificationGap(10, "Milk") }, gaps);
        }

        [Fact]
        public void UpToDateProduct_IsSkipped()
        {
            var candidates = new[] { new ListItemNameCandidate(10, "Milk") };
            var existing = new[] { new ExistingProduct(10, "milk", ClassifierVersion: CurrentVersion) };

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Empty(gaps);
        }

        [Fact]
        public void StaleVersionProduct_IsGap()
        {
            var candidates = new[] { new ListItemNameCandidate(10, "Milk") };
            var existing = new[] { new ExistingProduct(10, "milk", ClassifierVersion: CurrentVersion - 1) };

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Equal(new[] { new ClassificationGap(10, "Milk") }, gaps);
        }

        [Fact]
        public void MultipleSpellings_NormalizeToSingleGap()
        {
            var candidates = new[]
            {
                new ListItemNameCandidate(10, "Milk"),
                new ListItemNameCandidate(10, "  milk "),
                new ListItemNameCandidate(10, "MILK"),
            };
            var existing = Array.Empty<ExistingProduct>();

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Single(gaps);
            Assert.Equal(10, gaps[0].HouseholdId);
        }

        [Fact]
        public void SameNameDifferentHouseholds_AreIndependentGaps()
        {
            var candidates = new[]
            {
                new ListItemNameCandidate(10, "Milk"),
                new ListItemNameCandidate(20, "Milk"),
            };
            var existing = new[] { new ExistingProduct(10, "milk", CurrentVersion) };

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Equal(new[] { new ClassificationGap(20, "Milk") }, gaps);
        }

        [Fact]
        public void HighestProductVersionWins_WhenDuplicateRowsExist()
        {
            var candidates = new[] { new ListItemNameCandidate(10, "Milk") };
            // Duplicate rows for the same (household, name): one stale, one current. The current
            // one must win so the name is considered up-to-date and skipped.
            var existing = new[]
            {
                new ExistingProduct(10, "milk", ClassifierVersion: CurrentVersion - 1),
                new ExistingProduct(10, "milk", ClassifierVersion: CurrentVersion),
            };

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Empty(gaps);
        }

        [Fact]
        public void BlankText_IsSkipped()
        {
            var candidates = new[]
            {
                new ListItemNameCandidate(10, "   "),
                new ListItemNameCandidate(10, ""),
            };
            var existing = Array.Empty<ExistingProduct>();

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Empty(gaps);
        }

        [Fact]
        public void OverriddenProduct_IsSkipped_EvenWhenStale()
        {
            var candidates = new[] { new ListItemNameCandidate(10, "Milk") };
            var existing = new[]
            {
                new ExistingProduct(10, "milk", ClassifierVersion: CurrentVersion - 1, IsOverridden: true),
            };

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Empty(gaps);
        }
    }
}

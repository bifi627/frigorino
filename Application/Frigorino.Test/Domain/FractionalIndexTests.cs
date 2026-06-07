using Frigorino.Domain.Entities;
using Xunit;

namespace Frigorino.Test.Domain
{
    public class FractionalIndexTests
    {
        // Reference vectors from the canonical fractional-indexing spec.
        [Theory]
        [InlineData(null, null, "a0")]
        [InlineData("a0", null, "a1")]
        [InlineData("a1", null, "a2")]
        [InlineData(null, "a0", "Zz")]
        [InlineData(null, "Zz", "Zy")]
        [InlineData("a0", "a1", "a0V")]
        [InlineData("a0V", "a1", "a0l")]
        [InlineData("a0", "a0V", "a0G")]
        [InlineData("Zz", "a0", "ZzV")]
        [InlineData("Zz", null, "a0")]
        public void GenerateKeyBetween_MatchesReferenceVectors(string? a, string? b, string expected)
        {
            Assert.Equal(expected, FractionalIndex.GenerateKeyBetween(a, b));
        }

        [Fact]
        public void GenerateKeyBetween_ResultSortsStrictlyBetween()
        {
            var mid = FractionalIndex.GenerateKeyBetween("a0", "a1");
            Assert.True(string.CompareOrdinal("a0", mid) < 0);
            Assert.True(string.CompareOrdinal(mid, "a1") < 0);
        }

        [Fact]
        public void GenerateKeyBetween_ThrowsWhenOutOfOrder()
        {
            Assert.Throws<ArgumentException>(() => FractionalIndex.GenerateKeyBetween("a1", "a0"));
        }

        [Fact]
        public void GenerateKeyBetween_ThirteenDropsIntoSameSlot_ProducesDistinctKeys()
        {
            // The bug this whole change fixes: repeatedly inserting between "a0" and a moving
            // upper bound never collides — keys just get longer.
            var lower = "a0";
            var upper = "a1";
            var seen = new HashSet<string> { lower, upper };
            for (var i = 0; i < 50; i++)
            {
                var mid = FractionalIndex.GenerateKeyBetween(lower, upper);
                Assert.True(string.CompareOrdinal(lower, mid) < 0 && string.CompareOrdinal(mid, upper) < 0);
                Assert.True(seen.Add(mid), $"collision at iteration {i}: {mid}");
                upper = mid; // keep dropping into the same shrinking slot
            }
        }

        [Theory]
        [InlineData(0, new string[0])]
        [InlineData(1, new[] { "a0" })]
        [InlineData(2, new[] { "a0", "a1" })]
        [InlineData(3, new[] { "a0", "a1", "a2" })]
        public void GenerateKeysBetween_NullNull_AppendsSequentially(int n, string[] expected)
        {
            Assert.Equal(expected, FractionalIndex.GenerateKeysBetween(null, null, n).ToArray());
        }

        [Fact]
        public void GenerateKeysBetween_ResultsAreStrictlyIncreasing()
        {
            var keys = FractionalIndex.GenerateKeysBetween("a0", "a1", 5).ToArray();
            Assert.Equal(5, keys.Length);
            for (var i = 1; i < keys.Length; i++)
            {
                Assert.True(string.CompareOrdinal(keys[i - 1], keys[i]) < 0);
            }
            Assert.True(string.CompareOrdinal("a0", keys[0]) < 0);
            Assert.True(string.CompareOrdinal(keys[^1], "a1") < 0);
        }
    }
}

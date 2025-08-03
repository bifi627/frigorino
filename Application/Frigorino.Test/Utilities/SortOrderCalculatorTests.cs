using Frigorino.Application.Utilities;

namespace Frigorino.Test.Utilities
{
    public class SortOrderCalculatorTests
    {
        [Fact]
        public void GetNewItemSortOrder_ShouldReturnCorrectValue()
        {
            // Arrange & Act
            var result = SortOrderCalculator.GetNewItemSortOrder();

            // Assert
            Assert.Equal(99_000, result);
        }

        [Fact]
        public void GetCheckedStatusSortOrder_ShouldReturnCorrectValue()
        {
            // Arrange & Act
            var result = SortOrderCalculator.GetCheckedStatusSortOrder();

            // Assert
            Assert.Equal(1_099_000, result);
        }

        [Fact]
        public void GetUncheckedStatusSortOrder_ShouldReturnNewItemSortOrder()
        {
            // Arrange & Act
            var result = SortOrderCalculator.GetUncheckedStatusSortOrder();

            // Assert
            Assert.Equal(SortOrderCalculator.GetNewItemSortOrder(), result);
        }

        [Theory]
        [InlineData(0, true, 1_099_000)] // Move to top of checked section
        [InlineData(0, false, 99_000)]   // Move to top of unchecked section
        public void CalculateReorderSortOrder_MoveToTop_ShouldReturnCorrectValue(int afterSortOrder, bool isCheckedSection, int expectedResult)
        {
            // Arrange & Act
            var result = SortOrderCalculator.CalculateReorderSortOrder(afterSortOrder, null, isCheckedSection);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void CalculateReorderSortOrder_MoveToBottom_ShouldAddDefaultGap()
        {
            // Arrange
            var afterSortOrder = 100_000;
            var expectedResult = afterSortOrder + SortOrderCalculator.DefaultGap;

            // Act
            var result = SortOrderCalculator.CalculateReorderSortOrder(afterSortOrder, null, false);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void CalculateReorderSortOrder_InsertBetween_ShouldReturnMidpoint()
        {
            // Arrange
            var afterSortOrder = 100_000;
            var beforeSortOrder = 102_000;
            var expectedMidpoint = 101_000;

            // Act
            var result = SortOrderCalculator.CalculateReorderSortOrder(afterSortOrder, beforeSortOrder, false);

            // Assert
            Assert.Equal(expectedMidpoint, result);
        }

        [Fact]
        public void CalculateReorderSortOrder_SmallGap_ShouldStillReturnMidpoint()
        {
            // Arrange
            var afterSortOrder = 100_000;
            var beforeSortOrder = 100_050; // Gap of 50, which is less than MinGapThreshold (100)
            var expectedMidpoint = 100_025;

            // Act
            var result = SortOrderCalculator.CalculateReorderSortOrder(afterSortOrder, beforeSortOrder, false);

            // Assert
            Assert.Equal(expectedMidpoint, result);
        }

        [Fact]
        public void NeedsCompaction_EmptyList_ShouldReturnFalse()
        {
            // Arrange
            var sortOrders = new List<int>();

            // Act
            var result = SortOrderCalculator.NeedsCompaction(sortOrders);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void NeedsCompaction_SingleItem_ShouldReturnFalse()
        {
            // Arrange
            var sortOrders = new List<int> { 100_000 };

            // Act
            var result = SortOrderCalculator.NeedsCompaction(sortOrders);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void NeedsCompaction_LargeGaps_ShouldReturnFalse()
        {
            // Arrange
            var sortOrders = new List<int> { 100_000, 101_000, 102_000 };

            // Act
            var result = SortOrderCalculator.NeedsCompaction(sortOrders);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void NeedsCompaction_SmallGaps_ShouldReturnTrue()
        {
            // Arrange
            var sortOrders = new List<int> { 100_000, 100_050, 100_100 }; // Gaps of 50, less than threshold of 100

            // Act
            var result = SortOrderCalculator.NeedsCompaction(sortOrders);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void NeedsCompaction_UnorderedList_ShouldSortAndCheck()
        {
            // Arrange
            var sortOrders = new List<int> { 100_100, 100_000, 100_050 }; // Unordered with small gaps

            // Act
            var result = SortOrderCalculator.NeedsCompaction(sortOrders);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void GenerateCompactedSortOrders_ShouldReturnCorrectSequences()
        {
            // Arrange
            var uncheckedCount = 3;
            var checkedCount = 2;

            // Act
            var (uncheckedOrders, checkedOrders) = SortOrderCalculator.GenerateCompactedSortOrders(uncheckedCount, checkedCount);

            // Assert
            Assert.Equal(3, uncheckedOrders.Count);
            Assert.Equal(2, checkedOrders.Count);

            // Check unchecked sequence
            Assert.Equal(100_000, uncheckedOrders[0]);
            Assert.Equal(101_000, uncheckedOrders[1]);
            Assert.Equal(102_000, uncheckedOrders[2]);

            // Check checked sequence
            Assert.Equal(1_100_000, checkedOrders[0]);
            Assert.Equal(1_101_000, checkedOrders[1]);
        }

        [Fact]
        public void GenerateCompactedSortOrders_ZeroCounts_ShouldReturnEmptyLists()
        {
            // Arrange & Act
            var (uncheckedOrders, checkedOrders) = SortOrderCalculator.GenerateCompactedSortOrders(0, 0);

            // Assert
            Assert.Empty(uncheckedOrders);
            Assert.Empty(checkedOrders);
        }

        [Theory]
        [InlineData(5, 0)]
        [InlineData(0, 5)]
        [InlineData(1, 1)]
        public void GenerateCompactedSortOrders_VariousCounts_ShouldReturnCorrectCounts(int uncheckedCount, int checkedCount)
        {
            // Arrange & Act
            var (uncheckedOrders, checkedOrders) = SortOrderCalculator.GenerateCompactedSortOrders(uncheckedCount, checkedCount);

            // Assert
            Assert.Equal(uncheckedCount, uncheckedOrders.Count);
            Assert.Equal(checkedCount, checkedOrders.Count);
        }

        [Fact]
        public void GenerateCompactedSortOrders_ShouldMaintainCorrectGaps()
        {
            // Arrange
            var uncheckedCount = 5;
            var checkedCount = 3;

            // Act
            var (uncheckedOrders, checkedOrders) = SortOrderCalculator.GenerateCompactedSortOrders(uncheckedCount, checkedCount);

            // Assert - Check gaps between consecutive items
            for (int i = 1; i < uncheckedOrders.Count; i++)
            {
                var gap = uncheckedOrders[i] - uncheckedOrders[i - 1];
                Assert.Equal(SortOrderCalculator.DefaultGap, gap);
            }

            for (int i = 1; i < checkedOrders.Count; i++)
            {
                var gap = checkedOrders[i] - checkedOrders[i - 1];
                Assert.Equal(SortOrderCalculator.DefaultGap, gap);
            }
        }

        [Fact]
        public void Constants_ShouldHaveExpectedValues()
        {
            // Assert - Document the expected constant values
            Assert.Equal(100_000, SortOrderCalculator.UncheckedMinRange);
            Assert.Equal(999_999, SortOrderCalculator.UncheckedMaxRange);
            Assert.Equal(1_100_000, SortOrderCalculator.CheckedMinRange);
            Assert.Equal(1_000, SortOrderCalculator.DefaultGap);
            Assert.Equal(100, SortOrderCalculator.MinGapThreshold);
        }
    }
}

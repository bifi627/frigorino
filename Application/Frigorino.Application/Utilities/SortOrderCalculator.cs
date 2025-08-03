namespace Frigorino.Application.Utilities
{
    public static class SortOrderCalculator
    {
        // Unchecked items range: 100,000 - 999,999
        public const int UncheckedMinRange = 100_000;
        public const int UncheckedMaxRange = 999_999;
        
        // Checked items range: 1,100,000+
        public const int CheckedMinRange = 1_100_000;
        
        // Default gap between items
        public const int DefaultGap = 1_000;
        
        // Minimum gap before compaction is needed
        public const int MinGapThreshold = 100;

        /// <summary>
        /// Calculate sort order for a new unchecked item (always goes to top)
        /// </summary>
        public static int GetNewItemSortOrder()
        {
            return UncheckedMinRange - DefaultGap; // 99,000
        }

        /// <summary>
        /// Calculate sort order when changing status from unchecked to checked
        /// </summary>
        public static int GetCheckedStatusSortOrder()
        {
            return CheckedMinRange - DefaultGap; // 1,099,000
        }

        /// <summary>
        /// Calculate sort order when changing status from checked to unchecked
        /// </summary>
        public static int GetUncheckedStatusSortOrder()
        {
            return GetNewItemSortOrder(); // 99,000
        }

        /// <summary>
        /// Calculate sort order for reordering within a section
        /// </summary>
        /// <param name="afterSortOrder">Sort order of item to place after (0 means top of section)</param>
        /// <param name="beforeSortOrder">Sort order of item to place before (null means bottom of section)</param>
        /// <param name="isCheckedSection">Whether this is for checked or unchecked section</param>
        public static int CalculateReorderSortOrder(int afterSortOrder, int? beforeSortOrder, bool isCheckedSection)
        {
            if (afterSortOrder == 0) // Move to top of section
            {
                return isCheckedSection ? GetCheckedStatusSortOrder() : GetNewItemSortOrder();
            }

            if (beforeSortOrder == null) // Move to bottom of section
            {
                return afterSortOrder + DefaultGap;
            }

            // Calculate midpoint
            int midpoint = (afterSortOrder + beforeSortOrder.Value) / 2;
            
            // If gap is too small, we might need compaction later
            if (beforeSortOrder.Value - afterSortOrder < MinGapThreshold)
            {
                // For now, just use the midpoint and flag for future compaction
                return midpoint;
            }

            return midpoint;
        }

        /// <summary>
        /// Check if a list needs sort order compaction
        /// </summary>
        public static bool NeedsCompaction(List<int> sortOrders)
        {
            if (sortOrders.Count < 2) return false;

            sortOrders.Sort();
            for (int i = 1; i < sortOrders.Count; i++)
            {
                if (sortOrders[i] - sortOrders[i - 1] < MinGapThreshold)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Generate new sort orders for compaction
        /// </summary>
        /// <param name="uncheckedCount">Number of unchecked items</param>
        /// <param name="checkedCount">Number of checked items</param>
        public static (List<int> uncheckedOrders, List<int> checkedOrders) GenerateCompactedSortOrders(int uncheckedCount, int checkedCount)
        {
            var uncheckedOrders = new List<int>();
            var checkedOrders = new List<int>();

            // Generate unchecked sort orders
            for (int i = 0; i < uncheckedCount; i++)
            {
                uncheckedOrders.Add(UncheckedMinRange + (i * DefaultGap));
            }

            // Generate checked sort orders
            for (int i = 0; i < checkedCount; i++)
            {
                checkedOrders.Add(CheckedMinRange + (i * DefaultGap));
            }

            return (uncheckedOrders, checkedOrders);
        }
    }
}

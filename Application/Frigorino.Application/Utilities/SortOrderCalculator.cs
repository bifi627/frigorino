namespace Frigorino.Application.Utilities
{
    public enum InsertType
    {
        Start,
        End,
    }

    public static class SortOrderCalculator
    {
        public const int UncheckedMinRange = 1_000_000;
        public const int UncheckedMaxRange = 9_000_000;

        public const int CheckedMinRange = 10_000_000;
        public const int CheckedMaxRange = 19_000_000;

        // Default gap between items
        public const int DefaultGap = 10_000;

        // Minimum gap before compaction is needed
        public const int MinGapThreshold = 10;

        public static int? CalculateSortOrder(bool @checked, int? after, int? before, int? first, int? last, out bool needRecalculation)
        {
            needRecalculation = false;

            if (after is null) // Move to start/end of section
            {
                if (first is null) // No items in section
                {
                    return @checked ? CheckedMinRange + DefaultGap : UncheckedMinRange + DefaultGap;
                }
            }

            if (after is null && !@checked && last is not null)
            {
                return last + DefaultGap;
            }

            if (after is null && @checked && first is not null)
            {
                return first - DefaultGap;
            }

            if (after is not null && after is not 0)
            {
                if (before is not null && before is not 0)
                {
                    var mid = (before - after) / 2;
                    return after + mid;
                }
                return after + DefaultGap;
            }

            if (first is not null)
            {
                return first - DefaultGap;
            }

            //if (last == first)
            //{
            //    if (@checked)
            //    {
            //        last = null;
            //    }
            //    else
            //    {
            //        first = null;
            //    }
            //}

            //after ??= @checked ? first ?? CheckedMinRange + DefaultGap : last ?? UncheckedMinRange + DefaultGap;
            //before ??= @checked ? last ?? CheckedMinRange + DefaultGap : first ?? UncheckedMinRange + DefaultGap;

            //if (after == before)
            //{
            //    if (@checked)
            //    {
            //        before = last - DefaultGap;
            //    }
            //    else
            //    {
            //        after = first - DefaultGap;
            //    }
            //}

            //var midpoint = after + (before.Value - after.Value) / 2;

            //// If gap is too small, we might need compaction later
            //if (midpoint < MinGapThreshold)
            //{
            //    needRecalculation = true;
            //    return midpoint.Value;
            //}
            //return midpoint.Value;
            return -1;
        }

        //public static int CalculateSortOrderChecked(int after, int first, int last, out bool needRecalculation)
        //{
        //    needRecalculation = false;

        //    if (after == 0) // Move to start/end of section
        //    {
        //        if (last == 0) // No items in section
        //        {
        //            return CheckedMinRange + DefaultGap;
        //        }

        //        int next = first - DefaultGap;
        //        if (next < UncheckedMaxRange)
        //        {
        //            needRecalculation = true;
        //        }

        //        return next;
        //    }

        //    int midpoint = after + (after + DefaultGap) / 2;

        //    // If gap is too small, we might need compaction later
        //    if (midpoint < MinGapThreshold)
        //    {
        //        needRecalculation = true;
        //        return midpoint;
        //    }

        //    return midpoint;
        //}

        //public static int CalculateSortOrderUnchecked(int after, int first, int last, out bool needRecalculation)
        //{
        //    needRecalculation = false;

        //    if (after == 0) // Move to start/end of section
        //    {
        //        if (last == 0) // No items in section
        //        {
        //            return UncheckedMinRange + DefaultGap;
        //        }

        //        int next = first - DefaultGap;
        //        if (next < DefaultGap)
        //        {
        //            needRecalculation = true;
        //        }

        //        return next;
        //    }

        //    int midpoint = after / 2;

        //    // If gap is too small, we might need compaction later
        //    if (midpoint < MinGapThreshold)
        //    {
        //        needRecalculation = true;
        //        return midpoint;
        //    }

        //    return midpoint;
        //}

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

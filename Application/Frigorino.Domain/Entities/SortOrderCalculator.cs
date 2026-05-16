namespace Frigorino.Domain.Entities
{
    // Pure math used by the List aggregate to keep its ListItems in two parallel
    // sort-order ranges (unchecked 1M..9M, checked 10M..19M) with a configurable gap
    // between adjacent items. No DbContext awareness; List passes the relevant slice
    // of its ListItems collection in.
    public static class SortOrderCalculator
    {
        public const int UncheckedMinRange = 1_000_000;
        public const int UncheckedMaxRange = 9_000_000;

        public const int CheckedMinRange = 10_000_000;
        public const int CheckedMaxRange = 19_000_000;

        // Default gap between items.
        public const int DefaultGap = 10_000;

        // Minimum gap before compaction is needed.
        public const int MinGapThreshold = 10;

        // Position-resolver used by AddItem / ToggleItemStatus / ReorderItem. The four
        // anchor parameters describe the target position: `after`/`before` are the immediate
        // neighbours (or 0/null when there is none), `first`/`last` are the section bounds.
        //
        //   after=null, no items in section          → MinRange + DefaultGap
        //   after=null, unchecked, items exist       → last + DefaultGap (append to end)
        //   after=null, checked, items exist         → first - DefaultGap (prepend to top)
        //   after>0, before>0                        → midpoint
        //   after>0, no before                       → after + DefaultGap (last position)
        //   after=0, items exist                     → first - DefaultGap (move to top)
        public static int? CalculateSortOrder(bool @checked, int? after, int? before, int? first, int? last)
        {
            if (after is null)
            {
                if (first is null)
                {
                    return @checked ? CheckedMinRange + DefaultGap : UncheckedMinRange + DefaultGap;
                }
                if (!@checked && last is not null)
                {
                    return last + DefaultGap;
                }
                if (@checked)
                {
                    return first - DefaultGap;
                }
            }

            if (after is not null && after is not 0)
            {
                if (before is not null && before is not 0)
                {
                    return after + (before - after) / 2;
                }
                return after + DefaultGap;
            }

            if (first is not null)
            {
                return first - DefaultGap;
            }

            return -1;
        }

        // True when the minimum gap between adjacent sort orders has shrunk below the
        // compaction threshold. Callers use this as a hint to schedule CompactItems.
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

        // Fresh sort orders for the compact pass. Unchecked items get [MinRange, MinRange+Gap, ...],
        // checked items get [CheckedMinRange, CheckedMinRange+Gap, ...].
        public static (List<int> uncheckedOrders, List<int> checkedOrders) GenerateCompactedSortOrders(int uncheckedCount, int checkedCount)
        {
            var uncheckedOrders = new List<int>();
            var checkedOrders = new List<int>();

            for (int i = 0; i < uncheckedCount; i++)
            {
                uncheckedOrders.Add(UncheckedMinRange + (i * DefaultGap));
            }

            for (int i = 0; i < checkedCount; i++)
            {
                checkedOrders.Add(CheckedMinRange + (i * DefaultGap));
            }

            return (uncheckedOrders, checkedOrders);
        }
    }
}

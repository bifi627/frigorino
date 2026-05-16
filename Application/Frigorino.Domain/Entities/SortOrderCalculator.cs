namespace Frigorino.Domain.Entities
{
    // Pure math used by the List / Inventory aggregates to keep their items in two parallel
    // sort-order ranges (unchecked 1M..9M, checked 10M..19M) with a configurable gap
    // between adjacent items. No DbContext awareness; the aggregate passes the relevant slice
    // of its items collection in.
    public static class SortOrderCalculator
    {
        public const int UncheckedMinRange = 1_000_000;
        public const int CheckedMinRange = 10_000_000;

        // Default gap between items.
        public const int DefaultGap = 10_000;

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

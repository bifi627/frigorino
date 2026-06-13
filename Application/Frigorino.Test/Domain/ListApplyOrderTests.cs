using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class ListApplyOrderTests
    {
        // Builds a list with the given unchecked + one checked item, all active, ranks minted by AddItem.
        private static List NewListWith(out ListItem a, out ListItem b, out ListItem c, out ListItem checkedItem)
        {
            var list = List.Create("Groceries", null, 1, "user").Value;
            list.Id = 1;
            a = AddUnchecked(list, 101, "apples");
            b = AddUnchecked(list, 102, "bread");
            c = AddUnchecked(list, 103, "milk");
            checkedItem = AddUnchecked(list, 200, "done");
            checkedItem.Status = true; // move to checked section (rank irrelevant for this test)
            return list;
        }

        private static ListItem AddUnchecked(List list, int id, string text)
        {
            var item = list.AddItem(text).Value;
            item.Id = id;
            return item;
        }

        [Fact]
        public void ApplyOrder_ReordersUncheckedToGivenSequence()
        {
            var list = NewListWith(out var a, out var b, out var c, out _);

            var result = list.ApplyOrder(new[] { c.Id, a.Id, b.Id });

            Assert.True(result.IsSuccess);
            var ordered = list.ListItems
                .Where(i => i.IsActive && !i.Status)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .Select(i => i.Id)
                .ToArray();
            Assert.Equal(new[] { c.Id, a.Id, b.Id }, ordered);
        }

        [Fact]
        public void ApplyOrder_DoesNotTouchCheckedItems()
        {
            var list = NewListWith(out var a, out var b, out var c, out var checkedItem);
            var checkedRankBefore = checkedItem.Rank;

            list.ApplyOrder(new[] { c.Id, b.Id, a.Id });

            Assert.Equal(checkedRankBefore, checkedItem.Rank);
        }

        [Fact]
        public void ApplyOrder_IdSetMismatch_Fails()
        {
            var list = NewListWith(out var a, out var b, out _, out _);

            // Missing one unchecked id.
            var result = list.ApplyOrder(new[] { a.Id, b.Id });

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void ApplyOrder_ForeignId_Fails()
        {
            var list = NewListWith(out var a, out var b, out var c, out _);

            var result = list.ApplyOrder(new[] { a.Id, b.Id, c.Id, 9999 });

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void ApplyOrder_EmptyUncheckedSection_Succeeds()
        {
            var list = List.Create("Empty", null, 1, "user").Value;

            var result = list.ApplyOrder(Array.Empty<int>());

            Assert.True(result.IsSuccess);
        }
    }
}

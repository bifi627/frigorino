using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;

namespace Frigorino.Test.Domain
{
    // Pure unit tests for the List aggregate's ListItem coordination methods. No DbContext.
    // Covers the sort-order matrix that integration tests only check on happy paths, plus
    // the validation/not-found paths that route to ValidationProblem and NotFound at the
    // slice handler layer.
    public class ListAggregateItemTests
    {
        private const string CreatorId = "user-creator";
        private const int HouseholdId = 42;

        // ------- AddItem -------

        [Fact]
        public void AddItem_FirstItem_GetsBaseSortOrder()
        {
            var list = NewList();

            var result = list.AddItem("Milk", null);

            Assert.True(result.IsSuccess);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, result.Value.SortOrder);
            Assert.False(result.Value.Status);
            Assert.True(result.Value.IsActive);
            Assert.Equal(list.Id, result.Value.ListId);
        }

        [Fact]
        public void AddItem_AppendsBelowLastUnchecked()
        {
            var list = NewList();
            list.AddItem("Milk", null);
            list.AddItem("Eggs", "12");

            var third = list.AddItem("Bread", null);

            Assert.True(third.IsSuccess);
            var expected = SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap * 3;
            Assert.Equal(expected, third.Value.SortOrder);
        }

        [Fact]
        public void AddItem_EmptyText_FailsKeyedOnText()
        {
            var list = NewList();

            var result = list.AddItem("   ", null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddItem_TextTooLong_FailsKeyedOnText()
        {
            var list = NewList();
            var tooLong = new string('x', ListItem.TextMaxLength + 1);

            var result = list.AddItem(tooLong, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddItem_QuantityTooLong_FailsKeyedOnQuantity()
        {
            var list = NewList();
            var tooLong = new string('x', ListItem.QuantityMaxLength + 1);

            var result = list.AddItem("Milk", tooLong);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Quantity), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddItem_TrimsTextAndQuantity()
        {
            var list = NewList();

            var result = list.AddItem("  Milk  ", "  2 L  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Milk", result.Value.Text);
            Assert.Equal("2 L", result.Value.Quantity);
        }

        [Fact]
        public void AddItem_WhitespaceQuantity_NormalisedToNull()
        {
            var list = NewList();

            var result = list.AddItem("Milk", "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Quantity);
        }

        // ------- ToggleItemStatus into a populated checked section -------
        // Covers the asymmetric "checked-section prepends" branch of ComputeAppendSortOrder
        // (first - DefaultGap), which the empty-section toggle tests don't exercise.

        [Fact]
        public void ToggleItemStatus_UncheckedToChecked_WithExistingChecked_PrependsAboveFirstChecked()
        {
            var list = NewList();
            var firstChecked = AddSeed(list, "Bread", status: true,
                sortOrder: SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap);
            AddSeed(list, "Eggs", status: true,
                sortOrder: SortOrderCalculator.CheckedMinRange + (2 * SortOrderCalculator.DefaultGap));
            var moving = AddSeed(list, "Milk");

            var result = list.ToggleItemStatus(moving.Id);

            Assert.True(result.IsSuccess);
            Assert.True(moving.Status);
            Assert.Equal(firstChecked.SortOrder - SortOrderCalculator.DefaultGap, moving.SortOrder);
        }

        // ------- UpdateItem -------

        [Fact]
        public void UpdateItem_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.UpdateItem(itemId: 999, text: "x", quantity: null, status: null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void UpdateItem_InactiveItem_TreatedAsNotFound()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.IsActive = false;

            var result = list.UpdateItem(item.Id, "Soy milk", null, null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void UpdateItem_PartialUpdate_PreservesUnsetFields()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", quantity: "1 L");

            var result = list.UpdateItem(item.Id, text: "Soy milk", quantity: null, status: null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Soy milk", item.Text);
            Assert.Equal("1 L", item.Quantity);
        }

        [Fact]
        public void UpdateItem_AllNullFields_FailsAsValidationError()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", quantity: "1 L");
            var before = item.UpdatedAt;

            var result = list.UpdateItem(item.Id, text: null, quantity: null, status: null);

            Assert.True(result.IsFailed);
            Assert.IsNotType<EntityNotFoundError>(result.Errors[0]);
            Assert.Equal(before, item.UpdatedAt);
        }

        [Fact]
        public void UpdateItem_StatusChangeToChecked_MovesToCheckedSection()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, text: null, quantity: null, status: true);

            Assert.True(result.IsSuccess);
            Assert.True(item.Status);
            Assert.Equal(SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap, item.SortOrder);
        }

        [Fact]
        public void UpdateItem_StatusChangeToUnchecked_MovesToUncheckedSection()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true, sortOrder: SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap);

            var result = list.UpdateItem(item.Id, text: null, quantity: null, status: false);

            Assert.True(result.IsSuccess);
            Assert.False(item.Status);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, item.SortOrder);
        }

        [Fact]
        public void UpdateItem_StatusUnchanged_KeepsSortOrder()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", sortOrder: 1_234_567);

            var result = list.UpdateItem(item.Id, "Renamed", null, status: false);

            Assert.True(result.IsSuccess);
            Assert.Equal(1_234_567, item.SortOrder);
        }

        [Fact]
        public void UpdateItem_EmptyText_FailsKeyedOnText()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, "  ", null, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void UpdateItem_TextTooLong_FailsKeyedOnText()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            var tooLong = new string('x', ListItem.TextMaxLength + 1);

            var result = list.UpdateItem(item.Id, tooLong, null, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void UpdateItem_StampsUpdatedAt()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            var before = item.UpdatedAt;

            var result = list.UpdateItem(item.Id, "Soy milk", null, null);

            Assert.True(result.IsSuccess);
            Assert.True(item.UpdatedAt > before);
        }

        // ------- RemoveItem -------

        [Fact]
        public void RemoveItem_SoftDeletesAndStampsUpdatedAt()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            var before = item.UpdatedAt;

            var result = list.RemoveItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.False(item.IsActive);
            Assert.True(item.UpdatedAt > before);
        }

        [Fact]
        public void RemoveItem_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.RemoveItem(itemId: 999);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveItem_AlreadyInactive_ReturnsEntityNotFound()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.IsActive = false;

            var result = list.RemoveItem(item.Id);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        // ------- ToggleItemStatus -------

        [Fact]
        public void ToggleItemStatus_UncheckedToChecked_MovesToCheckedSection()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.ToggleItemStatus(item.Id);

            Assert.True(result.IsSuccess);
            Assert.True(item.Status);
            Assert.Equal(SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap, item.SortOrder);
        }

        [Fact]
        public void ToggleItemStatus_CheckedToUnchecked_MovesToUncheckedSection()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true, sortOrder: SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap);

            var result = list.ToggleItemStatus(item.Id);

            Assert.True(result.IsSuccess);
            Assert.False(item.Status);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, item.SortOrder);
        }

        [Fact]
        public void ToggleItemStatus_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.ToggleItemStatus(itemId: 999);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        // ------- ReorderItem -------

        [Fact]
        public void ReorderItem_MoveToTop_FromAfterIdZero()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", sortOrder: 1_010_000);
            var item2 = AddSeed(list, "Eggs", sortOrder: 1_020_000);
            var item3 = AddSeed(list, "Bread", sortOrder: 1_030_000);

            var result = list.ReorderItem(item3.Id, afterItemId: 0);

            Assert.True(result.IsSuccess);
            Assert.Equal(item1.SortOrder - SortOrderCalculator.DefaultGap, item3.SortOrder);
        }

        [Fact]
        public void ReorderItem_MidpointBetweenTwoItems()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", sortOrder: 100_000);
            var item2 = AddSeed(list, "Eggs", sortOrder: 102_000);
            var item3 = AddSeed(list, "Bread", sortOrder: 104_000);

            var result = list.ReorderItem(item3.Id, afterItemId: item1.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(101_000, item3.SortOrder);
        }

        [Fact]
        public void ReorderItem_AfterIsLastInSection_AppendsWithGap()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", sortOrder: 100_000);
            var item2 = AddSeed(list, "Eggs", sortOrder: 102_000);
            var item3 = AddSeed(list, "Bread", sortOrder: 104_000);

            // Move item1 after item3 (so item3 is "last" relative to item1 going below it).
            var result = list.ReorderItem(item1.Id, afterItemId: item3.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(item3.SortOrder + SortOrderCalculator.DefaultGap, item1.SortOrder);
        }

        [Fact]
        public void ReorderItem_AfterIdInDifferentSection_FallsBackToTopOfOwnSection()
        {
            var list = NewList();
            var checkedItem = AddSeed(list, "Milk", status: true, sortOrder: 10_010_000);
            var unchecked1 = AddSeed(list, "Eggs", sortOrder: 1_010_000);
            var unchecked2 = AddSeed(list, "Bread", sortOrder: 1_020_000);

            // Try to anchor unchecked2 against a checked item — legacy silently moves to top
            // of unchecked2's own section.
            var result = list.ReorderItem(unchecked2.Id, afterItemId: checkedItem.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(unchecked1.SortOrder - SortOrderCalculator.DefaultGap, unchecked2.SortOrder);
        }

        [Fact]
        public void ReorderItem_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.ReorderItem(itemId: 999, afterItemId: 0);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        // ------- CompactItems -------

        [Fact]
        public void CompactItems_RewritesSortOrdersWithCleanGaps()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", sortOrder: 100_001);
            var item2 = AddSeed(list, "Eggs", sortOrder: 100_002);
            var item3 = AddSeed(list, "Bread", status: true, sortOrder: 200_777);

            var result = list.CompactItems();

            Assert.True(result.IsSuccess);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange, item1.SortOrder);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, item2.SortOrder);
            Assert.Equal(SortOrderCalculator.CheckedMinRange, item3.SortOrder);
        }

        [Fact]
        public void CompactItems_EmptyList_NoOp()
        {
            var list = NewList();

            var result = list.CompactItems();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void CompactItems_PreservesSeparationBetweenSections()
        {
            var list = NewList();
            AddSeed(list, "A", sortOrder: 100);
            AddSeed(list, "B", sortOrder: 200);
            AddSeed(list, "C", status: true, sortOrder: 300);
            AddSeed(list, "D", status: true, sortOrder: 400);

            var result = list.CompactItems();

            Assert.True(result.IsSuccess);
            var maxUnchecked = list.ListItems.Where(i => !i.Status).Max(i => i.SortOrder);
            var minChecked = list.ListItems.Where(i => i.Status).Min(i => i.SortOrder);
            Assert.True(maxUnchecked < minChecked);
        }

        [Fact]
        public void CompactItems_SkipsInactiveItems()
        {
            var list = NewList();
            AddSeed(list, "Active", sortOrder: 100_000);
            var inactive = AddSeed(list, "Inactive", sortOrder: 99);
            inactive.IsActive = false;

            var result = list.CompactItems();

            Assert.True(result.IsSuccess);
            Assert.Equal(99, inactive.SortOrder);
        }

        // ------- Helpers -------

        private static List NewList()
        {
            return new List
            {
                Id = 1,
                Name = "Groceries",
                HouseholdId = HouseholdId,
                CreatedByUserId = CreatorId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsActive = true,
            };
        }

        private int _nextItemId = 100;

        private ListItem AddSeed(List list, string text, string? quantity = null, bool status = false, int? sortOrder = null)
        {
            var item = new ListItem
            {
                Id = ++_nextItemId,
                ListId = list.Id,
                Text = text,
                Quantity = quantity,
                Status = status,
                SortOrder = sortOrder ?? (status
                    ? SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap
                    : SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true,
            };
            list.ListItems.Add(item);
            return item;
        }
    }
}

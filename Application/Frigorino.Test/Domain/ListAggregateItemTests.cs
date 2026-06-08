using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Quantities;

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
        public void AddItem_FirstItem_GetsBaseRank()
        {
            var list = NewList();

            var result = list.AddItem("Milk");

            Assert.True(result.IsSuccess);
            Assert.Equal("a0", result.Value.Rank);
            Assert.False(result.Value.Status);
            Assert.True(result.Value.IsActive);
            Assert.Equal(list.Id, result.Value.ListId);
        }

        [Fact]
        public void AddItem_AppendsBelowLastUnchecked()
        {
            var list = NewList();
            var first = list.AddItem("Milk").Value;
            var second = list.AddItem("Eggs").Value;

            var third = list.AddItem("Bread");

            Assert.True(third.IsSuccess);
            Assert.True(string.CompareOrdinal(first.Rank, second.Rank) < 0);
            Assert.True(string.CompareOrdinal(second.Rank, third.Value.Rank) < 0);
        }

        [Fact]
        public void AddItem_EmptyText_FailsKeyedOnText()
        {
            var list = NewList();

            var result = list.AddItem("   ");

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddItem_TextTooLong_FailsKeyedOnText()
        {
            var list = NewList();
            var tooLong = new string('x', ListItem.TextMaxLength + 1);

            var result = list.AddItem(tooLong);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddItem_TrimsText()
        {
            var list = NewList();

            var result = list.AddItem("  Milk  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Milk", result.Value.Text);
            Assert.Null(result.Value.QuantityValue);
            Assert.Null(result.Value.QuantityUnit);
        }

        [Fact]
        public void AddItem_SetsTrimmedComment()
        {
            var list = NewList();

            var result = list.AddItem("Milk", quantity: null, comment: "  the blue one  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("the blue one", result.Value.Comment);
        }

        [Fact]
        public void AddItem_WhitespaceComment_StoredAsNull()
        {
            var list = NewList();

            var result = list.AddItem("Milk", quantity: null, comment: "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddItem_NoComment_StoredAsNull()
        {
            var list = NewList();

            var result = list.AddItem("Milk");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddItem_CommentTooLong_FailsKeyedOnComment()
        {
            var list = NewList();
            var tooLong = new string('x', ListItem.CommentMaxLength + 1);

            var result = list.AddItem("Milk", quantity: null, comment: tooLong);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Comment), result.Errors[0].Metadata["Property"]);
        }

        // ------- ToggleItemStatus into a populated checked section -------
        // Covers the asymmetric "checked-section prepends" branch of ComputeAppendRank
        // (key before the first checked), which the empty-section toggle tests don't exercise.

        [Fact]
        public void ToggleItemStatus_UncheckedToChecked_WithExistingChecked_PrependsAboveFirstChecked()
        {
            var list = NewList();
            var firstChecked = AddSeed(list, "Bread", status: true, rank: "a1");
            AddSeed(list, "Eggs", status: true, rank: "a2");
            var moving = AddSeed(list, "Milk");

            var result = list.ToggleItemStatus(moving.Id);

            Assert.True(result.IsSuccess);
            Assert.True(moving.Status);
            Assert.True(string.CompareOrdinal(moving.Rank, firstChecked.Rank) < 0);
        }

        // ------- UpdateItem -------

        [Fact]
        public void UpdateItem_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.UpdateItem(itemId: 999, text: "x", quantity: null, clearQuantity: false, status: null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void UpdateItem_InactiveItem_TreatedAsNotFound()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.IsActive = false;

            var result = list.UpdateItem(item.Id, "Soy milk", null, false, null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void UpdateItem_PartialUpdate_PreservesUnsetQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", quantity: Quantity.Create(1, QuantityUnit.Liter).Value);

            var result = list.UpdateItem(item.Id, text: "Soy milk", quantity: null, clearQuantity: false, status: null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Soy milk", item.Text);
            Assert.Equal(1m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Liter, item.QuantityUnit);
        }

        [Fact]
        public void UpdateItem_SetsQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            var result = list.UpdateItem(item.Id, text: null,
                quantity: Quantity.Create(2, QuantityUnit.Bottle).Value, clearQuantity: false, status: null);
            Assert.True(result.IsSuccess);
            Assert.Equal(2m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Bottle, item.QuantityUnit);
        }

        [Fact]
        public void UpdateItem_ClearQuantity_RemovesQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", quantity: Quantity.Create(1, QuantityUnit.Liter).Value);

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: true, status: null);

            Assert.True(result.IsSuccess);
            Assert.Null(item.QuantityValue);
            Assert.Null(item.QuantityUnit);
        }

        [Fact]
        public void UpdateItem_ClearQuantity_WinsOverProvidedQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", quantity: Quantity.Create(1, QuantityUnit.Liter).Value);

            var result = list.UpdateItem(item.Id, text: null,
                quantity: Quantity.Create(5, QuantityUnit.Piece).Value, clearQuantity: true, status: null);

            Assert.True(result.IsSuccess);
            Assert.Null(item.QuantityValue);
            Assert.Null(item.QuantityUnit);
        }

        [Fact]
        public void UpdateItem_AllNullFields_FailsAsValidationError()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", quantity: Quantity.Create(1, QuantityUnit.Liter).Value);
            var before = item.UpdatedAt;

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null);

            Assert.True(result.IsFailed);
            Assert.IsNotType<EntityNotFoundError>(result.Errors[0]);
            Assert.Equal(before, item.UpdatedAt);
        }

        [Fact]
        public void UpdateItem_StatusChangeToChecked_MovesToCheckedSection()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: true);

            Assert.True(result.IsSuccess);
            Assert.True(item.Status);
            // Empty checked section → base key.
            Assert.Equal("a0", item.Rank);
        }

        [Fact]
        public void UpdateItem_StatusChangeToUnchecked_MovesToUncheckedSection()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true, rank: "a0");

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: false);

            Assert.True(result.IsSuccess);
            Assert.False(item.Status);
            // Empty unchecked section → base key.
            Assert.Equal("a0", item.Rank);
        }

        [Fact]
        public void UpdateItem_StatusUnchanged_KeepsRank()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", rank: "a5");

            var result = list.UpdateItem(item.Id, "Renamed", null, clearQuantity: false, status: false);

            Assert.True(result.IsSuccess);
            Assert.Equal("a5", item.Rank);
        }

        [Fact]
        public void UpdateItem_EmptyText_FailsKeyedOnText()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, "  ", null, false, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void UpdateItem_TextTooLong_FailsKeyedOnText()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            var tooLong = new string('x', ListItem.TextMaxLength + 1);

            var result = list.UpdateItem(item.Id, tooLong, null, false, null);

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

            var result = list.UpdateItem(item.Id, "Soy milk", null, false, null);

            Assert.True(result.IsSuccess);
            Assert.True(item.UpdatedAt > before);
        }

        [Fact]
        public void UpdateItem_SetsTrimmedComment()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: "  ask the butcher  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("ask the butcher", item.Comment);
        }

        [Fact]
        public void UpdateItem_NullComment_PreservesExistingComment()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.Comment = "the blue one";

            var result = list.UpdateItem(item.Id, text: "Soy milk", quantity: null, clearQuantity: false, status: null, comment: null);

            Assert.True(result.IsSuccess);
            Assert.Equal("the blue one", item.Comment);
        }

        [Fact]
        public void UpdateItem_EmptyComment_ClearsExistingComment()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.Comment = "the blue one";

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(item.Comment);
        }

        [Fact]
        public void UpdateItem_CommentOnly_IsNotTreatedAsNoOp()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: "for Anna's party");

            Assert.True(result.IsSuccess);
            Assert.Equal("for Anna's party", item.Comment);
        }

        [Fact]
        public void UpdateItem_CommentTooLong_FailsKeyedOnComment()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            var tooLong = new string('x', ListItem.CommentMaxLength + 1);

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: tooLong);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Comment), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void UpdateItem_WhitespaceComment_ClearsToNull_NotTreatedAsNoOp()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(item.Comment);
        }

        // ------- ApplyExtractedQuantity -------

        [Fact]
        public void ApplyExtractedQuantity_RewritesTextAndSetsQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "20 apples");
            var result = list.ApplyExtractedQuantity(item.Id, "apples",
                Quantity.Create(20, QuantityUnit.Piece).Value);
            Assert.True(result.IsSuccess);
            Assert.Equal("apples", item.Text);
            Assert.Equal(20m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Piece, item.QuantityUnit);
        }

        [Fact]
        public void ApplyExtractedQuantity_OverwritesExistingQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "1l milk", quantity: Quantity.Create(1, QuantityUnit.Liter).Value);
            var result = list.ApplyExtractedQuantity(item.Id, "milk",
                Quantity.Create(20, QuantityUnit.Piece).Value);
            Assert.True(result.IsSuccess);
            Assert.Equal("milk", item.Text);
            Assert.Equal(20m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Piece, item.QuantityUnit);
        }

        [Fact]
        public void ApplyExtractedQuantity_NoQuantity_RewritesTextLeavesQuantityNull()
        {
            var list = NewList();
            var item = AddSeed(list, "milk");
            var result = list.ApplyExtractedQuantity(item.Id, "milk", quantity: null);
            Assert.True(result.IsSuccess);
            Assert.Equal("milk", item.Text);
            Assert.Null(item.QuantityValue);
            Assert.Null(item.QuantityUnit);
        }

        [Fact]
        public void ApplyExtractedQuantity_ItemNotFound_ReturnsEntityNotFound()
        {
            var list = NewList();
            var result = list.ApplyExtractedQuantity(itemId: 999, cleanName: "apples", quantity: null);
            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
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

        // ------- RestoreItem -------

        [Fact]
        public void RestoreItem_ReactivatesSoftDeletedItemAndStampsUpdatedAt()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            var before = item.UpdatedAt;

            var result = list.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.True(item.IsActive);
            Assert.Same(item, result.Value);
            Assert.True(item.UpdatedAt > before);
        }

        [Fact]
        public void RestoreItem_PreservesOriginalRank()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", rank: "a5");
            item.IsActive = false;

            var result = list.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal("a5", item.Rank);
        }

        [Fact]
        public void RestoreItem_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.RestoreItem(itemId: 999);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RestoreItem_AlreadyActive_ReturnsEntityNotFound()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk"); // active by default

            var result = list.RestoreItem(item.Id);

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
            // Empty checked section → base key.
            Assert.Equal("a0", item.Rank);
        }

        [Fact]
        public void ToggleItemStatus_CheckedToUnchecked_MovesToUncheckedSection()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true, rank: "a0");

            var result = list.ToggleItemStatus(item.Id);

            Assert.True(result.IsSuccess);
            Assert.False(item.Status);
            // Empty unchecked section → base key.
            Assert.Equal("a0", item.Rank);
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
        public void ReorderItem_MoveToTop_FromAfterIdZero_RanksBeforeFirst()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", rank: "a1");
            AddSeed(list, "Eggs", rank: "a2");
            var item3 = AddSeed(list, "Bread", rank: "a3");

            var result = list.ReorderItem(item3.Id, afterItemId: 0);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item3.Rank, item1.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_MidpointBetweenTwoItems_ProducesKeyStrictlyBetween()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", rank: "a0");
            var item2 = AddSeed(list, "Eggs", rank: "a1");
            var item3 = AddSeed(list, "Bread", rank: "a2");

            var result = list.ReorderItem(item3.Id, afterItemId: item1.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item1.Rank, item3.Rank) < 0);
            Assert.True(string.CompareOrdinal(item3.Rank, item2.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_AfterLastInSection_RanksAfterLast()
        {
            var list = NewList();
            var item1 = AddSeed(list, "Milk", rank: "a0");
            AddSeed(list, "Eggs", rank: "a1");
            var item3 = AddSeed(list, "Bread", rank: "a2");

            // Move item1 after item3 (the last in section); item1 now ranks after item3.
            var result = list.ReorderItem(item1.Id, afterItemId: item3.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item3.Rank, item1.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_AfterIdInDifferentSection_FallsBackToTopOfOwnSection()
        {
            var list = NewList();
            var checkedItem = AddSeed(list, "Milk", status: true, rank: "a0");
            var unchecked1 = AddSeed(list, "Eggs", rank: "a1");
            var unchecked2 = AddSeed(list, "Bread", rank: "a2");

            // Try to anchor unchecked2 against a checked item — legacy silently moves to top
            // of unchecked2's own section.
            var result = list.ReorderItem(unchecked2.Id, afterItemId: checkedItem.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(unchecked2.Rank, unchecked1.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.ReorderItem(itemId: 999, afterItemId: 0);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void ManyReordersIntoSameShrinkingSlot_NeverCollide()
        {
            var list = NewList();
            var top = AddSeed(list, "Top", rank: "a0");
            AddSeed(list, "Bottom", rank: "a1");

            // Repeatedly drop a fresh item into the same slot just below `top`; the gap shrinks each
            // time but a distinct key is always available (the old integer scheme collapsed to a
            // duplicate SortOrder after ~13 drops, which is the bug this whole change fixes).
            var ranks = new HashSet<string> { top.Rank };
            for (var i = 0; i < 20; i++)
            {
                var mover = AddSeed(list, $"Mover{i}");
                var r = list.ReorderItem(mover.Id, afterItemId: top.Id);
                Assert.True(r.IsSuccess);
                Assert.True(string.CompareOrdinal(top.Rank, mover.Rank) < 0);
                Assert.True(ranks.Add(mover.Rank), $"collision at iteration {i}: {mover.Rank}");
            }
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

        private ListItem AddSeed(List list, string text, Quantity? quantity = null, bool status = false, string? rank = null)
        {
            var item = new ListItem
            {
                Id = ++_nextItemId,
                ListId = list.Id,
                Text = text,
                QuantityValue = quantity?.Value,
                QuantityUnit = quantity?.Unit,
                Status = status,
                Rank = rank ?? FractionalIndex.GenerateKeyBetween(
                    list.ListItems.Where(i => i.Status == status).Select(i => i.Rank).LastOrDefault(), null),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true,
            };
            list.ListItems.Add(item);
            return item;
        }
    }
}

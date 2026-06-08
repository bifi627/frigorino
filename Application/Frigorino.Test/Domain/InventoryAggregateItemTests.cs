using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Quantities;

namespace Frigorino.Test.Domain
{
    // Pure unit tests for the Inventory aggregate's InventoryItem coordination methods. No
    // DbContext. Covers the sort-order matrix (single section — no checked/unchecked split),
    // partial-update + expiryDate write-through semantics, the structured-quantity tri-state
    // (set / preserve / clear), and the validation/not-found paths that route to ValidationProblem
    // and NotFound at the slice handler layer.
    public class InventoryAggregateItemTests
    {
        private const string CreatorId = "user-creator";
        private const int HouseholdId = 42;

        // ------- AddItem -------

        [Fact]
        public void AddItem_FirstItem_GetsBaseRank()
        {
            var inventory = NewInventory();

            var result = inventory.AddItem("Flour", null, null);

            Assert.True(result.IsSuccess);
            Assert.Equal("a0", result.Value.Rank);
            Assert.True(result.Value.IsActive);
            Assert.Equal(inventory.Id, result.Value.InventoryId);
        }

        [Fact]
        public void AddItem_AppendsBelowLast()
        {
            var inventory = NewInventory();
            var first = inventory.AddItem("Flour", null, null).Value;
            var second = inventory.AddItem("Sugar", Quantity.Create(1, QuantityUnit.Kilogram).Value, null).Value;

            var third = inventory.AddItem("Salt", null, null);

            Assert.True(third.IsSuccess);
            Assert.True(string.CompareOrdinal(first.Rank, second.Rank) < 0);
            Assert.True(string.CompareOrdinal(second.Rank, third.Value.Rank) < 0);
        }

        [Fact]
        public void AddItem_EmptyText_FailsKeyedOnText()
        {
            var inventory = NewInventory();

            var result = inventory.AddItem("   ", null, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(InventoryItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddItem_TextTooLong_FailsKeyedOnText()
        {
            var inventory = NewInventory();
            var tooLong = new string('x', InventoryItem.TextMaxLength + 1);

            var result = inventory.AddItem(tooLong, null, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(InventoryItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddItem_WithQuantity_SetsBothQuantityColumns()
        {
            var inventory = NewInventory();
            var quantity = Quantity.Create(2, QuantityUnit.Kilogram).Value;

            var result = inventory.AddItem("Flour", quantity, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(2m, result.Value.QuantityValue);
            Assert.Equal(QuantityUnit.Kilogram, result.Value.QuantityUnit);
        }

        [Fact]
        public void AddItem_TrimsText()
        {
            var inventory = NewInventory();

            var result = inventory.AddItem("  Flour  ", null, null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Flour", result.Value.Text);
        }

        [Fact]
        public void AddItem_WithoutQuantity_LeavesQuantityNull()
        {
            var inventory = NewInventory();

            var result = inventory.AddItem("Flour", null, null);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.QuantityValue);
            Assert.Null(result.Value.QuantityUnit);
        }

        [Fact]
        public void AddItem_WithExpiryDate_StoresIt()
        {
            var inventory = NewInventory();
            var expiry = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

            var result = inventory.AddItem("Milk", null, expiry);

            Assert.True(result.IsSuccess);
            Assert.Equal(expiry, result.Value.ExpiryDate);
        }

        // ------- UpdateItem -------

        [Fact]
        public void UpdateItem_NotFound_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();

            var result = inventory.UpdateItem(itemId: 999, text: "x", quantity: null, clearQuantity: false, expiryDate: null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void UpdateItem_InactiveItem_TreatedAsNotFound()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            item.IsActive = false;

            var result = inventory.UpdateItem(item.Id, "Sugar", null, clearQuantity: false, expiryDate: null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void UpdateItem_PartialUpdate_PreservesUnsetTextAndQuantity()
        {
            var inventory = NewInventory();
            var existingExpiry = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
            var item = AddSeed(inventory, "Flour", quantity: Quantity.Create(1, QuantityUnit.Kilogram).Value, expiryDate: existingExpiry);

            // Text/Quantity null = preserve. ExpiryDate echoed back to keep it.
            var result = inventory.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, expiryDate: existingExpiry);

            Assert.True(result.IsSuccess);
            Assert.Equal("Flour", item.Text);
            Assert.Equal(1m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Kilogram, item.QuantityUnit);
            Assert.Equal(existingExpiry, item.ExpiryDate);
        }

        [Fact]
        public void UpdateItem_SetsQuantity()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");

            var result = inventory.UpdateItem(item.Id, text: null,
                quantity: Quantity.Create(2, QuantityUnit.Bottle).Value, clearQuantity: false, expiryDate: null);

            Assert.True(result.IsSuccess);
            Assert.Equal(2m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Bottle, item.QuantityUnit);
        }

        [Fact]
        public void UpdateItem_ClearQuantity_RemovesQuantity()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour", quantity: Quantity.Create(1, QuantityUnit.Kilogram).Value);

            var result = inventory.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: true, expiryDate: null);

            Assert.True(result.IsSuccess);
            Assert.Null(item.QuantityValue);
            Assert.Null(item.QuantityUnit);
        }

        [Fact]
        public void UpdateItem_ClearQuantity_WinsOverProvidedQuantity()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour", quantity: Quantity.Create(1, QuantityUnit.Kilogram).Value);

            var result = inventory.UpdateItem(item.Id, text: null,
                quantity: Quantity.Create(5, QuantityUnit.Piece).Value, clearQuantity: true, expiryDate: null);

            Assert.True(result.IsSuccess);
            Assert.Null(item.QuantityValue);
            Assert.Null(item.QuantityUnit);
        }

        [Fact]
        public void UpdateItem_ExpiryDateNull_ClearsExistingValue()
        {
            // Documented asymmetry: ExpiryDate is write-through (null clears), while Text/Quantity
            // preserve on null. Matches the legacy mapping extension behaviour.
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Milk", expiryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3));

            var result = inventory.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, expiryDate: null);

            Assert.True(result.IsSuccess);
            Assert.Null(item.ExpiryDate);
        }

        [Fact]
        public void UpdateItem_ChangesAllFields()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour", quantity: Quantity.Create(1, QuantityUnit.Kilogram).Value);
            var newExpiry = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

            var result = inventory.UpdateItem(item.Id, "Sugar",
                Quantity.Create(2, QuantityUnit.Kilogram).Value, clearQuantity: false, expiryDate: newExpiry);

            Assert.True(result.IsSuccess);
            Assert.Equal("Sugar", item.Text);
            Assert.Equal(2m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Kilogram, item.QuantityUnit);
            Assert.Equal(newExpiry, item.ExpiryDate);
        }

        [Fact]
        public void UpdateItem_EmptyText_FailsKeyedOnText()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");

            var result = inventory.UpdateItem(item.Id, "  ", null, clearQuantity: false, expiryDate: null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(InventoryItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void UpdateItem_TextTooLong_FailsKeyedOnText()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            var tooLong = new string('x', InventoryItem.TextMaxLength + 1);

            var result = inventory.UpdateItem(item.Id, tooLong, null, clearQuantity: false, expiryDate: null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(InventoryItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void UpdateItem_StampsUpdatedAt()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            var before = item.UpdatedAt;

            var result = inventory.UpdateItem(item.Id, "Sugar", null, clearQuantity: false, expiryDate: null);

            Assert.True(result.IsSuccess);
            Assert.True(item.UpdatedAt > before);
        }

        // ------- RemoveItem -------

        [Fact]
        public void RemoveItem_SoftDeletesAndStampsUpdatedAt()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            var before = item.UpdatedAt;

            var result = inventory.RemoveItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.False(item.IsActive);
            Assert.True(item.UpdatedAt > before);
        }

        [Fact]
        public void RemoveItem_NotFound_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();

            var result = inventory.RemoveItem(itemId: 999);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveItem_AlreadyInactive_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            item.IsActive = false;

            var result = inventory.RemoveItem(item.Id);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        // ------- RestoreItem -------

        [Fact]
        public void RestoreItem_ReactivatesSoftDeletedItemAndStampsUpdatedAt()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            var before = item.UpdatedAt;

            var result = inventory.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.True(item.IsActive);
            Assert.Same(item, result.Value);
            Assert.True(item.UpdatedAt > before);
        }

        [Fact]
        public void RestoreItem_PreservesOriginalRank()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour", rank: "a5");
            item.IsActive = false;

            var result = inventory.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal("a5", item.Rank);
        }

        [Fact]
        public void RestoreItem_NotFound_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();

            var result = inventory.RestoreItem(itemId: 999);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RestoreItem_AlreadyActive_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour"); // active by default

            var result = inventory.RestoreItem(item.Id);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        // ------- ReorderItem -------

        [Fact]
        public void ReorderItem_MoveToTop_FromAfterIdZero_RanksBeforeFirst()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", rank: "a1");
            AddSeed(inventory, "Sugar", rank: "a2");
            var item3 = AddSeed(inventory, "Salt", rank: "a3");

            var result = inventory.ReorderItem(item3.Id, afterItemId: 0);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item3.Rank, item1.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_MidpointBetweenTwoItems_ProducesKeyStrictlyBetween()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", rank: "a0");
            var item2 = AddSeed(inventory, "Sugar", rank: "a1");
            var item3 = AddSeed(inventory, "Salt", rank: "a2");

            var result = inventory.ReorderItem(item3.Id, afterItemId: item1.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item1.Rank, item3.Rank) < 0);
            Assert.True(string.CompareOrdinal(item3.Rank, item2.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_AfterIsLast_RanksAfterLast()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", rank: "a0");
            AddSeed(inventory, "Sugar", rank: "a1");
            var item3 = AddSeed(inventory, "Salt", rank: "a2");

            var result = inventory.ReorderItem(item1.Id, afterItemId: item3.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item3.Rank, item1.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_UnknownAfterId_FallsBackToTopOfSection()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", rank: "a1");
            var item2 = AddSeed(inventory, "Sugar", rank: "a2");

            // afterItemId points to a non-existent item — legacy silently moves to top.
            var result = inventory.ReorderItem(item2.Id, afterItemId: 99_999);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(item2.Rank, item1.Rank) < 0);
        }

        [Fact]
        public void ReorderItem_SelfAnchor_NoOp()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour", rank: "a1");

            var result = inventory.ReorderItem(item.Id, afterItemId: item.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal("a1", item.Rank);
        }

        [Fact]
        public void ReorderItem_NotFound_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();

            var result = inventory.ReorderItem(itemId: 999, afterItemId: 0);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void ManyReordersIntoSameShrinkingSlot_NeverCollide()
        {
            var inventory = NewInventory();
            var top = AddSeed(inventory, "Top", rank: "a0");
            AddSeed(inventory, "Bottom", rank: "a1");

            // Repeatedly drop a fresh item into the same slot just below `top`; the gap shrinks each
            // time but a distinct key is always available (the old integer scheme collapsed to a
            // duplicate SortOrder after ~13 drops, which is the bug this whole change fixes).
            var ranks = new HashSet<string> { top.Rank };
            for (var i = 0; i < 20; i++)
            {
                var mover = AddSeed(inventory, $"Mover{i}");
                var r = inventory.ReorderItem(mover.Id, afterItemId: top.Id);
                Assert.True(r.IsSuccess);
                Assert.True(string.CompareOrdinal(top.Rank, mover.Rank) < 0);
                Assert.True(ranks.Add(mover.Rank), $"collision at iteration {i}: {mover.Rank}");
            }
        }

        // ------- Helpers -------

        private static Inventory NewInventory()
        {
            return new Inventory
            {
                Id = 1,
                Name = "Pantry",
                HouseholdId = HouseholdId,
                CreatedByUserId = CreatorId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsActive = true,
            };
        }

        private int _nextItemId = 100;

        private InventoryItem AddSeed(Inventory inventory, string text, Quantity? quantity = null, DateOnly? expiryDate = null, string? rank = null)
        {
            var item = new InventoryItem
            {
                Id = ++_nextItemId,
                InventoryId = inventory.Id,
                Text = text,
                QuantityValue = quantity?.Value,
                QuantityUnit = quantity?.Unit,
                ExpiryDate = expiryDate,
                Rank = rank ?? FractionalIndex.GenerateKeyBetween(
                    inventory.InventoryItems.Select(i => i.Rank).LastOrDefault(), null),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true,
            };
            inventory.InventoryItems.Add(item);
            return item;
        }
    }
}

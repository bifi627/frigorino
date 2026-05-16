using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;

namespace Frigorino.Test.Domain
{
    // Pure unit tests for the Inventory aggregate's InventoryItem coordination methods. No
    // DbContext. Covers the sort-order matrix (single section — no checked/unchecked split),
    // partial-update + expiryDate write-through semantics, and the validation/not-found paths
    // that route to ValidationProblem and NotFound at the slice handler layer.
    public class InventoryAggregateItemTests
    {
        private const string CreatorId = "user-creator";
        private const int HouseholdId = 42;

        // ------- AddItem -------

        [Fact]
        public void AddItem_FirstItem_GetsBaseSortOrder()
        {
            var inventory = NewInventory();

            var result = inventory.AddItem("Flour", null, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, result.Value.SortOrder);
            Assert.True(result.Value.IsActive);
            Assert.Equal(inventory.Id, result.Value.InventoryId);
        }

        [Fact]
        public void AddItem_AppendsBelowLast()
        {
            var inventory = NewInventory();
            inventory.AddItem("Flour", null, null);
            inventory.AddItem("Sugar", "1 kg", null);

            var third = inventory.AddItem("Salt", null, null);

            Assert.True(third.IsSuccess);
            var expected = SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap * 3;
            Assert.Equal(expected, third.Value.SortOrder);
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
        public void AddItem_QuantityTooLong_FailsKeyedOnQuantity()
        {
            var inventory = NewInventory();
            var tooLong = new string('x', InventoryItem.QuantityMaxLength + 1);

            var result = inventory.AddItem("Flour", tooLong, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(InventoryItem.Quantity), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void AddItem_TrimsTextAndQuantity()
        {
            var inventory = NewInventory();

            var result = inventory.AddItem("  Flour  ", "  2 kg  ", null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Flour", result.Value.Text);
            Assert.Equal("2 kg", result.Value.Quantity);
        }

        [Fact]
        public void AddItem_WhitespaceQuantity_NormalisedToNull()
        {
            var inventory = NewInventory();

            var result = inventory.AddItem("Flour", "   ", null);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Quantity);
        }

        [Fact]
        public void AddItem_WithExpiryDate_StoresIt()
        {
            var inventory = NewInventory();
            var expiry = DateTime.UtcNow.AddDays(5);

            var result = inventory.AddItem("Milk", null, expiry);

            Assert.True(result.IsSuccess);
            Assert.Equal(expiry, result.Value.ExpiryDate);
        }

        // ------- UpdateItem -------

        [Fact]
        public void UpdateItem_NotFound_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();

            var result = inventory.UpdateItem(itemId: 999, text: "x", quantity: null, expiryDate: null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void UpdateItem_InactiveItem_TreatedAsNotFound()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            item.IsActive = false;

            var result = inventory.UpdateItem(item.Id, "Sugar", null, null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void UpdateItem_PartialUpdate_PreservesUnsetTextAndQuantity()
        {
            var inventory = NewInventory();
            var existingExpiry = DateTime.UtcNow.AddDays(3);
            var item = AddSeed(inventory, "Flour", quantity: "1 kg", expiryDate: existingExpiry);

            // Text/Quantity null = preserve. ExpiryDate null = clear (write-through).
            var result = inventory.UpdateItem(item.Id, text: null, quantity: null, expiryDate: existingExpiry);

            Assert.True(result.IsSuccess);
            Assert.Equal("Flour", item.Text);
            Assert.Equal("1 kg", item.Quantity);
            Assert.Equal(existingExpiry, item.ExpiryDate);
        }

        [Fact]
        public void UpdateItem_ExpiryDateNull_ClearsExistingValue()
        {
            // Documented asymmetry: ExpiryDate is write-through (null clears), while Text/Quantity
            // preserve on null. Matches the legacy mapping extension behaviour.
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Milk", expiryDate: DateTime.UtcNow.AddDays(3));

            var result = inventory.UpdateItem(item.Id, text: null, quantity: null, expiryDate: null);

            Assert.True(result.IsSuccess);
            Assert.Null(item.ExpiryDate);
        }

        [Fact]
        public void UpdateItem_ChangesAllFields()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour", quantity: "1 kg");
            var newExpiry = DateTime.UtcNow.AddDays(10);

            var result = inventory.UpdateItem(item.Id, "Sugar", "2 kg", newExpiry);

            Assert.True(result.IsSuccess);
            Assert.Equal("Sugar", item.Text);
            Assert.Equal("2 kg", item.Quantity);
            Assert.Equal(newExpiry, item.ExpiryDate);
        }

        [Fact]
        public void UpdateItem_EmptyText_FailsKeyedOnText()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");

            var result = inventory.UpdateItem(item.Id, "  ", null, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(InventoryItem.Text), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void UpdateItem_TextTooLong_FailsKeyedOnText()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            var tooLong = new string('x', InventoryItem.TextMaxLength + 1);

            var result = inventory.UpdateItem(item.Id, tooLong, null, null);

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

            var result = inventory.UpdateItem(item.Id, "Sugar", null, null);

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

        // ------- ReorderItem -------

        [Fact]
        public void ReorderItem_MoveToTop_FromAfterIdZero()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", sortOrder: 1_010_000);
            AddSeed(inventory, "Sugar", sortOrder: 1_020_000);
            var item3 = AddSeed(inventory, "Salt", sortOrder: 1_030_000);

            var result = inventory.ReorderItem(item3.Id, afterItemId: 0);

            Assert.True(result.IsSuccess);
            Assert.Equal(item1.SortOrder - SortOrderCalculator.DefaultGap, item3.SortOrder);
        }

        [Fact]
        public void ReorderItem_MidpointBetweenTwoItems()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", sortOrder: 100_000);
            AddSeed(inventory, "Sugar", sortOrder: 102_000);
            var item3 = AddSeed(inventory, "Salt", sortOrder: 104_000);

            var result = inventory.ReorderItem(item3.Id, afterItemId: item1.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(101_000, item3.SortOrder);
        }

        [Fact]
        public void ReorderItem_AfterIsLast_AppendsWithGap()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", sortOrder: 100_000);
            AddSeed(inventory, "Sugar", sortOrder: 102_000);
            var item3 = AddSeed(inventory, "Salt", sortOrder: 104_000);

            var result = inventory.ReorderItem(item1.Id, afterItemId: item3.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(item3.SortOrder + SortOrderCalculator.DefaultGap, item1.SortOrder);
        }

        [Fact]
        public void ReorderItem_UnknownAfterId_FallsBackToTopOfSection()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", sortOrder: 1_010_000);
            var item2 = AddSeed(inventory, "Sugar", sortOrder: 1_020_000);

            // afterItemId points to a non-existent item — legacy silently moves to top.
            var result = inventory.ReorderItem(item2.Id, afterItemId: 99_999);

            Assert.True(result.IsSuccess);
            Assert.Equal(item1.SortOrder - SortOrderCalculator.DefaultGap, item2.SortOrder);
        }

        [Fact]
        public void ReorderItem_SelfAnchor_NoOp()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour", sortOrder: 1_010_000);

            var result = inventory.ReorderItem(item.Id, afterItemId: item.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(1_010_000, item.SortOrder);
        }

        [Fact]
        public void ReorderItem_NotFound_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();

            var result = inventory.ReorderItem(itemId: 999, afterItemId: 0);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        // ------- CompactItems -------

        [Fact]
        public void CompactItems_RewritesSortOrdersWithCleanGaps()
        {
            var inventory = NewInventory();
            var item1 = AddSeed(inventory, "Flour", sortOrder: 100_001);
            var item2 = AddSeed(inventory, "Sugar", sortOrder: 100_002);
            var item3 = AddSeed(inventory, "Salt", sortOrder: 200_777);

            var result = inventory.CompactItems();

            Assert.True(result.IsSuccess);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange, item1.SortOrder);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, item2.SortOrder);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + 2 * SortOrderCalculator.DefaultGap, item3.SortOrder);
        }

        [Fact]
        public void CompactItems_EmptyInventory_NoOp()
        {
            var inventory = NewInventory();

            var result = inventory.CompactItems();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void CompactItems_PreservesOrder()
        {
            var inventory = NewInventory();
            var first = AddSeed(inventory, "Flour", sortOrder: 50);
            var second = AddSeed(inventory, "Sugar", sortOrder: 200);
            var third = AddSeed(inventory, "Salt", sortOrder: 999);

            var result = inventory.CompactItems();

            Assert.True(result.IsSuccess);
            Assert.True(first.SortOrder < second.SortOrder);
            Assert.True(second.SortOrder < third.SortOrder);
        }

        [Fact]
        public void CompactItems_SkipsInactiveItems()
        {
            var inventory = NewInventory();
            AddSeed(inventory, "Active", sortOrder: 100_000);
            var inactive = AddSeed(inventory, "Inactive", sortOrder: 99);
            inactive.IsActive = false;

            var result = inventory.CompactItems();

            Assert.True(result.IsSuccess);
            Assert.Equal(99, inactive.SortOrder);
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

        private InventoryItem AddSeed(Inventory inventory, string text, string? quantity = null, DateTime? expiryDate = null, int? sortOrder = null)
        {
            var item = new InventoryItem
            {
                Id = ++_nextItemId,
                InventoryId = inventory.Id,
                Text = text,
                Quantity = quantity,
                ExpiryDate = expiryDate,
                SortOrder = sortOrder ?? SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true,
            };
            inventory.InventoryItems.Add(item);
            return item;
        }
    }
}

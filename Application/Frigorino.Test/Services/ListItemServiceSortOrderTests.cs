using Frigorino.Application.Services;
using Frigorino.Application.Utilities;
using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Frigorino.Test.Services
{
    public class ListItemServiceSortOrderTests : IDisposable
    {
        private readonly TestApplicationDbContext _dbContext;
        private readonly ListItemService _service;
        private readonly string _testUserId = "test-user-123";
        private readonly int _testHouseholdId = 1;
        private readonly int _testListId = 1;

        public ListItemServiceSortOrderTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options;

            _dbContext = new TestApplicationDbContext(options);

            // Ensure the database is created
            _dbContext.Database.EnsureCreated();

            _service = new ListItemService(_dbContext);

            SeedTestData();
        }

        private void SeedTestData()
        {
            // Add test user
            _dbContext.Users.Add(new User
            {
                ExternalId = _testUserId,
                Name = "Test User",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            });

            // Add test household
            _dbContext.Households.Add(new Household
            {
                Id = _testHouseholdId,
                Name = "Test Household",
                CreatedByUserId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // Add user to household
            _dbContext.UserHouseholds.Add(new UserHousehold
            {
                UserId = _testUserId,
                HouseholdId = _testHouseholdId,
                Role = HouseholdRole.Owner,
                JoinedAt = DateTime.UtcNow
            });

            // Add test list
            _dbContext.Lists.Add(new List
            {
                Id = _testListId,
                Name = "Test List",
                HouseholdId = _testHouseholdId,
                CreatedByUserId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task CreateItemAsync_FirstItem_ShouldGetNewItemSortOrder()
        {
            // Arrange
            var request = new CreateListItemRequest
            {
                Text = "First item",
                Quantity = "1"
            };

            // Act
            var result = await _service.CreateItemAsync(_testListId, request, _testUserId);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Status, "Should be unchecked by default");
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, result.SortOrder);
        }

        [Fact]
        public async Task CreateItemAsync_MultipleItems_Unchecked_ShouldGetAscendingSortOrder()
        {
            // Arrange
            var request1 = new CreateListItemRequest { Text = "Item 1", Quantity = "1" };
            var request2 = new CreateListItemRequest { Text = "Item 2", Quantity = "2" };
            var request3 = new CreateListItemRequest { Text = "Item 3", Quantity = "3" };

            // Act
            var item1 = await _service.CreateItemAsync(_testListId, request1, _testUserId);
            var item2 = await _service.CreateItemAsync(_testListId, request2, _testUserId);
            var item3 = await _service.CreateItemAsync(_testListId, request3, _testUserId);

            // All unchecked items should have the same sort order (new items go to top)
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, item1.SortOrder);
            Assert.Equal(item1.SortOrder + SortOrderCalculator.DefaultGap, item2.SortOrder);
            Assert.Equal(item2.SortOrder + SortOrderCalculator.DefaultGap, item3.SortOrder);
        }

        [Fact]
        public async Task ToggleItemStatusAsync_UncheckedToChecked_ShouldMoveToCheckedSection()
        {
            // Arrange
            var request = new CreateListItemRequest { Text = "Test item", Quantity = "1" };
            var item = await _service.CreateItemAsync(_testListId, request, _testUserId);

            // Act
            var toggledItem = await _service.ToggleItemStatusAsync(item.Id, _testUserId);

            // Assert
            Assert.NotNull(toggledItem);
            Assert.True(toggledItem.Status);
            Assert.Equal(SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap, toggledItem.SortOrder);
        }

        [Fact]
        public async Task ToggleItemStatusAsync_CheckedToUnchecked_ShouldMoveToUncheckedSection()
        {
            // Arrange
            var request = new CreateListItemRequest { Text = "Test item", Quantity = "1" };
            var item = await _service.CreateItemAsync(_testListId, request, _testUserId);

            // First toggle to checked
            await _service.ToggleItemStatusAsync(item.Id, _testUserId);

            // Act - Toggle back to unchecked
            var toggledItem = await _service.ToggleItemStatusAsync(item.Id, _testUserId);

            // Assert
            Assert.NotNull(toggledItem);
            Assert.False(toggledItem.Status);
            Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, toggledItem.SortOrder);
        }

        [Fact]
        public async Task UpdateItemAsync_StatusChange_Check_ShouldUpdateSortOrder()
        {
            // Arrange
            var request = new CreateListItemRequest { Text = "Test item", Quantity = "1" };
            var item = await _service.CreateItemAsync(_testListId, request, _testUserId);

            var updateRequest = new UpdateListItemRequest
            {
                Text = "Updated item",
                Quantity = "2",
                Status = true // Change from unchecked to checked
            };

            // Act
            var updatedItem = await _service.UpdateItemAsync(item.Id, updateRequest, _testUserId);

            // Assert
            Assert.NotNull(updatedItem);
            Assert.True(updatedItem.Status);
            Assert.Equal(SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap, updatedItem.SortOrder);
            Assert.Equal("Updated item", updatedItem.Text);
            Assert.Equal("2", updatedItem.Quantity);
        }

        [Fact]
        public async Task UpdateItemAsync_StatusChange_Uncheck_ShouldUpdateSortOrder()
        {
            // Arrange
            var request = new CreateListItemRequest { Text = "Test item", Quantity = "1" };
            var item = await _service.CreateItemAsync(_testListId, request, _testUserId);

            var updateRequest = new UpdateListItemRequest
            {
                Text = "Updated item",
                Quantity = "2",
                Status = true // Change from unchecked to checked
            };

            // Act
            var updatedItem = await _service.UpdateItemAsync(item.Id, updateRequest, _testUserId);

            // Assert
            Assert.NotNull(updatedItem);
            Assert.True(updatedItem.Status);
            Assert.Equal(SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap, updatedItem.SortOrder);
            Assert.Equal("Updated item", updatedItem.Text);
            Assert.Equal("2", updatedItem.Quantity);
        }

        [Fact]
        public async Task UpdateItemAsync_StatusChange_Uncheck_ShouldUpdateSortOrder_WithExistingItems()
        {
            // Arrange
            var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 1" }, _testUserId);
            var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 2" }, _testUserId);
            var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 3" }, _testUserId);

            var item4 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 4" }, _testUserId);
            var item5 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 5" }, _testUserId);
            var item6 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 6" }, _testUserId);

            await _service.UpdateItemAsync(item4.Id, new UpdateListItemRequest() { Status = true }, _testUserId);
            await _service.UpdateItemAsync(item5.Id, new UpdateListItemRequest() { Status = true }, _testUserId);
            await _service.UpdateItemAsync(item6.Id, new UpdateListItemRequest() { Status = true }, _testUserId);

            var dbItem1 = await _dbContext.ListItems.FindAsync(item1.Id);
            var dbItem2 = await _dbContext.ListItems.FindAsync(item2.Id);
            var dbItem3 = await _dbContext.ListItems.FindAsync(item3.Id);
            var dbItem4 = await _dbContext.ListItems.FindAsync(item4.Id);
            var dbItem5 = await _dbContext.ListItems.FindAsync(item5.Id);
            var dbItem6 = await _dbContext.ListItems.FindAsync(item6.Id);

            //// Act
            //var updatedItem = await _service.UpdateItemAsync(item.Id, updateRequest, _testUserId);

            //// Assert
            //Assert.NotNull(updatedItem);
            //Assert.False(updatedItem.Status);
            //Assert.Equal(SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap, updatedItem.SortOrder);
            //Assert.Equal("Updated item", updatedItem.Text);
            //Assert.Equal("2", updatedItem.Quantity);
        }

        [Fact]
        public async Task ReorderItemAsync_MoveToTop_ShouldGetTopSortOrder()
        {
            // Arrange
            // Create multiple items
            var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 1" }, _testUserId);
            var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 2" }, _testUserId);
            var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 3" }, _testUserId);

            // Set up different sort orders manually to simulate existing items
            var dbItem1 = await _dbContext.ListItems.FindAsync(item1.Id);
            var dbItem2 = await _dbContext.ListItems.FindAsync(item2.Id);
            var dbItem3 = await _dbContext.ListItems.FindAsync(item3.Id);

            var reorderRequest = new ReorderItemRequest { AfterId = 0 }; // Move to top

            // Act
            var reorderedItem = await _service.ReorderItemAsync(item3.Id, reorderRequest, _testUserId);

            // Assert
            Assert.NotNull(reorderedItem);
            Assert.Equal(dbItem1!.SortOrder - SortOrderCalculator.DefaultGap, reorderedItem.SortOrder);
        }

        [Fact]
        public async Task ReorderItemAsync_MoveBetweenItems_ShouldCalculateMidpoint()
        {
            // Arrange
            // Create items with specific sort orders
            var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 1" }, _testUserId);
            var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 2" }, _testUserId);
            var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 3" }, _testUserId);

            // Set up sort orders with gaps
            var dbItem1 = await _dbContext.ListItems.FindAsync(item1.Id);
            var dbItem2 = await _dbContext.ListItems.FindAsync(item2.Id);
            var dbItem3 = await _dbContext.ListItems.FindAsync(item3.Id);

            dbItem1!.SortOrder = 100_000;
            dbItem2!.SortOrder = 102_000; // Gap of 2000
            dbItem3!.SortOrder = 104_000;
            await _dbContext.SaveChangesAsync();

            // Move item3 between item1 and item2
            var reorderRequest = new ReorderItemRequest { AfterId = dbItem1.Id };

            // Act
            var reorderedItem = await _service.ReorderItemAsync(item3.Id, reorderRequest, _testUserId);

            // Assert
            Assert.NotNull(reorderedItem);
            Assert.Equal(101_000, reorderedItem.SortOrder); // Midpoint between 100_000 and 102_000
        }


        //[Fact]
        //public async Task GetItemsByListIdAsync_ShouldReturnItemsInSortOrder()
        //{
        //    // Arrange
        //    // Create items and set different sort orders
        //    var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 1" }, _testUserId);
        //    var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 2" }, _testUserId);
        //    var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 3" }, _testUserId);

        //    // Set different sort orders
        //    var dbItem1 = await _dbContext.ListItems.FindAsync(item1.Id);
        //    var dbItem2 = await _dbContext.ListItems.FindAsync(item2.Id);
        //    var dbItem3 = await _dbContext.ListItems.FindAsync(item3.Id);

        //    dbItem1!.SortOrder = 102_000;
        //    dbItem2!.SortOrder = 100_000; // Should be first
        //    dbItem3!.SortOrder = 101_000;
        //    await _dbContext.SaveChangesAsync();

        //    // Act
        //    var items = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
        //    var itemsList = items.ToList();

        //    // Assert
        //    Assert.Equal(3, itemsList.Count);
        //    Assert.Equal(item2.Id, itemsList[0].Id); // 100_000
        //    Assert.Equal(item3.Id, itemsList[1].Id); // 101_000
        //    Assert.Equal(item1.Id, itemsList[2].Id); // 102_000
        //}

        //[Fact]
        //public async Task CompactListSortOrdersAsync_ShouldResetToCleanGaps()
        //{
        //    // Arrange
        //    // Create items with messy sort orders
        //    var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 1" }, _testUserId);
        //    var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 2" }, _testUserId);
        //    var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 3" }, _testUserId);

        //    // Set messy sort orders (some checked, some unchecked)
        //    var dbItem1 = await _dbContext.ListItems.FindAsync(item1.Id);
        //    var dbItem2 = await _dbContext.ListItems.FindAsync(item2.Id);
        //    var dbItem3 = await _dbContext.ListItems.FindAsync(item3.Id);

        //    dbItem1!.SortOrder = 100_050; // Unchecked
        //    dbItem1.Status = false;

        //    dbItem2!.SortOrder = 100_075; // Unchecked  
        //    dbItem2.Status = false;

        //    dbItem3!.SortOrder = 1_100_025; // Checked
        //    dbItem3.Status = true;

        //    await _dbContext.SaveChangesAsync();

        //    // Act
        //    var result = await _service.CompactListSortOrdersAsync(_testListId, _testUserId);

        //    // Assert
        //    Assert.True(result);

        //    var compactedItems = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
        //    var itemsList = compactedItems.ToList();

        //    // Check unchecked items (should be first two)
        //    var uncheckedItems = itemsList.Where(i => !i.Status).OrderBy(i => i.SortOrder).ToList();
        //    Assert.Equal(2, uncheckedItems.Count);
        //    Assert.Equal(100_000, uncheckedItems[0].SortOrder);
        //    Assert.Equal(101_000, uncheckedItems[1].SortOrder);

        //    // Check checked items (should be last one)
        //    var checkedItems = itemsList.Where(i => i.Status).OrderBy(i => i.SortOrder).ToList();
        //    Assert.Single(checkedItems);
        //    Assert.Equal(1_100_000, checkedItems[0].SortOrder);
        //}

        //[Fact]
        //public async Task MixedStatusItems_ShouldMaintainSeparateSortRanges()
        //{
        //    // Arrange
        //    var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Unchecked 1" }, _testUserId);
        //    var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Unchecked 2" }, _testUserId);

        //    // Toggle one to checked
        //    var checkedItem = await _service.ToggleItemStatusAsync(item1.Id, _testUserId);

        //    var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Unchecked 3" }, _testUserId);

        //    // Act
        //    var allItems = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
        //    var itemsList = allItems.ToList();

        //    // Assert
        //    var uncheckedItems = itemsList.Where(i => !i.Status).ToList();
        //    var checkedItems = itemsList.Where(i => i.Status).ToList();

        //    Assert.Equal(2, uncheckedItems.Count);
        //    Assert.Single(checkedItems);

        //    // All unchecked items should have sort order < checked items
        //    var maxUncheckedSort = uncheckedItems.Max(i => i.SortOrder);
        //    var minCheckedSort = checkedItems.Min(i => i.SortOrder);

        //    Assert.True(maxUncheckedSort < minCheckedSort, 
        //        $"Unchecked items max sort order ({maxUncheckedSort}) should be less than checked items min sort order ({minCheckedSort})");
        //}

        public void Dispose()
        {
            _dbContext?.Dispose();
        }
    }
}

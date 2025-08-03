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
    public class ListItemServiceWorkflowTests : IDisposable
    {
        private readonly TestApplicationDbContext _dbContext;
        private readonly ListItemService _service;
        private readonly string _testUserId = "workflow-user-123";
        private readonly int _testHouseholdId = 1;
        private readonly int _testListId = 1;

        public ListItemServiceWorkflowTests()
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
            _dbContext.Users.Add(new User
            {
                ExternalId = _testUserId,
                Name = "Workflow Test User",
                Email = "workflow@example.com",
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            });

            _dbContext.Households.Add(new Household
            {
                Id = _testHouseholdId,
                Name = "Workflow Test Household",
                CreatedByUserId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _dbContext.UserHouseholds.Add(new UserHousehold
            {
                UserId = _testUserId,
                HouseholdId = _testHouseholdId,
                Role = HouseholdRole.Owner,
                JoinedAt = DateTime.UtcNow
            });

            _dbContext.Lists.Add(new List
            {
                Id = _testListId,
                Name = "Workflow Test List",
                HouseholdId = _testHouseholdId,
                CreatedByUserId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task TypicalShoppingListWorkflow_ShouldMaintainCorrectOrder()
        {
            // Scenario: User creates a shopping list, adds items, checks some off, reorders, and adds more items

            // Step 1: Add initial items
            var milk = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Milk", Quantity = "1 gallon" }, _testUserId);
            var bread = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Bread", Quantity = "1 loaf" }, _testUserId);
            var eggs = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Eggs", Quantity = "12 count" }, _testUserId);

            // Verify: All items should be unchecked with same sort order (newest items go to top)
            var items = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
            var itemsList = items.ToList();
            Assert.Equal(3, itemsList.Count);
            Assert.All(itemsList, item => Assert.False(item.Status));
            Assert.All(itemsList, item => Assert.Equal(SortOrderCalculator.GetNewItemSortOrder(), item.SortOrder));

            // Step 2: Check off milk (mark as completed)
            var checkedMilk = await _service.ToggleItemStatusAsync(milk.Id, _testUserId);
            Assert.True(checkedMilk!.Status);
            Assert.Equal(SortOrderCalculator.GetCheckedStatusSortOrder(), checkedMilk.SortOrder);

            // Step 3: Verify order - unchecked items first, then checked items
            items = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
            itemsList = items.ToList();
            
            var uncheckedItems = itemsList.Where(i => !i.Status).ToList();
            var checkedItems = itemsList.Where(i => i.Status).ToList();
            
            Assert.Equal(2, uncheckedItems.Count);
            Assert.Single(checkedItems);
            
            // All unchecked should come before all checked
            var minCheckedSort = checkedItems.Min(i => i.SortOrder);
            var maxUncheckedSort = uncheckedItems.Max(i => i.SortOrder);
            Assert.True(maxUncheckedSort < minCheckedSort);

            // Step 4: Add more items while shopping
            var butter = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Butter", Quantity = "1 stick" }, _testUserId);
            var cheese = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Cheese", Quantity = "1 block" }, _testUserId);

            // Step 5: Check off bread and eggs
            await _service.ToggleItemStatusAsync(bread.Id, _testUserId);
            await _service.ToggleItemStatusAsync(eggs.Id, _testUserId);

            // Step 6: Final verification
            items = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
            itemsList = items.ToList();
            
            uncheckedItems = itemsList.Where(i => !i.Status).OrderBy(i => i.SortOrder).ToList();
            checkedItems = itemsList.Where(i => i.Status).OrderBy(i => i.SortOrder).ToList();

            // Should have 2 unchecked (butter, cheese) and 3 checked (milk, bread, eggs)
            Assert.Equal(2, uncheckedItems.Count);
            Assert.Equal(3, checkedItems.Count);

            // Verify separation
            Assert.True(uncheckedItems.Max(i => i.SortOrder) < checkedItems.Min(i => i.SortOrder));
        }

        [Fact]
        public async Task ReorderingWithinSections_ShouldWorkCorrectly()
        {
            // Create multiple unchecked items
            var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 1" }, _testUserId);
            var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 2" }, _testUserId);
            var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 3" }, _testUserId);
            var item4 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 4" }, _testUserId);

            // Set up distinct sort orders for testing
            var dbItems = await _dbContext.ListItems.Where(li => li.ListId == _testListId).ToListAsync();
            dbItems[0].SortOrder = 100_000; // item1
            dbItems[1].SortOrder = 101_000; // item2
            dbItems[2].SortOrder = 102_000; // item3
            dbItems[3].SortOrder = 103_000; // item4
            await _dbContext.SaveChangesAsync();

            // Move item4 to position between item1 and item2
            await _service.ReorderItemAsync(item4.Id, new ReorderItemRequest { AfterItemId = item1.Id }, _testUserId);

            // Verify new order
            var items = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
            var orderedItems = items.OrderBy(i => i.SortOrder).ToList();

            Assert.Equal(item1.Id, orderedItems[0].Id); // 100,000
            Assert.Equal(item4.Id, orderedItems[1].Id); // 100,500 (midpoint between 100,000 and 101,000)
            Assert.Equal(item2.Id, orderedItems[2].Id); // 101,000
            Assert.Equal(item3.Id, orderedItems[3].Id); // 102,000

            // Verify the midpoint calculation
            var reorderedItem4 = orderedItems[1];
            Assert.Equal(100_500, reorderedItem4.SortOrder);
        }

        [Fact]
        public async Task CompactionNeeded_ShouldBeDetectedAndHandled()
        {
            // Create items with very small gaps to trigger compaction need
            var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 1" }, _testUserId);
            var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 2" }, _testUserId);
            var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 3" }, _testUserId);

            // Manually set very small gaps
            var dbItems = await _dbContext.ListItems.Where(li => li.ListId == _testListId).ToListAsync();
            dbItems[0].SortOrder = 100_000;
            dbItems[1].SortOrder = 100_050; // Gap of 50 (less than threshold of 100)
            dbItems[2].SortOrder = 100_075; // Gap of 25
            await _dbContext.SaveChangesAsync();

            // Check if compaction is needed
            var sortOrders = dbItems.Select(i => i.SortOrder).ToList();
            Assert.True(SortOrderCalculator.NeedsCompaction(sortOrders));

            // Perform compaction
            var compactionResult = await _service.CompactListSortOrdersAsync(_testListId, _testUserId);
            Assert.True(compactionResult);

            // Verify clean gaps after compaction
            var compactedItems = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
            var compactedList = compactedItems.OrderBy(i => i.SortOrder).ToList();

            Assert.Equal(100_000, compactedList[0].SortOrder);
            Assert.Equal(101_000, compactedList[1].SortOrder);
            Assert.Equal(102_000, compactedList[2].SortOrder);

            // Verify no compaction is needed after cleanup
            var newSortOrders = compactedList.Select(i => i.SortOrder).ToList();
            Assert.False(SortOrderCalculator.NeedsCompaction(newSortOrders));
        }

        [Fact]
        public async Task MixedStatusCompaction_ShouldSeparateCorrectly()
        {
            // Create mixed status items with messy sort orders
            var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Unchecked 1" }, _testUserId);
            var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Unchecked 2" }, _testUserId);
            var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Will be checked" }, _testUserId);
            var item4 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Unchecked 3" }, _testUserId);

            // Check item3
            await _service.ToggleItemStatusAsync(item3.Id, _testUserId);

            // Create messy sort orders
            var dbItems = await _dbContext.ListItems.Where(li => li.ListId == _testListId).ToListAsync();
            foreach (var item in dbItems)
            {
                if (!item.Status)
                {
                    item.SortOrder = Random.Shared.Next(100_000, 150_000);
                }
                else
                {
                    item.SortOrder = Random.Shared.Next(1_100_000, 1_150_000);
                }
            }
            await _dbContext.SaveChangesAsync();

            // Perform compaction
            await _service.CompactListSortOrdersAsync(_testListId, _testUserId);

            // Verify proper separation and ordering
            var items = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
            var itemsList = items.ToList();

            var uncheckedItems = itemsList.Where(i => !i.Status).OrderBy(i => i.SortOrder).ToList();
            var checkedItems = itemsList.Where(i => i.Status).OrderBy(i => i.SortOrder).ToList();

            Assert.Equal(3, uncheckedItems.Count);
            Assert.Single(checkedItems);

            // Verify unchecked items are in clean sequence
            Assert.Equal(100_000, uncheckedItems[0].SortOrder);
            Assert.Equal(101_000, uncheckedItems[1].SortOrder);
            Assert.Equal(102_000, uncheckedItems[2].SortOrder);

            // Verify checked item is in correct range
            Assert.Equal(1_100_000, checkedItems[0].SortOrder);

            // Verify separation
            Assert.True(uncheckedItems.Max(i => i.SortOrder) < checkedItems.Min(i => i.SortOrder));
        }

        [Fact]
        public async Task ReorderToTop_MultipleTimes_ShouldAllGetSameSortOrder()
        {
            // Create items
            var item1 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 1" }, _testUserId);
            var item2 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 2" }, _testUserId);
            var item3 = await _service.CreateItemAsync(_testListId, new CreateListItemRequest { Text = "Item 3" }, _testUserId);

            // Set initial different sort orders
            var dbItems = await _dbContext.ListItems.Where(li => li.ListId == _testListId).ToListAsync();
            dbItems[0].SortOrder = 101_000;
            dbItems[1].SortOrder = 102_000;
            dbItems[2].SortOrder = 103_000;
            await _dbContext.SaveChangesAsync();

            // Move all to top one by one
            await _service.ReorderItemAsync(item2.Id, new ReorderItemRequest { AfterItemId = 0 }, _testUserId);
            await _service.ReorderItemAsync(item3.Id, new ReorderItemRequest { AfterItemId = 0 }, _testUserId);
            await _service.ReorderItemAsync(item1.Id, new ReorderItemRequest { AfterItemId = 0 }, _testUserId);

            // All should have the same "top" sort order
            var items = await _service.GetItemsByListIdAsync(_testListId, _testUserId);
            var itemsList = items.ToList();

            var topSortOrder = SortOrderCalculator.GetNewItemSortOrder();
            Assert.All(itemsList, item => Assert.Equal(topSortOrder, item.SortOrder));
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
        }
    }
}

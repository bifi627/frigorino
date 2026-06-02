using Frigorino.Domain.Entities;
using Frigorino.Features.Inventories;

namespace Frigorino.Test.Features
{
    public class InventoryResponseProjectionTests
    {
        private static Inventory BuildInventory(params InventoryItem[] items)
        {
            return new Inventory
            {
                Id = 1,
                Name = "Fridge",
                Description = null,
                HouseholdId = 1,
                CreatedByUser = new User
                {
                    ExternalId = "u1",
                    Name = "Tester",
                    Email = "t@example.com",
                },
                InventoryItems = items.ToList(),
            };
        }

        [Fact]
        public void EarliestExpiryDate_IsMinAmongActiveItemsWithDate()
        {
            var inventory = BuildInventory(
                new InventoryItem { IsActive = true, ExpiryDate = new DateOnly(2026, 7, 1) },
                new InventoryItem { IsActive = true, ExpiryDate = new DateOnly(2026, 6, 10) },
                new InventoryItem { IsActive = true, ExpiryDate = null });

            var result = InventoryResponse.ToProjection.Compile().Invoke(inventory);

            Assert.Equal(new DateOnly(2026, 6, 10), result.EarliestExpiryDate);
        }

        [Fact]
        public void EarliestExpiryDate_IgnoresInactiveItems()
        {
            var inventory = BuildInventory(
                new InventoryItem { IsActive = false, ExpiryDate = new DateOnly(2026, 1, 1) },
                new InventoryItem { IsActive = true, ExpiryDate = new DateOnly(2026, 6, 10) });

            var result = InventoryResponse.ToProjection.Compile().Invoke(inventory);

            Assert.Equal(new DateOnly(2026, 6, 10), result.EarliestExpiryDate);
        }

        [Fact]
        public void EarliestExpiryDate_IsNullWhenNoItemHasDate()
        {
            var inventory = BuildInventory(
                new InventoryItem { IsActive = true, ExpiryDate = null });

            var result = InventoryResponse.ToProjection.Compile().Invoke(inventory);

            Assert.Null(result.EarliestExpiryDate);
        }
    }
}

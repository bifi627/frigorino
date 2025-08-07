using Frigorino.Domain.DTOs;
using Frigorino.Domain.Entities;

namespace Frigorino.Application.Extensions
{
    public static class InventoryMappingExtensions
    {
        public static InventoryDto ToDto(this Inventory inventory)
        {
            return new InventoryDto
            {
                Id = inventory.Id,
                Name = inventory.Name,
                Description = inventory.Description,
                HouseholdId = inventory.HouseholdId,
                CreatedAt = inventory.CreatedAt,
                UpdatedAt = inventory.UpdatedAt,
                CreatedByUser = inventory.CreatedByUser.ToDto(),
                TotalItems = inventory.InventoryItems.Where(ii => ii.IsActive).Count(),
                ExpiringItems = inventory.InventoryItems.Where(ii => ii.IsActive && ii.ExpiryDate.HasValue && ii.ExpiryDate.Value <= DateTime.UtcNow.AddDays(7)).Count()
            };
        }

        public static Inventory ToEntity(this CreateInventoryRequest request, int householdId, string userId)
        {
            return new Inventory
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                HouseholdId = householdId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        public static void UpdateFromRequest(this Inventory inventory, UpdateInventoryRequest request)
        {
            inventory.Name = request.Name.Trim();
            inventory.Description = request.Description?.Trim();
            inventory.UpdatedAt = DateTime.UtcNow;
        }
    }
}

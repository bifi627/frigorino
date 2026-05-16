using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Inventories
{
    public sealed record InventoryResponse(
        int Id,
        string Name,
        string? Description,
        int HouseholdId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        InventoryCreatorResponse CreatedByUser,
        int TotalItems,
        int ExpiringItems)
    {
        // Threshold (in days) for the "expiring soon" highlight on inventory items. Shared by
        // both the aggregate-count projection here and InventoryItemResponse.IsExpiring so the
        // overview card count and per-item flag stay in sync.
        public const int ExpiringWithinDays = 7;

        public static InventoryResponse From(Inventory inventory, User creator, int totalItems, int expiringItems)
        {
            return new InventoryResponse(
                inventory.Id,
                inventory.Name,
                inventory.Description,
                inventory.HouseholdId,
                inventory.CreatedAt,
                inventory.UpdatedAt,
                new InventoryCreatorResponse(creator.ExternalId, creator.Name, creator.Email),
                totalItems,
                expiringItems);
        }

        // EF-translatable projection used by read slices (GetInventory, GetInventories). Lifted
        // out of both queries so the shape stays in one place; expression body must stay simple
        // enough for EF to translate (no method calls outside Count, no captured variables).
        // `DateTime.UtcNow.AddDays(...)` lives inside the expression tree, so EF translates it
        // to the SQL side's `now()` per query — each query computes its own threshold.
        public static readonly Expression<Func<Inventory, InventoryResponse>> ToProjection = i => new InventoryResponse(
            i.Id,
            i.Name,
            i.Description,
            i.HouseholdId,
            i.CreatedAt,
            i.UpdatedAt,
            new InventoryCreatorResponse(i.CreatedByUser.ExternalId, i.CreatedByUser.Name, i.CreatedByUser.Email),
            i.InventoryItems.Count(x => x.IsActive),
            i.InventoryItems.Count(x => x.IsActive && x.ExpiryDate.HasValue && x.ExpiryDate.Value <= DateTime.UtcNow.AddDays(ExpiringWithinDays)));
    }

    public sealed record InventoryCreatorResponse(
        string ExternalId,
        string Name,
        string? Email);
}

using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Inventories.Items
{
    public sealed record InventoryItemResponse(
        int Id,
        int InventoryId,
        string Text,
        string? Quantity,
        DateTime? ExpiryDate,
        int SortOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool IsExpiring)
    {
        public static InventoryItemResponse From(InventoryItem item)
        {
            return new InventoryItemResponse(
                item.Id,
                item.InventoryId,
                item.Text,
                item.Quantity,
                item.ExpiryDate,
                item.SortOrder,
                item.CreatedAt,
                item.UpdatedAt,
                item.ExpiryDate.HasValue && item.ExpiryDate.Value <= DateTime.UtcNow.AddDays(InventoryResponse.ExpiringWithinDays));
        }

        // EF-translatable projection used by read slices. Stays simple enough for EF (no
        // method calls outside the inline date comparison, no captured variables).
        public static readonly Expression<Func<InventoryItem, InventoryItemResponse>> ToProjection = i => new InventoryItemResponse(
            i.Id,
            i.InventoryId,
            i.Text,
            i.Quantity,
            i.ExpiryDate,
            i.SortOrder,
            i.CreatedAt,
            i.UpdatedAt,
            i.ExpiryDate.HasValue && i.ExpiryDate.Value <= DateTime.UtcNow.AddDays(InventoryResponse.ExpiringWithinDays));
    }
}

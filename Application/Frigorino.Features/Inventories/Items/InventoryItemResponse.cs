using System.Linq.Expressions;
using Frigorino.Domain.Entities;
using Frigorino.Features.Quantities;

namespace Frigorino.Features.Inventories.Items
{
    public sealed record InventoryItemResponse(
        int Id,
        int InventoryId,
        string Text,
        QuantityDto? Quantity,
        DateOnly? ExpiryDate,
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
                item.QuantityValue == null
                    ? null
                    : new QuantityDto(item.QuantityValue.Value, item.QuantityUnit!.Value),
                item.ExpiryDate,
                item.SortOrder,
                item.CreatedAt,
                item.UpdatedAt,
                item.ExpiryDate.HasValue && item.ExpiryDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(InventoryResponse.ExpiringWithinDays));
        }

        // EF-translatable projection used by read slices. Stays simple enough for EF (no
        // method calls outside the inline date comparison, no captured variables).
        public static readonly Expression<Func<InventoryItem, InventoryItemResponse>> ToProjection = i => new InventoryItemResponse(
            i.Id,
            i.InventoryId,
            i.Text,
            i.QuantityValue == null
                ? null
                : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.ExpiryDate,
            i.SortOrder,
            i.CreatedAt,
            i.UpdatedAt,
            i.ExpiryDate.HasValue && i.ExpiryDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(InventoryResponse.ExpiringWithinDays));
    }
}

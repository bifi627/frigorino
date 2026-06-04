using System.Linq.Expressions;
using Frigorino.Domain.Entities;
using Frigorino.Features.Quantities;

namespace Frigorino.Features.Inventories
{
    // Flat per-bar payload for the household-wide cook-by calendar. One row per active inventory
    // item that has an expiry date; ExpiryDate is non-null by construction (the slice filters out
    // items without one). InventoryName drives the per-bar "which inventory" cue.
    public sealed record ExpiryCalendarItemResponse(
        int Id,
        int InventoryId,
        string InventoryName,
        string Text,
        QuantityDto? Quantity,
        DateOnly ExpiryDate)
    {
        // EF-translatable projection used by the read slice. Reads InventoryName through the
        // InventoryItem.Inventory navigation; stays inline (no method calls) so EF can translate it.
        public static readonly Expression<Func<InventoryItem, ExpiryCalendarItemResponse>> ToProjection = i => new ExpiryCalendarItemResponse(
            i.Id,
            i.InventoryId,
            i.Inventory.Name,
            i.Text,
            i.QuantityValue == null
                ? null
                : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.ExpiryDate!.Value);
    }
}

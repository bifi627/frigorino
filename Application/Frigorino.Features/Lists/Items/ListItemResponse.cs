using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Lists.Items
{
    public sealed record ListItemResponse(
        int Id,
        int ListId,
        string Text,
        QuantityDto? Quantity,
        bool Status,
        int SortOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static ListItemResponse From(ListItem item)
        {
            return new ListItemResponse(
                item.Id,
                item.ListId,
                item.Text,
                item.QuantityValue == null
                    ? null
                    : new QuantityDto(item.QuantityValue.Value, item.QuantityUnit!.Value),
                item.Status,
                item.SortOrder,
                item.CreatedAt,
                item.UpdatedAt);
        }

        // EF-translatable projection used by read slices. Stays simple enough for EF
        // (no method calls, no captured variables).
        public static readonly Expression<Func<ListItem, ListItemResponse>> ToProjection = i => new ListItemResponse(
            i.Id,
            i.ListId,
            i.Text,
            i.QuantityValue == null
                ? null
                : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.Status,
            i.SortOrder,
            i.CreatedAt,
            i.UpdatedAt);
    }
}

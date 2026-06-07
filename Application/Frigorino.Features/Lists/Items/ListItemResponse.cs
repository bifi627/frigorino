using System.Linq.Expressions;
using Frigorino.Domain.Entities;
using Frigorino.Features.Quantities;

namespace Frigorino.Features.Lists.Items
{
    public sealed record ListItemResponse(
        int Id,
        int ListId,
        string Text,
        string? Comment,
        QuantityDto? Quantity,
        bool Status,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        // Item type — Text (default) | Image | Document. Serialized as its string name.
        ListItemType Type = ListItemType.Text,
        // Media metadata — all null for Text items.
        string? FileName = null,
        string? ContentType = null,
        long? FileSize = null,
        // True only on the create response when the router enqueued LLM extraction for this item
        // (route NeedsExtraction). The client drives its extraction poll off this single signal
        // instead of re-deriving a digit gate; read/projection paths always leave it false.
        bool ExtractionPending = false)
    {
        public static ListItemResponse From(ListItem item)
        {
            return new ListItemResponse(
                item.Id,
                item.ListId,
                item.Text,
                item.Comment,
                item.QuantityValue == null
                    ? null
                    : new QuantityDto(item.QuantityValue.Value, item.QuantityUnit!.Value),
                item.Status,
                item.Rank,
                item.CreatedAt,
                item.UpdatedAt,
                item.Type,
                item.OriginalFileName,
                item.ContentType,
                item.FileSizeBytes);
        }

        // Promote-to-inventory hint, set only by the ToggleItemStatus slice via `with { Promote = ... }`.
        // Not part of the positional ctor: read/projection paths (From, ToProjection) leave it null.
        public PromoteSuggestion? Promote { get; init; }

        // EF-translatable projection used by read slices. Stays simple enough for EF
        // (no method calls, no captured variables).
        public static readonly Expression<Func<ListItem, ListItemResponse>> ToProjection = i => new ListItemResponse(
            i.Id,
            i.ListId,
            i.Text,
            i.Comment,
            i.QuantityValue == null
                ? null
                : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.Status,
            i.Rank,
            i.CreatedAt,
            i.UpdatedAt,
            i.Type,
            i.OriginalFileName,
            i.ContentType,
            i.FileSizeBytes,
            false);
    }
}

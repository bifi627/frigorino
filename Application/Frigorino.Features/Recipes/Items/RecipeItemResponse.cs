using System.Linq.Expressions;
using Frigorino.Domain.Entities;
using Frigorino.Features.Quantities;

namespace Frigorino.Features.Recipes.Items
{
    public sealed record RecipeItemResponse(
        int Id,
        int RecipeId,
        int SectionId,
        string Text,
        string? Comment,
        QuantityDto? Quantity,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool ExtractionPending)
    {
        public static RecipeItemResponse From(RecipeItem item)
            => new(item.Id, item.RecipeId, item.SectionId, item.Text, item.Comment,
                   item.QuantityValue == null ? null : new QuantityDto(item.QuantityValue.Value, item.QuantityUnit!.Value),
                   item.Rank, item.CreatedAt, item.UpdatedAt, ExtractionPending: false);

        public static readonly Expression<Func<RecipeItem, RecipeItemResponse>> ToProjection = i => new RecipeItemResponse(
            i.Id, i.RecipeId, i.SectionId, i.Text, i.Comment,
            i.QuantityValue == null ? null : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.Rank, i.CreatedAt, i.UpdatedAt, false);
    }
}

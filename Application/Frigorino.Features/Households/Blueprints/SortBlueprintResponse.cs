using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Features.Households.Blueprints
{
    // Categories is the walk-order (already sorted by OrderIndex). ProductCategory serializes as
    // its string name → the TS client gets a string union.
    public sealed record SortBlueprintResponse(int Id, string Name, IReadOnlyList<ProductCategory> Categories)
    {
        public static SortBlueprintResponse From(SortBlueprint blueprint)
        {
            return new SortBlueprintResponse(blueprint.Id, blueprint.Name, blueprint.OrderedCategories());
        }
    }
}

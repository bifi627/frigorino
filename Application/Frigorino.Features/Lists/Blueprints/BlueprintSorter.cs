using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Features.Lists.Blueprints
{
    // Pure ordering of a list's active unchecked items by a blueprint's walk-order. Items whose
    // category is not in the blueprint (or is a sentinel / unclassified → Unknown) sink to the
    // bottom. Ties within a category — and the whole uncategorized bucket — keep their existing
    // Rank order (stable). No EF, no I/O: the handler resolves categories and passes them in.
    public static class BlueprintSorter
    {
        public static IReadOnlyList<int> OrderUncheckedItemIds(
            IReadOnlyList<ListItem> uncheckedItems,
            IReadOnlyDictionary<int, ProductCategory> categoryByItemId,
            IReadOnlyList<ProductCategory> blueprintOrder)
        {
            var rankByCategory = new Dictionary<ProductCategory, int>();
            for (var i = 0; i < blueprintOrder.Count; i++)
            {
                rankByCategory[blueprintOrder[i]] = i;
            }

            return uncheckedItems
                .Select(item =>
                {
                    var category = categoryByItemId.TryGetValue(item.Id, out var c)
                        ? c
                        : ProductCategory.Unknown;
                    var primary = rankByCategory.TryGetValue(category, out var rank)
                        ? rank
                        : int.MaxValue;
                    return (item, primary);
                })
                .OrderBy(x => x.primary)
                .ThenBy(x => x.item.Rank, StringComparer.Ordinal)
                .Select(x => x.item.Id)
                .ToList();
        }
    }
}

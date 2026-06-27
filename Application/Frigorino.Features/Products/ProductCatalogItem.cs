using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Features.Products
{
    // One catalog row. Effective* is what the app uses (override ?? AI). The Ai* fields carry the
    // preserved AI verdict so the edit UI can show "AI suggested X" and make Reset meaningful.
    public sealed record ProductCatalogItem(
        int Id,
        string Name,
        ProductCategory EffectiveCategory,
        ExpiryHandling EffectiveExpiryHandling,
        int? EffectiveShelfLifeDays,
        bool IsOverridden,
        ProductCategory AiCategory,
        ExpiryHandling AiExpiryHandling,
        int? AiShelfLifeDays)
    {
        // For already-loaded entities only (uses computed getters EF can't translate).
        public static ProductCatalogItem From(Product p) => new(
            p.Id,
            p.NormalizedName,
            p.EffectiveCategory,
            p.EffectiveExpiry.Handling,
            p.EffectiveExpiry.ShelfLifeDays,
            p.IsOverridden,
            p.ClassificationProductCategory,
            p.ClassificationExpiryHandling,
            p.ClassificationShelfLifeDays);
    }
}

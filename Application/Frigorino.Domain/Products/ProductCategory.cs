namespace Frigorino.Domain.Products
{
    // What kind of thing the catalog entry is — a facet distinct from how it expires (ExpiryHandling).
    // Drives whether Cycle 3 offers the item for inventory promotion. Other = 0 is the safe default
    // (default(ProductCategory) and the refusal/uncertain fallback): unknown things are not promoted.
    public enum ProductCategory
    {
        Unknown = 0,
        Other = 1,
        Food = 2,
        HouseholdSupply = 3,
    }
}

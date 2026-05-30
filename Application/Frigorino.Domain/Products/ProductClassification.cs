namespace Frigorino.Domain.Products
{
    // Composite classifier result. One facet today (Expiry); future facets (storage location,
    // default unit) are additive — add a field here + a column on Product + a schema line.
    public sealed record ProductClassification(ExpiryProfile Expiry);
}

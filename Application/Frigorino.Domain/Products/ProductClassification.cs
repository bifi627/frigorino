namespace Frigorino.Domain.Products
{
    // Composite classifier result. Two orthogonal facets today: what kind of thing it is (Category)
    // and how it expires (Expiry). Future facets (storage location, default unit) stay additive —
    // add a field here + a column on Product + a schema line.
    public sealed record ProductClassification(ProductCategory Category, ExpiryProfile Expiry);
}

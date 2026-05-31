namespace Frigorino.Domain.Quantities
{
    // Result of inline extraction: the product name with any quantity removed, plus the
    // structured quantity (null when the text carried none).
    public sealed record QuantityExtraction(string CleanName, Quantity? Quantity);
}

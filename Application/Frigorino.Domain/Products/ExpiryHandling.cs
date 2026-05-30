namespace Frigorino.Domain.Products
{
    public enum ExpiryHandling
    {
        Unknown = 0,
        NonPerishable = 1,
        UserEntersFromPackage = 2,
        AiRecommendsShelfLife = 3,
    }
}

namespace Frigorino.Domain.Products
{
    public enum ExpiryHandling
    {
        NonPerishable = 0,
        UserEntersFromPackage = 1,
        AiRecommendsShelfLife = 2,
    }
}

using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Features.Lists.Items;

namespace Frigorino.Test.Features;

public class PromoteSuggestionTests
{
    private static readonly DateOnly Today = new(2026, 5, 31);

    private static Product ProductWith(ExpiryHandling handling, int? shelfLifeDays)
    {
        var classification = new ProductClassification(
            ProductCategory.DairyAndEggs, ExpiryProfile.Create(handling, shelfLifeDays).Value);
        return Product.Create(1, "milk", classification, classifierVersion: 1).Value;
    }

    [Fact]
    public void For_null_product_returns_null()
    {
        Assert.Null(PromoteSuggestion.For(null, Today));
    }

    [Fact]
    public void For_ai_recommended_returns_handling_and_dated_suggestion()
    {
        var suggestion = PromoteSuggestion.For(
            ProductWith(ExpiryHandling.AiRecommendsShelfLife, 7), Today);

        Assert.NotNull(suggestion);
        Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, suggestion!.ExpiryHandling);
        Assert.Equal(new DateOnly(2026, 6, 7), suggestion.SuggestedExpiry);
    }

    [Fact]
    public void For_user_enters_from_package_returns_handling_with_null_date()
    {
        var suggestion = PromoteSuggestion.For(
            ProductWith(ExpiryHandling.UserEntersFromPackage, null), Today);

        Assert.NotNull(suggestion);
        Assert.Equal(ExpiryHandling.UserEntersFromPackage, suggestion!.ExpiryHandling);
        Assert.Null(suggestion.SuggestedExpiry);
    }

    [Fact]
    public void For_non_perishable_returns_null()
    {
        Assert.Null(PromoteSuggestion.For(
            ProductWith(ExpiryHandling.NonPerishable, null), Today));
    }
}

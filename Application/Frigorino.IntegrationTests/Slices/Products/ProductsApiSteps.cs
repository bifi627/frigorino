using System.Text.Json;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Products;

[Binding]
public class ProductsApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    private int _productId;

    [Given("a classified product {string} with AI shelf life {int}")]
    public async Task GivenAClassifiedProductWithAiShelfLife(string normalizedName, int days)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = Product.Create(
            ctx.HouseholdId,
            normalizedName,
            new ProductClassification(
                ProductCategory.DairyAndEggs,
                ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value),
            classifierVersion: 1).Value;
        db.Products.Add(product);
        await db.SaveChangesAsync();
        _productId = product.Id;
    }

    [When("I PUT a product override with category {string} expiry {string} shelf life {int}")]
    public async Task WhenIPutAProductOverrideWithShelfLife(string category, string expiry, int days)
    {
        ctx.LastApiResponse = await api.TryOverrideProductClassificationAsync(_productId, category, expiry, days);
    }

    [When("I PUT a product override with category {string} expiry {string} and no shelf life")]
    public async Task WhenIPutAProductOverrideNoShelfLife(string category, string expiry)
    {
        ctx.LastApiResponse = await api.TryOverrideProductClassificationAsync(_productId, category, expiry, null);
    }

    [When("I DELETE the product override")]
    public async Task WhenIDeleteTheProductOverride()
    {
        ctx.LastApiResponse = await api.TryResetProductClassificationAsync(_productId);
    }

    [When("I GET the product catalog via the API")]
    public async Task WhenIGetTheProductCatalogViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetProductsAsync();
    }

    [Then("the product API response is overridden")]
    public async Task ThenTheProductApiResponseIsOverridden()
    {
        var body = await ReadBodyAsync();
        Assert.True(body.GetProperty("isOverridden").GetBoolean());
    }

    [Then("the product API response is not overridden")]
    public async Task ThenTheProductApiResponseIsNotOverridden()
    {
        var body = await ReadBodyAsync();
        Assert.False(body.GetProperty("isOverridden").GetBoolean());
    }

    [Then("the product API response effective expiry is {string}")]
    public async Task ThenTheProductApiResponseEffectiveExpiryIs(string expected)
    {
        var body = await ReadBodyAsync();
        Assert.Equal(expected, body.GetProperty("effectiveExpiryHandling").GetString());
    }

    [Then("the product API response effective shelf life is {int}")]
    public async Task ThenTheProductApiResponseEffectiveShelfLifeIs(int expected)
    {
        var body = await ReadBodyAsync();
        Assert.Equal(expected, body.GetProperty("effectiveShelfLifeDays").GetInt32());
    }

    [Then("the product API response has no effective shelf life")]
    public async Task ThenTheProductApiResponseHasNoEffectiveShelfLife()
    {
        var body = await ReadBodyAsync();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("effectiveShelfLifeDays").ValueKind);
    }

    private async Task<JsonElement> ReadBodyAsync()
    {
        Assert.NotNull(ctx.LastApiResponse);
        var body = await ctx.LastApiResponse.JsonAsync();
        Assert.NotNull(body);
        return body.Value;
    }
}

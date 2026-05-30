using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ClassificationApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I POST an item with text {string} to {string} via the API")]
    public async Task WhenIPostAnItemWithTextViaTheApi(string text, string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryCreateListItemAsync(listId, text);
        Assert.Equal(201, ctx.LastApiResponse.Status);
    }

    [Then("the product catalog eventually contains {string} with AI-recommended shelf life {int}")]
    public async Task ThenCatalogContainsWithShelfLife(string normalizedName, int days)
    {
        var product = await PollForProductAsync(normalizedName);
        Assert.NotNull(product);
        Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product!.ClassificationExpiryHandling);
        Assert.Equal(days, product.ClassificationShelfLifeDays);
    }

    [Then("the product catalog eventually contains {string} as non-perishable")]
    public async Task ThenCatalogContainsNonPerishable(string normalizedName)
    {
        var product = await PollForProductAsync(normalizedName);
        Assert.NotNull(product);
        Assert.Equal(ExpiryHandling.NonPerishable, product!.ClassificationExpiryHandling);
    }

    // Classification is fire-and-forget; poll the catalog (real Postgres) until the row appears.
    private async Task<Product?> PollForProductAsync(string normalizedName)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.HouseholdId == ctx.HouseholdId && p.NormalizedName == normalizedName);
            if (product is not null)
            {
                return product;
            }
            await Task.Delay(100);
        }
        return null;
    }
}

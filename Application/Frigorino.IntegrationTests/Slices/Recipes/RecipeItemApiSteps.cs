using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeItemApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("the recipe {string} has an item {string}")]
    public async Task GivenTheRecipeHasAnItem(string recipeName, string itemText)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var itemId = await api.CreateRecipeItemAsync(recipeId, itemText);
        ctx.SetRecipeItemId(recipeName, itemText, itemId);
    }

    [When("I POST a recipe item with empty text to {string} via the API")]
    public async Task WhenIPostARecipeItemWithEmptyTextViaTheApi(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryCreateRecipeItemAsync(recipeId, "");
    }

    [When("I POST a recipe item with text {string} to {string} via the API")]
    public async Task WhenIPostARecipeItemWithTextViaTheApi(string itemText, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryCreateRecipeItemAsync(recipeId, itemText);
    }

    [When("I DELETE the recipe item {string} in {string} via the API")]
    public async Task WhenIDeleteTheRecipeItemViaTheApi(string itemText, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var itemId = ctx.GetRecipeItemId(recipeName, itemText);
        ctx.LastApiResponse = await api.TryDeleteRecipeItemAsync(recipeId, itemId);
    }

    [When("I POST restore for the recipe item {string} in recipe {string} via the API")]
    public async Task WhenIPostRestoreForTheRecipeItemViaTheApi(string itemText, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var itemId = ctx.GetRecipeItemId(recipeName, itemText);
        ctx.LastApiResponse = await api.TryRestoreRecipeItemAsync(recipeId, itemId);
    }

    [When("I PATCH {string} to the top of recipe {string} via the API")]
    public async Task WhenIPatchToTheTopOfRecipeViaTheApi(string itemText, string recipeName)
    {
        // AfterId=0 is the "top of section" sentinel — covers the no-anchor branch of
        // Recipe.ReorderItem that the drag-based UI test cannot reach directly.
        var recipeId = ctx.RecipeIds[recipeName];
        var itemId = ctx.GetRecipeItemId(recipeName, itemText);
        ctx.LastApiResponse = await api.TryReorderRecipeItemAsync(recipeId, itemId, afterId: 0);
    }

    [Then("the API response when getting items of recipe {string} omits {string}")]
    public async Task ThenTheApiResponseWhenGettingItemsOfRecipeOmits(string recipeName, string itemText)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var response = await api.TryGetRecipeItemsAsync(recipeId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var items = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();
        Assert.DoesNotContain(itemText, items);
    }

    [Then("the API response when getting items of recipe {string} includes {string}")]
    public async Task ThenTheApiResponseWhenGettingItemsOfRecipeIncludes(string recipeName, string itemText)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var response = await api.TryGetRecipeItemsAsync(recipeId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var items = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();
        Assert.Contains(itemText, items);
    }

    [Then("the API items of recipe {string} appear in order: {string}")]
    public async Task ThenTheApiItemsOfRecipeAppearInOrder(string recipeName, string commaSeparated)
    {
        var expected = commaSeparated.Split(',').Select(s => s.Trim()).ToArray();
        var recipeId = ctx.RecipeIds[recipeName];
        var response = await api.TryGetRecipeItemsAsync(recipeId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var actual = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString()!)
            .ToArray();
        Assert.Equal(expected, actual);
    }

    // Polls until the async recipe quantity-extraction job lands (mirrors ExtractionApiSteps for
    // lists). The recipe extraction job rewrites text → clean name and sets the structured quantity.
    [Then("the recipe item eventually has text {string} with quantity {int} unit {int}")]
    public async Task ThenRecipeItemEventuallyHasQuantity(string expectedText, int value, int unit)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.RecipeItems
                .AsNoTracking()
                .Include(i => i.Recipe)
                .FirstOrDefaultAsync(i => i.Text == expectedText
                    && i.Recipe.HouseholdId == ctx.HouseholdId);
            if (item is not null && item.QuantityValue == (decimal)value
                && item.QuantityUnit == (QuantityUnit)unit)
            {
                return;
            }
            await Task.Delay(100);
        }
        Assert.Fail($"Recipe item '{expectedText}' with quantity {value}/{unit} did not appear in time.");
    }

    // Recipes must NEVER classify into the Product catalog (unlike lists). After extraction lands,
    // the Products table must stay empty — this is the load-bearing no-classify guarantee.
    [Then("the Products table is empty")]
    public async Task ThenTheProductsTableIsEmpty()
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await db.Products.CountAsync();
        Assert.Equal(0, count);
    }
}

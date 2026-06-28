using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeImportApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    private int _importedRecipeId;

    [When("I import the recipe URL {string} via the API")]
    public async Task WhenIImportTheRecipeUrlViaTheApi(string url)
    {
        ctx.LastApiResponse = await api.TryImportRecipeAsync(url);
        if (ctx.LastApiResponse.Status == 201)
        {
            var json = (await ctx.LastApiResponse.JsonAsync())!.Value;
            _importedRecipeId = json.GetProperty("id").GetInt32();
        }
    }

    [Then("the imported recipe has {int} ingredients")]
    public async Task ThenTheImportedRecipeHasIngredients(int count)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actual = await db.RecipeItems.CountAsync(i => i.RecipeId == _importedRecipeId && i.IsActive);
        Assert.Equal(count, actual);
    }

    [Then("the imported recipe has {int} source link")]
    [Then("the imported recipe has {int} source links")]
    public async Task ThenTheImportedRecipeHasSourceLinks(int count)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actual = await db.RecipeLinks.CountAsync(l => l.RecipeId == _importedRecipeId && l.IsActive);
        Assert.Equal(count, actual);
    }

    [Then("the imported recipe item eventually has text {string} with quantity {int}")]
    public async Task ThenTheImportedRecipeItemEventuallyHasText(string text, int quantity)
    {
        // The extraction job runs async on the queue; poll briefly (mirrors the existing recipe
        // extraction Then-step's eventual-consistency pattern).
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.RecipeItems.FirstOrDefaultAsync(
                i => i.RecipeId == _importedRecipeId && i.Text == text && i.QuantityValue == quantity);
            if (item is not null)
            {
                return;
            }
            await Task.Delay(100);
        }
        throw new Xunit.Sdk.XunitException($"Recipe item '{text}' with quantity {quantity} did not appear.");
    }

    [Then("the API response has the import error code {string}")]
    public async Task ThenTheApiResponseHasTheImportErrorCode(string code)
    {
        var json = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        Assert.Equal(code, json.GetProperty("code").GetString());
    }
}

using Frigorino.Domain.Entities;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class CopyToListApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // Reqnroll instantiates one binding instance per scenario, so this remembers the quantities
    // seeded on recipe items within the scenario — the copy step replays them as the client would
    // (the frontend sends the scaled/edited quantity explicitly; the backend does not read it from
    // the recipe). Keyed by (recipe, item) so same-named items across recipes don't collide.
    private readonly Dictionary<(string Recipe, string Item), (decimal Value, int Unit)> _seeded = new();

    [Given("the recipe {string} has an item {string} with quantity {int} unit {int}")]
    public async Task GivenRecipeHasItemWithQuantity(string recipeName, string itemText, int value, int unit)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var itemId = await api.CreateRecipeItemWithQuantityAsync(
            recipeId, itemText, value, ((QuantityUnit)unit).ToString());
        ctx.SetRecipeItemId(recipeName, itemText, itemId);
        _seeded[(recipeName, itemText)] = (value, unit);
    }

    [When("I copy items {string} from recipe {string} to list {string} via the API")]
    public async Task WhenICopyItemsToList(string commaSeparated, string recipeName, string listName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var listId = ctx.ListIds[listName];
        var items = commaSeparated.Split(',').Select(s => s.Trim())
            .Select(name =>
            {
                var (value, unit) = _seeded[(recipeName, name)];
                return (object)new
                {
                    recipeItemId = ctx.GetRecipeItemId(recipeName, name),
                    quantity = new { value, unit = ((QuantityUnit)unit).ToString() },
                };
            })
            .ToList();
        ctx.LastApiResponse = await api.TryCopyRecipeToListAsync(recipeId, listId, items);
    }

    [When("I copy items {string} plus a stale id from recipe {string} to list {string} via the API")]
    public async Task WhenICopyItemsPlusStaleId(string commaSeparated, string recipeName, string listName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var listId = ctx.ListIds[listName];
        var items = commaSeparated.Split(',').Select(s => s.Trim())
            .Select(name =>
            {
                var (value, unit) = _seeded[(recipeName, name)];
                return (object)new
                {
                    recipeItemId = ctx.GetRecipeItemId(recipeName, name),
                    quantity = new { value, unit = ((QuantityUnit)unit).ToString() },
                };
            })
            .ToList();
        items.Add(new { recipeItemId = 999999, quantity = (object?)null }); // id not in the recipe
        ctx.LastApiResponse = await api.TryCopyRecipeToListAsync(recipeId, listId, items);
    }

    [When("I copy text-only item {string} from recipe {string} to list {string} via the API")]
    public async Task WhenICopyTextOnlyItem(string itemText, string recipeName, string listName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var listId = ctx.ListIds[listName];
        var items = new[]
        {
            (object)new { recipeItemId = ctx.GetRecipeItemId(recipeName, itemText), quantity = (object?)null },
        };
        ctx.LastApiResponse = await api.TryCopyRecipeToListAsync(recipeId, listId, items);
    }

    [When("I copy item {string} from recipe {string} to a non-existent list via the API")]
    public async Task WhenICopyToNonExistentList(string itemText, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var (value, unit) = _seeded[(recipeName, itemText)];
        var items = new[]
        {
            (object)new
            {
                recipeItemId = ctx.GetRecipeItemId(recipeName, itemText),
                quantity = new { value, unit = ((QuantityUnit)unit).ToString() },
            },
        };
        ctx.LastApiResponse = await api.TryCopyRecipeToListAsync(recipeId, 999999, items);
    }

    [When("I copy no items from recipe {string} to list {string} via the API")]
    public async Task WhenICopyNoItems(string recipeName, string listName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryCopyRecipeToListAsync(recipeId, listId, Array.Empty<object>());
    }

    // Non-member case: membership is checked first, so the recipe/list ids are irrelevant — any ids
    // produce a 404 before they're loaded. ctx.HouseholdId is the household the caller isn't in.
    [When("I attempt to copy from recipe {string} to a list in that household via the API")]
    public async Task WhenIAttemptToCopyAsNonMember(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var items = new[] { (object)new { recipeItemId = 1, quantity = (object?)null } };
        ctx.LastApiResponse = await api.TryCopyRecipeToListAsync(recipeId, 999999, items);
    }

    [Then("the copy response reports {int} copied")]
    public async Task ThenCopyResponseReports(int expected)
    {
        var json = await ctx.LastApiResponse!.JsonAsync();
        Assert.Equal(expected, json!.Value.GetProperty("copiedCount").GetInt32());
    }

    [Then("the list {string} contains an item {string} with quantity {int} unit {int}")]
    public async Task ThenListContainsItemWithQuantity(string listName, string itemText, int value, int unit)
    {
        var listId = ctx.ListIds[listName];
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.ListItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ListId == listId && i.Text == itemText && i.IsActive);
        Assert.NotNull(item);
        Assert.Equal((decimal)value, item!.QuantityValue);
        Assert.Equal((QuantityUnit)unit, item.QuantityUnit);
        Assert.Equal(ListItemType.Text, item.Type);
    }

    [Then("the list {string} contains a text-only item {string}")]
    public async Task ThenListContainsTextOnlyItem(string listName, string itemText)
    {
        var listId = ctx.ListIds[listName];
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.ListItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ListId == listId && i.Text == itemText && i.IsActive);
        Assert.NotNull(item);
        Assert.Null(item!.QuantityValue);
        Assert.Null(item.QuantityUnit);
        Assert.Equal(ListItemType.Text, item.Type);
    }

    [Then("the list {string} does not contain an item {string}")]
    public async Task ThenListDoesNotContainItem(string listName, string itemText)
    {
        var listId = ctx.ListIds[listName];
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.ListItems.AsNoTracking()
            .AnyAsync(i => i.ListId == listId && i.Text == itemText && i.IsActive);
        Assert.False(exists);
    }

    [Then("every item in list {string} is unchecked")]
    public async Task ThenEveryItemUnchecked(string listName)
    {
        var listId = ctx.ListIds[listName];
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var anyChecked = await db.ListItems.AsNoTracking()
            .AnyAsync(i => i.ListId == listId && i.IsActive && i.Status);
        Assert.False(anyChecked);
    }
}

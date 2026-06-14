using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // Reqnroll creates one instance of this binding class per scenario, so this list accumulates
    // the revisions captured within a single scenario (mirrors RevisionApiSteps).
    private readonly List<string> _revisions = new();

    [Given("{string} has created a recipe named {string}")]
    public async Task GivenHasCreatedARecipeNamed(string alias, string recipeName)
    {
        var creatorUserId = alias;

        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Recipe.Create(recipeName, null, ctx.HouseholdId, creatorUserId);
        if (creation.IsFailed)
        {
            throw new InvalidOperationException(
                $"Seed failed for recipe '{recipeName}': {string.Join(", ", creation.Errors.Select(e => e.Message))}");
        }

        db.Recipes.Add(creation.Value);
        await db.SaveChangesAsync();
        ctx.RecipeIds[recipeName] = creation.Value.Id;
    }

    [When("I POST a recipe with an empty name via the API")]
    public async Task WhenIPostARecipeWithAnEmptyNameViaTheApi()
    {
        // Goes through TestApiClient (not the form) to bypass HTML5 required-validation
        // and exercise the slice's Result<T>.ToValidationProblem() branch directly.
        ctx.LastApiResponse = await api.TryCreateRecipeAsync("");
    }

    [When("I GET the recipes of that household via the API")]
    public async Task WhenIGetTheRecipesOfThatHouseholdViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetRecipesAsync();
    }

    [When("I DELETE the recipe {string} via the API")]
    public async Task WhenIDeleteTheRecipeViaTheApi(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryDeleteRecipeAsync(recipeId);
    }

    [When("I capture the revision of recipe {string} via the API")]
    public async Task WhenICaptureTheRevisionOfRecipe(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var response = await api.TryGetRecipeRevisionAsync(recipeId);
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        _revisions.Add(json.GetProperty("rev").GetString()!);
    }

    [Then("the two captured recipe revisions differ")]
    public void ThenTheTwoCapturedRecipeRevisionsDiffer()
    {
        Assert.Equal(2, _revisions.Count);
        Assert.NotEqual(_revisions[0], _revisions[1]);
    }

    [When("I POST a recipe named {string} with servings {int} via the API")]
    public async Task WhenIPostARecipeNamedWithServings(string recipeName, int servings)
    {
        var response = await api.TryCreateRecipeWithServingsAsync(recipeName, servings);
        ctx.LastApiResponse = response;
        if (response.Status == 201)
        {
            var json = (await response.JsonAsync())!.Value;
            ctx.RecipeIds[recipeName] = json.GetProperty("id").GetInt32();
        }
    }

    [When("I PUT recipe {string} with servings {int} via the API")]
    public async Task WhenIPutRecipeWithServings(string recipeName, int servings)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryUpdateRecipeAsync(recipeId, recipeName, servings);
    }

    [Then("the API recipe response has servings {int}")]
    public async Task ThenTheApiRecipeResponseHasServings(int expected)
    {
        var json = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        Assert.Equal(expected, json.GetProperty("servings").GetInt32());
    }
}

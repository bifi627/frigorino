namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeLinkApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // Reused as both When (action under test) and Given/And (setup). This project's Reqnroll is
    // keyword-sensitive, so both attributes are required.
    [Given("I POST a source link {string} labelled {string} to recipe {string} via the API")]
    [When("I POST a source link {string} labelled {string} to recipe {string} via the API")]
    public async Task WhenIPostLink(string url, string label, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var response = await api.TryCreateRecipeLinkAsync(recipeId, url, label);
        ctx.LastApiResponse = response;
        if (response.Status == 201)
        {
            var json = (await response.JsonAsync())!.Value;
            ctx.RecipeLinkIds[(recipeName, label)] = json.GetProperty("id").GetInt32();
        }
    }

    [When("I POST a source link {string} with no scheme to recipe {string} via the API")]
    public async Task WhenIPostInvalidLink(string url, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryCreateRecipeLinkAsync(recipeId, url, null);
    }

    [When("I GET the source links of recipe {string} via the API")]
    public async Task WhenIGetLinks(string recipeName)
    {
        ctx.LastApiResponse = await api.TryGetRecipeLinksAsync(ctx.RecipeIds[recipeName]);
    }

    [Then("the API source links of recipe {string} number {int}")]
    public async Task ThenLinksNumber(string recipeName, int expected)
    {
        var response = await api.TryGetRecipeLinksAsync(ctx.RecipeIds[recipeName]);
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        Assert.Equal(expected, json.GetArrayLength());
    }

    [When("I DELETE the source link {string} of recipe {string} via the API")]
    public async Task WhenIDeleteLink(string label, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryDeleteRecipeLinkAsync(recipeId, ctx.RecipeLinkIds[(recipeName, label)]);
    }

    [When("I POST restore for the source link {string} of recipe {string} via the API")]
    public async Task WhenIRestoreLink(string label, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryRestoreRecipeLinkAsync(recipeId, ctx.RecipeLinkIds[(recipeName, label)]);
    }
}

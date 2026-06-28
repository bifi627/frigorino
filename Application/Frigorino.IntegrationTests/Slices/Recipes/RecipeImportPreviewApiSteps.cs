namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeImportPreviewApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I preview the recipe URL {string} via the API")]
    public async Task WhenIPreviewTheRecipeUrlViaTheApi(string url)
    {
        ctx.LastApiResponse = await api.TryPreviewRecipeImportAsync(url);
    }

    [Then("the preview response name is {string}")]
    public async Task ThenThePreviewResponseNameIs(string name)
    {
        var json = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        Assert.Equal(name, json.GetProperty("name").GetString());
    }
}

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeTagSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("the recipe {string} has tags {string}")]
    public async Task GivenTheRecipeHasTags(string recipeName, string csvTags)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var tags = csvTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await api.SetRecipeTagsAsync(recipeId, tags);
    }

    [When("I open the recipes overview")]
    public async Task WhenIOpenTheRecipesOverview()
    {
        await ctx.Page.GotoAsync("/recipes",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    [When("I toggle the overview tag filter {string}")]
    public async Task WhenIToggleTheOverviewTagFilter(string tag)
    {
        await ctx.Page.GetByTestId($"recipe-filter-tag-{tag}").ClickAsync();
    }

    [Then("the recipe view shows the tag {string}")]
    public async Task ThenTheRecipeViewShowsTheTag(string tag)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-tag-{tag}")).ToBeVisibleAsync();
    }

    [When("I tap suggest tags")]
    public async Task WhenITapSuggestTags()
    {
        await ctx.Page.GetByTestId("recipe-suggest-tags").ClickAsync();
    }

    [Then("a suggested tag chip {string} is shown")]
    public async Task ThenASuggestedTagChipIsShown(string tag)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-tag-suggested-{tag}")).ToBeVisibleAsync();
    }

    [When("I accept the suggested tag {string}")]
    public async Task WhenIAcceptTheSuggestedTag(string tag)
    {
        // Await the PUT so the follow-up "selected" assertion sees a stable DOM.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/tags")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        await ctx.Page.GetByTestId($"recipe-tag-suggested-{tag}").ClickAsync();
        await responseTask;
    }

    [Then("the recipe tag {string} is selected")]
    public async Task ThenTheRecipeTagIsSelected(string tag)
    {
        // The selectable chip in the edit selector goes filled (MUI adds MuiChip-filled). Assert it
        // is present and visible; the accept flow removed it from the ghost row.
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-tag-select-{tag}")).ToBeVisibleAsync();
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-tag-suggested-{tag}")).Not.ToBeVisibleAsync();
    }
}

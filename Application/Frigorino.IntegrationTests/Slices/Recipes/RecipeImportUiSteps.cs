namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeImportUiSteps(ScenarioContextHolder ctx)
{
    [When("I submit the import URL {string}")]
    public async Task WhenISubmitTheImportUrl(string url)
    {
        await ctx.Page.GetByTestId("recipe-import-url").FillAsync(url);
        await ctx.Page.GetByTestId("recipe-import-submit").ClickAsync();
    }

    [When("I enter the import URL {string}")]
    public async Task WhenIEnterTheImportUrl(string url)
    {
        await ctx.Page.GetByTestId("recipe-import-url").FillAsync(url);
    }

    [When("the import preview shows {string}")]
    public async Task WhenTheImportPreviewShows(string name)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-peek-name")).ToHaveTextAsync(name);
    }

    [When("I confirm the import")]
    public async Task WhenIConfirmTheImport()
    {
        await ctx.Page.GetByTestId("recipe-import-confirm").ClickAsync();
    }

    [Then("I am taken to the recipe edit page")]
    public async Task ThenIAmTakenToTheRecipeEditPage()
    {
        await ctx.Page.WaitForURLAsync("**/recipes/*/edit");
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-details-accordion")).ToBeVisibleAsync();
    }

    [Then("the import shows an error")]
    public async Task ThenTheImportShowsAnError()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-import-error")).ToBeVisibleAsync();
    }

    [Then("the import submit is disabled")]
    public async Task ThenTheImportSubmitIsDisabled()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-import-submit")).ToBeDisabledAsync();
    }

    [Then("the recipe name field shows {string}")]
    public async Task ThenTheRecipeNameFieldShows(string name)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-name-input")).ToHaveValueAsync(name);
    }
}

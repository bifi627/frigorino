using Microsoft.Playwright;

namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeImportUiSteps(ScenarioContextHolder ctx)
{
    [Given("I am on the recipes page")]
    public async Task GivenIAmOnTheRecipesPage()
    {
        await ctx.Page.GotoAsync("/recipes",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    [When("I open the import dialog")]
    public async Task WhenIOpenTheImportDialog()
    {
        await ctx.Page.GetByTestId("recipe-import-open").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-import-url")).ToBeVisibleAsync();
    }

    [When("I submit the import URL {string}")]
    public async Task WhenISubmitTheImportUrl(string url)
    {
        await ctx.Page.GetByTestId("recipe-import-url").FillAsync(url);
        await ctx.Page.GetByTestId("recipe-import-submit").ClickAsync();
    }

    [Then("I am taken to the recipe edit page")]
    public async Task ThenIAmTakenToTheRecipeEditPage()
    {
        await ctx.Page.WaitForURLAsync("**/recipes/*/edit");
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-details-accordion")).ToBeVisibleAsync();
    }

    [Then("the import dialog shows an error")]
    public async Task ThenTheImportDialogShowsAnError()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-import-error")).ToBeVisibleAsync();
    }
}

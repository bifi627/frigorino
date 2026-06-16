namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class CopyToListUiSteps(ScenarioContextHolder ctx)
{
    [When("I open the copy-to-list sheet")]
    public async Task WhenIOpenTheCopyToListSheet()
    {
        await ctx.Page.GetByTestId("recipe-header-menu-toggle").ClickAsync();
        await ctx.Page.GetByTestId("recipe-copy-to-list-button").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("copy-to-list-sheet")).ToBeVisibleAsync();
    }

    [When("I confirm the copy")]
    public async Task WhenIConfirmTheCopy()
    {
        // Register the response wait before the click so the POST can't land first; this guarantees
        // the rows are persisted before the DB assertions run.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/copy-to-list")
            && r.Request.Method == "POST"
            && r.Status == 200);
        await ctx.Page.GetByTestId("copy-to-list-add-button").ClickAsync();
        await responseTask;
        // The sheet closes on success — wait it out so a follow-up navigation/assert isn't racing it.
        await Assertions.Expect(ctx.Page.GetByTestId("copy-to-list-sheet")).Not.ToBeVisibleAsync();
    }
}

namespace Frigorino.IntegrationTests.Shared;

[Binding]
public class ToastSteps(ScenarioContextHolder ctx)
{
    [When("I click undo in the delete toast")]
    public async Task WhenIClickUndoInTheDeleteToast()
    {
        // Register the response wait before any action that could trigger the restore,
        // matching the codebase convention and avoiding a click/response race.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Url.EndsWith("/restore")
            && r.Request.Method == "POST"
            && r.Status == 200);
        await Assertions.Expect(ctx.Page.Locator("[data-sonner-toast] .undo-action-button"))
            .ToBeVisibleAsync();
        await ctx.Page.Locator("[data-sonner-toast] .undo-action-button").ClickAsync();
        await responseTask;
    }
}

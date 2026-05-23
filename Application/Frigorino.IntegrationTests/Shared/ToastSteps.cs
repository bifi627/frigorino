namespace Frigorino.IntegrationTests.Shared;

[Binding]
public class ToastSteps(ScenarioContextHolder ctx)
{
    [When("I click undo in the delete toast")]
    public async Task WhenIClickUndoInTheDeleteToast()
    {
        await Assertions.Expect(ctx.Page.Locator("[data-sonner-toast] .undo-action-button"))
            .ToBeVisibleAsync();
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Url.EndsWith("/restore")
            && r.Request.Method == "POST"
            && r.Status == 200);
        await ctx.Page.Locator("[data-sonner-toast] .undo-action-button").ClickAsync();
        await responseTask;
    }
}

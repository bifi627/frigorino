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

        var toast = ctx.Page.Locator("[data-sonner-toast]");
        var undoButton = toast.Locator(".undo-action-button");

        // The toast auto-dismisses after its sonner `duration` (5s). Under load the test can
        // reach the click just as the toast begins its dismiss transition: the button is still
        // hit-testable but its pointer-events are disabled, so the click is silently swallowed,
        // the restore POST never fires, and the wait above hangs the full timeout (the observed
        // intermittent failure). Hovering the toast pauses sonner's auto-dismiss timer
        // (expanded/interacting => pauseTimer) for as long as the pointer stays over it, so the
        // subsequent click no longer races the timer.
        await Assertions.Expect(toast).ToBeVisibleAsync();
        await toast.HoverAsync();
        await Assertions.Expect(undoButton).ToBeVisibleAsync();
        await undoButton.ClickAsync();
        await responseTask;
    }
}

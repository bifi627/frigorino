namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class ExpiryCalendarSteps(ScenarioContextHolder ctx)
{
    [When("I open the inventories overview")]
    public async Task WhenIOpenTheInventoriesOverview()
    {
        await ctx.Page.GotoAsync("/inventories", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    [When("I open the expiry calendar from the header")]
    public async Task WhenIOpenTheExpiryCalendarFromTheHeader()
    {
        await ctx.Page.GetByTestId("inventories-calendar-button").ClickAsync();
        await ctx.Page.WaitForURLAsync("**/inventories/calendar");
    }

    [Then("the calendar shows the item {string}")]
    public async Task ThenTheCalendarShowsTheItem(string itemText)
    {
        // A bar can wrap into multiple week-row segments (same testid each); assert the first.
        await Assertions.Expect(ctx.Page.GetByTestId($"cal-event-{itemText}").First)
            .ToBeVisibleAsync();
    }

    [When("I select the calendar item {string}")]
    public async Task WhenISelectTheCalendarItem(string itemText)
    {
        await ctx.Page.GetByTestId($"cal-event-{itemText}").First.ClickAsync();
    }

    [Then("the calendar item {string} is focused")]
    public async Task ThenTheCalendarItemIsFocused(string itemText)
    {
        await Assertions.Expect(
                ctx.Page.GetByTestId($"cal-event-{itemText}").First)
            .ToHaveAttributeAsync("data-selected", "true");
    }

    [Then("the calendar action bar shows {string}")]
    public async Task ThenTheCalendarActionBarShows(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-item-action-bar"))
            .ToBeVisibleAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-action-bar-title"))
            .ToHaveTextAsync(itemText);
    }

    [When("I tap edit in the calendar action bar")]
    public async Task WhenITapEditInTheCalendarActionBar()
    {
        await ctx.Page.GetByTestId("calendar-action-bar-edit").ClickAsync();
    }

    [Then("the calendar action bar is in edit mode")]
    public async Task ThenTheCalendarActionBarIsInEditMode()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-action-bar-composer"))
            .ToBeVisibleAsync();
    }

    [When("I change the item text to {string} and save")]
    public async Task WhenIChangeTheItemTextToAndSave(string newText)
    {
        var input = ctx.Page
            .GetByTestId("calendar-action-bar-composer")
            .GetByTestId("autocomplete-input-textfield")
            .Locator("input");
        await input.FillAsync(newText);
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
        // The composer collapses on save; wait for it to disappear before asserting.
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-action-bar-composer"))
            .Not.ToBeVisibleAsync();
    }

    [When("I turn off the {string} level filter")]
    public async Task WhenITurnOffTheLevelFilter(string level)
    {
        await ctx.Page.GetByTestId($"calendar-level-{level}").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId($"calendar-level-{level}"))
            .ToHaveAttributeAsync("data-active", "false");
    }

    [Then("the calendar does not show the item {string}")]
    public async Task ThenTheCalendarDoesNotShowTheItem(string itemText)
    {
        // Filtered-out items are not rendered at all, so the locator resolves to zero elements.
        await Assertions.Expect(ctx.Page.GetByTestId($"cal-event-{itemText}"))
            .ToHaveCountAsync(0);
    }

    [When("I reload the calendar page")]
    public async Task WhenIReloadTheCalendarPage()
    {
        await ctx.Page.ReloadAsync(
            new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await ctx.Page.WaitForURLAsync("**/inventories/calendar");
    }
}

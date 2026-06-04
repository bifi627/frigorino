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
}

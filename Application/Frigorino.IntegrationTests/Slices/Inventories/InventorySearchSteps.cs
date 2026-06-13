namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class InventorySearchSteps(ScenarioContextHolder ctx)
{
    [When("I open the inventory search")]
    public async Task WhenIOpenTheInventorySearch()
    {
        await ctx.Page.GetByTestId("inventory-header-menu-toggle").ClickAsync();
        await ctx.Page.GetByTestId("inventory-search-button").ClickAsync();
    }

    [When("I search the inventory for {string}")]
    public async Task WhenISearchTheInventoryFor(string query)
    {
        await ctx.Page.GetByTestId("inventory-search-input").FillAsync(query);
    }

    [When("I clear the inventory search")]
    public async Task WhenIClearTheInventorySearch()
    {
        await ctx.Page.GetByTestId("inventory-search-clear").ClickAsync();
    }

    [Then("the inventory item {string} shows a drag handle")]
    public async Task ThenTheInventoryItemShowsADragHandle(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"drag-handle-item-{itemText}"))
            .ToBeVisibleAsync();
    }

    [Then("the inventory item {string} shows no drag handle")]
    public async Task ThenTheInventoryItemShowsNoDragHandle(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"drag-handle-item-{itemText}"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the inventory search shows no results")]
    public async Task ThenTheInventorySearchShowsNoResults()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("inventory-search-no-results"))
            .ToBeVisibleAsync();
    }

    [When("I open the calendar search")]
    public async Task WhenIOpenTheCalendarSearch()
    {
        await ctx.Page.GetByTestId("calendar-search-button").ClickAsync();
    }

    [When("I search the calendar for {string}")]
    public async Task WhenISearchTheCalendarFor(string query)
    {
        await ctx.Page.GetByTestId("calendar-search-input").FillAsync(query);
    }
}

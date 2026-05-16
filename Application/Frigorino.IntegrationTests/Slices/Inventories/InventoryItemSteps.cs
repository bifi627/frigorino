namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class InventoryItemSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("there is an inventory named {string} with item {string}")]
    public async Task GivenThereIsAnInventoryNamedWithItem(string inventoryName, string itemText)
    {
        var inventoryId = await api.CreateInventoryAsync(inventoryName);
        ctx.InventoryIds[inventoryName] = inventoryId;
        var itemId = await api.CreateInventoryItemAsync(inventoryId, itemText);
        ctx.InventoryItemIds[itemText] = itemId;
    }

    [Given("the inventory {string} also has item {string}")]
    public async Task GivenTheInventoryAlsoHasItem(string inventoryName, string itemText)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var itemId = await api.CreateInventoryItemAsync(inventoryId, itemText);
        ctx.InventoryItemIds[itemText] = itemId;
    }

    [When("I open the inventory item menu for {string}")]
    public async Task WhenIOpenTheInventoryItemMenuFor(string itemText)
    {
        // The menu button sits inside dnd-kit's sortable container, which contributes
        // ancestor aria attributes that Playwright's actionability check reads as
        // "element is not enabled". Force=true skips the check — same pattern as ListItemSteps.
        await ctx.Page.GetByTestId($"item-menu-button-{itemText}")
            .ClickAsync(new LocatorClickOptions { Force = true });
    }

    [When("I click delete from the inventory item menu")]
    public async Task WhenIClickDeleteFromTheInventoryItemMenu()
    {
        // Wait for the DELETE response so the next Then-step inspects post-server-confirm DOM
        // rather than the optimistic-update window where rollback could still re-add the row.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Request.Method == "DELETE"
            && r.Status == 204);
        await ctx.Page.GetByTestId("delete-item-button")
            .ClickAsync(new LocatorClickOptions { Force = true });
        await responseTask;
    }

    [Then("{string} no longer appears in the inventory")]
    public async Task ThenNoLongerAppearsInTheInventory(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"toggle-item-{itemText}"))
            .Not.ToBeVisibleAsync();
    }
}

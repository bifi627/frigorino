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
        ctx.SetInventoryItemId(inventoryName, itemText, itemId);
    }

    [Given("there is an inventory named {string} with item {string} and quantity {string}")]
    public async Task GivenThereIsAnInventoryNamedWithItemAndQuantity(string inventoryName, string itemText, string quantity)
    {
        var inventoryId = await api.CreateInventoryAsync(inventoryName);
        ctx.InventoryIds[inventoryName] = inventoryId;
        var itemId = await api.CreateInventoryItemAsync(inventoryId, itemText, quantity);
        ctx.SetInventoryItemId(inventoryName, itemText, itemId);
    }

    [Given("the inventory {string} also has item {string}")]
    public async Task GivenTheInventoryAlsoHasItem(string inventoryName, string itemText)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var itemId = await api.CreateInventoryItemAsync(inventoryId, itemText);
        ctx.SetInventoryItemId(inventoryName, itemText, itemId);
    }

    [When("I open the inventory item menu for {string}")]
    public async Task WhenIOpenTheInventoryItemMenuFor(string itemText)
    {
        await ctx.Page.GetByTestId($"item-menu-button-{itemText}").ClickAsync();
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
        await ctx.Page.GetByTestId("delete-item-button").ClickAsync();
        await responseTask;
    }

    [Then("{string} no longer appears in the inventory")]
    public async Task ThenNoLongerAppearsInTheInventory(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"toggle-item-{itemText}"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the inventory item {string} shows quantity {string}")]
    public async Task ThenTheInventoryItemShowsQuantity(string itemText, string quantity)
    {
        // The quantity caption also renders a ShoppingBag icon (no text), so assert the
        // value is contained rather than exact-equal.
        await Assertions.Expect(ctx.Page.GetByTestId($"inventory-item-quantity-{itemText}"))
            .ToContainTextAsync(quantity);
    }

    [Then("the inventory item {string} shows no quantity")]
    public async Task ThenTheInventoryItemShowsNoQuantity(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"inventory-item-quantity-{itemText}"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the inventory item {string} shows an expiry indicator")]
    public async Task ThenTheInventoryItemShowsAnExpiryIndicator(string itemText)
    {
        // The coloured highlight bar renders for ANY expiryDate, unlike the human-readable
        // caption which is translated and stays empty for dates more than 30 days out.
        await Assertions.Expect(ctx.Page.GetByTestId($"inventory-item-expiry-{itemText}"))
            .ToBeVisibleAsync();
    }
}

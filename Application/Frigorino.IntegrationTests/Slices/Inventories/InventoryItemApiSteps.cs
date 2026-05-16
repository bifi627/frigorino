namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class InventoryItemApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I POST an inventory item with empty text to {string} via the API")]
    public async Task WhenIPostAnInventoryItemWithEmptyTextViaTheApi(string inventoryName)
    {
        // Bypasses the autocomplete input's client-side empty guard to exercise the slice's
        // Result<T>.ToValidationProblem() branch on Inventory.AddItem.
        var inventoryId = ctx.InventoryIds[inventoryName];
        ctx.LastApiResponse = await api.TryCreateInventoryItemAsync(inventoryId, "");
    }

    [When("I GET the items of inventory {string} via the API")]
    public async Task WhenIGetTheItemsOfInventoryViaTheApi(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        ctx.LastApiResponse = await api.TryGetInventoryItemsAsync(inventoryId);
    }

    [When("I DELETE the inventory item {string} in {string} via the API")]
    public async Task WhenIDeleteTheInventoryItemViaTheApi(string itemText, string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var itemId = ctx.InventoryItemIds[itemText];
        ctx.LastApiResponse = await api.TryDeleteInventoryItemAsync(inventoryId, itemId);
    }

    [When("I POST compact for inventory {string} via the API")]
    public async Task WhenIPostCompactForInventoryViaTheApi(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        ctx.LastApiResponse = await api.TryCompactInventoryItemsAsync(inventoryId);
    }

    [When("I PATCH {string} to the top of inventory {string} via the API")]
    public async Task WhenIPatchToTheTopOfInventoryViaTheApi(string itemText, string inventoryName)
    {
        // AfterId=0 is the "top of section" sentinel — covers the no-anchor branch of
        // Inventory.ReorderItem that the drag-based UI test cannot reach directly.
        var inventoryId = ctx.InventoryIds[inventoryName];
        var itemId = ctx.InventoryItemIds[itemText];
        ctx.LastApiResponse = await api.TryReorderInventoryItemAsync(inventoryId, itemId, afterId: 0);
    }

    [When("I PATCH {string} after {string} in inventory {string} via the API")]
    public async Task WhenIPatchAfterInInventoryViaTheApi(string itemText, string anchorText, string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var itemId = ctx.InventoryItemIds[itemText];
        var anchorId = ctx.InventoryItemIds[anchorText];
        ctx.LastApiResponse = await api.TryReorderInventoryItemAsync(inventoryId, itemId, afterId: anchorId);
    }

    [Then("the API response when getting items of inventory {string} omits {string}")]
    public async Task ThenTheApiResponseWhenGettingItemsOfInventoryOmits(string inventoryName, string itemText)
    {
        // Second GET issued after the DELETE step to confirm the soft-delete is hidden from
        // the read projection (IsActive filter on the slice query).
        var inventoryId = ctx.InventoryIds[inventoryName];
        var response = await api.TryGetInventoryItemsAsync(inventoryId);
        response.Status.Should().Be(200);

        var json = await response.JsonAsync();
        var items = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();
        items.Should().NotContain(itemText);
    }

    [Then("the API items of inventory {string} appear in order: {string}")]
    public async Task ThenTheApiItemsOfInventoryAppearInOrder(string inventoryName, string commaSeparated)
    {
        var expected = commaSeparated.Split(',').Select(s => s.Trim()).ToArray();
        var inventoryId = ctx.InventoryIds[inventoryName];
        var response = await api.TryGetInventoryItemsAsync(inventoryId);
        response.Status.Should().Be(200);

        var json = await response.JsonAsync();
        var actual = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString()!)
            .ToArray();
        actual.Should().Equal(expected);
    }
}

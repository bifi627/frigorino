namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class ReorderSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I click add to list from the inventory item menu")]
    public async Task WhenIClickAddToListFromTheInventoryItemMenu()
    {
        await ctx.Page.GetByTestId("add-to-list-button").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("reorder-sheet")).ToBeVisibleAsync();
    }

    [When("I confirm the re-order")]
    public async Task WhenIConfirmTheReorder()
    {
        // The list-item create returns 201; wait for it so the assertion reads post-commit state.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.EndsWith("/items")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("reorder-confirm-button").ClickAsync();
        await responseTask;
    }

    [Then("the list {string} contains an item {string}")]
    public async Task ThenTheListContainsAnItem(string listName, string itemText)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);
        var items = (await response.JsonAsync())!.Value;
        var texts = items.EnumerateArray()
            .Select(i => i.GetProperty("text").GetString())
            .ToList();
        Assert.Contains(itemText, texts);
    }
}

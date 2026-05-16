namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ListItemApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I POST an item with empty text to {string} via the API")]
    public async Task WhenIPostAnItemWithEmptyTextViaTheApi(string listName)
    {
        // Bypasses the autocomplete input's client-side empty guard to exercise the slice's
        // Result<T>.ToValidationProblem() branch on List.AddItem.
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryCreateListItemAsync(listId, "");
    }

    [When("I GET the items of {string} via the API")]
    public async Task WhenIGetTheItemsOfViaTheApi(string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryGetListItemsAsync(listId);
    }

    [When("I DELETE the item {string} in {string} via the API")]
    public async Task WhenIDeleteTheItemViaTheApi(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.ListItemIds[itemText];
        ctx.LastApiResponse = await api.TryDeleteListItemAsync(listId, itemId);
    }

    [When("I POST compact for {string} via the API")]
    public async Task WhenIPostCompactForViaTheApi(string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryCompactListItemsAsync(listId);
    }

    [When("I PUT an all-null update to {string} in {string} via the API")]
    public async Task WhenIPutAnAllNullUpdateToViaTheApi(string itemText, string listName)
    {
        // Exercises List.UpdateItem's all-null guard — text/quantity/status all null is a
        // guaranteed no-op on the server, so the slice should reject it as a validation error
        // instead of returning 200 OK on garbage input.
        var listId = ctx.ListIds[listName];
        var itemId = ctx.ListItemIds[itemText];
        ctx.LastApiResponse = await api.TryUpdateListItemAsync(listId, itemId, text: null, quantity: null, status: null);
    }

    [When("I PATCH {string} to the top of {string} via the API")]
    public async Task WhenIPatchToTheTopViaTheApi(string itemText, string listName)
    {
        // AfterId=0 is the "top of section" sentinel — covers the no-anchor branch of
        // List.ReorderItem that the drag-based UI test cannot reach directly.
        var listId = ctx.ListIds[listName];
        var itemId = ctx.ListItemIds[itemText];
        ctx.LastApiResponse = await api.TryReorderListItemAsync(listId, itemId, afterId: 0);
    }

    [When("I PATCH {string} after {string} in {string} via the API")]
    public async Task WhenIPatchAfterViaTheApi(string itemText, string anchorText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.ListItemIds[itemText];
        var anchorId = ctx.ListItemIds[anchorText];
        ctx.LastApiResponse = await api.TryReorderListItemAsync(listId, itemId, afterId: anchorId);
    }

    [When("I PATCH toggle on {string} in {string} via the API")]
    public async Task WhenIPatchToggleViaTheApi(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.ListItemIds[itemText];
        ctx.LastApiResponse = await api.TryToggleListItemStatusAsync(listId, itemId);
    }

    [Then("the API response when getting items of {string} omits {string}")]
    public async Task ThenTheApiResponseWhenGettingItemsOmits(string listName, string itemText)
    {
        // Second GET issued after the DELETE step to confirm the soft-delete is hidden from
        // the read projection (IsActive filter on the slice query).
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        response.Status.Should().Be(200);

        var json = await response.JsonAsync();
        var items = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();
        items.Should().NotContain(itemText);
    }

    [Then("the API items of {string} appear in order: {string}")]
    public async Task ThenTheApiItemsAppearInOrder(string listName, string commaSeparated)
    {
        var expected = commaSeparated.Split(',').Select(s => s.Trim()).ToArray();
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        response.Status.Should().Be(200);

        var json = await response.JsonAsync();
        var actual = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString()!)
            .ToArray();
        actual.Should().Equal(expected);
    }
}

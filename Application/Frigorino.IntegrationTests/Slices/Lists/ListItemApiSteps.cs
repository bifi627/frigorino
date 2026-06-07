namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ListItemApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I POST an item with empty text to {string} via the API")]
    public async Task WhenIPostAnItemWithEmptyTextViaTheApi(string listName)
    {
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
        var itemId = ctx.GetListItemId(listName, itemText);
        ctx.LastApiResponse = await api.TryDeleteListItemAsync(listId, itemId);
    }

    [When("I POST restore for the item {string} in list {string} via the API")]
    public async Task WhenIPostRestoreForTheItemInListViaTheApi(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        ctx.LastApiResponse = await api.TryRestoreListItemAsync(listId, itemId);
    }

    [When("I PUT an all-null update to {string} in {string} via the API")]
    public async Task WhenIPutAnAllNullUpdateToViaTheApi(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        ctx.LastApiResponse = await api.TryUpdateListItemAsync(listId, itemId, text: null, quantity: null, status: null);
    }

    [When("I PATCH {string} to the top of {string} via the API")]
    public async Task WhenIPatchToTheTopViaTheApi(string itemText, string listName)
    {
        // AfterId=0 is the "top of section" sentinel — covers the no-anchor branch of
        // List.ReorderItem that the drag-based UI test cannot reach directly.
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        ctx.LastApiResponse = await api.TryReorderListItemAsync(listId, itemId, afterId: 0);
    }

    [When("I PATCH {string} after {string} in {string} via the API")]
    public async Task WhenIPatchAfterViaTheApi(string itemText, string anchorText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        var anchorId = ctx.GetListItemId(listName, anchorText);
        ctx.LastApiResponse = await api.TryReorderListItemAsync(listId, itemId, afterId: anchorId);
    }

    [When("I PATCH toggle on {string} in {string} via the API")]
    public async Task WhenIPatchToggleViaTheApi(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        ctx.LastApiResponse = await api.TryToggleListItemStatusAsync(listId, itemId);
    }

    [Then("the API response when getting items of {string} omits {string}")]
    public async Task ThenTheApiResponseWhenGettingItemsOmits(string listName, string itemText)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var items = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();
        Assert.DoesNotContain(itemText, items);
    }

    [Then("the API response when getting items of list {string} includes {string}")]
    public async Task ThenTheApiResponseWhenGettingItemsOfListIncludes(string listName, string itemText)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var items = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();
        Assert.Contains(itemText, items);
    }

    [Then("the API items of {string} appear in order: {string}")]
    public async Task ThenTheApiItemsAppearInOrder(string listName, string commaSeparated)
    {
        var expected = commaSeparated.Split(',').Select(s => s.Trim()).ToArray();
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var actual = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString()!)
            .ToArray();
        Assert.Equal(expected, actual);
    }

    [When("I POST an item {string} with comment {string} to {string} via the API")]
    public async Task WhenIPostAnItemWithCommentViaTheApi(string itemText, string comment, string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryCreateListItemAsync(listId, itemText, comment: comment);

        if (ctx.LastApiResponse.Ok)
        {
            var json = await ctx.LastApiResponse.JsonAsync();
            var itemId = json!.Value.GetProperty("id").GetInt32();
            ctx.SetListItemId(listName, itemText, itemId);
        }
    }

    [When("I PUT a comment {string} onto {string} in {string} via the API")]
    public async Task WhenIPutACommentOntoViaTheApi(string comment, string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        ctx.LastApiResponse = await api.TryUpdateListItemAsync(listId, itemId, text: null, quantity: null, status: null, comment: comment);
    }

    [Then("the API item {string} in {string} has comment {string}")]
    public async Task ThenTheApiItemHasComment(string itemText, string listName, string expectedComment)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var item = json!.Value.EnumerateArray()
            .First(e => e.GetProperty("text").GetString() == itemText);
        Assert.Equal(expectedComment, item.GetProperty("comment").GetString());
    }

    [Then("the API item {string} in {string} has no comment")]
    public async Task ThenTheApiItemHasNoComment(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var item = json!.Value.EnumerateArray()
            .First(e => e.GetProperty("text").GetString() == itemText);
        var comment = item.GetProperty("comment");
        Assert.True(comment.ValueKind == System.Text.Json.JsonValueKind.Null);
    }
}

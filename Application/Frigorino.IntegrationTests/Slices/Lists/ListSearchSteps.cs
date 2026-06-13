namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ListSearchSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("the list {string} also has item {string} with comment {string}")]
    public async Task GivenTheListAlsoHasItemWithComment(string listName, string itemText, string comment)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryCreateListItemAsync(listId, itemText, comment: comment);
        if (!response.Ok)
        {
            throw new InvalidOperationException(
                $"Failed to seed list item '{itemText}' with comment: {response.Status}");
        }
    }

    [When("I open the list search")]
    public async Task WhenIOpenTheListSearch()
    {
        await ctx.Page.GetByTestId("list-header-menu-toggle").ClickAsync();
        await ctx.Page.GetByTestId("list-search-button").ClickAsync();
    }

    [When("I search the list for {string}")]
    public async Task WhenISearchTheListFor(string query)
    {
        await ctx.Page.GetByTestId("list-search-input").FillAsync(query);
    }

    [When("I clear the list search")]
    public async Task WhenIClearTheListSearch()
    {
        await ctx.Page.GetByTestId("list-search-clear").ClickAsync();
    }

    [Then("the list item {string} shows a drag handle")]
    public async Task ThenTheListItemShowsADragHandle(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"drag-handle-item-{itemText}"))
            .ToBeVisibleAsync();
    }

    [Then("the list item {string} shows no drag handle")]
    public async Task ThenTheListItemShowsNoDragHandle(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"drag-handle-item-{itemText}"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the list search shows no results")]
    public async Task ThenTheListSearchShowsNoResults()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("list-search-no-results"))
            .ToBeVisibleAsync();
    }
}

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ListSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("there is a list named {string}")]
    public async Task GivenThereIsAListNamed(string name)
    {
        var listId = await api.CreateListAsync(name);
        ctx.ListIds[name] = listId;
    }

    [Given("there is a list named {string} with item {string}")]
    public async Task GivenThereIsAListNamedWithItem(string listName, string itemText)
    {
        var listId = await api.CreateListAsync(listName);
        ctx.ListIds[listName] = listId;
        await api.CreateListItemAsync(listId, itemText);
    }

    [When("I open the list {string}")]
    public async Task WhenIOpenTheList(string listName)
    {
        var listId = ctx.ListIds[listName];
        await ctx.Page.GotoAsync($"/lists/{listId}/view", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    [When("I fill in the list name {string}")]
    public async Task WhenIFillInTheListName(string name)
    {
        await ctx.Page.GetByRole(AriaRole.Textbox).First.FillAsync(name);
    }

    [When("I submit the list form")]
    public async Task WhenISubmitTheListForm()
    {
        await ctx.Page.GetByTestId("list-create-submit-button").ClickAsync();
        await ctx.Page.WaitForURLAsync("**/lists/*/view");
    }

    [When("I add item {string} to the list")]
    public async Task WhenIAddItemToTheList(string itemText)
    {
        await ctx.Page.GetByTestId("autocomplete-input-textfield").ClickAsync();
        await ctx.Page.GetByTestId("autocomplete-input-textfield").PressSequentiallyAsync(itemText);
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
    }

    [When("I toggle {string} as done")]
    public async Task WhenIToggleAsDone(string itemText)
    {
        var item = ctx.Page.GetByTestId($"toggle-item-{itemText}");
        var selector = $"[data-testid='toggle-item-{itemText}']";
        await item.WaitForAsync();
        await ctx.Page.DispatchEventAsync(selector, "click");
    }

    [When("I delete the list {string}")]
    public async Task WhenIDeleteTheList(string listName)
    {
        await ctx.Page.GetByTestId($"list-item-menu-button-{listName}").ClickAsync();
        await ctx.Page.GetByTestId("delete-list-button").ClickAsync();
    }

    [Then("I am on the list view page")]
    public async Task ThenIAmOnTheListViewPage()
    {
        await ctx.Page.WaitForURLAsync("**/lists/*/view");
    }

    [Then("{string} appears in the list overview")]
    public async Task ThenAppearsInTheListOverview(string listName)
    {
        await ctx.Page.GetByTestId($"list-item-{listName}").WaitForAsync();
    }

    [Then("{string} appears in the list")]
    public async Task ThenAppearsInTheList(string itemText)
    {
        await ctx.Page.Locator("[data-section='unchecked-items']")
            .GetByText(itemText).WaitForAsync();
    }

    [Then("{string} is shown as checked")]
    public async Task ThenIsShownAsChecked(string itemText)
    {
        await ctx.Page.Locator("[data-section='checked-items'] li")
            .Filter(new LocatorFilterOptions { HasText = itemText })
            .WaitForAsync();
    }

    [Then("{string} no longer appears in the list overview")]
    public async Task ThenNoLongerAppearsInTheListOverview(string listName)
    {
        await ctx.Page.GetByTestId($"list-item-{listName}")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }
}

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
        // Wait for the POST 201 BEFORE the URL wait so a server-side failure surfaces as a
        // precise response-match miss instead of an opaque "URL didn't change in 30s". Form's
        // catch swallows mutation errors to console only, so without this the test has no
        // visibility into the actual cause when CI contention slows the POST past 30s.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/lists")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("list-create-submit-button").ClickAsync();
        await responseTask;
        await ctx.Page.WaitForURLAsync("**/lists/*/view");
    }

    [When("I delete the list {string}")]
    public async Task WhenIDeleteTheList(string listName)
    {
        await ctx.Page.GetByTestId($"list-item-menu-button-{listName}").ClickAsync();
        await ctx.Page.GetByTestId("delete-list-button").ClickAsync();
    }

    [When("I open the list edit page for {string}")]
    public async Task WhenIOpenTheListEditPageFor(string listName)
    {
        // Goes direct to the edit URL because the overview's edit affordance is currently
        // disabled (TODO in routes/lists/index.tsx). The view-page Edit button does navigate
        // here, but it lives inside a shared component without a testid, so URL nav is the
        // most stable path until that affordance is testidified.
        var listId = ctx.ListIds[listName];
        await ctx.Page.GotoAsync(
            $"/lists/{listId}/edit",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    [When("I save the list")]
    public async Task WhenISaveTheList()
    {
        // Explicitly wait for the PUT to complete before the next step navigates away —
        // otherwise the mutation can race against navigation and TanStack Query serves a
        // stale list overview (the onSuccess invalidation fires AFTER the new mount has
        // already consumed the cache).
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/lists/")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        await ctx.Page.GetByTestId("list-edit-save-button").ClickAsync();
        await responseTask;
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

    [Then("{string} no longer appears in the list overview")]
    public async Task ThenNoLongerAppearsInTheListOverview(string listName)
    {
        await ctx.Page.GetByTestId($"list-item-{listName}")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }
}

using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

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

    [Given("{string} has created a list named {string}")]
    public async Task GivenHasCreatedAListNamed(string alias, string listName)
    {
        // Seed a list with a creator other than the currently-logged-in user, so role-policy
        // negatives (non-creator Member tries to edit/delete) can be exercised. Goes through
        // the List.Create factory rather than open-coding the row so the seeded list is
        // semantically identical to one produced by the CreateList slice.
        var scenarioSuffix = ctx.DatabaseName[^8..];
        var creatorUserId = $"user-{alias}-{scenarioSuffix}";

        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = List.Create(listName, null, ctx.HouseholdId, creatorUserId);
        if (creation.IsFailed)
        {
            throw new InvalidOperationException(
                $"Seed failed for list '{listName}': {string.Join(", ", creation.Errors.Select(e => e.Message))}");
        }

        db.Lists.Add(creation.Value);
        await db.SaveChangesAsync();
        ctx.ListIds[listName] = creation.Value.Id;
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

    [When("I POST a list with an empty name via the API")]
    public async Task WhenIPostAListWithAnEmptyNameViaTheApi()
    {
        // Goes through TestApiClient (not the form) to bypass HTML5 required-validation
        // and exercise the slice's Result<T>.ToValidationProblem() branch directly.
        ctx.LastApiResponse = await api.TryCreateListAsync("");
    }

    [When("I GET the lists of that household via the API")]
    public async Task WhenIGetTheListsOfThatHouseholdViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetListsAsync();
    }

    [When("I DELETE the list {string} via the API")]
    public async Task WhenIDeleteTheListViaTheApi(string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryDeleteListAsync(listId);
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

using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Sync;

[Binding]
public class RevisionApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // Reqnroll creates one instance of this binding class per scenario, so this list accumulates
    // the revisions captured within a single scenario.
    private readonly List<string> _revisions = new();

    [When("I capture the revision of list {string} via the API")]
    public async Task WhenICaptureTheRevisionOfList(string listName)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListRevisionAsync(listId);
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        _revisions.Add(json.GetProperty("rev").GetString()!);
    }

    [When("I capture the revision of inventory {string} via the API")]
    public async Task WhenICaptureTheRevisionOfInventory(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var response = await api.TryGetInventoryRevisionAsync(inventoryId);
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        _revisions.Add(json.GetProperty("rev").GetString()!);
    }

    [When("I capture the expiry-calendar revision via the API")]
    public async Task WhenICaptureTheExpiryCalendarRevision()
    {
        var response = await api.TryGetExpiryCalendarRevisionAsync();
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        _revisions.Add(json.GetProperty("rev").GetString()!);
    }

    // Edits an item's text straight through the DbContext (bypassing the API) so a scenario can mutate
    // a NON-perishable inventory item — used to prove that edit does NOT move the calendar token.
    [When("I edit the text of item {string} in inventory {string} to {string} via the database")]
    public async Task WhenIEditTheTextOfItemViaTheDatabase(string itemText, string inventoryName, string newText)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inventoryId = ctx.InventoryIds[inventoryName];
        var item = await db.InventoryItems.FirstAsync(i => i.InventoryId == inventoryId && i.Text == itemText);
        item.Text = newText;
        await db.SaveChangesAsync();
    }

    [When("I rename the list {string} to {string} via the database")]
    public async Task WhenIRenameTheListViaTheDatabase(string listName, string newName)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var listId = ctx.ListIds[listName];
        var list = await db.Lists.FirstAsync(l => l.Id == listId);
        list.Name = newName;
        await db.SaveChangesAsync();
    }

    [When("I GET the expiry-calendar revision via the API")]
    public async Task WhenIGetTheExpiryCalendarRevision()
    {
        ctx.LastApiResponse = await api.TryGetExpiryCalendarRevisionAsync();
    }

    [When("I GET the revision of list {string} via the API")]
    public async Task WhenIGetTheRevisionOfList(string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryGetListRevisionAsync(listId);
    }

    [Then("the two captured revisions differ")]
    public void ThenTheTwoCapturedRevisionsDiffer()
    {
        Assert.Equal(2, _revisions.Count);
        Assert.NotEqual(_revisions[0], _revisions[1]);
    }

    [Then("the two captured revisions are equal")]
    public void ThenTheTwoCapturedRevisionsAreEqual()
    {
        Assert.Equal(2, _revisions.Count);
        Assert.Equal(_revisions[0], _revisions[1]);
    }
}

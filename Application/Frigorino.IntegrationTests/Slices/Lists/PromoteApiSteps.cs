using System.Text.Json;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class PromoteApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    private JsonElement _lastToggle;

    [Given("the product {string} is in the catalog")]
    public async Task GivenTheProductIsInTheCatalog(string normalizedName)
    {
        // Classification is fire-and-forget on item add; poll real Postgres until the row lands.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var exists = await db.Products.AsNoTracking().AnyAsync(p =>
                p.HouseholdId == ctx.HouseholdId && p.NormalizedName == normalizedName);
            if (exists)
            {
                return;
            }
            await Task.Delay(100);
        }
        throw new Exception($"Product '{normalizedName}' was not classified within 10s.");
    }

    [When("I toggle item {string} in list {string} via the API")]
    public async Task WhenIToggleItemViaTheApi(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        var response = await api.TryToggleListItemStatusAsync(listId, itemId);
        Assert.Equal(200, response.Status);
        _lastToggle = (await response.JsonAsync())!.Value;
    }

    [Then("the toggle response has a promote suggestion with handling {string}")]
    public void ThenToggleHasPromoteWithHandling(string handling)
    {
        Assert.True(_lastToggle.TryGetProperty("promote", out var promote)
            && promote.ValueKind == JsonValueKind.Object);
        Assert.Equal(handling, promote.GetProperty("expiryHandling").GetString());
    }

    [Then("the promote suggestion has a non-null suggested expiry")]
    public void ThenPromoteHasNonNullExpiry()
    {
        var promote = _lastToggle.GetProperty("promote");
        Assert.Equal(JsonValueKind.String, promote.GetProperty("suggestedExpiry").ValueKind);
    }

    [Then("the toggle response has no promote suggestion")]
    public void ThenToggleHasNoPromote()
    {
        var hasPromote = _lastToggle.TryGetProperty("promote", out var promote)
            && promote.ValueKind == JsonValueKind.Object;
        Assert.False(hasPromote);
    }
}

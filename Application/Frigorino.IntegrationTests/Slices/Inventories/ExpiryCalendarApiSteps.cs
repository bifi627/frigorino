using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class ExpiryCalendarApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("an inventory {string} has an item {string} expiring in {int} days")]
    public async Task GivenAnInventoryHasAnItemExpiringInDays(string inventoryName, string itemText, int days)
    {
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(days);
        await SeedItemAsync(inventoryName, itemText, expiry);
    }

    [Given("an inventory {string} has an item {string} with no expiry")]
    public async Task GivenAnInventoryHasAnItemWithNoExpiry(string inventoryName, string itemText)
    {
        await SeedItemAsync(inventoryName, itemText, null);
    }

    [When("I GET the expiry calendar via the API")]
    public async Task WhenIGetTheExpiryCalendarViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetExpiryCalendarAsync();
    }

    [Then("the API expiry calendar contains {string}")]
    public async Task ThenTheApiExpiryCalendarContains(string text)
    {
        var body = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        var found = body.EnumerateArray().Any(e => e.GetProperty("text").GetString() == text);
        Xunit.Assert.True(found, $"Expected the expiry calendar to contain '{text}'.");
    }

    [Then("the API expiry calendar does not contain {string}")]
    public async Task ThenTheApiExpiryCalendarDoesNotContain(string text)
    {
        var body = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        var found = body.EnumerateArray().Any(e => e.GetProperty("text").GetString() == text);
        Xunit.Assert.False(found, $"Expected the expiry calendar NOT to contain '{text}'.");
    }

    private async Task SeedItemAsync(string inventoryName, string itemText, DateOnly? expiry)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!ctx.InventoryIds.TryGetValue(inventoryName, out var inventoryId))
        {
            var creation = Inventory.Create(inventoryName, null, ctx.HouseholdId, ctx.UserContext.UserId);
            if (creation.IsFailed)
            {
                throw new InvalidOperationException(
                    $"Seed failed for inventory '{inventoryName}': {string.Join(", ", creation.Errors.Select(e => e.Message))}");
            }

            db.Inventories.Add(creation.Value);
            await db.SaveChangesAsync();
            inventoryId = creation.Value.Id;
            ctx.InventoryIds[inventoryName] = inventoryId;
        }

        db.InventoryItems.Add(new InventoryItem
        {
            InventoryId = inventoryId,
            Text = itemText,
            ExpiryDate = expiry,
            SortOrder = 0,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }
}

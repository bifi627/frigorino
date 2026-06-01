using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Notifications;

[Binding]
public class ExpiryScanApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // Must match TestWebApplicationFactory's MaintenanceSettings:TriggerToken.
    private const string ValidMaintenanceKey = "integration-test-maintenance-key";

    [Given("I am opted in to expiry notifications with a registered device")]
    public async Task GivenIAmOptedInWithARegisteredDevice()
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var settings = UserSettings.Create(ctx.UserContext.UserId);
        settings.SetExpiryNotifications(enabled: true, leadDays: UserSettings.DefaultExpiryLeadDays);
        db.UserSettings.Add(settings);
        db.FcmTokens.Add(FcmToken.Create(ctx.UserContext.UserId, "test-device-token"));
        await db.SaveChangesAsync();
    }

    [Given("an inventory {string} with an item {string} expiring in {int} day(s)")]
    public async Task GivenAnInventoryWithAnItemExpiringIn(
        string inventoryName, string itemText, int daysUntilExpiry)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Inventory.Create(inventoryName, null, ctx.HouseholdId, ctx.UserContext.UserId);
        if (creation.IsFailed)
        {
            throw new InvalidOperationException(
                $"Seed failed for inventory '{inventoryName}': {string.Join(", ", creation.Errors.Select(e => e.Message))}");
        }

        db.Inventories.Add(creation.Value);
        await db.SaveChangesAsync();
        ctx.InventoryIds[inventoryName] = creation.Value.Id;

        // Seed the item directly with an expiry relative to the server's UTC "today" — the same clock
        // the endpoint uses (DateOnly.FromDateTime(DateTime.UtcNow)) — so it lands inside the lead window.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.InventoryItems.Add(new InventoryItem
        {
            InventoryId = creation.Value.Id,
            Text = itemText,
            ExpiryDate = today.AddDays(daysUntilExpiry),
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    [When("I POST the expiry scan with a valid maintenance key")]
    public async Task WhenIPostTheExpiryScanWithAValidKey()
    {
        ctx.LastApiResponse = await api.TryTriggerExpiryScanAsync(ValidMaintenanceKey);
    }

    [When("I POST the expiry scan with an invalid maintenance key")]
    public async Task WhenIPostTheExpiryScanWithAnInvalidKey()
    {
        ctx.LastApiResponse = await api.TryTriggerExpiryScanAsync("wrong-key");
    }

    // Fires two scans at once to exercise the real (UserId, HouseholdId, SentOn) unique index.
    // The two requests may serialize (the second sees the first's ledger row via the in-memory
    // pre-filter and skips) OR genuinely race (both pass the pre-filter, both INSERT, and the loser
    // catches SQLSTATE 23505). Either way the invariant is the same: both respond 200 and exactly
    // one dispatch row exists. This can only go red if the claim-slot-first fix regresses.
    [When("I trigger the expiry scan twice concurrently")]
    public async Task WhenITriggerTheExpiryScanTwiceConcurrently()
    {
        var post1 = api.TryTriggerExpiryScanAsync(ValidMaintenanceKey);
        var post2 = api.TryTriggerExpiryScanAsync(ValidMaintenanceKey);
        var responses = await Task.WhenAll(post1, post2);
        ctx.ConcurrentApiResponses = responses;
    }

    [Then("both concurrent API responses have status {int}")]
    public void ThenBothConcurrentApiResponsesHaveStatus(int expected)
    {
        Assert.NotNull(ctx.ConcurrentApiResponses);
        Assert.All(ctx.ConcurrentApiResponses!, r => Assert.Equal(expected, r.Status));
    }

    [Then("exactly {int} notification dispatch(es) exists for me today")]
    public async Task ThenExactlyNotificationDispatchesExistForMeToday(int expected)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var count = await db.NotificationDispatches.CountAsync(
            d => d.UserId == ctx.UserContext.UserId
                 && d.HouseholdId == ctx.HouseholdId
                 && d.SentOn == today);
        Assert.Equal(expected, count);
    }
}

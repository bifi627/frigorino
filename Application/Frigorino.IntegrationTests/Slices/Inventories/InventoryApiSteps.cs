using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class InventoryApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("{string} has created an inventory named {string}")]
    public async Task GivenHasCreatedAnInventoryNamed(string alias, string inventoryName)
    {
        // Seed an inventory with a creator other than the currently-logged-in user, so
        // role-policy negatives (non-creator Member tries to delete) can be exercised. Goes
        // through the Inventory.Create factory rather than open-coding the row so the seed is
        // semantically identical to one produced by the CreateInventory slice.
        var scenarioSuffix = ctx.DatabaseName[^8..];
        var creatorUserId = $"user-{alias}-{scenarioSuffix}";

        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Inventory.Create(inventoryName, null, ctx.HouseholdId, creatorUserId);
        if (creation.IsFailed)
        {
            throw new InvalidOperationException(
                $"Seed failed for inventory '{inventoryName}': {string.Join(", ", creation.Errors.Select(e => e.Message))}");
        }

        db.Inventories.Add(creation.Value);
        await db.SaveChangesAsync();
        ctx.InventoryIds[inventoryName] = creation.Value.Id;
    }

    [When("I POST an inventory with an empty name via the API")]
    public async Task WhenIPostAnInventoryWithAnEmptyNameViaTheApi()
    {
        // Goes through TestApiClient (not the form) to bypass HTML5 required-validation
        // and exercise the slice's Result<T>.ToValidationProblem() branch directly.
        ctx.LastApiResponse = await api.TryCreateInventoryAsync("");
    }

    [When("I GET the inventories of that household via the API")]
    public async Task WhenIGetTheInventoriesOfThatHouseholdViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetInventoriesAsync();
    }

    [When("I DELETE the inventory {string} via the API")]
    public async Task WhenIDeleteTheInventoryViaTheApi(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        ctx.LastApiResponse = await api.TryDeleteInventoryAsync(inventoryId);
    }
}

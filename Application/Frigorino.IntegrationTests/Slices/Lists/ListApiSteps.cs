using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ListApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
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
}

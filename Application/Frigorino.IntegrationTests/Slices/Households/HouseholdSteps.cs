using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Households;

[Binding]
public class HouseholdSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I fill in the household name {string}")]
    public async Task WhenIFillInTheHouseholdName(string name)
    {
        await ctx.Page.GetByRole(AriaRole.Textbox).First.FillAsync(name);
    }

    [When("I submit the household form")]
    public async Task WhenISubmitTheHouseholdForm()
    {
        await ctx.Page.GetByTestId("household-create-submit-button").ClickAsync();
        await ctx.Page.WaitForURLAsync("**/");
    }

    [Then("I am redirected to {string}")]
    public async Task ThenIAmRedirectedTo(string path)
    {
        await ctx.Page.WaitForURLAsync($"**{path}");
    }

    // ---- Delete-flow steps ----

    [When("I open the household management menu")]
    public async Task WhenIOpenTheHouseholdManagementMenu()
    {
        await ctx.Page.GetByTestId("household-manage-menu-toggle").ClickAsync();
    }

    [When("I select delete household from the menu")]
    public async Task WhenISelectDeleteHouseholdFromTheMenu()
    {
        await ctx.Page.GetByTestId("household-manage-menu-delete").ClickAsync();
    }

    [When("I type {string} into the delete confirmation input")]
    public async Task WhenITypeIntoTheDeleteConfirmationInput(string text)
    {
        await ctx.Page.GetByTestId("household-delete-confirm-input").FillAsync(text);
    }

    [When("I confirm the household deletion")]
    public async Task WhenIConfirmTheHouseholdDeletion()
    {
        await ctx.Page.GetByTestId("household-delete-confirm-button").ClickAsync();
    }

    [When("I cancel the household deletion")]
    public async Task WhenICancelTheHouseholdDeletion()
    {
        await ctx.Page.GetByTestId("household-delete-cancel-button").ClickAsync();
    }

    [Then("the delete confirmation button is disabled")]
    public async Task ThenTheDeleteConfirmationButtonIsDisabled()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-delete-confirm-button"))
            .ToBeDisabledAsync();
    }

    [Then("the delete confirmation button is enabled")]
    public async Task ThenTheDeleteConfirmationButtonIsEnabled()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-delete-confirm-button"))
            .ToBeEnabledAsync();
    }

    [Then("the delete confirmation dialog is closed")]
    public async Task ThenTheDeleteConfirmationDialogIsClosed()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-delete-confirm-button"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the household management menu trigger is not visible")]
    public async Task ThenTheHouseholdManagementMenuTriggerIsNotVisible()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-manage-menu-toggle"))
            .Not.ToBeVisibleAsync();
    }

    // ---- Seeding step for non-owner scenarios ----

    [Given("an existing household {string} owned by {string} with me as a {string}")]
    public async Task GivenAnExistingHouseholdOwnedByWithMeAs(
        string householdName, string ownerAlias, string myRole)
    {
        // Requires `Given I am logged in as "<alias>"` to have run first so ctx.UserContext is populated.
        var scenarioSuffix = ctx.DatabaseName[^8..];
        var ownerUserId = $"user-{ownerAlias}-{scenarioSuffix}";

        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        db.Users.AddRange(
            new User
            {
                ExternalId = ownerUserId,
                Name = ownerAlias,
                Email = $"{ownerAlias}@test.frigorino.local",
                CreatedAt = now,
                LastLoginAt = now,
                IsActive = true,
            },
            new User
            {
                ExternalId = ctx.UserContext.UserId,
                Name = ctx.UserContext.Name,
                Email = ctx.UserContext.Email,
                CreatedAt = now,
                LastLoginAt = now,
                IsActive = true,
            });

        var household = new Household
        {
            Name = householdName,
            CreatedByUserId = ownerUserId,
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true,
        };
        db.Households.Add(household);
        await db.SaveChangesAsync();

        var role = myRole.ToLowerInvariant() switch
        {
            "member" => HouseholdRole.Member,
            "admin" => HouseholdRole.Admin,
            "owner" => HouseholdRole.Owner,
            _ => throw new ArgumentException($"Unknown role: {myRole}"),
        };

        db.UserHouseholds.AddRange(
            new UserHousehold
            {
                UserId = ownerUserId,
                HouseholdId = household.Id,
                Role = HouseholdRole.Owner,
                JoinedAt = now,
                IsActive = true,
            },
            new UserHousehold
            {
                UserId = ctx.UserContext.UserId,
                HouseholdId = household.Id,
                Role = role,
                JoinedAt = now,
                IsActive = true,
            });
        await db.SaveChangesAsync();

        ctx.HouseholdId = household.Id;
        await api.SetCurrentHouseholdAsync(household.Id);
    }

    [Given("the household also has {string} as a {string}")]
    public async Task GivenTheHouseholdAlsoHas(string alias, string roleName)
    {
        var scenarioSuffix = ctx.DatabaseName[^8..];
        var externalId = $"user-{alias}-{scenarioSuffix}";

        var role = roleName.ToLowerInvariant() switch
        {
            "member" => HouseholdRole.Member,
            "admin" => HouseholdRole.Admin,
            "owner" => HouseholdRole.Owner,
            _ => throw new ArgumentException($"Unknown role: {roleName}"),
        };

        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        db.Users.Add(new User
        {
            ExternalId = externalId,
            Name = alias,
            Email = $"{alias}@test.frigorino.local",
            CreatedAt = now,
            LastLoginAt = now,
            IsActive = true,
        });

        db.UserHouseholds.Add(new UserHousehold
        {
            UserId = externalId,
            HouseholdId = ctx.HouseholdId,
            Role = role,
            JoinedAt = now,
            IsActive = true,
        });

        await db.SaveChangesAsync();
    }
}

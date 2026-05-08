namespace Frigorino.IntegrationTests.Slices.Households.Members;

[Binding]
public class MemberSteps(ScenarioContextHolder ctx)
{
    [Then("the household members list shows {int} members")]
    public async Task ThenTheHouseholdMembersListShowsMembers(int expectedCount)
    {
        var rows = ctx.Page.GetByTestId("household-members-list").Locator(":scope > li");
        await Assertions.Expect(rows).ToHaveCountAsync(expectedCount);
    }

    [Then("the household member {string} has the role {string}")]
    public async Task ThenTheHouseholdMemberHasTheRole(string alias, string expectedRole)
    {
        var externalId = ExternalIdFor(alias);
        var roleChip = ctx.Page.GetByTestId($"household-member-{externalId}-role");
        await Assertions.Expect(roleChip).ToHaveTextAsync(expectedRole);
    }

    [Then("the household members appear in this order: {string}, {string}, {string}")]
    public async Task ThenTheHouseholdMembersAppearInThisOrder(string first, string second, string third)
    {
        var rows = ctx.Page.GetByTestId("household-members-list").Locator(":scope > li");
        await Assertions.Expect(rows).ToHaveCountAsync(3);

        var expected = new[] { first, second, third };
        for (var i = 0; i < expected.Length; i++)
        {
            var externalId = ExternalIdFor(expected[i]);
            await Assertions.Expect(rows.Nth(i)).ToHaveAttributeAsync(
                "data-testid", $"household-member-{externalId}");
        }
    }

    [When("I open the add member dialog")]
    public async Task WhenIOpenTheAddMemberDialog()
    {
        await ctx.Page.GetByTestId("household-add-member-button").ClickAsync();
    }

    [When("I fill in the new member email {string}")]
    public async Task WhenIFillInTheNewMemberEmail(string email)
    {
        await ctx.Page.GetByTestId("household-add-member-email-input").FillAsync(email);
    }

    [When("I submit the add member form")]
    public async Task WhenISubmitTheAddMemberForm()
    {
        await ctx.Page.GetByTestId("household-add-member-submit").ClickAsync();
    }

    [Then("the add member error contains {string}")]
    public async Task ThenTheAddMemberErrorContains(string substring)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-add-member-error"))
            .ToContainTextAsync(substring);
    }

    [When("I open the member menu for {string}")]
    public async Task WhenIOpenTheMemberMenuFor(string alias)
    {
        var externalId = ExternalIdFor(alias);
        await ctx.Page.GetByTestId($"household-member-{externalId}-menu-toggle").ClickAsync();
    }

    [When("I click remove from the member menu")]
    public async Task WhenIClickRemoveFromTheMemberMenu()
    {
        await ctx.Page.GetByTestId("household-member-action-remove").ClickAsync();
    }

    [When("I confirm the member removal")]
    public async Task WhenIConfirmTheMemberRemoval()
    {
        await ctx.Page.GetByTestId("household-member-remove-confirm").ClickAsync();
    }

    [Then("the add member button is not visible")]
    public async Task ThenTheAddMemberButtonIsNotVisible()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-add-member-button"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the member menu for {string} is not visible")]
    public async Task ThenTheMemberMenuForIsNotVisible(string alias)
    {
        var externalId = ExternalIdFor(alias);
        await Assertions.Expect(ctx.Page.GetByTestId($"household-member-{externalId}-menu-toggle"))
            .Not.ToBeVisibleAsync();
    }

    private string ExternalIdFor(string alias)
    {
        var scenarioSuffix = ctx.DatabaseName[^8..];
        return $"user-{alias}-{scenarioSuffix}";
    }
}

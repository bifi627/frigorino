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

    private string ExternalIdFor(string alias)
    {
        var scenarioSuffix = ctx.DatabaseName[^8..];
        return $"user-{alias}-{scenarioSuffix}";
    }
}

namespace Frigorino.IntegrationTests.Slices.CurrentHousehold;

[Binding]
public class CurrentHouseholdSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I switch the active household to {string}")]
    public async Task WhenISwitchTheActiveHouseholdTo(string name)
    {
        await ctx.Page.GetByTestId("household-switcher-toggle").ClickAsync();
        await ctx.Page.GetByTestId($"household-switcher-option-{name}").ClickAsync();
    }

    [When("I attempt to switch the active household to the seeded one via the API")]
    public async Task WhenIAttemptToSwitchTheActiveHouseholdToTheSeededOneViaTheApi()
    {
        // Bypasses the UI — the household selector wouldn't expose another user's household
        // to begin with, so the security branch needs a direct API call to be exercised.
        ctx.LastApiResponse = await api.TrySetCurrentHouseholdAsync(ctx.HouseholdId);
    }

    [Then("the active household should be {string}")]
    public async Task ThenTheActiveHouseholdShouldBe(string name)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-switcher-current-name"))
            .ToHaveTextAsync(name);
    }

    [When("I reload the page")]
    public async Task WhenIReloadThePage()
    {
        await ctx.Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }
}

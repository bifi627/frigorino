namespace Frigorino.IntegrationTests.Slices.CurrentHousehold;

[Binding]
public class CurrentHouseholdSteps(ScenarioContextHolder ctx)
{
    [When("I switch the active household to {string}")]
    public async Task WhenISwitchTheActiveHouseholdTo(string name)
    {
        await ctx.Page.GetByTestId("household-switcher-toggle").ClickAsync();
        await ctx.Page.GetByTestId($"household-switcher-option-{name}").ClickAsync();
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

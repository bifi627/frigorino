namespace Frigorino.IntegrationTests.Slices.Onboarding;

[Binding]
public class OnboardingSteps(ScenarioContextHolder ctx)
{
    [When("I skip onboarding")]
    public async Task WhenISkipOnboarding()
    {
        await ctx.Page.GetByTestId("onboarding-skip-button").ClickAsync();
        await ctx.Page.WaitForURLAsync("**/");
    }

    [Then("the onboarding page is not shown")]
    public async Task ThenTheOnboardingPageIsNotShown()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("onboarding-skip-button"))
            .Not.ToBeVisibleAsync();
    }
}

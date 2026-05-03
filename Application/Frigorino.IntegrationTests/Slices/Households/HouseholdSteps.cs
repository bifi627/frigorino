namespace Frigorino.IntegrationTests.Slices.Households;

[Binding]
public class HouseholdSteps(ScenarioContextHolder ctx)
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
}

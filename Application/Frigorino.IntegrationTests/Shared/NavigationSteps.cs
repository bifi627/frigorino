namespace Frigorino.IntegrationTests.Shared;

[Binding]
public class NavigationSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("I am logged in as {string}")]
    public void GivenIAmLoggedInAs(string userAlias)
    {
        // Append a per-scenario suffix derived from the unique DB name to prevent
        // InitialConnectionMiddleware's static _checkedConnections cache from skipping
        // user creation in a fresh database.
        var scenarioSuffix = ctx.DatabaseName[^8..];
        ctx.UserContext.UserId = $"user-{userAlias}-{scenarioSuffix}";
        ctx.UserContext.Email = $"{userAlias}@test.frigorino.local";
        ctx.UserContext.Name = userAlias;
    }

    [Given("I am logged in with an active household")]
    public async Task GivenIAmLoggedInWithAnActiveHousehold()
    {
        GivenIAmLoggedInAs("owner");
        var householdId = await api.CreateHouseholdAsync("Test Household");
        await api.SetCurrentHouseholdAsync(householdId);
        ctx.HouseholdId = householdId;
    }

    [When("I navigate to {string}")]
    public async Task WhenINavigateTo(string path)
    {
        // DOMContentLoaded over NetworkIdle: Playwright officially discourages NetworkIdle
        // because a SPA with TanStack Query / background refetches may never settle (caused a
        // 30s timeout flake on `When I navigate to "/"`) and concurrent programmatic
        // navigations interrupt a still-waiting Goto with "navigation interrupted by
        // another navigation to about:blank". Step-level assertions already use retrying
        // expectations (ToBeVisibleAsync etc.), so they don't need NetworkIdle to settle.
        await ctx.Page.GotoAsync(path, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    [Then("the page title should contain {string}")]
    public async Task ThenThePageTitleShouldContain(string expected)
    {
        var title = await ctx.Page.TitleAsync();
        if (!title.Contains(expected))
        {
            var url = ctx.Page.Url;
            var content = await ctx.Page.ContentAsync();
            throw new Exception(
                $"Expected title to contain \"{expected}\" but was \"{title}\".\n" +
                $"URL: {url}\n" +
                $"Page content (first 2000 chars):\n{content[..Math.Min(content.Length, 2000)]}");
        }
    }
}

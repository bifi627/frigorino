namespace Frigorino.IntegrationTests.StepDefinitions;

[Binding]
public class CommonSteps(ScenarioContextHolder ctx)
{
    [Given("I am logged in as {string}")]
    public void GivenIAmLoggedInAs(string userAlias)
    {
        ctx.UserContext.UserId = $"user-{userAlias}";
        ctx.UserContext.Email = $"{userAlias}@test.frigorino.local";
        ctx.UserContext.Name = userAlias;
    }

    [When("I navigate to {string}")]
    public async Task WhenINavigateTo(string path)
    {
        await ctx.Page.GotoAsync(path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
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

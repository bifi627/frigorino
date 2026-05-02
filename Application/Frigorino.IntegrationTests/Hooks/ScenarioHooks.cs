namespace Frigorino.IntegrationTests.Hooks;

[Binding]
public class ScenarioHooks(ScenarioContextHolder ctx, TestUserContext userContext)
{
    [BeforeScenario(Order = 0)]
    public async Task BeforeScenario()
    {
        var dbName = "frig_test_" + Guid.NewGuid().ToString("N");
        ctx.DatabaseName = dbName;
        ctx.UserContext = userContext;

        await PostgresFixture.Instance.CreateDatabaseAsync(dbName);

        var factory = new TestWebApplicationFactory
        {
            ConnectionString = PostgresFixture.Instance.ConnectionStringFor(dbName),
        };

        // Access Services to trigger host initialisation (starts Kestrel, runs EF migrations)
        _ = factory.Services;
        ctx.Factory = factory;

        var browserContext = await PlaywrightFixture.Instance.Browser.NewContextAsync(new()
        {
            BaseURL = factory.BaseAddress,
            IgnoreHTTPSErrors = true,
        });

        // Inject test-user identity headers on every API call
        await browserContext.RouteAsync("**/api/**", async route =>
        {
            var headers = new Dictionary<string, string>(route.Request.Headers)
            {
                ["X-Test-User"] = userContext.UserId,
                ["X-Test-Email"] = userContext.Email,
                ["X-Test-Name"] = userContext.Name,
            };
            await route.ContinueAsync(new RouteContinueOptions { Headers = headers });
        });

        ctx.BrowserContext = browserContext;
        ctx.Page = await browserContext.NewPageAsync();
    }

    [AfterScenario(Order = 100)]
    public async Task AfterScenario()
    {
        if (ctx.Page != null)
            await ctx.Page.CloseAsync();

        if (ctx.BrowserContext != null)
            await ctx.BrowserContext.CloseAsync();

        if (ctx.Factory != null)
            await ctx.Factory.DisposeAsync();

        if (ctx.DatabaseName != null)
            await PostgresFixture.Instance.DropDatabaseAsync(ctx.DatabaseName);
    }
}

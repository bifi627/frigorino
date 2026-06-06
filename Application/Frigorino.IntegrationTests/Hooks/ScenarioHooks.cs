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

        // Boot the host (single Kestrel build, kernel-allocated port, runs EF migrations)
        factory.StartServer();
        ctx.Factory = factory;

        var browserContext = await PlaywrightFixture.Instance.Browser.NewContextAsync(new()
        {
            BaseURL = factory.BaseAddress,
            IgnoreHTTPSErrors = true,
            // Pin the browser locale so the app's i18n language detection is deterministic
            // across host machines (a German dev box otherwise runs the suite in German).
            // English fixes the MUI X date field mask to MM/dd/yyyy, which the date-entry
            // steps rely on.
            Locale = "en-US",
        });

        // Bypass Firebase auth guard: inject a fake user before any page JS runs
        await browserContext.AddInitScriptAsync(@"
            window.__PLAYWRIGHT_TEST_USER__ = {
                uid: 'playwright-test-user',
                email: 'test@playwright.local',
                displayName: 'Playwright Test',
            };
        ");

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

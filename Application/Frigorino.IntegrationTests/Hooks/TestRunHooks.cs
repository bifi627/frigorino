namespace Frigorino.IntegrationTests.Hooks;

[Binding]
public class TestRunHooks
{
    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        await PostgresFixture.Instance.StartAsync();
        await PlaywrightFixture.Instance.InitializeAsync();
        SpaBuildHelper.EnsureSpaIsBuilt();
    }

    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        await PlaywrightFixture.Instance.DisposeAsync();
        await PostgresFixture.Instance.DisposeAsync();
    }
}

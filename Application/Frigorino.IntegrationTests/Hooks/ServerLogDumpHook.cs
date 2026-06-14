namespace Frigorino.IntegrationTests.Hooks;

/// <summary>
/// On the FIRST failing run, dumps the host's buffered Warning+ logs (including the framework's
/// Error-level log of any unhandled exception) into the xUnit test output. Kestrel handles requests
/// off the test's execution context, so the host's AddConsole() output isn't attributed to the
/// failing scenario by the runner — this explicit dump closes that diagnosability gap.
/// Runs before the Order=100 cleanup hook so the factory (and its LogSink) is still alive.
/// </summary>
[Binding]
public class ServerLogDumpHook(
    ScenarioContextHolder ctx,
    ScenarioContext scenarioContext,
    IReqnrollOutputHelper output)
{
    [AfterScenario(Order = 1)]
    public void DumpServerLogsOnFailure()
    {
        if (scenarioContext.TestError is null)
        {
            return;
        }

        var entries = ctx.Factory?.LogSink.Snapshot();
        if (entries is null || entries.Count == 0)
        {
            return;
        }

        output.WriteLine("===== Server logs (Warning+) for failed scenario =====");
        foreach (var entry in entries)
        {
            output.WriteLine(entry);
        }
        output.WriteLine("======================================================");
    }
}

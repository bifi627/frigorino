namespace Frigorino.IntegrationTests.Infrastructure;

public sealed class PlaywrightFixture : IAsyncDisposable
{
    public static readonly PlaywrightFixture Instance = new();

    private IPlaywright? _playwright;
    public IBrowser Browser { get; private set; } = null!;

    private PlaywrightFixture() { }

    public async Task InitializeAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
            throw new InvalidOperationException($"Playwright browser install failed (exit {exitCode}).");

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = false });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser != null)
            await Browser.CloseAsync();
        _playwright?.Dispose();
    }
}

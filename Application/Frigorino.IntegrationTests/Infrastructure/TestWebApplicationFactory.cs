using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Frigorino.IntegrationTests.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public required string ConnectionString { get; init; }

    public TestWebApplicationFactory()
    {
        // Bind to a kernel-allocated port; Kestrel itself owns the port between bind and accept,
        // so there is no TOCTOU window for another process to grab it.
        UseKestrel(0);
    }

    // Populated by WebApplicationFactory after StartServer() reads IServerAddressesFeature.
    public string BaseAddress => ClientOptions.BaseAddress.ToString().TrimEnd('/');

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("ConnectionStrings:Database", ConnectionString);

        var webRoot = SpaBuildHelper.FindWebProjectRoot();
        builder.UseContentRoot(webRoot);
        // Point WebRoot at the SPA build output so UseDefaultFiles can rewrite "/" → "/index.html"
        // and UseStaticFiles can serve assets. In production the Dockerfile copies these to wwwroot;
        // in tests we serve them directly from ClientApp/build.
        builder.UseWebRoot(Path.Combine(webRoot, "ClientApp", "build"));

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Disable HTTPS redirect — Kestrel has no HTTPS endpoint in tests
            services.Configure<HttpsRedirectionOptions>(opts => opts.HttpsPort = null);
        });
    }
}

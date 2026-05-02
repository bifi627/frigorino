using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Sockets;

namespace Frigorino.IntegrationTests.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public required string ConnectionString { get; init; }

    private IHost? _kestrelHost;
    private string? _baseAddress;

    public string BaseAddress => _baseAddress
        ?? throw new InvalidOperationException("Host has not been started yet. Access Services first.");

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build a TestServer-backed dummy host that WebApplicationFactory uses for Services/disposal
        var dummyHost = builder.Build();
        dummyHost.Start();

        // Pre-allocate a free port so we can set a concrete URL (port=0 does not reliably
        // populate IServerAddressesFeature when building a second host from DeferredHostBuilder)
        _baseAddress = $"http://127.0.0.1:{FindFreePort()}";
        builder.ConfigureWebHost(web => web.UseKestrel().UseUrls(_baseAddress));

        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        return dummyHost;
    }

    private static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("ConnectionStrings:Database", ConnectionString);

        var webRoot = SpaBuildHelper.FindWebProjectRoot();
        builder.UseContentRoot(webRoot);
        // Point the WebRoot at the SPA build output so UseDefaultFiles can rewrite "/" → "/index.html"
        // and UseStaticFiles can serve assets.  In production the Dockerfile copies these to wwwroot;
        // in tests we serve them directly from ClientApp/build.
        builder.UseWebRoot(Path.Combine(webRoot, "ClientApp", "build"));

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Disable HTTPS redirect — Kestrel has no HTTPS endpoint in tests
            services.Configure<HttpsRedirectionOptions>(opts => opts.HttpsPort = null);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        if (_kestrelHost != null)
        {
            await _kestrelHost.StopAsync();
            _kestrelHost.Dispose();
            _kestrelHost = null;
        }

        await base.DisposeAsync();
    }
}

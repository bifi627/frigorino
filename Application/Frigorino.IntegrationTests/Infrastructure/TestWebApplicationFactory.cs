using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Frigorino.IntegrationTests.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Buffers Warning+ server logs for this host so an AfterScenario hook can dump them on the
    /// first failing run (Kestrel request logs otherwise never reach the xUnit test output).
    /// </summary>
    public InMemoryLogSink LogSink { get; } = new();

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
        builder.UseSetting("Ai:Classifier:Enabled", "true");
        builder.UseSetting("Ai:QuantityExtractor:Enabled", "true");
        builder.UseSetting("Ai:ApiKey", "integration-test-stub-key");
        // Non-empty trigger key for the /internal/expiry-scan machine endpoint (an empty configured
        // token rejects everything with 404). The Notifications.Api.feature uses this same value.
        builder.UseSetting("MaintenanceSettings:TriggerToken", "integration-test-maintenance-key");

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
            // Mirror Warning+ records into an in-memory buffer the AfterScenario hook can dump on
            // failure — Console output from the Kestrel request context isn't attributed to the
            // failing scenario by the xUnit runner, so it never shows in `dotnet test`.
            logging.AddProvider(new InMemoryLoggerProvider(LogSink, LogLevel.Warning));
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Disable HTTPS redirect — Kestrel has no HTTPS endpoint in tests
            services.Configure<HttpsRedirectionOptions>(opts => opts.HttpsPort = null);

            // Replace the real OpenAI classifier with a deterministic, network-free stub. The
            // QueueingProductClassificationTrigger is registered (Ai:Classifier:Enabled=true above), so
            // the full slice→trigger→queue→job→DB path runs without any network call.
            services.RemoveAll<IItemClassifier>();
            services.AddScoped<IItemClassifier, StubItemClassifier>();

            // Replace the real OpenAI extractor with a deterministic, network-free stub. The
            // QueueingQuantityExtractionTrigger is registered (Ai:QuantityExtractor:Enabled=true above),
            // so the full slice→trigger→queue→job→DB path runs without any network call.
            services.RemoveAll<IQuantityExtractor>();
            services.AddScoped<IQuantityExtractor, StubQuantityExtractor>();

            // Deterministic, network-free recipe tag suggester (IRecipeTagSuggester is always
            // registered — real or Null — so RemoveAll + replace works regardless of AI config).
            services.RemoveAll<IRecipeTagSuggester>();
            services.AddScoped<IRecipeTagSuggester, StubRecipeTagSuggester>();

            // Replace the real (network-hitting) recipe importer with a deterministic stub. Registered
            // as a concrete type (no interface), so RemoveAll + re-add the concrete service.
            services.RemoveAll<RecipeImportService>();
            services.AddSingleton<RecipeImportService>(new StubRecipeImportService());

            // Real blob storage bound to a unique temp dir per factory instance, registered under BOTH
            // keyed storage interfaces (one shared instance per area) so the startup orphan-sweep operates
            // on the temp dir, never a real path. Only the AI classifiers stay stubbed; IImageProcessor
            // stays real. Keyed re-registration wins on resolution (last keyed descriptor for a key).
            services.RemoveAllKeyed<IFileStorage>(BlobAreas.ListItem);
            services.RemoveAllKeyed<IFileStorageMaintenance>(BlobAreas.ListItem);
            var blobRoot = Path.Combine(Path.GetTempPath(), "frigorino-it-blobs", Guid.NewGuid().ToString("N"));
            var blobStorage = new LocalFileStorage(blobRoot);
            services.AddKeyedSingleton<IFileStorage>(BlobAreas.ListItem, blobStorage);
            services.AddKeyedSingleton<IFileStorageMaintenance>(BlobAreas.ListItem, blobStorage);

            // Recipe attachments live in their own blob area. Give them a SEPARATE temp dir so the
            // per-area orphan sweep never sees another area's blobs as unreferenced (one shared dir
            // would let ListItem's sweep delete recipe-attachment blobs, and vice versa).
            services.RemoveAllKeyed<IFileStorage>(BlobAreas.RecipeAttachment);
            services.RemoveAllKeyed<IFileStorageMaintenance>(BlobAreas.RecipeAttachment);
            var attachmentBlobRoot = Path.Combine(Path.GetTempPath(), "frigorino-it-blobs", Guid.NewGuid().ToString("N"));
            var attachmentBlobStorage = new LocalFileStorage(attachmentBlobRoot);
            services.AddKeyedSingleton<IFileStorage>(BlobAreas.RecipeAttachment, attachmentBlobStorage);
            services.AddKeyedSingleton<IFileStorageMaintenance>(BlobAreas.RecipeAttachment, attachmentBlobStorage);
        });
    }
}

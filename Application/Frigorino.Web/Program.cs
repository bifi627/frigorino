using System.Reflection;
using System.Text.Json.Serialization;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Households.Blueprints;
using Frigorino.Features.Households.Members;
using Frigorino.Features.Households.Settings;
using Frigorino.Features.Lists.Blueprints;
using Frigorino.Features.Inventories;
using Frigorino.Features.Inventories.Items;
using Frigorino.Features.Inventories.Notifications;
using Frigorino.Features.Inventories.Settings;
using Frigorino.Features.Lists;
using Frigorino.Features.Lists.Items;
using Frigorino.Features.Recipes;
using Frigorino.Features.Recipes.Items;
using Frigorino.Features.Lists.Promote;
using Frigorino.Features.Me.ActiveHousehold;
using Frigorino.Features.Me.Settings;
using Frigorino.Features.Notifications;
using Frigorino.Features.Version;
using Frigorino.Infrastructure.Auth;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Notifications;
using Frigorino.Infrastructure.Services;
using Frigorino.Infrastructure.HealthChecks;
using Frigorino.Web.Middlewares;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using static Frigorino.Infrastructure.EntityFramework.DependencyInjection;

var isBuildTimeOpenApi =
    Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Enums serialize as their string names on the wire (not ints). Minimal-API slices read
// their JSON options from ConfigureHttpJsonOptions; this also drives the OpenAPI enum schema
// so the generated TS client emits string union types instead of `number`.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<Frigorino.Web.OpenApi.IntegerSchemaTransformer>();
    options.AddSchemaTransformer<Frigorino.Web.OpenApi.RequiredSchemaTransformer>();
});

builder.Services.AddEntityFramework(builder.Configuration);
if (!builder.Environment.IsEnvironment("IntegrationTest") && !isBuildTimeOpenApi)
{
    // DevAuth is a Development-only bypass for the Firebase JWT flow so a fresh clone
    // can run end-to-end without a real Firebase tenant. Both gates required.
    var devAuthEnabled = builder.Environment.IsDevelopment()
        && builder.Configuration.GetValue<bool>($"{DevAuthSettings.SECTION_NAME}:Enabled");

    if (devAuthEnabled)
    {
        builder.Services.AddDevAuth(builder.Configuration);
        builder.Services.AddScoped<INotificationSender, LogOnlyNotificationSender>();
    }
    else
    {
        builder.Services.AddFirebaseAuth(builder.Configuration);
        builder.Services.AddScoped<INotificationSender, FcmNotificationSender>();
    }
}
// Fallback: IntegrationTest + build-time paths skip the block above, so ensure
// INotificationSender always resolves (TryAddScoped is a no-op when already registered).
builder.Services.TryAddScoped<INotificationSender, LogOnlyNotificationSender>();

builder.Services.AddBackgroundTaskQueue();
builder.Services.AddFileStorage(builder.Configuration);
builder.Services.AddImageProcessing();
builder.Services.AddItemClassification(builder.Configuration);
builder.Services.AddQuantityExtraction(builder.Configuration);
builder.Services.AddRecipeQuantityExtraction(builder.Configuration);
builder.Services.AddMaintenanceServices(builder.Configuration);
builder.Services.AddExpiryNotifications(builder.Configuration);

if (!isBuildTimeOpenApi)
{
    builder.Services.Configure<FirebaseSettings>(
        builder.Configuration.GetSection(FirebaseSettings.SECTION_NAME));

    var healthCheckConnectionString = ConvertPostgresUrlToConnectionString(
        builder.Configuration.GetConnectionString("Database") ?? "");
    builder.Services
        .AddHealthChecks()
        .AddNpgSql(healthCheckConnectionString, name: "postgres", tags: new[] { "ready" })
        .AddCheck<ConfigHealthCheck>("config", tags: new[] { "ready" });
}
builder.Services.AddProblemDetails();

builder.Services.AddHttpContextAccessor();

// Add session support for household context
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddSpaStaticFiles(configuration => { configuration.RootPath = "ClientApp/build"; });

// OpenTelemetry → Grafana Cloud OTLP gateway. Activates only when the auth header is set
// (Railway env in stage/prod; dotnet user-secrets locally for opt-in). No header → no
// registration → zero Grafana dependency. Endpoint + protocol are non-secret and live
// in appsettings.json. Single endpoint serves traces (Tempo), metrics (Mimir), logs (Loki).
var otlpHeaders = builder.Configuration["OpenTelemetry:OtlpHeaders"];
if (!isBuildTimeOpenApi && !string.IsNullOrWhiteSpace(otlpHeaders))
{
    // Grafana Cloud's OTLP gateway is a base URL (e.g. /otlp); the OTel SDK appends
    // /v1/{traces,metrics,logs} ONLY when the endpoint comes from OTEL_EXPORTER_OTLP_ENDPOINT.
    // Setting OtlpExporterOptions.Endpoint programmatically silently sets the internal
    // AppendSignalPathToEndpoint = false (see OBSERVABILITY.md decision 2026-05-17),
    // so we append the signal path ourselves per exporter.
    var otlpBaseEndpoint = (builder.Configuration["OpenTelemetry:OtlpEndpoint"]
        ?? throw new InvalidOperationException("OpenTelemetry:OtlpEndpoint must be set when OpenTelemetry:OtlpHeaders is configured"))
        .TrimEnd('/');
    var otlpProtocol =
        string.Equals(builder.Configuration["OpenTelemetry:OtlpProtocol"], "grpc", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.Grpc
            : OtlpExportProtocol.HttpProtobuf;

    // Filter every Grafana panel/alert on deployment.environment so local debug data
    // never bleeds into stage/prod views (shared single stack — see OBSERVABILITY.md).
    var deploymentEnvironment = builder.Environment.EnvironmentName switch
    {
        "Production" => "prod",
        "Staging" => "stage",
        _ => "local",
    };

    var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    // RAILWAY_SERVICE_NAME is injected by Railway at runtime; "local" otherwise.
    var railwayServiceName = Environment.GetEnvironmentVariable("RAILWAY_SERVICE_NAME") ?? "frigorino-local";

    void ConfigureOtlpFor(string signalPath, OtlpExporterOptions opt)
    {
        opt.Endpoint = new Uri($"{otlpBaseEndpoint}/{signalPath}");
        opt.Headers = otlpHeaders;
        opt.Protocol = otlpProtocol;
    }

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: railwayServiceName, serviceVersion: serviceVersion)
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", deploymentEnvironment),
                new("railway.service", railwayServiceName),
            }))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(opt => ConfigureOtlpFor("v1/metrics", opt)))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = ctx =>
                {
                    var path = ctx.Request.Path.Value ?? string.Empty;
                    return !path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
                        && !path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase)
                        && !path.Equals("/healthz", StringComparison.OrdinalIgnoreCase)
                        && !path.Equals("/readyz", StringComparison.OrdinalIgnoreCase);
                };
            })
            .AddHttpClientInstrumentation()
            // SDK 1.15+ captures the parameterized SQL text but never parameter values,
            // so user data (which lives in parameters) does not leak into spans.
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(opt => ConfigureOtlpFor("v1/traces", opt)));

    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.AddOtlpExporter(opt => ConfigureOtlpFor("v1/logs", opt));
    });

    Console.WriteLine($"[OpenTelemetry] Registered. base={otlpBaseEndpoint} protocol={otlpProtocol} environment={deploymentEnvironment} service.version={serviceVersion}");
}
else if (!isBuildTimeOpenApi)
{
    Console.WriteLine("[OpenTelemetry] Not registered (OpenTelemetry:OtlpHeaders is empty). No telemetry will be exported.");
}

var app = builder.Build();

// Database migration
if (!isBuildTimeOpenApi)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseRouting();

// Static files for SPA — shared options enable pre-compressed (.br/.gz) sibling
// serving and long-cache headers on hashed assets. Order matters: the
// pre-compressed middleware rewrites Request.Path BEFORE UseStaticFiles reads it.
var staticFileOptions = new StaticFileOptions
{
    ContentTypeProvider = new CompressedAwareContentTypeProvider(),
    OnPrepareResponse = ctx =>
    {
        var requestPath = ctx.Context.Request.Path.Value ?? "";
        if (requestPath.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            // Vite hashes asset filenames; safe to cache forever.
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return;
        }

        // index.html (served directly, via UseDefaultFiles, or via MapFallbackToFile)
        // must always revalidate so SPA deploys land for returning users.
        var fileName = ctx.File.Name;
        if (IsIndexHtml(fileName))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
            return;
        }

        // The service worker script must always revalidate so a new push worker is
        // picked up on the next visit. Browsers already bypass the HTTP cache for the
        // SW script on update checks; this is belt-and-suspenders against any proxy
        // or older client caching a stale sw.js and getting "stuck".
        if (IsServiceWorker(fileName))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
        }

        static bool IsIndexHtml(string name) =>
            name.Equals("index.html", StringComparison.OrdinalIgnoreCase)
            || name.Equals("index.html.br", StringComparison.OrdinalIgnoreCase)
            || name.Equals("index.html.gz", StringComparison.OrdinalIgnoreCase);

        static bool IsServiceWorker(string name) =>
            name.Equals("sw.js", StringComparison.OrdinalIgnoreCase)
            || name.Equals("sw.js.br", StringComparison.OrdinalIgnoreCase)
            || name.Equals("sw.js.gz", StringComparison.OrdinalIgnoreCase);
    },
};

app.UseDefaultFiles();
app.UseMiddleware<PreCompressedStaticFilesMiddleware>();
app.UseStaticFiles(staticFileOptions);
app.UseSpaStaticFiles(staticFileOptions);

// Session middleware (before authentication)
app.UseSession();

// Authentication & Authorization. JwtBearer's OnTokenValidated event handles lazy User-row
// sync (see Frigorino.Infrastructure/Auth/FirebaseAuth.cs) — fires once per real Firebase
// login via the auth_time claim, not per request.
app.UseAuthentication();
app.UseAuthorization();

// Anonymous health + version endpoints (skipped at build-time OpenAPI generation
// because AddHealthChecks isn't registered there).
if (!isBuildTimeOpenApi)
{
    app.MapHealthChecks("/healthz", new HealthCheckOptions
    {
        Predicate = _ => false,
    }).AllowAnonymous();

    app.MapHealthChecks("/readyz", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }).AllowAnonymous();

    app.MapGroup("/api/version")
        .WithTags("Health")
        .MapGetVersion();
}

// API endpoints
app.MapControllers();

var households = app.MapGroup("/api/household")
    .RequireAuthorization()
    .WithTags("Households");
households.MapCreateHousehold();
households.MapGetUserHouseholds();
households.MapDeleteHousehold();

var members = app.MapGroup("/api/household/{householdId:int}/members")
    .RequireAuthorization()
    .WithTags("Members");
members.MapGetMembers();
members.MapAddMember();
members.MapRemoveMember();
members.MapUpdateMemberRole();

var householdSettings = app.MapGroup("/api/household/{householdId:int}/settings")
    .RequireAuthorization()
    .WithTags("HouseholdSettings");
householdSettings.MapGetHouseholdSettings();
householdSettings.MapUpdateHouseholdSettings();

var blueprints = app.MapGroup("/api/household/{householdId:int}/blueprints")
    .RequireAuthorization()
    .WithTags("Blueprints");
blueprints.MapGetBlueprints();
blueprints.MapGetBlueprint();
blueprints.MapCreateBlueprint();
blueprints.MapUpdateBlueprint();
blueprints.MapDeleteBlueprint();
blueprints.MapRestoreBlueprint();

var lists = app.MapGroup("/api/household/{householdId:int}/lists")
    .RequireAuthorization()
    .WithTags("Lists");
lists.MapCreateList();
lists.MapGetLists();
lists.MapGetList();
lists.MapUpdateList();
lists.MapDeleteList();
lists.MapGetPendingPromotions();
lists.MapPromoteListItems();
lists.MapSkipPromotion();
lists.MapApplyBlueprint();
lists.MapGetListRevision();

var listItems = app.MapGroup("/api/household/{householdId:int}/lists/{listId:int}/items")
    .RequireAuthorization()
    .WithTags("ListItems");
listItems.MapGetItems();
listItems.MapGetItem();
listItems.MapCreateItem();
listItems.MapCreateMediaItem();
listItems.MapGetItemFile();
listItems.MapGetItemThumbnail();
listItems.MapUpdateItem();
listItems.MapDeleteItem();
listItems.MapRestoreItem();
listItems.MapToggleItemStatus();
listItems.MapReorderItem();

var inventories = app.MapGroup("/api/household/{householdId:int}/inventories")
    .RequireAuthorization()
    .WithTags("Inventories");
inventories.MapCreateInventory();
inventories.MapGetInventories();
inventories.MapGetExpiryCalendar();
inventories.MapGetExpiryCalendarRevision();
inventories.MapGetInventory();
inventories.MapGetInventoryRevision();
inventories.MapUpdateInventory();
inventories.MapDeleteInventory();

var inventoryItems = app.MapGroup("/api/household/{householdId:int}/inventories/{inventoryId:int}/items")
    .RequireAuthorization()
    .WithTags("InventoryItems");
inventoryItems.MapGetInventoryItems();
inventoryItems.MapCreateInventoryItem();
inventoryItems.MapUpdateInventoryItem();
inventoryItems.MapDeleteInventoryItem();
inventoryItems.MapRestoreInventoryItem();
inventoryItems.MapReorderInventoryItem();

var inventorySettings = app.MapGroup("/api/household/{householdId:int}/inventories/{inventoryId:int}/settings")
    .RequireAuthorization()
    .WithTags("InventorySettings");
inventorySettings.MapGetInventorySettings();
inventorySettings.MapUpdateInventorySettings();

var inventoryNotifications = app.MapGroup("/api/household/{householdId:int}/inventories/{inventoryId:int}/notifications")
    .RequireAuthorization()
    .WithTags("Inventories");
inventoryNotifications.MapGetMyInventoryNotification();
inventoryNotifications.MapUpdateMyInventoryNotification();

var recipes = app.MapGroup("/api/household/{householdId:int}/recipes")
    .RequireAuthorization()
    .WithTags("Recipes");
recipes.MapCreateRecipe();
recipes.MapGetRecipes();
recipes.MapGetRecipe();
recipes.MapGetRecipeRevision();
recipes.MapUpdateRecipe();
recipes.MapDeleteRecipe();

var recipeItems = app.MapGroup("/api/household/{householdId:int}/recipes/{recipeId:int}/items")
    .RequireAuthorization()
    .WithTags("RecipeItems");
recipeItems.MapGetRecipeItems();
recipeItems.MapGetRecipeItem();
recipeItems.MapCreateRecipeItem();
recipeItems.MapUpdateRecipeItem();
recipeItems.MapDeleteRecipeItem();
recipeItems.MapRestoreRecipeItem();
recipeItems.MapReorderRecipeItem();

var me = app.MapGroup("/api/me")
    .RequireAuthorization()
    .WithTags("Me");
me.MapGetActiveHousehold();
me.MapSetActiveHousehold();
me.MapGetUserSettings();
me.MapUpdateUserSettings();
me.MapUpdateUserNotificationSettings();

var notifications = app.MapGroup("/api/notifications")
    .RequireAuthorization()
    .WithTags("Notifications");
notifications.MapRegisterFcmToken();
notifications.MapUnregisterFcmToken();

// Machine-to-machine trigger (key-guarded inside the handler; not in the auth group).
app.MapTriggerExpiryScan();

// SPA configuration
app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";

    if (app.Environment.IsDevelopment())
    {
        //spa.UseProxyToSpaDevelopmentServer("https://localhost:44375");
    }
});

app.MapFallbackToFile("index.html", staticFileOptions);

app.Run();

public partial class Program { }

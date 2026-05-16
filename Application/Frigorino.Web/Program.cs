using System.Reflection;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Households.Members;
using Frigorino.Features.Inventories;
using Frigorino.Features.Inventories.Items;
using Frigorino.Features.Lists;
using Frigorino.Features.Lists.Items;
using Frigorino.Features.Me.ActiveHousehold;
using Frigorino.Features.Version;
using Frigorino.Infrastructure.Auth;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Infrastructure.HealthChecks;
using Frigorino.Web.Middlewares;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using static Frigorino.Infrastructure.EntityFramework.DependencyInjection;

var isBuildTimeOpenApi =
    Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<Frigorino.Web.OpenApi.IntegerSchemaTransformer>();
    options.AddSchemaTransformer<Frigorino.Web.OpenApi.RequiredSchemaTransformer>();
});

builder.Services.AddEntityFramework(builder.Configuration);
if (!builder.Environment.IsEnvironment("IntegrationTest") && !isBuildTimeOpenApi)
{
    builder.Services.AddFirebaseAuth(builder.Configuration);
}
builder.Services.AddMaintenanceServices();

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
builder.Services.AddScoped<InitialConnectionMiddleware>();

builder.Services.AddSpaStaticFiles(configuration => { configuration.RootPath = "ClientApp/build"; });

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

// Static files for SPA
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSpaStaticFiles();

// Session middleware (before authentication)
app.UseSession();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Custom middleware
app.UseMiddleware<InitialConnectionMiddleware>();

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

var lists = app.MapGroup("/api/household/{householdId:int}/lists")
    .RequireAuthorization()
    .WithTags("Lists");
lists.MapCreateList();
lists.MapGetLists();
lists.MapGetList();
lists.MapUpdateList();
lists.MapDeleteList();

var listItems = app.MapGroup("/api/household/{householdId:int}/lists/{listId:int}/items")
    .RequireAuthorization()
    .WithTags("ListItems");
listItems.MapGetItems();
listItems.MapGetItem();
listItems.MapCreateItem();
listItems.MapUpdateItem();
listItems.MapDeleteItem();
listItems.MapToggleItemStatus();
listItems.MapReorderItem();
listItems.MapCompactItems();

var inventories = app.MapGroup("/api/household/{householdId:int}/inventories")
    .RequireAuthorization()
    .WithTags("Inventories");
inventories.MapCreateInventory();
inventories.MapGetInventories();
inventories.MapGetInventory();
inventories.MapUpdateInventory();
inventories.MapDeleteInventory();

var inventoryItems = app.MapGroup("/api/household/{householdId:int}/inventories/{inventoryId:int}/items")
    .RequireAuthorization()
    .WithTags("InventoryItems");
inventoryItems.MapGetInventoryItems();
inventoryItems.MapCreateInventoryItem();
inventoryItems.MapUpdateInventoryItem();
inventoryItems.MapDeleteInventoryItem();
inventoryItems.MapReorderInventoryItem();
inventoryItems.MapCompactInventoryItems();

var me = app.MapGroup("/api/me")
    .RequireAuthorization()
    .WithTags("Me");
me.MapGetActiveHousehold();
me.MapSetActiveHousehold();

// SPA configuration
app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";

    if (app.Environment.IsDevelopment())
    {
        //spa.UseProxyToSpaDevelopmentServer("https://localhost:44375");
    }
});

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }

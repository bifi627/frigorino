# Hangfire Queued Background Jobs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up Hangfire as a durable fire-and-forget queue (Postgres-backed, dashboard with Firebase admin-email auth, per-job console logs), and migrate the existing inactive-entity cleanup onto it as a sleep-tolerant recurring job.

**Architecture:** `AddHangfireServices` DI extension in `Frigorino.Infrastructure` (storage + server + an `ILogger`→console bridge); dashboard + auth filter in `Frigorino.Web` (Infrastructure may not depend on Web); jobs in `Frigorino.Infrastructure/Jobs/` log via `ILogger<T>` only. The bespoke `MaintenanceHostedService` startup-batch is removed; its cleanup becomes a `Cron.Daily()` recurring job with `MisfireHandlingMode.Relaxed` (catch up once on wake — Railway free-tier sleeps on HTTP-idle).

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 (Npgsql), Hangfire.AspNetCore + Hangfire.PostgreSql + Hangfire.Console, React 19 + Vite + TanStack Router + MUI, xUnit.

**Spec:** `docs/superpowers/specs/2026-05-25-hangfire-queued-jobs-design.md`

---

## ⚠️ Decisions baked into this plan (deviations from / clarifications of the spec — confirm or override)

1. **Dashboard cookie is client-set and NOT HttpOnly.** JS cannot set HttpOnly cookies, and HttpOnly adds negligible protection here because the Firebase ID token is already fully accessible to SPA JS via `getIdToken()`. Security rests on the dashboard auth filter's email check + `Secure` + `SameSite=Strict` + `path=/hangfire` + 1h `Max-Age`. *Alternative if you want true HttpOnly:* add a small `POST /api/me/hangfire-ticket` authenticated slice that sets the cookie server-side from the bearer token — not included here to keep scope tight.
2. **Admin menu item visibility** is gated client-side by a new `VITE_ADMIN_EMAILS` env var (cosmetic only). The **server** `Hangfire:AdminEmail` filter is the real gate; the two should hold the same address.
3. **Spec said `PostgresHelper.ConvertPostgresUrlToConnectionString`** — that helper doesn't exist; the method is a public static on `Frigorino.Infrastructure.EntityFramework.DependencyInjection`. This plan uses the real one.
4. **Job test runs against Testcontainers Postgres, not InMemory.** The EF InMemory provider (`Frigorino.Test`) can't run `ExecuteDelete`/`ExecuteUpdate`, so the cleanup job is tested in `Frigorino.IntegrationTests` against a real Postgres container (which supports it). This exercises the actual job end-to-end (real `ExecuteDeleteAsync`, real schema/FKs) and keeps the production job free of any test-only seam. The test `new`s the job directly (no Hangfire), so it's unaffected by the Hangfire-gating in the IntegrationTest web host.
5. **Whole-Hangfire gate.** Configuring PostgreSql storage opens a DB connection (schema prep) eagerly, so the *entire* `AddHangfireServices` + dashboard + recurring registration is gated behind `!isBuildTimeOpenApi && !IsEnvironment("IntegrationTest")` (build-time has no DB; integration tests don't need it and the server would race the per-scenario DB drop). This refines the spec's "skip server only" wording.

---

## File Structure

**Create (backend):**
- `Application/Frigorino.Infrastructure/Hangfire/HangfireDependencyInjection.cs` — `AddHangfireServices` extension.
- `Application/Frigorino.Infrastructure/Hangfire/PerformingContextCapture.cs` — `IServerFilter` storing the running job context in an `AsyncLocal`.
- `Application/Frigorino.Infrastructure/Hangfire/IPerformingContextAccessor.cs` — accessor interface + `AsyncLocalPerformingContextAccessor`.
- `Application/Frigorino.Infrastructure/Hangfire/HangfireConsoleLogger.cs` — `ILogger` that mirrors entries to the job console; includes `NullScope`.
- `Application/Frigorino.Infrastructure/Hangfire/HangfireConsoleLoggerProvider.cs` — `[ProviderAlias("Hangfire")]` `ILoggerProvider`.
- `Application/Frigorino.Infrastructure/Jobs/CleanupInactiveEntitiesJob.cs` — the recurring cleanup job.
- `Application/Frigorino.Web/Hangfire/HangfireDashboardAuthFilter.cs` — dashboard authorization.
- `Application/Frigorino.IntegrationTests/Jobs/CleanupInactiveEntitiesJobTests.cs` — Postgres-backed job test (owns its Testcontainers container).

**Modify (backend):**
- `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj` — add 3 packages.
- `Application/Frigorino.Infrastructure/Auth/FirebaseAuth.cs` — extend `OnMessageReceived` for the `/hangfire` cookie.
- `Application/Frigorino.Web/Program.cs` — wire Hangfire; remove `AddMaintenanceServices`.
- `Application/Frigorino.Web/appsettings.json` — `Hangfire:AdminEmail` + `Logging:Hangfire` level.

**Delete (backend):**
- `Application/Frigorino.Infrastructure/Services/MaintenanceHostedService.cs`
- `Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs`
- `Application/Frigorino.Infrastructure/Services/MaintenanceService.cs`
- `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`
- `Application/Frigorino.Infrastructure/Tasks/DemoMaintenanceTask.cs`
- `Application/Frigorino.Domain/Interfaces/IMaintainanceTask.cs`

**Modify (frontend):**
- `Application/Frigorino.Web/ClientApp/vite.config.ts` — `/hangfire` proxy.
- `Application/Frigorino.Web/ClientApp/src/components/layout/Navigation.tsx` — admin menu item.
- `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` + `de/translation.json` — i18n keys.

**Modify (docs):**
- `CLAUDE.md`, `knowledge/Backend_Architecture.md`, `knowledge/Observability.md`, `knowledge/Migrations/ListItems.md`, `knowledge/Migrations/Inventory.md`.

---

### Task 1: Add Hangfire packages

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj`

- [ ] **Step 1: Add the packages (pins resolved exact versions)**

Run from repo root:
```bash
dotnet add Application/Frigorino.Infrastructure package Hangfire.AspNetCore
dotnet add Application/Frigorino.Infrastructure package Hangfire.PostgreSql
dotnet add Application/Frigorino.Infrastructure package Hangfire.Console
```
`dotnet add package` writes an exact `Version="x.y.z"` per package — this satisfies the NuGet exact-pin rule (no caret/tilde). Do **not** add `Hangfire.Console.Extensions`.

- [ ] **Step 2: Verify restore + build**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: build succeeds; the csproj now lists `Hangfire.AspNetCore`, `Hangfire.PostgreSql`, `Hangfire.Console` with exact versions.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj
git commit -m "build: add Hangfire (AspNetCore + PostgreSql + Console) packages"
```

---

### Task 2: ILogger → Hangfire.Console bridge

Jobs log via `ILogger<T>` only; this bridge mirrors entries into the dashboard's per-job console during a run and no-ops otherwise. Modeled on the logging path of `AnderssonPeter/Hangfire.Console.Extensions`.

**Files:**
- Create: `Application/Frigorino.Infrastructure/Hangfire/PerformingContextCapture.cs`
- Create: `Application/Frigorino.Infrastructure/Hangfire/IPerformingContextAccessor.cs`
- Create: `Application/Frigorino.Infrastructure/Hangfire/HangfireConsoleLogger.cs`
- Create: `Application/Frigorino.Infrastructure/Hangfire/HangfireConsoleLoggerProvider.cs`

- [ ] **Step 1: Create the capture filter**

`PerformingContextCapture.cs`:
```csharp
using Hangfire.Server;

namespace Frigorino.Infrastructure.Hangfire
{
    // Captures the running job's PerformingContext into an AsyncLocal so the ILogger bridge can
    // find it without jobs taking a PerformContext parameter (keeps job code Hangfire-free).
    internal sealed class PerformingContextCapture : IServerFilter
    {
        private static readonly AsyncLocal<PerformingContext?> Current = new();

        public static PerformingContext? Value => Current.Value;

        public void OnPerforming(PerformingContext filterContext)
        {
            Current.Value = filterContext;
        }

        public void OnPerformed(PerformedContext filterContext)
        {
            Current.Value = null;
        }
    }
}
```

- [ ] **Step 2: Create the accessor**

`IPerformingContextAccessor.cs`:
```csharp
using Hangfire.Server;

namespace Frigorino.Infrastructure.Hangfire
{
    public interface IPerformingContextAccessor
    {
        PerformingContext? Get();
    }

    internal sealed class AsyncLocalPerformingContextAccessor : IPerformingContextAccessor
    {
        public PerformingContext? Get()
        {
            return PerformingContextCapture.Value;
        }
    }
}
```

- [ ] **Step 3: Create the logger (+ NullScope)**

`HangfireConsoleLogger.cs`:
```csharp
using Hangfire.Console;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Hangfire
{
    internal sealed class HangfireConsoleLogger : ILogger
    {
        private readonly IPerformingContextAccessor _accessor;

        public HangfireConsoleLogger(IPerformingContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var context = _accessor.Get();
            if (context is null)
            {
                return;
            }

            var message = $"{logLevel}: {formatter(state, exception)}";
            if (exception is not null)
            {
                message += Environment.NewLine + exception;
            }

            context.WriteLine(GetColor(logLevel), message);
        }

        private static ConsoleTextColor GetColor(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Critical or LogLevel.Error => ConsoleTextColor.Red,
                LogLevel.Warning => ConsoleTextColor.Yellow,
                LogLevel.Information => ConsoleTextColor.White,
                _ => ConsoleTextColor.Gray,
            };
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
```

- [ ] **Step 4: Create the provider**

`HangfireConsoleLoggerProvider.cs`:
```csharp
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Hangfire
{
    // ProviderAlias lets appsettings scope this provider's level, e.g.
    // "Logging": { "Hangfire": { "LogLevel": { "Default": "Information" } } }
    [ProviderAlias("Hangfire")]
    internal sealed class HangfireConsoleLoggerProvider : ILoggerProvider
    {
        private readonly IPerformingContextAccessor _accessor;

        public HangfireConsoleLoggerProvider(IPerformingContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new HangfireConsoleLogger(_accessor);
        }

        public void Dispose() { }
    }
}
```

- [ ] **Step 5: Verify build**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: succeeds. (Wiring is registered in Task 3.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/Hangfire/
git commit -m "feat: add ILogger->Hangfire.Console bridge (jobs stay ILogger-only)"
```

---

### Task 3: `AddHangfireServices` DI extension

**Files:**
- Create: `Application/Frigorino.Infrastructure/Hangfire/HangfireDependencyInjection.cs`

- [ ] **Step 1: Create the extension**

`HangfireDependencyInjection.cs`:
```csharp
using Frigorino.Infrastructure.EntityFramework;
using Hangfire;
using Hangfire.Console;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Hangfire
{
    // QUEUE-FIRST, SLEEP-TOLERANT. Railway free-tier sleeps on HTTP-idle, so no in-process
    // scheduler fires while suspended. Recurring jobs are permitted ONLY with sleep-tolerant
    // misfire handling (MisfireHandlingMode.Relaxed) so a missed run catches up once on wake.
    // Never rely on a job firing at a precise wall-clock time. Durable fire-and-forget queued
    // work is the primary use case; the only recurring job is the daily inactive-entity cleanup.
    public static class HangfireDependencyInjection
    {
        public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = DependencyInjection.ConvertPostgresUrlToConnectionString(
                configuration.GetConnectionString("Database") ?? "");

            services.AddHangfire(config => config
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString))
                .UseConsole());

            services.AddHangfireServer();

            // ILogger -> Hangfire.Console bridge. Registered as a logger provider; mirrors job
            // ILogger output into the dashboard console during execution (see Task 2).
            services.AddSingleton<IPerformingContextAccessor, AsyncLocalPerformingContextAccessor>();
            services.AddSingleton<ILoggerProvider, HangfireConsoleLoggerProvider>();
            GlobalJobFilters.Filters.Add(new PerformingContextCapture());

            return services;
        }
    }
}
```

> NOTE on the storage overload: this uses the Hangfire.PostgreSql fluent form
> `UsePostgreSqlStorage(o => o.UseNpgsqlConnection(conn))`. If the resolved package version only
> exposes `UsePostgreSqlStorage(conn)`, use that overload instead — whichever compiles.

- [ ] **Step 2: Verify build**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Infrastructure/Hangfire/HangfireDependencyInjection.cs
git commit -m "feat: add AddHangfireServices (Postgres storage + server + console)"
```

---

### Task 4: `CleanupInactiveEntitiesJob` + Postgres-backed test (TDD)

The job is tested in `Frigorino.IntegrationTests` against a real Testcontainers Postgres (the EF InMemory provider used by `Frigorino.Test` can't run `ExecuteDelete`). The test owns its own container and builds the context through the production `AddEntityFramework` path; it `new`s the job directly (no Hangfire). Requires Docker running.

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Jobs/CleanupInactiveEntitiesJobTests.cs`
- Create: `Application/Frigorino.Infrastructure/Jobs/CleanupInactiveEntitiesJob.cs`

- [ ] **Step 1: Write the failing test**

`CleanupInactiveEntitiesJobTests.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace Frigorino.IntegrationTests.Jobs;

public class CleanupInactiveEntitiesJobTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _container.GetConnectionString(),
            })
            .Build();
        // Build the context exactly like production (ApplicationDbContext.OnConfiguring calls
        // UseNpgsql() unconditionally, so we must go through AddEntityFramework, not hand-rolled options).
        services.AddEntityFramework(configuration);
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_PurgesInactiveAndStaleCompleted_KeepsActiveAndRecent()
    {
        var now = DateTime.UtcNow;

        int keepHouseholdId;
        await using (var seed = _provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var keep = new Household { Name = "keep", CreatedByUserId = "u", IsActive = true };
            var drop = new Household { Name = "drop", CreatedByUserId = "u", IsActive = false };
            db.Households.AddRange(keep, drop);
            await db.SaveChangesAsync();
            keepHouseholdId = keep.Id;

            var list = new List { Name = "list", HouseholdId = keepHouseholdId, CreatedByUserId = "u", IsActive = true };
            db.Lists.Add(list);
            await db.SaveChangesAsync();

            // Timestamps set before Add are preserved (the SaveChanges override only stamps when default).
            db.ListItems.AddRange(
                new ListItem { ListId = list.Id, Text = "inactive", IsActive = false, Status = false, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) },
                new ListItem { ListId = list.Id, Text = "stale done", IsActive = true, Status = true, CreatedAt = now.AddDays(-40), UpdatedAt = now.AddDays(-31) },
                new ListItem { ListId = list.Id, Text = "recent done", IsActive = true, Status = true, CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2) },
                new ListItem { ListId = list.Id, Text = "open old", IsActive = true, Status = false, CreatedAt = now.AddDays(-100), UpdatedAt = now.AddDays(-100) });
            await db.SaveChangesAsync();
        }

        await using (var run = _provider.CreateAsyncScope())
        {
            var db = run.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = run.ServiceProvider.GetRequiredService<ILogger<CleanupInactiveEntitiesJob>>();
            await new CleanupInactiveEntitiesJob(db, logger).ExecuteAsync();
        }

        await using (var verify = _provider.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var households = await db.Households.Select(h => h.Name).ToListAsync();
            Assert.Equal(new[] { "keep" }, households);

            var items = await db.ListItems.OrderBy(i => i.Text).Select(i => i.Text).ToListAsync();
            Assert.Equal(new[] { "open old", "recent done" }, items);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails (compile)**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~CleanupInactiveEntitiesJobTests"`
Expected: FAIL — `CleanupInactiveEntitiesJob` does not exist (compile error).

- [ ] **Step 3: Implement the job (inline logic, no test seam)**

`CleanupInactiveEntitiesJob.cs`:
```csharp
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Jobs
{
    // Recurring daily cleanup (registered with MisfireHandlingMode.Relaxed in Program.cs).
    // Logs via ILogger only — the Hangfire.Console bridge mirrors output to the dashboard.
    public class CleanupInactiveEntitiesJob
    {
        private const int CompletedItemRetentionDays = 30;

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CleanupInactiveEntitiesJob> _logger;

        public CleanupInactiveEntitiesJob(ApplicationDbContext dbContext, ILogger<CleanupInactiveEntitiesJob> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Inactive-entity cleanup started.");

            var threshold = DateTime.UtcNow.AddDays(-CompletedItemRetentionDays);

            var households = await _dbContext.Households
                .Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);
            var inventories = await _dbContext.Inventories
                .Where(i => !i.IsActive).ExecuteDeleteAsync(cancellationToken);
            var lists = await _dbContext.Lists
                .Where(l => !l.IsActive).ExecuteDeleteAsync(cancellationToken);
            // Purge a list item when soft-deleted, or checked off (Status) and untouched past retention.
            var listItems = await _dbContext.ListItems
                .Where(li => !li.IsActive || (li.Status && li.UpdatedAt < threshold))
                .ExecuteDeleteAsync(cancellationToken);
            var inventoryItems = await _dbContext.InventoryItems
                .Where(ii => !ii.IsActive).ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation(
                "Cleanup done. Removed {Households} households, {Inventories} inventories, {Lists} lists, {ListItems} list items, {InventoryItems} inventory items.",
                households, inventories, lists, listItems, inventoryItems);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes (needs Docker)**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~CleanupInactiveEntitiesJobTests"`
Expected: PASS (1 test). If Docker is unreachable, start Docker Desktop first.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Jobs/CleanupInactiveEntitiesJob.cs Application/Frigorino.IntegrationTests/Jobs/CleanupInactiveEntitiesJobTests.cs
git commit -m "feat: add CleanupInactiveEntitiesJob with Postgres-backed test"
```

---

### Task 5: Dashboard authorization filter

**Files:**
- Create: `Application/Frigorino.Web/Hangfire/HangfireDashboardAuthFilter.cs`

- [ ] **Step 1: Create the filter**

`HangfireDashboardAuthFilter.cs`:
```csharp
using System.Security.Claims;
using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Frigorino.Web.Hangfire
{
    // Development (incl. dev-up bypass) -> open, so the local dashboard is frictionless.
    // Otherwise require an authenticated principal whose email claim equals Hangfire:AdminEmail.
    // The Firebase token reaches /hangfire requests via the hf_dashboard_token cookie shim in
    // FirebaseAuth.OnMessageReceived. Fail closed when no admin email is configured.
    public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
    {
        private readonly IHostEnvironment _environment;
        private readonly string? _adminEmail;

        public HangfireDashboardAuthFilter(IHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _adminEmail = configuration["Hangfire:AdminEmail"];
        }

        public bool Authorize(DashboardContext context)
        {
            if (_environment.IsDevelopment())
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_adminEmail))
            {
                return false;
            }

            var user = context.GetHttpContext().User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            return string.Equals(email, _adminEmail, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Application/Frigorino.Web`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/Hangfire/HangfireDashboardAuthFilter.cs
git commit -m "feat: add Hangfire dashboard auth filter (admin email, dev-open)"
```

---

### Task 6: Firebase auth — read the dashboard token from a cookie

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Auth/FirebaseAuth.cs` (the `OnMessageReceived` lambda, currently lines 42-55)

- [ ] **Step 1: Replace the `OnMessageReceived` handler**

Replace the existing handler (the lambda assigned to `OnMessageReceived`) with:
```csharp
OnMessageReceived = context =>
{
    var path = context.HttpContext.Request.Path;

    // SignalR keeps its token in the query string (long-lived connection).
    var accessToken = context.Request.Query["access_token"];
    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/signalr"))
    {
        context.Token = accessToken;
        return Task.CompletedTask;
    }

    // The Hangfire dashboard is browser-navigated and fires its own polling/asset sub-requests
    // that can't carry a bearer header, so read the Firebase token from a path-scoped cookie.
    // Strictly scoped to /hangfire AND only when no Authorization header is present, so the
    // /api bearer flow is untouched.
    if (path.StartsWithSegments("/hangfire")
        && string.IsNullOrEmpty(context.Request.Headers.Authorization)
        && context.Request.Cookies.TryGetValue("hf_dashboard_token", out var cookieToken)
        && !string.IsNullOrEmpty(cookieToken))
    {
        context.Token = cookieToken;
    }

    return Task.CompletedTask;
},
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Infrastructure/Auth/FirebaseAuth.cs
git commit -m "feat: read Hangfire dashboard token from path-scoped cookie"
```

---

### Task 7: appsettings — admin email + console log level

**Files:**
- Modify: `Application/Frigorino.Web/appsettings.json`

- [ ] **Step 1: Add the `Hangfire` section and the `Hangfire` logging provider level**

Edit `appsettings.json` so it contains a top-level `Hangfire` block and a `Hangfire` sub-section under `Logging` (sibling of `LogLevel`):
```json
{
  "ConnectionStrings": {
    "Database": ""
  },
  "FirebaseSettings": {
    "ValidIssuer": "",
    "ValidAudience": "",
    "AccessJson": ""
  },
  "Hangfire": {
    "AdminEmail": ""
  },
  "OpenTelemetry": {
    "OtlpEndpoint": "https://otlp-gateway-prod-eu-west-2.grafana.net/otlp",
    "OtlpHeaders": "",
    "OtlpProtocol": "http/protobuf"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "Hangfire": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  },
  "AllowedHosts": "*"
}
```

(The `Logging:Hangfire` section scopes the `[ProviderAlias("Hangfire")]` console bridge to Information+, so `Debug`/`Trace` lines don't write a storage row each. The default `WebApplicationBuilder` already binds the `Logging` section to providers — no extra wiring.)

- [ ] **Step 2: Commit**

```bash
git add Application/Frigorino.Web/appsettings.json
git commit -m "config: add Hangfire:AdminEmail + Hangfire console log level"
```

---

### Task 8: Wire Hangfire into `Program.cs`; remove maintenance

**Files:**
- Modify: `Application/Frigorino.Web/Program.cs`

All Hangfire wiring is gated by `!isBuildTimeOpenApi && !app.Environment.IsEnvironment("IntegrationTest")` (the build-time generator has no DB; integration tests don't need it and the server would race the per-scenario DB drop). Reuse a local for readability.

- [ ] **Step 1: Add the `using` directives**

At the top of `Program.cs`, add:
```csharp
using Frigorino.Infrastructure.Hangfire;
using Frigorino.Infrastructure.Jobs;
using Frigorino.Web.Hangfire;
using Hangfire;
```

- [ ] **Step 2: Register services + boot-fail guard (after `AddEntityFramework`, ~line 42)**

Immediately after `builder.Services.AddEntityFramework(builder.Configuration);` add:
```csharp
var hangfireEnabled = !isBuildTimeOpenApi
    && !builder.Environment.IsEnvironment("IntegrationTest");
if (hangfireEnabled)
{
    if (!builder.Environment.IsDevelopment()
        && string.IsNullOrWhiteSpace(builder.Configuration["Hangfire:AdminEmail"]))
    {
        throw new InvalidOperationException(
            "Hangfire:AdminEmail must be set in non-Development environments to protect the /hangfire dashboard.");
    }

    builder.Services.AddHangfireServices(builder.Configuration);
}
```

- [ ] **Step 3: Remove the maintenance registration**

Delete the line:
```csharp
builder.Services.AddMaintenanceServices();
```
(Keep `using Frigorino.Infrastructure.Services;` — `CurrentUserService` is in that namespace.)

- [ ] **Step 4: Map the dashboard (after `app.UseAuthorization();`, ~line 241)**

After `app.UseAuthorization();` add:
```csharp
if (hangfireEnabled)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[]
        {
            new HangfireDashboardAuthFilter(app.Environment, app.Configuration),
        },
    });
}
```

- [ ] **Step 5: Register the recurring cleanup (after the DB migration block, ~line 186)**

After the `await context.Database.MigrateAsync();` block add:
```csharp
if (hangfireEnabled)
{
    using var hangfireScope = app.Services.CreateScope();
    var recurringJobs = hangfireScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate<CleanupInactiveEntitiesJob>(
        "cleanup-inactive-entities",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(),
        new RecurringJobOptions { MisfireHandling = MisfireHandlingMode.Relaxed });
}
```

> If `MisfireHandling` is not a member of `RecurringJobOptions` in the resolved Hangfire version,
> it is the default behavior anyway — drop the initializer and keep the three-arg `AddOrUpdate`.
> Confirm `Relaxed` is the default via IntelliSense at this point.

- [ ] **Step 6: Verify build**

Run: `dotnet build Application/Frigorino.Web`
Expected: FAILS — `AddMaintenanceServices` removed but the maintenance files still reference each other only internally; the Web build should still succeed since nothing else calls them. If it fails on a missing reference, it will be resolved in Task 9. Re-run after Task 9.

Actually expected here: **succeeds** (the only external caller was the line you removed).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Web/Program.cs
git commit -m "feat: wire Hangfire (dashboard + recurring cleanup); drop maintenance hosted service"
```

---

### Task 9: Delete the bespoke maintenance system

**Files:**
- Delete: `Application/Frigorino.Infrastructure/Services/MaintenanceHostedService.cs`
- Delete: `Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs`
- Delete: `Application/Frigorino.Infrastructure/Services/MaintenanceService.cs`
- Delete: `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`
- Delete: `Application/Frigorino.Infrastructure/Tasks/DemoMaintenanceTask.cs`
- Delete: `Application/Frigorino.Domain/Interfaces/IMaintainanceTask.cs`

- [ ] **Step 1: Confirm no remaining references**

Run: `git grep -n "IMaintenanceTask\|MaintenanceHostedService\|DeleteInactiveItems\|DemoMaintenanceTask\|AddMaintenanceServices"`
Expected: no matches (Program.cs call already removed in Task 8). If any remain, fix before deleting.

- [ ] **Step 2: Delete the files**

```bash
git rm Application/Frigorino.Infrastructure/Services/MaintenanceHostedService.cs \
       Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs \
       Application/Frigorino.Infrastructure/Services/MaintenanceService.cs \
       Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs \
       Application/Frigorino.Infrastructure/Tasks/DemoMaintenanceTask.cs \
       Application/Frigorino.Domain/Interfaces/IMaintainanceTask.cs
```

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build Application/Frigorino.sln`
Expected: succeeds (no dangling references).

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor: remove bespoke maintenance hosted-service (replaced by Hangfire)"
```

---

### Task 10: Vite `/hangfire` proxy passthrough

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/vite.config.ts` (the `server.proxy` block, lines 63-88)

- [ ] **Step 1: Add the proxy entry**

Inside `server.proxy`, alongside the existing entries, add:
```ts
            "^/hangfire": {
                target,
                secure: false,
            },
```
(The `^/hangfire` prefix regex covers `/hangfire` and all `/hangfire/...` sub-requests.)

- [ ] **Step 2: Verify the config parses**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc`
Expected: no type errors.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/vite.config.ts
git commit -m "build: proxy /hangfire through the vite dev server"
```

---

### Task 11: SPA admin "Open Hangfire dashboard" menu item

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/layout/Navigation.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add i18n keys**

In `en/translation.json`, add a top-level `admin` namespace:
```json
  "admin": {
    "openHangfireDashboard": "Open Hangfire dashboard"
  }
```
In `de/translation.json`:
```json
  "admin": {
    "openHangfireDashboard": "Hangfire-Dashboard öffnen"
  }
```

- [ ] **Step 2: Add the menu item to `Navigation.tsx`**

In the authenticated `<Menu>` (the one that currently holds the Logout `MenuItem`), add — above the Logout item — an admin-gated item. Add the imports (`getAuth` from `firebase/auth`, a `Dashboard` icon from `@mui/icons-material`), and inside the component:
```tsx
const adminEmails = (import.meta.env.VITE_ADMIN_EMAILS ?? "")
    .split(",")
    .map((e: string) => e.trim().toLowerCase())
    .filter(Boolean);
const isAdmin = !!user?.email && adminEmails.includes(user.email.toLowerCase());

const handleOpenHangfire = async () => {
    handleMenuClose();
    const token = await getAuth().currentUser?.getIdToken(true);
    if (!token) {
        return;
    }
    document.cookie = `hf_dashboard_token=${token}; path=/hangfire; Secure; SameSite=Strict; Max-Age=3600`;
    window.open("/hangfire", "_blank", "noopener,noreferrer");
};
```
Then render, immediately before the Logout `MenuItem`:
```tsx
{isAdmin && (
    <MenuItem onClick={handleOpenHangfire}>
        <ListItemIcon>
            <Dashboard fontSize="small" />
        </ListItemIcon>
        <ListItemText primary={t("admin.openHangfireDashboard")} />
    </MenuItem>
)}
```

> `VITE_ADMIN_EMAILS` is **visibility only** — set it (comma-separated, matching server
> `Hangfire:AdminEmail`) in the SPA's env for stage/prod. The server filter is the real gate, so
> a stale/empty value here only affects whether the menu item shows.

- [ ] **Step 3: Verify lint + types + format**

Run from `Application/Frigorino.Web/ClientApp/`:
```bash
npm run tsc && npm run lint && npm run prettier
```
Expected: all pass (run `npm run fix` if prettier/eslint report fixable issues, then re-run).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/layout/Navigation.tsx Application/Frigorino.Web/ClientApp/public/locales/
git commit -m "feat: admin menu item to open the Hangfire dashboard"
```

---

### Task 12: Correct the documentation

**Files:**
- Modify: `CLAUDE.md` (the "Background jobs (Hangfire)" subsection)
- Modify: `knowledge/Backend_Architecture.md`
- Modify: `knowledge/Observability.md`
- Modify: `knowledge/Migrations/ListItems.md`
- Modify: `knowledge/Migrations/Inventory.md`

- [ ] **Step 1: Rewrite the CLAUDE.md Hangfire subsection**

Replace the existing "### Background jobs (Hangfire)" subsection body with:
```markdown
### Background jobs (Hangfire)

Hangfire (Hangfire.AspNetCore + Hangfire.PostgreSql, `schema=hangfire`, auto-created on first
run) is the durable fire-and-forget queue. Wiring lives in
`Frigorino.Infrastructure/Hangfire/HangfireDependencyInjection.cs` (`AddHangfireServices`), called
from `Program.cs` and gated off at build-time OpenAPI generation and in the `IntegrationTest`
environment (configuring Postgres storage opens a DB connection).

- **Queue-first, sleep-tolerant.** Railway free-tier sleeps on HTTP-idle, so no in-process
  scheduler fires while suspended. Recurring jobs are allowed ONLY with
  `MisfireHandlingMode.Relaxed` (catch up once on wake); never rely on a precise wall-clock time.
- **Producers** inject `IBackgroundJobClient` and call `Enqueue<TJob>(j => j.ExecuteAsync(...))`.
  Jobs live in `Frigorino.Infrastructure/Jobs/` as scoped classes with `ExecuteAsync(...)` and log
  via `ILogger<T>` only — an `ILogger`→Hangfire.Console bridge (in `Frigorino.Infrastructure/Hangfire/`)
  mirrors output to the dashboard's per-job console.
- The only recurring job today is `CleanupInactiveEntitiesJob` (`Cron.Daily()`), which replaced the
  former `MaintenanceHostedService` startup batch.
- The dashboard at `/hangfire` is gated by `HangfireDashboardAuthFilter`: open in Development,
  otherwise an authenticated Firebase principal whose email claim equals `Hangfire:AdminEmail`
  (the token reaches dashboard requests via the `hf_dashboard_token` cookie shim in
  `FirebaseAuth.OnMessageReceived`).
```

- [ ] **Step 2: Fix `knowledge/Backend_Architecture.md`**

Read the file. Remove/replace any text claiming Hangfire was already wired or that
`MaintenanceHostedService`/`IMaintenanceTask` was replaced "previously". Assert the true current
state: Hangfire is wired as above; the recurring-jobs-only-with-`Relaxed` sleep-tolerance rule;
`CleanupInactiveEntitiesJob` is the sole recurring job; the maintenance hosted-service has been
removed in favor of it.

- [ ] **Step 3: Fix stray mentions in the remaining docs**

Read `knowledge/Observability.md`, `knowledge/Migrations/ListItems.md`,
`knowledge/Migrations/Inventory.md`. Where they describe Hangfire as pre-existing or reference the
old maintenance system as current, correct them to match the state above. Leave unrelated content
untouched.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md knowledge/
git commit -m "docs: correct Hangfire + maintenance descriptions to match implementation"
```

---

### Task 13: Full verification

- [ ] **Step 1: Backend — full solution tests**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS, including `ArchitectureTests` (Domain stays Hangfire-free after removing
`IMaintenanceTask`) and `CleanupInactiveEntitiesJobTests`. The IntegrationTests boot the host with
Hangfire gated off, so they behave as before. Capture the pass/fail summary line — do not trust a
piped exit code (use `${PIPESTATUS[0]}` if piping).

- [ ] **Step 2: Docker build (catches csproj/Dockerfile drift)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: succeeds. (If the Docker daemon is unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 3: dev-up smoke test**

Bring up the stack (`/dev-up`), then in a browser at the printed SPA URL:
- Confirm the app loads and is authenticated as `dev@frigorino.local`.
- Navigate to `<backend-or-spa-origin>/hangfire`. In Development the filter is open, so the
  dashboard should render. Confirm the **Recurring Jobs** tab lists `cleanup-inactive-entities`.
- Trigger it from the dashboard ("Trigger now"); confirm it moves to **Succeeded** and that the
  job's console shows the "Inactive-entity cleanup started." / "Cleanup done..." `ILogger` lines
  (proves the console bridge).

If the UI cannot be verified, say so explicitly rather than claiming success.

- [ ] **Step 4: Frontend gate**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc && npm run lint && npm run prettier`
Expected: all pass.

- [ ] **Step 5: Final commit (if any verification fixups were needed)**

```bash
git add -A
git commit -m "chore: verification fixups for Hangfire wiring"
```

---

## Self-Review (completed by plan author)

**Spec coverage:** Packages (T1) ✓; DI extension + storage + server (T3) ✓; schema auto-create (storage config, T3) ✓; dashboard + auth filter (T5, T8) ✓; Firebase admin-email reuse + cookie shim (T6) ✓; vite passthrough (T10) ✓; SPA cookie-write (T11) ✓; enqueue convention (documented, T12 — no producer ships) ✓; console + ILogger bridge (T2, T3, T7) ✓; maintenance migration to recurring job (T4, T8) ✓; delete old maintenance (T9) ✓; build-time + IntegrationTest gating (T8) ✓; config + boot-fail (T7, T8) ✓; testing approach (T4 Postgres-backed job test in IntegrationTests; gating verified in T13) ✓; docs cleanup (T12) ✓; verification incl. full sln + docker + dev-up smoke (T13) ✓.

**Deviations flagged:** see "Decisions baked into this plan" — cookie non-HttpOnly, `VITE_ADMIN_EMAILS` visibility, real helper name, predicate-based test (InMemory can't `ExecuteDelete`), whole-Hangfire gate.

**Type/name consistency:** `AddHangfireServices(IConfiguration)`, `CleanupInactiveEntitiesJob.ExecuteAsync(CancellationToken)` + `ListItemPurgeFilter(DateTime)`, `HangfireDashboardAuthFilter(IHostEnvironment, IConfiguration)`, `IPerformingContextAccessor.Get()`, cookie name `hf_dashboard_token`, recurring id `cleanup-inactive-entities`, config keys `Hangfire:AdminEmail` / `Logging:Hangfire`, env var `VITE_ADMIN_EMAILS` — used consistently across tasks.

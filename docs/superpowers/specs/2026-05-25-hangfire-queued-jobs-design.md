# Wire up Hangfire for queued background jobs (queue-first, sleep-tolerant)

**Date:** 2026-05-25
**Branch:** `feat/hangfire-queued-jobs` (off `stage`)
**Status:** Design approved — pending spec review

## Problem

CLAUDE.md and `knowledge/Backend_Architecture.md` describe Hangfire as already wired
(DI extension, `/hangfire` dashboard, recurring jobs, `hangfire` schema). **None of that
exists.** Verified current state:

- No `Hangfire.*` package reference in any `.csproj`.
- No `Hangfire*.cs`, no `Frigorino.Infrastructure/Jobs/` folder.
- The only `/hangfire` reference in `Program.cs:152` is an anticipatory path-exclusion in
  the OpenTelemetry trace filter — no dashboard is mapped.
- The vite proxy (`vite.config.ts:63-88`) does **not** include `/hangfire` (CLAUDE.md claims
  it does).
- The in-process maintenance system is **fully live**, not "replaced by Hangfire":
  `MaintenanceHostedService` (a `BackgroundService` that runs every `IMaintenanceTask` once,
  ~5s after boot), `IMaintenanceTask` (in `Domain/Interfaces/IMaintainanceTask.cs`),
  `DeleteInactiveItems`, and a no-op `DemoMaintenanceTask`. `MaintenanceService.cs` is empty.

So the docs are aspirational fiction and must be corrected as part of this work.

The real need: a durable fire-and-forget queue for upcoming async features — classifying a
list item via an LLM (~1s, would block list-add), OCR-ing receipt photos, sending invite
emails. These need durability + retry + visibility across restarts. Hangfire fills that gap
and amortizes across the next several features instead of each rolling its own runner.

## Scope

**Primary purpose:** stand up Hangfire as a durable fire-and-forget queue.

**First / proving consumer:** migrate the existing `DeleteInactiveItems` cleanup off the
bespoke `MaintenanceHostedService` startup-batch onto a Hangfire **recurring job**. This
exercises the pipeline end-to-end and retires the hand-rolled background runner.

**Not in this PR:** the classifier / OCR / email producers themselves (future, separate
tasks). This PR is plumbing + the one recurring consumer + doc correction.

## Key constraint: Railway free-tier sleep

The container sleeps on HTTP-idle. No in-process scheduler (Hangfire recurring, a timer, a
`BackgroundService`) can fire while the process is suspended. A cleanup job has **no hard
timing requirement** — while asleep nobody writes data, so nothing accumulates, and
"catch up once on next wake" is adequate. Therefore:

- Recurring jobs are permitted **only** with sleep-tolerant misfire handling
  (`MisfireHandlingMode.Relaxed`): on the next wake after a missed occurrence, enqueue **one**
  catch-up run, then resume the schedule — never multiple catch-up runs, never relying on a
  run firing at a precise wall-clock time.
- `Strict` (enqueues every missed occurrence) and `Ignorable` (skips missed runs entirely)
  are both wrong for this use case.
- Hangfire server polling does **not** keep the container awake (Railway sleeps on HTTP-idle,
  not DB-activity), so it does not defeat free-tier sleep. Confirm once on stage post-merge.

## Design

### Component placement (driven by `ArchitectureTests`)

The architecture tests forbid only **Domain → Hangfire** (`ArchitectureTests.cs:38`) and
**Infrastructure → Web** (`:47`). Features → Hangfire is allowed. This dictates:

- **`AddHangfireServices(IConfiguration, IHostEnvironment)`** → `Frigorino.Infrastructure/Hangfire/`.
  Registers `AddHangfire(c => c.UsePostgreSqlStorage(...))` + `AddHangfireServer(...)`.
  Reuses `ConnectionStrings:Database` converted via `PostgresHelper.ConvertPostgresUrlToConnectionString`
  (same helper as health checks at `Program.cs:66`). Called from `Program.cs` near
  `AddEntityFramework`.
- **Dashboard** (`UseHangfireDashboard("/hangfire", ...)`) + **`HangfireDashboardAuthFilter`**
  → `Frigorino.Web` (Infrastructure cannot depend on Web).
- **Jobs** → `Frigorino.Infrastructure/Jobs/` as scoped classes with
  `ExecuteAsync(CancellationToken)`, resolved via Hangfire's DI activator.
- **Recurring-job registration** (`RecurringJob.AddOrUpdate`) → in `Program.cs` after
  `app.Build()`, gated `!isBuildTimeOpenApi`.
- Domain stays Hangfire-free: the old `IMaintenanceTask` interface in Domain is deleted, not
  extended.

### Enqueue convention (for future producers)

Producer slices inject Hangfire's **`IBackgroundJobClient`** directly and call
`_jobs.Enqueue<TJob>(j => j.ExecuteAsync(args, default))`. No vendor-neutral wrapper
interface: `IBackgroundJobClient` is itself the abstraction, and its strongly-typed
expression-capture (`Enqueue<TJob>`) is the ergonomic point a wrapper would shed. (Deliberate
carve-out from the usual vendor-agnostic-by-default stance, because the wrapper would be pure
overhead here.) No producer code ships in this PR; this is documented guidance for the future
classifier / OCR / email slices.

### Dashboard job logs (Hangfire.Console + `ILogger` bridge)

Jobs surface their logs inline on the dashboard's per-job details page, but **the jobs stay
decoupled from Hangfire.Console** — they log only through
`Microsoft.Extensions.Logging.ILogger<T>` (which already flows to stdout + OTel → Grafana/Loki).
Hangfire.Console does **not** scrape stdout or hook `ILogger`; it only renders lines explicitly
pushed into a per-job storage buffer. A small bridge wired in the composition root connects the
two:

- `.UseConsole()` added to the storage config in `AddHangfireServices`.
- A self-written bridge in `Frigorino.Infrastructure/Hangfire/`, modeled on the logging path of
  `AnderssonPeter/Hangfire.Console.Extensions` (last release ~8 months old, so we own a minimal
  copy — only the `ILogger` path, none of its progress-bar / job-manager / cancellation extras):
  - `HangfireConsoleSubscriber : IServerFilter` — stores the running `PerformingContext` in an
    `AsyncLocal` on `OnPerforming`, clears it on `OnPerformed`.
  - `IPerformingContextAccessor` + `HangfireConsoleLogger : ILogger` whose `Log<TState>` calls
    `context.WriteLine(color, message)` **only when** a job context is active on the async flow,
    and no-ops otherwise (so non-job callers and unit tests are unaffected).
  - `HangfireConsoleLoggerProvider : ILoggerProvider` with `[ProviderAlias("Hangfire")]`.
  - Registered as singletons; the filter added to Hangfire's job filters.
- **Level-filtered via config** so we don't write a storage row per `Debug`/`Trace` line: the
  `[ProviderAlias("Hangfire")]` lets `appsettings.json` scope it, e.g.
  `"Logging": { "Hangfire": { "LogLevel": { "Default": "Information" } } }`.

Result: open a job in the dashboard → see its `ILogger` output inline, while job code (and its
unit tests) know nothing about Hangfire.Console. Console output renders on the job-details page,
already behind the admin-email filter — no new auth surface; retention follows job retention by
default. (See the memory note: jobs/services log via `ILogger` only, never `PerformContext`.)

### Maintenance migration (the proving consumer)

- New `Frigorino.Infrastructure/Jobs/CleanupInactiveEntitiesJob.cs` — the body of today's
  `DeleteInactiveItems.Run` verbatim: hard-delete soft-deleted households / inventories /
  lists, purge completed list items older than 30 days, hard-delete soft-deleted inventory
  items.
- Registered recurring in `Program.cs`:
  ```csharp
  RecurringJob.AddOrUpdate<CleanupInactiveEntitiesJob>(
      "cleanup-inactive-entities",
      j => j.ExecuteAsync(CancellationToken.None),
      Cron.Daily(),
      new RecurringJobOptions { MisfireHandling = MisfireHandlingMode.Relaxed });
  ```
  Cadence: daily (midnight UTC). `Relaxed` confirmed-at-impl as the default (context7 would not
  surface the misfire page; trivially checkable in IntelliSense / source).
- **Delete:** `MaintenanceHostedService.cs`, `MaintenanceDependencyInjection.cs`, empty
  `MaintenanceService.cs`, `DeleteInactiveItems.cs`, `DemoMaintenanceTask.cs`,
  `Domain/Interfaces/IMaintainanceTask.cs`, and the `AddMaintenanceServices()` call at
  `Program.cs:59`.

### Dashboard auth — reuse Firebase, gate on admin email, isolated from bearer flow

The dashboard is browser-navigated and fires many polling / asset sub-requests
(`/hangfire/stats`, embedded css/js) that the auth filter also gates. A `?access_token=`
query param (the existing `/signalr` shim) authenticates only the first paint, so it cannot
carry the dashboard. The credential must ride every `/hangfire` request via a **cookie**.

Mechanism (mirrors the proven path-scoped isolation of the existing `/signalr` shim at
`FirebaseAuth.cs:42-54`):

- `OnMessageReceived` gains one branch that fires **only when** `path.StartsWithSegments("/hangfire")`
  **and** no `Authorization` header is present → reads the Firebase ID token from a dedicated
  cookie `hf_dashboard_token` (HttpOnly, Secure, SameSite=Strict, path-scoped to `/hangfire`).
  For every `/api` request the branch is never entered and a present `Authorization` header is
  never overridden, so the existing header-bearer flow is byte-for-byte unchanged. No new auth
  scheme, no default-scheme change.
- `HangfireDashboardAuthFilter.Authorize`: **Development** (incl. dev-up bypass) → allow open;
  **non-Development** → require `User` authenticated **and** `email` claim == `Hangfire:AdminEmail`.
  Fail-closed if no admin email configured.
- SPA: an admin-only "open dashboard" action writes the current Firebase ID token into
  `hf_dashboard_token`, then opens `/hangfire`. Same-origin (vite proxy in dev, .NET host in
  prod) so the cookie applies to all dashboard sub-requests. Rough edge: Firebase ID tokens
  expire ~1h → reopen from the SPA to refresh.
- The `HangfireAuth:Username/Password` basic-auth idea is dropped. Config becomes
  `"Hangfire": { "AdminEmail": "" }` (real value via env / user-secrets).

Header-injection (ModHeader-style `Authorization: Bearer`) remains a lighter fallback if the
SPA cookie-write ever feels like too much, but cookie-bridge matches "reachable through vite
in the browser" without a browser extension.

### Vite passthrough

Add `^/hangfire` and `^/hangfire/*` proxy entries to `vite.config.ts` so the dashboard is
reachable at `https://localhost:44375/hangfire` and same-origin with the SPA in dev. In prod
there is no vite — the .NET host serves both at the same origin.

### Config & boot

- Add `"Hangfire": { "AdminEmail": "" }` placeholder to `appsettings.json`.
- **Boot-fail** if `!IsDevelopment && Hangfire:AdminEmail` is empty (loud misconfig detection;
  the filter is also fail-closed as defence in depth).
- **Build-time gate:** skip `AddHangfireServer` + dashboard mapping + recurring registration
  when `isBuildTimeOpenApi` (`GetDocument.Insider` has no DB), mirroring the existing
  EF-migrate / Firebase / health-check gating.
- **IntegrationTest gate:** also skip `AddHangfireServer` + recurring registration when
  `IsEnvironment("IntegrationTest")` (same gate that swaps real auth for `TestAuthHandler` at
  `Program.cs:43`). See the Testing section for why.

### Schema

Hangfire.PostgreSql auto-creates the `hangfire` schema and tables on first server start
(`PrepareSchemaIfNecessary = true`, default). No EF migration. EF uses the `public` schema, so
no collision with the existing model snapshot.

## Packages

Add to `Frigorino.Infrastructure.csproj`, pinned to exact versions per the dependency-pinning
convention:

- `Hangfire.AspNetCore`
- `Hangfire.PostgreSql`
- `Hangfire.Console` (dashboard per-job console). **Not** `Hangfire.Console.Extensions` — we
  own a minimal `ILogger` bridge instead (see "Dashboard job logs").

## Docs cleanup (same PR)

- `CLAUDE.md`: rewrite the Hangfire bullet to match what is actually built; correct the
  "maintenance replaced by Hangfire" narrative to reference the real `CleanupInactiveEntitiesJob`
  (and drop the fictional `ClassifyListsJob` / `Cron.Never` examples).
- `knowledge/Backend_Architecture.md`: replace aspirational Hangfire sections with accurate
  ones; document the recurring-jobs-only-with-`Relaxed` sleep-tolerance rule.
- Verify and correct stray Hangfire mentions in `knowledge/Observability.md`,
  `knowledge/Migrations/ListItems.md`, `knowledge/Migrations/Inventory.md`.

## Testing

The integration tests (`Frigorino.IntegrationTests`, Reqnroll + Playwright + Testcontainers)
boot a **complete app instance per scenario**: `BeforeScenario` creates a fresh
`frig_test_<guid>` database, calls `TestWebApplicationFactory.StartServer()` (runs EF
migrations), and `AfterScenario` disposes the factory then **force-drops the database**
(terminating active connections first). The host runs under the `IntegrationTest` environment.
That per-scenario lifecycle drives the rules below.

**Do not start the Hangfire server in `IntegrationTest`.** An unconditional `AddHangfireServer`
would boot polling threads (recurring scheduler, schedule poller, workers, heartbeat) holding
open DB connections in every scenario — then `AfterScenario` drops the DB out from under them.
That risks shutdown-timeout-driven teardown slowdowns (default ~15s) and noisy
connection-terminated exceptions, multiplied across every scenario. Gating the server off (see
the IntegrationTest gate under Config & boot) removes the threads and the race entirely.

**Do not register the recurring cleanup in `IntegrationTest`.** `CleanupInactiveEntitiesJob`
hard-deletes data. A freshly-added `Cron.Daily()` + `Relaxed` job won't backfill on a short
run, but "almost certainly won't delete my test data" is not a guarantee worth keeping in the
pipeline — gate the `RecurringJob.AddOrUpdate` call off too.

**This migration improves test determinism.** `AddMaintenanceServices()` runs *unconditionally*
today (`Program.cs:59`), so `MaintenanceHostedService` currently boots in every scenario and
runs the data-deleting `DeleteInactiveItems` ~5s after startup — a latent
"background task mutates my test DB mid-scenario" hazard, since many Playwright scenarios run
longer than 5s. Removing it (and not scheduling its Hangfire replacement in tests) is strictly
safer. No test references `Maintenance` / `DeleteInactiveItems`, so removal is zero-risk.

**Test the job logic as a plain unit test.** `CleanupInactiveEntitiesJob` is a DI service with
`ExecuteAsync(CancellationToken)` — add an xUnit test in `Frigorino.Test` that seeds
soft-deleted + old-completed rows into a `TestApplicationDbContext`, calls `ExecuteAsync`, and
asserts the deletions. No Hangfire involved. The current `DeleteInactiveItems` has no test, so
this adds coverage that doesn't exist today.

**Future producers: assert enqueue, not execution, and use in-memory storage in tests.** No
slice injects `IBackgroundJobClient` in this PR, so DI resolves fine in tests with the server
gated off. When the first producer (classifier / OCR / email) lands, register
`Hangfire.InMemory` storage in the test host so `IBackgroundJobClient` resolves with zero
Postgres schema-prep, and assert the job was *enqueued* rather than driving async
enqueue→process→assert through Reqnroll (async completion = polling = flaky). Per-scenario DB
isolation means each scenario gets its own `hangfire` schema anyway, so there is no
cross-scenario contention even if storage is registered.

## Verification

- `dotnet test Application/Frigorino.sln` (architecture rules + existing suite + the new
  `CleanupInactiveEntitiesJob` unit test; also boots the IntegrationTests, which is why the
  IntegrationTest gate above must be in place).
- `docker build -f Application/Dockerfile` (catches csproj/Dockerfile drift).
- dev-up smoke: dashboard loads behind the email gate; manually trigger
  `cleanup-inactive-entities` → it shows **Succeeded**; a no-op enqueue runs and completes.
- Post-merge stage observation: in-process Hangfire polling does not trip Railway's HTTP-idle
  sleep detector.

## Reversibility

If Hangfire proves wrong: remove the DI call + dashboard route, drop the `hangfire` schema,
and the (future) queued consumers swap to `System.Threading.Channels` or similar. The
maintenance cleanup would revert to a startup-batch or a persisted-last-run guard.

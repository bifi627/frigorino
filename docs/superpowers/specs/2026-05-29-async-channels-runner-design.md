# Async fire-and-forget runner (in-process Channels) — design

- **Date:** 2026-05-29
- **Status:** Approved (design) — ready for planning
- **Branch:** `feat/async-channels-runner` (off `stage`)

## Summary

Frigorino has no way to run **request-triggered** work off the response thread. `MaintenanceHostedService`
covers *periodic* startup-batch work, but not fire-and-forget tasks kicked off by a request (the upcoming
AI item-classifier, later OCR / invite emails). Running such work inline would add its latency (~1s for an
LLM call) to the user's request.

This introduces a generic, in-process background work queue: an `IBackgroundTaskQueue` wrapping a bounded
`System.Threading.Channels` channel, drained by a single `BackgroundService`. Producers enqueue a delegate;
the consumer runs it shortly after — in a fresh DI scope — while the app is still awake. It is
**event-driven** (`WaitToReadAsync` parks at zero CPU when idle) so it never polls, which is why it does not
defeat Railway's serverless sleep, unlike the reverted Hangfire trial.

**Boundary:** this spec ships the queue + consumer + DI wiring only. It has **no producers** — the first
real producer (the classify job) is a later cycle. Zero new packages (BCL only), no schema, no API surface,
no frontend.

## Goals

- A generic `IBackgroundTaskQueue` any slice / handler can inject to enqueue fire-and-forget work.
- Non-blocking enqueue — never adds latency to, or blocks, the triggering request.
- Each work item runs in its own DI scope (fresh scoped `DbContext`, etc.).
- One misbehaving work item never kills the consumer or starves other items.
- Idle = zero CPU (sleep-safe on Railway), no polling.
- Honour Clean Architecture: producers live in `Features`, which cannot reference `Infrastructure`.

## Non-goals / out of scope

- **Durability.** In-memory; work queued-but-not-run is lost on restart / deploy / sleep-eviction. Accepted
  by design — see "Accepted tradeoffs". A DB-outbox is a separate future option, **not** Hangfire.
- **Retry / dead-letter.** A failed work item is logged and dropped.
- **Scheduling / periodic execution.** That stays with `MaintenanceHostedService`.
- **Configurable parallelism.** Single consumer in v1; degree-of-parallelism is a later knob.
- **Custom OTel metrics** (queue-depth / processed / failed Meter). ILogger only in v1; a Meter is a trivial
  later add.
- **Producers.** The classify job and any other callers are their own cycles.

## Key decisions & rationale

1. **Work item is a delegate, not a typed message + handler.**
   `Func<IServiceProvider, CancellationToken, Task>`. *Rationale: minimal, generic, matches the
   canonical ASP.NET Core "queued background tasks" pattern and the repo's no-MediatR style. A typed message
   + `IHandler<T>` registry would reintroduce the dispatch indirection the vertical-slice architecture
   deliberately avoids. Orchestration that wants to be testable becomes its own service the delegate resolves
   and calls (e.g. `sp.GetRequiredService<IClassifyProductJob>().Run(...)`), so nothing is lost.*

2. **The interface lives in `Frigorino.Domain/Interfaces/`.**
   Producers are slices in `Frigorino.Features`, which — by the ArchUnit layer rule — cannot reference
   `Infrastructure`. The signature uses only BCL types (`System.IServiceProvider`, `Func`,
   `CancellationToken`), so it sits cleanly in `Domain` with no framework dependency. Impl + consumer live
   in `Infrastructure`.

3. **Bounded channel, non-blocking enqueue.**
   `TryEnqueue` returns `bool` via `Channel.Writer.TryWrite`. When the channel is full it returns `false`
   immediately (never blocks the request thread) and logs a warning. *Rationale: blocking the request to
   apply backpressure would defeat the purpose; dropping is consistent with lossy-by-design. Capacity is a
   `const 1000` for v1 — items are tiny delegates; a config knob (`IOptions`) is a trivial later add if a
   real workload ever needs it.*

4. **Single consumer, serial processing.**
   One `BackgroundService` reads the channel; `SingleReader = true`, `SingleWriter = false` (many producers,
   one consumer) lets the channel optimize. *Rationale: simplest, ordered, sufficient for the expected
   volume (~1s classify jobs, low frequency). Parallelism is a future knob, not a v1 need.*

5. **Fresh DI scope per work item.**
   `using var scope = scopeFactory.CreateScope(); await work(scope.ServiceProvider, token);` *Rationale: the
   request scope that enqueued the work is long gone (and its scoped `DbContext` disposed) by the time the
   item runs. Each item gets its own scope so scoped services behave correctly.*

6. **Resilience: log-and-continue, no retry.**
   Each item is wrapped in try/catch. `OperationCanceledException` during shutdown breaks the loop cleanly;
   any other exception is logged as an error (`ILogger`) and the consumer continues to the next item.
   *Rationale: one bad item must never take down the consumer or starve the queue. Retry is out of scope —
   lossy by design; the triggering action re-runs the work.*

7. **Best-effort shutdown.**
   The consumer respects the host `stoppingToken`; on shutdown it stops reading and abandons whatever is
   still queued. *Rationale: consistent with lossy-by-design; a drain-with-timeout adds complexity for
   little value when the work re-triggers on the next user action.*

8. **ILogger-only telemetry.**
   Structured logs: warning on dropped enqueue, error on item failure. No custom `Meter` / `ActivitySource`.
   *Rationale: keeps the runner small; existing OTel already captures ASP.NET / EF / HttpClient spans,
   and the classify job will add LLM telemetry via `Microsoft.Extensions.AI`. A queue-depth Meter is a
   trivial later add if dashboards need it.*

## Components (4 files, no new packages)

1. **`Frigorino.Domain/Interfaces/IBackgroundTaskQueue.cs`** — producer-facing interface:
   ```csharp
   public interface IBackgroundTaskQueue
   {
       // Enqueue fire-and-forget work. Returns false (and logs a warning) if the queue is full; never blocks.
       bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work);
   }
   ```

2. **`Frigorino.Infrastructure/Services/BackgroundTaskQueue.cs`** — singleton impl wrapping
   `Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(new BoundedChannelOptions(Capacity) { SingleReader = true, SingleWriter = false })`.
   `TryEnqueue` → `TryWrite` + warn-on-false. Exposes an Infrastructure-internal
   `ChannelReader<…> Reader` for the consumer (kept off the `Domain` interface). `const int Capacity = 1000`.

3. **`Frigorino.Infrastructure/Services/QueuedHostedService.cs`** — `BackgroundService` consumer:
   ```csharp
   await foreach (var work in _queue.Reader.ReadAllAsync(stoppingToken))
   {
       try
       {
           using var scope = _scopeFactory.CreateScope();
           await work(scope.ServiceProvider, stoppingToken);
       }
       catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
       catch (Exception ex) { _logger.LogError(ex, "Background work item failed."); }
   }
   ```
   Injects the concrete `BackgroundTaskQueue` (for `Reader`) + `IServiceScopeFactory` + `ILogger`.

4. **`Frigorino.Infrastructure/Services/BackgroundQueueDependencyInjection.cs`** — `AddBackgroundTaskQueue()`:
   ```csharp
   services.AddSingleton<BackgroundTaskQueue>();
   services.AddSingleton<IBackgroundTaskQueue>(sp => sp.GetRequiredService<BackgroundTaskQueue>());
   services.AddHostedService<QueuedHostedService>();
   ```
   Wired in `Frigorino.Web/Program.cs` next to `AddMaintenanceServices()`.

## Persistence / API / Frontend

None. No schema, no migration, no DTOs, no endpoints, no client regeneration, no UI.

## Accepted tradeoffs

- **Lossy on restart / deploy / sleep.** Fine for re-derivable enrichment (re-trigger on next user action).
  Durability-critical work (emails, audit) must **not** use this queue — that is the outbox path (see
  "Composition with domain events").
- **Drop when full.** A full queue drops + warns rather than blocking. At capacity 1000 with tiny delegates
  this is effectively unreachable in normal operation; the warning surfaces it if it ever happens.

## Composition with domain events (forward note, not built here)

If the domain-events infrastructure (separate `IDEAS.md` entry) lands, its post-commit dispatcher
(`SavedChangesAsync`) runs on the request thread. Handlers that do slow or best-effort work should
**enqueue onto this runner** rather than block the request — the same one-line `TryEnqueue` a slice uses;
the runner needs **no** changes. The lossy/durable split still holds: best-effort reactions → this runner;
at-least-once reactions (email, audit) → a durable outbox, never this queue.

## Testing (xUnit + FakeItEasy, `Frigorino.Test`, no DB)

- **`BackgroundTaskQueue`:** enqueues when space (`TryEnqueue` → `true`); returns `false` + logs a warning
  when full (fill to capacity); null-arg guard.
- **`QueuedHostedService`:** runs an enqueued delegate (asserted via a `TaskCompletionSource` the delegate
  completes); each item gets a fresh scope (assert a distinct scoped instance per item); a throwing item is
  swallowed and a subsequent item still runs (resilience); stops cleanly on cancellation. Uses a real
  `ServiceProvider` for `IServiceScopeFactory` / `IServiceScope`.

## Verification

- Dev loop: filtered unit tests
  (`dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~BackgroundTaskQueue|FullyQualifiedName~QueuedHostedService"`).
- Gate: `dotnet test Application/Frigorino.sln` (full Test + IntegrationTests).
- **No `docker build` needed** — files are added to the existing `Frigorino.Infrastructure` project (no new
  project), so the Dockerfile / csproj layout is unchanged. (First time a new project is added, revisit per
  the Dockerfile-sync rule.)

## Sequencing / relationship to other work

- **Prerequisite for the classification engine** ("Promote checked list items into inventory
  (classifier-driven)" in `IDEAS.md`). That cycle's classify job is the first real producer — it enqueues
  `(sp, ct) => sp.GetRequiredService<IClassifyProductJob>().Run(...)`.
- Supersedes the reverted Hangfire trial (commit `7fb8937`) — see the `IDEAS.md` entry "Async
  fire-and-forget runner (in-process Channels)".

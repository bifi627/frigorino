# Async fire-and-forget runner (in-process Channels) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a generic, in-process background work queue so request-triggered work runs off the response thread, in a fresh DI scope, without polling.

**Architecture:** An `IBackgroundTaskQueue` (in `Frigorino.Domain`, BCL-only signature) wraps a bounded `System.Threading.Channels` channel. A single `QueuedHostedService : BackgroundService` (in `Frigorino.Infrastructure`) drains it — one fresh DI scope per work item, log-and-continue on failure, event-driven (zero CPU when idle). A DI extension wires both as a shared singleton plus the hosted service. No producers yet; no schema; no new packages.

**Tech Stack:** .NET 10, `System.Threading.Channels` (BCL), `Microsoft.Extensions.Hosting` (`BackgroundService`), `Microsoft.Extensions.DependencyInjection`, xUnit (`Frigorino.Test`).

**Spec:** `docs/superpowers/specs/2026-05-29-async-channels-runner-design.md`
**Branch:** `feat/async-channels-runner` (already created; spec already committed at `f5036c1`).

---

## File Structure

**Create:**
- `Application/Frigorino.Domain/Interfaces/IBackgroundTaskQueue.cs` — producer-facing interface; BCL-only signature so `Frigorino.Features` slices can inject it without referencing `Infrastructure`.
- `Application/Frigorino.Infrastructure/Services/BackgroundTaskQueue.cs` — singleton bounded-channel implementation; `TryEnqueue` (non-blocking) + a consumer-only `Reader`.
- `Application/Frigorino.Infrastructure/Services/QueuedHostedService.cs` — single `BackgroundService` consumer; fresh scope per item, log-and-continue.
- `Application/Frigorino.Infrastructure/Services/BackgroundQueueDependencyInjection.cs` — `AddBackgroundTaskQueue()` DI extension.
- `Application/Frigorino.Test/Infrastructure/BackgroundTaskQueueTests.cs` — unit tests for the queue.
- `Application/Frigorino.Test/Infrastructure/QueuedHostedServiceTests.cs` — unit tests for the consumer.

**Modify:**
- `Application/Frigorino.Web/Program.cs:59` — register `AddBackgroundTaskQueue()` next to `AddMaintenanceServices()`.

**Conventions to follow (already in this repo):**
- Block-style braces everywhere (matches `MaintenanceHostedService.cs`).
- `Frigorino.Domain` has `ImplicitUsings` enabled — interface files need no `using` directives (see `IMaintainanceTask.cs`).
- Tests: xUnit `[Fact]`, method names `Method_Scenario_Expectation`, namespace `Frigorino.Test.Infrastructure` (folder `Infrastructure/`). `Frigorino.Test` references Domain + Infrastructure + Features.

---

## Task 1: The queue (`IBackgroundTaskQueue` + `BackgroundTaskQueue`)

**Files:**
- Create: `Application/Frigorino.Domain/Interfaces/IBackgroundTaskQueue.cs`
- Create: `Application/Frigorino.Infrastructure/Services/BackgroundTaskQueue.cs`
- Test: `Application/Frigorino.Test/Infrastructure/BackgroundTaskQueueTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Application/Frigorino.Test/Infrastructure/BackgroundTaskQueueTests.cs`:

```csharp
using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class BackgroundTaskQueueTests
    {
        private static BackgroundTaskQueue NewQueue() =>
            new BackgroundTaskQueue(NullLogger<BackgroundTaskQueue>.Instance);

        [Fact]
        public void TryEnqueue_WithCapacity_ReturnsTrue()
        {
            var queue = NewQueue();

            var enqueued = queue.TryEnqueue((_, _) => Task.CompletedTask);

            Assert.True(enqueued);
        }

        [Fact]
        public void TryEnqueue_Null_Throws()
        {
            var queue = NewQueue();

            Assert.Throws<ArgumentNullException>(() => queue.TryEnqueue(null!));
        }

        [Fact]
        public void TryEnqueue_WhenFull_ReturnsFalse()
        {
            var queue = NewQueue();

            // No consumer is running, so nothing drains the channel.
            // Fill it exactly to capacity — every write must succeed.
            for (var i = 0; i < BackgroundTaskQueue.Capacity; i++)
            {
                Assert.True(queue.TryEnqueue((_, _) => Task.CompletedTask));
            }

            // The next write has nowhere to go and must be rejected (not block).
            var overflow = queue.TryEnqueue((_, _) => Task.CompletedTask);

            Assert.False(overflow);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~BackgroundTaskQueueTests"`
Expected: **FAIL** — compile error, `BackgroundTaskQueue` / `IBackgroundTaskQueue` do not exist yet.

- [ ] **Step 3: Create the interface**

Create `Application/Frigorino.Domain/Interfaces/IBackgroundTaskQueue.cs`:

```csharp
namespace Frigorino.Domain.Interfaces
{
    public interface IBackgroundTaskQueue
    {
        /// <summary>
        /// Enqueues fire-and-forget work to run shortly after — off the request thread, in a fresh DI scope.
        /// Returns false (and logs a warning) if the queue is full; never blocks the caller.
        /// </summary>
        bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work);
    }
}
```

(No `using` directives needed — `Frigorino.Domain` has `ImplicitUsings` enabled; `IServiceProvider`, `Func`, `CancellationToken`, `Task` are all BCL types covered by the implicit `System` / `System.Threading` / `System.Threading.Tasks` usings.)

- [ ] **Step 4: Create the implementation**

Create `Application/Frigorino.Infrastructure/Services/BackgroundTaskQueue.cs`:

```csharp
using System.Threading.Channels;
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // In-memory, bounded, lossy-by-design queue of fire-and-forget work items.
    // Single consumer (QueuedHostedService) drains it; many producers may enqueue.
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        public const int Capacity = 1000;

        private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel;
        private readonly ILogger<BackgroundTaskQueue> _logger;

        public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger)
        {
            _logger = logger;
            _channel = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(
                new BoundedChannelOptions(Capacity)
                {
                    // One consumer, many producers — lets the channel optimize.
                    SingleReader = true,
                    SingleWriter = false,
                    // FullMode defaults to Wait, so TryWrite returns false when full
                    // (we never call WriteAsync, so it never actually blocks).
                });
        }

        // Consumer-only. Deliberately kept off IBackgroundTaskQueue so producers can't dequeue.
        public ChannelReader<Func<IServiceProvider, CancellationToken, Task>> Reader => _channel.Reader;

        public bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work)
        {
            ArgumentNullException.ThrowIfNull(work);

            if (_channel.Writer.TryWrite(work))
            {
                return true;
            }

            _logger.LogWarning(
                "Background task queue is full (capacity {Capacity}); dropping work item.", Capacity);
            return false;
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~BackgroundTaskQueueTests"`
Expected: **PASS** — 3 tests passed.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Interfaces/IBackgroundTaskQueue.cs Application/Frigorino.Infrastructure/Services/BackgroundTaskQueue.cs Application/Frigorino.Test/Infrastructure/BackgroundTaskQueueTests.cs
git commit -m "feat: add in-process background task queue"
```

---

## Task 2: The consumer (`QueuedHostedService`)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/QueuedHostedService.cs`
- Test: `Application/Frigorino.Test/Infrastructure/QueuedHostedServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Application/Frigorino.Test/Infrastructure/QueuedHostedServiceTests.cs`:

```csharp
using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class QueuedHostedServiceTests
    {
        // A scoped service whose Id is unique per DI scope — lets a test prove
        // each work item ran in its own scope.
        private sealed class ScopeProbe
        {
            public Guid Id { get; } = Guid.NewGuid();
        }

        private static (QueuedHostedService service, BackgroundTaskQueue queue) Build()
        {
            var queue = new BackgroundTaskQueue(NullLogger<BackgroundTaskQueue>.Instance);

            var provider = new ServiceCollection()
                .AddScoped<ScopeProbe>()
                .BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new QueuedHostedService(
                queue, scopeFactory, NullLogger<QueuedHostedService>.Instance);
            return (service, queue);
        }

        [Fact]
        public async Task ExecuteAsync_RunsEnqueuedWork()
        {
            var (service, queue) = Build();
            var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await service.StartAsync(CancellationToken.None);
            queue.TryEnqueue((_, _) =>
            {
                ran.SetResult();
                return Task.CompletedTask;
            });

            await ran.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.StopAsync(CancellationToken.None);

            Assert.True(ran.Task.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task ExecuteAsync_CreatesFreshScopePerItem()
        {
            var (service, queue) = Build();
            var first = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            var second = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

            await service.StartAsync(CancellationToken.None);
            queue.TryEnqueue((sp, _) =>
            {
                first.SetResult(sp.GetRequiredService<ScopeProbe>().Id);
                return Task.CompletedTask;
            });
            queue.TryEnqueue((sp, _) =>
            {
                second.SetResult(sp.GetRequiredService<ScopeProbe>().Id);
                return Task.CompletedTask;
            });

            var firstId = await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var secondId = await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.StopAsync(CancellationToken.None);

            // Different scopes => different scoped ScopeProbe instances.
            Assert.NotEqual(firstId, secondId);
        }

        [Fact]
        public async Task ExecuteAsync_ThrowingItem_DoesNotStopConsumer()
        {
            var (service, queue) = Build();
            var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await service.StartAsync(CancellationToken.None);
            queue.TryEnqueue((_, _) => throw new InvalidOperationException("boom"));
            queue.TryEnqueue((_, _) =>
            {
                second.SetResult();
                return Task.CompletedTask;
            });

            // The throwing item must be swallowed; the next item must still run.
            await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.StopAsync(CancellationToken.None);

            Assert.True(second.Task.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task StopAsync_CompletesCleanly()
        {
            var (service, _) = Build();

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None); // must not throw
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QueuedHostedServiceTests"`
Expected: **FAIL** — compile error, `QueuedHostedService` does not exist yet.

- [ ] **Step 3: Create the consumer**

Create `Application/Frigorino.Infrastructure/Services/QueuedHostedService.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Single consumer that drains BackgroundTaskQueue, running each work item in its own DI scope.
    // Event-driven: ReadAllAsync parks at zero CPU when the queue is empty.
    public class QueuedHostedService : BackgroundService
    {
        private readonly BackgroundTaskQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QueuedHostedService> _logger;

        public QueuedHostedService(
            BackgroundTaskQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<QueuedHostedService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var work in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        await work(scope.ServiceProvider, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // One bad item must never take down the consumer or starve the queue.
                        _logger.LogError(ex, "Background work item failed.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown — ReadAllAsync observed the stopping token; stop draining.
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QueuedHostedServiceTests"`
Expected: **PASS** — 4 tests passed.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/QueuedHostedService.cs Application/Frigorino.Test/Infrastructure/QueuedHostedServiceTests.cs
git commit -m "feat: add background queue consumer (QueuedHostedService)"
```

---

## Task 3: DI wiring (`AddBackgroundTaskQueue()` + `Program.cs`)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/BackgroundQueueDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/Program.cs:59`

- [ ] **Step 1: Create the DI extension**

Create `Application/Frigorino.Infrastructure/Services/BackgroundQueueDependencyInjection.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class BackgroundQueueDependencyInjection
    {
        public static IServiceCollection AddBackgroundTaskQueue(this IServiceCollection services)
        {
            // ONE singleton instance backs both the producer interface and the consumer.
            services.AddSingleton<BackgroundTaskQueue>();
            services.AddSingleton<IBackgroundTaskQueue>(sp => sp.GetRequiredService<BackgroundTaskQueue>());
            services.AddHostedService<QueuedHostedService>();

            return services;
        }
    }
}
```

- [ ] **Step 2: Register it in `Program.cs`**

In `Application/Frigorino.Web/Program.cs`, find line 59:

```csharp
builder.Services.AddMaintenanceServices();
```

Add the queue registration immediately before it, so the block reads:

```csharp
builder.Services.AddBackgroundTaskQueue();
builder.Services.AddMaintenanceServices();
```

(No new `using` is needed — `Program.cs` already calls `AddMaintenanceServices()` from the same `Frigorino.Infrastructure.Services` namespace.)

- [ ] **Step 3: Build the solution to verify it compiles and the DI graph is valid**

Run: `dotnet build Application/Frigorino.sln`
Expected: **Build succeeded**, 0 errors.

- [ ] **Step 4: Run the unit + architecture test project**

Run: `dotnet test Application/Frigorino.Test`
Expected: **PASS** — all tests, including `ArchitectureTests` (confirms `IBackgroundTaskQueue` in `Frigorino.Domain` introduces no infrastructure-framework dependency) and the 7 new queue/consumer tests.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/BackgroundQueueDependencyInjection.cs Application/Frigorino.Web/Program.cs
git commit -m "feat: register background task queue in DI"
```

---

## Final verification (gate before merge)

- [ ] **Full solution build:** `dotnet build Application/Frigorino.sln` → Build succeeded.
- [ ] **Full test suite:** `dotnet test Application/Frigorino.sln` → all pass. (This adds `Frigorino.IntegrationTests`, which needs **Docker Desktop running** for Postgres Testcontainers; if Docker is down, ask the user to start it rather than skipping. `Frigorino.Test` alone already covers this feature's behavior + the layer rules.)
- [ ] **No Dockerfile change required** — all files land in the existing `Frigorino.Infrastructure` project; no new project was added, so the `Application/Dockerfile` project list is unchanged.

## Notes for the implementer

- **Why the interface lives in `Domain`:** future producers are vertical slices in `Frigorino.Features`, which the ArchUnit rules forbid from referencing `Infrastructure`. The signature is BCL-only so `Domain` stays framework-free (the `ArchitectureTests` enforce this — they will fail if the interface ever pulls in an infrastructure type).
- **Why `BackgroundTaskQueue` is injected concretely into the consumer:** the `Reader` is intentionally not on `IBackgroundTaskQueue` (producers must not dequeue). The consumer depends on the concrete class to reach `Reader`; the DI extension registers the concrete singleton and aliases the interface to the *same* instance.
- **Lossy by design:** no retry, no durability. A full queue drops + warns; a deploy/restart loses queued-but-unrun work. This is intended — the first consumer (the future classify job) re-triggers on the next user action.
- **This ships with no producers.** That is correct: the queue is the prerequisite primitive. The classify job (separate cycle) is the first `TryEnqueue` caller.

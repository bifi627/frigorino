# Backfill Product Classification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a startup maintenance task that backfills AI classification for existing ListItem product names that have no up-to-date `Product`.

**Architecture:** A pure, unit-tested helper (`ProductClassificationGaps`) decides which distinct normalized names referenced by active ListItems lack an up-to-date `Product` (never classified, or below the current `ClassifierVersion`). A thin `IMaintenanceTask` (`BackfillProductClassification`) loads the data, runs the helper, and enqueues each gap through the existing `IProductClassificationTrigger` (the live classification path). Registration is gated on the same `Ai:Classifier:Enabled` + API-key condition as live classification, so it is wired only when classification is actually on.

**Tech Stack:** .NET 10, EF Core (Postgres), xUnit. Reuses existing `BackgroundTaskQueue` / `QueuedHostedService` / `ClassifyProductJob` infrastructure.

---

## File Structure

- **Create** `Application/Frigorino.Infrastructure/Tasks/ProductClassificationGaps.cs` — pure gap-selection helper + its input/output records. Mirrors `CheckedItemPurge.cs` (same folder, EF-free so it is unit-testable).
- **Create** `Application/Frigorino.Test/Infrastructure/ProductClassificationGapsTests.cs` — unit tests for the helper. Mirrors `CheckedItemPurgeTests.cs`.
- **Create** `Application/Frigorino.Infrastructure/Tasks/BackfillProductClassification.cs` — the `IMaintenanceTask`. Thin orchestration (load → helper → enqueue → log).
- **Modify** `Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs` — add `IConfiguration` param; register the task only when classification is enabled.
- **Modify** `Application/Frigorino.Web/Program.cs:85` — pass `builder.Configuration` into `AddMaintenanceServices`.

---

## Task 1: Pure gap-selection helper

**Files:**
- Create: `Application/Frigorino.Infrastructure/Tasks/ProductClassificationGaps.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ProductClassificationGapsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Infrastructure/ProductClassificationGapsTests.cs`:

```csharp
using Frigorino.Infrastructure.Tasks;

namespace Frigorino.Test.Infrastructure
{
    public class ProductClassificationGapsTests
    {
        private const int CurrentVersion = 2;

        [Fact]
        public void NeverClassifiedName_IsGap()
        {
            var candidates = new[] { new ListItemNameCandidate(HouseholdId: 10, RawText: "Milk") };
            var existing = Array.Empty<ExistingProduct>();

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Equal(new[] { new ClassificationGap(10, "Milk") }, gaps);
        }

        [Fact]
        public void UpToDateProduct_IsSkipped()
        {
            var candidates = new[] { new ListItemNameCandidate(10, "Milk") };
            var existing = new[] { new ExistingProduct(10, "milk", ClassifierVersion: CurrentVersion) };

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Empty(gaps);
        }

        [Fact]
        public void StaleVersionProduct_IsGap()
        {
            var candidates = new[] { new ListItemNameCandidate(10, "Milk") };
            var existing = new[] { new ExistingProduct(10, "milk", ClassifierVersion: CurrentVersion - 1) };

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Equal(new[] { new ClassificationGap(10, "Milk") }, gaps);
        }

        [Fact]
        public void MultipleSpellings_NormalizeToSingleGap()
        {
            var candidates = new[]
            {
                new ListItemNameCandidate(10, "Milk"),
                new ListItemNameCandidate(10, "  milk "),
                new ListItemNameCandidate(10, "MILK"),
            };
            var existing = Array.Empty<ExistingProduct>();

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Single(gaps);
            Assert.Equal(10, gaps[0].HouseholdId);
        }

        [Fact]
        public void SameNameDifferentHouseholds_AreIndependentGaps()
        {
            var candidates = new[]
            {
                new ListItemNameCandidate(10, "Milk"),
                new ListItemNameCandidate(20, "Milk"),
            };
            var existing = new[] { new ExistingProduct(10, "milk", CurrentVersion) };

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Equal(new[] { new ClassificationGap(20, "Milk") }, gaps);
        }

        [Fact]
        public void BlankText_IsSkipped()
        {
            var candidates = new[]
            {
                new ListItemNameCandidate(10, "   "),
                new ListItemNameCandidate(10, ""),
            };
            var existing = Array.Empty<ExistingProduct>();

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

            Assert.Empty(gaps);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductClassificationGapsTests"`
Expected: FAIL — does not compile (`ProductClassificationGaps`, `ListItemNameCandidate`, `ExistingProduct`, `ClassificationGap` do not exist).

- [ ] **Step 3: Write the helper**

Create `Application/Frigorino.Infrastructure/Tasks/ProductClassificationGaps.cs`:

```csharp
using Frigorino.Domain.Products;

namespace Frigorino.Infrastructure.Tasks
{
    // A distinct product name referenced by an active ListItem, tagged with its household
    // (resolved via ListItem -> List -> HouseholdId). RawText is normalized by the helper.
    public sealed record ListItemNameCandidate(int HouseholdId, string RawText);

    // An existing product catalog row, reduced to the fields needed to decide staleness.
    public sealed record ExistingProduct(int HouseholdId, string NormalizedName, int ClassifierVersion);

    // A name needing (re)classification, carrying one representative raw name so the trigger/job
    // normalizes it consistently with the live path.
    public sealed record ClassificationGap(int HouseholdId, string RawName);

    // Pure gap decision: which referenced names have no up-to-date Product (never classified, or
    // below the current classifier version). Kept free of EF so it is unit-testable without a
    // database (mirrors CheckedItemPurge).
    public static class ProductClassificationGaps
    {
        public static List<ClassificationGap> SelectGaps(
            IReadOnlyCollection<ListItemNameCandidate> candidates,
            IReadOnlyCollection<ExistingProduct> existingProducts,
            int currentClassifierVersion)
        {
            // Highest classifier version per (household, normalized name). The unique index makes
            // duplicates unlikely, but Max keeps the decision well-defined regardless.
            var versionByName = new Dictionary<(int Household, string Name), int>();
            foreach (var product in existingProducts)
            {
                var key = (product.HouseholdId, product.NormalizedName);
                if (!versionByName.TryGetValue(key, out var current) || product.ClassifierVersion > current)
                {
                    versionByName[key] = product.ClassifierVersion;
                }
            }

            var gaps = new List<ClassificationGap>();
            var seen = new HashSet<(int Household, string Name)>();
            foreach (var candidate in candidates)
            {
                var normalized = ProductName.Normalize(candidate.RawText);
                if (normalized.Length == 0)
                {
                    continue;
                }

                var key = (candidate.HouseholdId, normalized);
                if (!seen.Add(key))
                {
                    continue;
                }

                var isUpToDate = versionByName.TryGetValue(key, out var version)
                    && version >= currentClassifierVersion;
                if (!isUpToDate)
                {
                    gaps.Add(new ClassificationGap(candidate.HouseholdId, candidate.RawText));
                }
            }

            return gaps;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductClassificationGapsTests"`
Expected: PASS (6 tests, 0 failures).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Tasks/ProductClassificationGaps.cs Application/Frigorino.Test/Infrastructure/ProductClassificationGapsTests.cs
git commit -m "feat: add product classification gap-selection helper"
```

---

## Task 2: The backfill maintenance task

**Files:**
- Create: `Application/Frigorino.Infrastructure/Tasks/BackfillProductClassification.cs`

This task is thin glue over the Task 1 helper plus EF reads and the existing trigger; its logic is covered by the helper's unit tests (the same pattern as `DeleteInactiveItems`, which has no test of its own — only `CheckedItemPurge` does). It is verified by a successful build here and the full suite in Task 4.

- [ ] **Step 1: Write the task**

Create `Application/Frigorino.Infrastructure/Tasks/BackfillProductClassification.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Tasks
{
    // Startup backfill: enqueue classification for ListItem product names that have no up-to-date
    // Product (never classified, or below the current ClassifierVersion). Idempotent and
    // version-aware via the existing classify job; capped per run, with the remainder picked up on
    // the next cold start (the queue is lossy, but loss here is recoverable by re-scanning).
    public class BackfillProductClassification : IMaintenanceTask
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IProductClassificationTrigger _trigger;
        private readonly IItemClassifier _classifier;
        private readonly ILogger<BackfillProductClassification> _logger;

        public BackfillProductClassification(
            ApplicationDbContext dbContext,
            IProductClassificationTrigger trigger,
            IItemClassifier classifier,
            ILogger<BackfillProductClassification> logger)
        {
            _dbContext = dbContext;
            _trigger = trigger;
            _classifier = classifier;
            _logger = logger;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var candidates = await _dbContext.ListItems
                .Where(li => li.IsActive)
                .Select(li => new ListItemNameCandidate(li.List.HouseholdId, li.Text))
                .Distinct()
                .ToListAsync(cancellationToken);

            var existing = await _dbContext.Products
                .Select(p => new ExistingProduct(p.HouseholdId, p.NormalizedName, p.ClassifierVersion))
                .ToListAsync(cancellationToken);

            var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, _classifier.Version);
            if (gaps.Count == 0)
            {
                return;
            }

            // Cap per run to the queue capacity so a large first backfill cannot overflow (and
            // silently drop) the lossy queue; the remainder is enqueued on the next cold start.
            var toEnqueue = gaps.Take(BackgroundTaskQueue.Capacity).ToList();
            foreach (var gap in toEnqueue)
            {
                _trigger.OnProductReferenced(gap.HouseholdId, gap.RawName);
            }

            var deferred = gaps.Count - toEnqueue.Count;
            _logger.LogInformation(
                "Backfill classification: {Total} gap(s) found, {Enqueued} enqueued, {Deferred} deferred to next cold start.",
                gaps.Count, toEnqueue.Count, deferred);
        }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Infrastructure/Tasks/BackfillProductClassification.cs
git commit -m "feat: add backfill product classification maintenance task"
```

---

## Task 3: Gated DI registration

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/Program.cs:85`

The task injects `IItemClassifier` and the queueing `IProductClassificationTrigger`, both of which are registered only when `Ai:Classifier:Enabled` is true **and** an API key is present (see `ItemClassificationDependencyInjection`). Register the task under the identical condition so DI `ValidateOnBuild` cannot fail.

- [ ] **Step 1: Update `AddMaintenanceServices`**

Replace the entire contents of `Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs` with:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class MaintenanceDependencyInjection
    {
        public static IServiceCollection AddMaintenanceServices(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IMaintenanceTask, DeleteInactiveItems>();

            // Registered only when live classification is on (same condition as
            // ItemClassificationDependencyInjection); otherwise IItemClassifier and the queueing
            // trigger are not in the container and ValidateOnBuild would fail.
            var classificationEnabled = configuration.GetValue<bool>("Ai:Classifier:Enabled");
            var apiKey = configuration["Ai:ApiKey"];
            if (classificationEnabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddScoped<IMaintenanceTask, BackfillProductClassification>();
            }

            services.AddHostedService<MaintenanceHostedService>();

            return services;
        }
    }
}
```

- [ ] **Step 2: Update the call site in `Program.cs`**

In `Application/Frigorino.Web/Program.cs`, change line 85 from:

```csharp
builder.Services.AddMaintenanceServices();
```

to:

```csharp
builder.Services.AddMaintenanceServices(builder.Configuration);
```

- [ ] **Step 3: Build the web host to verify wiring compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs Application/Frigorino.Web/Program.cs
git commit -m "feat: register backfill classification task when AI is enabled"
```

---

## Task 4: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Run the full solution test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS — all existing tests plus the 6 new `ProductClassificationGapsTests`. Capture the pass/fail summary line; do not trust a piped exit code (use the printed totals).

- [ ] **Step 2: Confirm no other call sites of `AddMaintenanceServices` were missed**

Run: `git grep -n "AddMaintenanceServices"`
Expected: exactly two hits — the definition in `MaintenanceDependencyInjection.cs` and the call in `Program.cs` (now passing `builder.Configuration`). If any other call site exists, update it to pass configuration.

- [ ] **Step 3: Final commit (if Step 2 required a fix; otherwise skip)**

```bash
git add -A
git commit -m "fix: pass configuration to remaining AddMaintenanceServices call sites"
```

> **Docker note:** No project was added/removed and the Dockerfile is unchanged, so a `docker build` is not required for this change. The full `dotnet test` run in Step 1 is the gate.

---

## Self-Review notes

- **Spec coverage:** gap helper (Task 1) ↔ spec §"Gap selection"; task + per-run cap + logging (Task 2) ↔ spec §"The maintenance task"; gated registration (Task 3) ↔ spec §"DI registration & AI-disabled behavior"; tests (Task 1) ↔ spec §"Testing"; error handling is inherited from `MaintenanceHostedService` (no code, noted in spec). Non-goals (orphan cleanup, inventory, new config) produce no tasks — correct.
- **Type consistency:** `ListItemNameCandidate(HouseholdId, RawText)`, `ExistingProduct(HouseholdId, NormalizedName, ClassifierVersion)`, `ClassificationGap(HouseholdId, RawName)`, and `ProductClassificationGaps.SelectGaps(candidates, existingProducts, currentClassifierVersion)` are used identically in Tasks 1 and 2. `BackgroundTaskQueue.Capacity` (public const, `Frigorino.Infrastructure.Services`) and `IItemClassifier.Version` are referenced as defined in the existing code.
- **No placeholders:** every code/command step is complete.

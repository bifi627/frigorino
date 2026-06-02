# Backfill Product Classification — Design

**Date:** 2026-06-02
**Branch:** `feat/backfill-product-classification`
**Status:** Approved (brainstorming)

## Problem

The AI classification feature produces a `Product` row keyed by `(HouseholdId, NormalizedName)`,
created/updated asynchronously by `ClassifyProductJob` when a ListItem is created or updated.
Items added **before** the feature shipped — and any names whose classification predates a
`ClassifierVersion` bump — have no up-to-date `Product`. We need a way to backfill classification
for existing items without manual intervention.

## Key facts that shape the design

- Items (`ListItem`/`InventoryItem`) have **no FK** to `Product`; they reference it only by
  normalized text (`ProductName.Normalize`) resolved at query time.
- `ClassifyProductJob` is already **idempotent and version-aware**: it skips any product already
  classified at or above the current `IItemClassifier.Version`. The "classify if not yet done"
  logic therefore already exists at the product level.
- The real unit of work is **not "every item"** — it is *every distinct normalized name referenced
  by items that lacks an up-to-date `Product`*. The existing job handles the rest.
- One "missing or stale" rule covers **two** cases: names never classified, and names whose
  `Product.ClassifierVersion` is below the current version (post-bump re-sweep) — no extra code.

## Scope decisions

- **Backfill source: ListItems only.** Matches current classification behavior; InventoryItems are
  not classified today and stay out of scope.
- **No orphan-product cleanup.** Products act as a per-household classification cache; re-adding a
  previously-removed item reuses the cached classification rather than paying for a new OpenAI call.
  Orphaned rows are cheap and are deliberately retained.

### Non-goals (YAGNI)

- No orphan-`Product` cleanup task.
- No `InventoryItem` classification.
- No new config knobs beyond the existing `Ai:Classifier:Enabled` flag.

## Approach (chosen: enqueue gaps via the existing trigger/queue)

A new startup maintenance task scans active ListItem texts, computes the distinct names lacking an
up-to-date `Product`, and enqueues each through the existing `IProductClassificationTrigger`
(`OnProductReferenced`) — the exact path live classification uses. Each enqueued job then runs in
its own DI scope via `QueuedHostedService`.

Rationale over a synchronous in-task classify loop:

- Reuses the proven live code path; per-job DI-scope isolation.
- When AI is disabled the injected trigger is `NullProductClassificationTrigger`, so the task
  naturally no-ops.
- The 1000-item queue cap is a natural throttle: cap enqueues per run, log the remainder, and the
  next cold start picks up what's left (idempotent). Progressive backfill across restarts.
- The queue is lossy, but loss here is **recoverable** (re-scanned next cold start), unlike the
  notification-ledger cron case where loss is unrecoverable. That is why the "cron batches send
  synchronously" rule does not apply.

## Components

### 1. Gap-selection helper (pure, unit-testable)

Mirrors the `CheckedItemPurge.SelectExpiredItemIds` pattern — a pure static helper so the logic is
unit-testable without a DB.

- **Input:**
  - distinct `(HouseholdId, rawText)` pairs from **active** ListItems
  - existing products as `(HouseholdId, NormalizedName, ClassifierVersion)`
  - the current `IItemClassifier.Version`
- **Logic:**
  - normalize each raw text via `ProductName.Normalize` (C#-side — cannot run in SQL)
  - dedupe to distinct `(HouseholdId, NormalizedName)`, keeping one representative raw name per name
  - a name is a **gap** if no product exists for it, *or* its product's `ClassifierVersion < current`
- **Output:** the list of gap `(HouseholdId, rawName)` to enqueue (one representative raw name per
  gap so the trigger/job normalizes consistently).

### 2. The maintenance task — `BackfillProductClassification : IMaintenanceTask`

Located in `Frigorino.Infrastructure/Tasks/`, alongside `DeleteInactiveItems`. Injects
`ApplicationDbContext` (read) and `IProductClassificationTrigger` (enqueue).

1. Load distinct `(List.HouseholdId, Text)` from active ListItems (projection, not full entities).
2. Load existing products' `(HouseholdId, NormalizedName, ClassifierVersion)`.
3. Run the helper to compute gaps.
4. Apply a **per-run cap** (= queue capacity, `1000`); enqueue each capped gap via
   `trigger.OnProductReferenced(householdId, rawName)`.
5. Log: total gaps found, number enqueued, and number deferred to the next cold start when capped
   (so silent truncation is visible). The remainder is picked up on the next cold start (idempotent).

### 3. DI registration & AI-disabled behavior

Register in `AddMaintenanceServices`, gated on the same `Ai:Classifier:Enabled` flag that
`ItemClassificationDependencyInjection` uses — so when AI is off the task is not registered at all
(no pointless scan). Belt-and-suspenders: even if it ran, the injected trigger would be
`NullProductClassificationTrigger` and enqueue nothing.

### 4. Error handling

Inherits `MaintenanceHostedService`'s per-task try/catch — a failure logs and never crashes startup.
Individual job failures are already isolated inside `QueuedHostedService` and the job's own
race-safe handling.

## Runtime characteristics

- Runs once per cold start (Railway sleeps on idle), 5s after boot, in its own DI scope.
- Steady state after a successful backfill: the scan finds no gaps and enqueues nothing — cost is
  just the DB scan.
- After a `ClassifierVersion` bump: the next cold start re-sweeps stale products automatically.
- First run on a large dataset: capped at 1000 enqueues; remainder backfilled across subsequent
  cold starts.

## Testing

- **Unit-test the pure helper** (xUnit, no DB):
  - never-classified name → gap
  - up-to-date product → skipped
  - stale-version product (`ClassifierVersion < current`) → gap
  - multiple raw spellings normalizing to one name → single gap
  - per-household isolation (same name in two households → two independent gaps)
- **No integration test** — reuses the proven live classification path.

## Files (anticipated)

- `Frigorino.Infrastructure/Tasks/BackfillProductClassification.cs` — new task
- `Frigorino.Infrastructure/Tasks/ProductClassificationGaps.cs` — pure gap-selection helper,
  placed alongside the task to match the `CheckedItemPurge` precedent
- `Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs` — gated registration
- `Frigorino.Test/Infrastructure/ProductClassificationGapsTests.cs` — unit tests for the helper
  (mirrors `CheckedItemPurgeTests` placement)

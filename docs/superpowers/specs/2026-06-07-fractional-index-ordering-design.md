# Fractional-index ordering (`SortOrder` int → `Rank` string)

**Date:** 2026-06-07
**Status:** Design approved, pending spec review
**Tech debt item:** "Sparse sort-order scheme has no rebalancer left" (TECH_DEBT.md)

## Problem

`List.ReorderItem` / `Inventory.ReorderItem` place a moved item at the integer midpoint
between its neighbours: `after + (before - after) / 2`. Integer division halves the gap
each time you drop into the same slot (`10000 → 5000 → … → 1 → 0`). After ~13 consecutive
drops into one region the gap collapses to 1, where `(1)/2 == 0` and the moved item lands on
the **exact same `SortOrder` as its neighbour**. `GetItems` orders by `Status, SortOrder`
with **no tiebreaker**, so equal orders sort nondeterministically — the reorder "won't stick"
and the list reshuffles on refetch. The rebalancer that used to recover from this
(`CompactItems` / `POST /compact`) is still in the tree but has **no caller** (the
`useCompactListItems` / `useCompactInventoryItems` hooks are imported by nothing), so the
degradation is currently permanent for an affected region.

The bug is narrow-trigger and low-severity at household-list scale today. The decision driver
is the **committed, near-term real-time multi-user sync** roadmap: once two members can reorder
the same list concurrently, the bounded-integer scheme would have to be reworked anyway
(it forces either an N-row renumber or a whole-list conflict unit). Iterating it further is
throwaway work.

## Decision

Replace the integer `SortOrder` with a **lexicographic string `Rank`** (fractional indexing,
Figma/rocicorp "Implementing Fractional Indexing" algorithm). String keys have unbounded
precision — they never exhaust, so no rebalancer ever exists — one-row write per reorder, and
independent per-item keys merge concurrent reorders cleanly. This is the canonical primitive
for collaborative sequences and the only scheme that satisfies both "no N-row renumber" and
"no whole-list conflict unit," making it the right fit for the sync roadmap.

### Settled decisions (brainstorm)

1. **Scheme:** string fractional indexing (candidate C). Not the integer band-aid.
2. **Sequencing:** full migration now, standalone — sync inherits a ready ordering primitive.
3. **Minting:** **server-authoritative.** Keep the existing `afterId` wire contract; the server
   mints the key. The client needs no fractional-indexing implementation — `common/sortOrder.ts`
   is deleted, not ported. One implementation, in C#, transport-agnostic.
4. **Algorithm:** **in-house** (~150 LOC) validated against the published reference test vectors.
   No new dependency; needed in exactly one language (server only).
5. **Collisions:** partial unique index + bounded handler retry (no client-minted salt).
6. **Migration:** **expand now, contract later.** This change ships the expand half (one
   EF-generated add-only migration + backfill + code switched to `rank`). `sort_order` is left as
   a dead, unused column and dropped in a separate cleanup at some later point — kept in the spec
   as a deferred follow-up, not part of this change's acceptance. Sorting data is non-critical and
   partial-backfill-on-failure is acceptable.

## Architecture

### 1. Algorithm component (Domain)

New `FractionalIndex` static class in `Frigorino.Domain` — **replaces `SortOrderCalculator`**.

```csharp
string GenerateKeyBetween(string? a, string? b);
IReadOnlyList<string> GenerateKeysBetween(string? a, string? b, int n);
```

- Canonical base-62 algorithm, alphabet `0-9A-Za-z`, integer-length-prefixed format.
- Pure: no DbContext, no I/O. Lives beside the aggregates that use it.
- `a >= b` throws (programming error — neighbours out of order).
- **Spec = the published reference test vectors**, written as unit tests *first* (TDD):
  `(null,null)→"a0"`, `("a0",null)→"a1"`, `(null,"a0")→"Zz"`, `("a0","a1")→"a0V"`, plus the
  `GenerateKeysBetween` multi-key vectors and the integer-rollover cases (`"az"→"b00"` etc.).

### 2. Entity + EF changes

- Add `ListItem.Rank (string)` and `InventoryItem.Rank (string)`. **`SortOrder` stays on the
  entities as a now-unused property** for this change — so the migration is a clean add-only
  (`AddColumn("rank")`), with no auto-generated `DropColumn`. The aggregates write/read `Rank`
  exclusively; `SortOrder` is dead but present, and is removed (property + column) in the deferred
  cleanup.
- EF config: `Rank` is `text` with **`.UseCollation("C")`** (byte-ordinal). Required — Postgres'
  default locale collation sorts case/punctuation in a way that disagrees with the key alphabet
  and silently *almost* works. `C` collation makes `ORDER BY rank` match the key total order.
- **Rank scoping differs by aggregate** (the asymmetry that must be preserved):
  - `ListItem`: rank space **per `(ListId, Status)`** — two sections (unchecked / checked).
    Read order `Status, Rank, Id`.
  - `InventoryItem`: rank space **per `(InventoryId)`** — single section, no status flag.
    Read order `Rank, Id`.
- **Partial unique indexes** over active rows:
  - `(ListId, Status, Rank) WHERE IsActive`
  - `(InventoryId, Rank) WHERE IsActive`
- `Rank` left nullable at the DB level (code always assigns it; avoids extra DDL). Integrity is
  code-enforced via the aggregate methods.

### 3. Aggregate methods

- `ComputeAppendRank` (replaces `ComputeAppendSortOrder`):
  - unchecked / inventory append → `GenerateKeyBetween(lastRankInSection, null)`
  - checked prepend → `GenerateKeyBetween(null, firstRankInSection)`
  - empty section → `GenerateKeyBetween(null, null)` (`"a0"`)
- `ReorderItem` (both aggregates): `GenerateKeyBetween(afterRank, beforeRank)`;
  top → `GenerateKeyBetween(null, firstRank)`, bottom → `GenerateKeyBetween(lastRank, null)`.
  The integer-division midpoint and its exhaustion are **gone**.
- The `afterItemId == 0` → top-of-section fallback and the self-anchor no-op are preserved
  (wire contract the optimistic UI depends on).
- **Deleted entirely** (fractional indexing never exhausts → the rebalancer has no reason to
  exist; clears the orphaned-rebalancer debt as a side effect):
  - `SortOrderCalculator`
  - `List.CompactItems()`, `Inventory.CompactItems()`
  - `POST /compact` slices: `CompactItems.cs`, `CompactInventoryItems.cs`
  - their wiring in `Program.cs` (`listItems.MapCompactItems()`, `inventoryItems.MapCompactInventoryItems()`)
  - the `useCompactListItems` / `useCompactInventoryItems` hooks

### 4. Read path

- `GetItems`: `OrderBy(i => i.Status).ThenBy(i => i.Rank).ThenBy(i => i.Id)`.
- `GetInventoryItems`: `OrderBy(i => i.Rank).ThenBy(i => i.Id)`.
- The `Id` tertiary is the cheap **read-side backstop** for any residual rank tie (the writeup's
  tiebreaker, retained even under fractional indexing — defense in depth for the brief window
  between a concurrent collision and its retry-resolution).

### 5. Concurrency / collisions

- Server-authoritative minting keeps the `afterId` contract. Two concurrent requests inserting
  into the *same* slot can both compute the same key before either commits.
- The **partial unique index** rejects the duplicate at `SaveChanges`. The handler catches the
  unique-violation `DbUpdateException`, reloads the aggregate (now sees the committed neighbour),
  re-mints, and retries — **bounded (3 attempts)**, then surfaces a 409/conflict if still racing.
- This keeps the key space healthy: two equal adjacent keys would make a later between-insert
  `GenerateKeyBetween(equal, equal)` throw. The `Id` read-tiebreaker covers reads in the gap.
- Retry logic is shared (a small helper) across the reorder / add / toggle-status handlers that
  mint a rank.

### 6. Frontend

- **Delete `common/sortOrder.ts`** (client no longer mints keys).
- `useReorderListItem` / `useReorderInventoryItem` optimistic update: splice the moved element
  into its new array position (visual order only) instead of computing a numeric `sortOrder`.
  The authoritative `rank` arrives on refetch and reconciles.
- Regenerate the TS client via `npm run api` — response DTOs change `sortOrder: number` →
  `rank: string`. Audit every `sortOrder` reference in the SPA and remove it.
- **Plan-time check:** confirm the list / inventory components render in the server-provided
  array order, not by re-sorting on a numeric field. If anything sorts by `sortOrder` client-side,
  it must switch to string `rank` comparison or trust array order.

### 7. Migration / backfill (expand now, contract later)

The hard constraint: the backfill must read the old order, and canonical keys can't be generated
in pure SQL (needs C#). Expand/contract sidesteps the interleaving problem cleanly — the backfill
runs at startup with `sort_order` still present, and the column is dropped in a *separate, later*
migration once it's confirmed dead.

**This change (expand):**

1. **Entity** gains `Rank`, keeps `SortOrder` as an unused property (step 2) — so the migration
   is add-only.
2. **EF migration M1** (generated, no hand-edits): `AddColumn("rank")` as `text` with
   `COLLATE "C"` + the two partial unique indexes. Because the entity still has `SortOrder`,
   nothing is dropped. Partial unique indexes tolerate the transient all-`NULL` `rank` (Postgres
   treats `NULL`s as distinct).
3. **One-time startup backfill**, run after `MigrateAsync`, before serving, guarded on
   `WHERE rank IS NULL` so it's idempotent and a no-op once complete:
   - Reads `(id, sort_order)` per section, ordered by `sort_order`.
   - Computes canonical keys in C# via `GenerateKeysBetween(null, null, count)`.
   - `UPDATE` each row's `rank` — **does not bump `UpdatedAt`** (deliberate; avoids the retention
     regression that killed the old periodic `RecalculateSortOrderTask` sweep).
   - Logs row counts. **Not** an `IMaintenanceTask` (one-shot, not periodic).
   - A crash mid-backfill leaves partial ranks → degraded sort only (accepted). Next boot re-runs
     the `WHERE rank IS NULL` remainder. Rows created by new code already carry a `rank`, so the
     guard only touches pre-existing rows.
4. From this deploy on, the aggregates use `rank` exclusively. `sort_order` is dead, but retained
   as a passive rollback net (old code reading it still finds the pre-existing values).

**Deferred cleanup (contract — separate change, "at some point later"):**

- Remove the `SortOrder` property from both entities and the EF config.
- **EF migration M2** (generated): `DropColumn("sort_order")` on both tables.
- Remove the startup backfill code.
- Gate: only once stage + prod have run the backfill and `rank` is confirmed fully populated.

## Testing

- **Unit (Domain):** `FractionalIndex` against the full published reference vectors (TDD-first);
  `ReorderItem` / `ComputeAppendRank` for both aggregates — append, top, bottom, between,
  empty-section, status-toggle re-mint, and the "13 drops into one slot" case that used to
  collapse (now produces ever-longer distinct keys).
- **Architecture:** existing ArchUnitNET layer rules still hold (algorithm stays in Domain).
- **Integration (Reqnroll + Playwright):** reorder persists across refetch (the regression that
  motivated this); concurrent same-slot reorder resolves to a deterministic order (collision
  retry). Assert on testids / `data-*`, never translated text.
- **Verify:** full `dotnet test Application/Frigorino.sln`, `npm run lint`/`tsc`/`prettier`,
  `npm run build`, and `docker build` (Dockerfile drift). Manual browser verify of drag-reorder
  on a dev-up stack — static checks won't catch a render-order bug.

## Out of scope

- The real-time sync feature itself (transport, presence, conflict propagation). This change only
  delivers the ordering primitive it will build on.
- Any change to item semantics (text, quantity, expiry, status, promotion).

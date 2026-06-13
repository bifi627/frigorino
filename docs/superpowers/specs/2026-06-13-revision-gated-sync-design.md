# Revision-gated collaborative sync (lists, inventories, calendar)

**Date:** 2026-06-13
**Branch:** `feat/realtime-sync` (off `stage`)
**Status:** Design approved, pending spec review

## Problem

When two household members view the same list, inventory, or the expiry
calendar at the same time, one user's changes are invisible to the other until
a manual refresh. The goal is a near-live "shared document" feel — remote
changes appear within a couple of seconds — **without** re-pulling the full
payload on every poll and **without** a persistent connection that would defeat
Railway's idle-sleep policy.

## Why not SignalR / WebSockets (v1)

The original IDEAS.md sketch proposed SignalR Hubs. We deliberately reject that
for v1:

- A persistent WebSocket keeps the container awake, defeating Railway's
  idle-sleep and driving up compute cost (see `project_no_synthetic_uptime_checks`
  / `project_cron_batch_sync_send` reasoning — always-on connections are the
  same anti-pattern).
- The user's actual requirement is "a few seconds is fine," not sub-second
  co-editing. WebSocket latency buys nothing the requirement asks for.
- SignalR needs new infra: `AddSignalR()`, a Hub, auth-over-querystring, the
  `@microsoft/signalr` client. Revision-gated polling needs none of that.

SignalR remains cleanly addable later; nothing in this design precludes it.

## Prerequisite — already resolved

IDEAS.md flagged the float `SortOrder` collision risk as a hard prerequisite.
**This is already done:** `SortOrder` was dropped (`DropItemSortOrder` migration)
and replaced by `Rank`, a lexicographic string fractional index
(`Frigorino.Domain.Entities.FractionalIndex`, `AddItemRank` migration). Concurrent
reorders already produce composable, collision-free keys. No blocker remains.

## Core idea

Replace blind polling with a **cheap change-detection token**. The client polls
a tiny `revision` endpoint that returns an opaque string computed from a cheap
indexed aggregate. The client compares the token to the last value it saw; only
when it changes does it invalidate the real data query, triggering exactly one
full fetch. Steady-state cost is ~20 bytes every 2s instead of the whole payload.

### Why the token is cheap (and correct)

The token folds three things into an opaque string:

1. The **parent row's** `UpdatedAt` (catches a list/inventory *rename*).
2. `MAX(item.UpdatedAt)` over the in-scope active items.
3. `COUNT(*)` of in-scope active items.

Every mutation auto-stamps `UpdatedAt` in `ApplicationDbContext.SaveChangesAsync`,
so edits, adds, reorders (Rank change → stamp), and soft-deletes (stamp +
`IsActive=false`) all move the `(max, count)` tuple. The tuple is robust to
soft-delete (count drops) and to add (count rises / max rises). The client never
parses the token — it only compares for equality.

**Cost:** every revision query is supported by an existing composite index and
does strictly less work than one full fetch (no projection, no row transfer).
Aggregate load scales with *concurrent focused viewers* (1–3 per household),
not table size — well under 1 query/sec per resource at peak. Idle/backgrounded
tabs poll nothing. Postgres does not notice this.

## Architecture

Three revision slices on the backend, three gate hooks on the frontend. One per
syncable page. No migration, no schema change, no new dependency.

| Page | Existing data query | New revision endpoint | Token scope |
|---|---|---|---|
| List view | `GetItems` (`getItemsOptions`) | `GET .../lists/{listId}/revision` | one list row + its active items |
| Inventory view | inventory `GetItems` | `GET .../inventories/{inventoryId}/revision` | one inventory row + its active items |
| Expiry calendar | `GetExpiryCalendar` | `GET .../inventories/calendar/revision` | **all** household inventory items with `ExpiryDate != null` |

Full paths (all under the existing `/api/household/{householdId:int}` prefix and
`RequireAuthorization()` groups):

- `GET /api/household/{householdId}/lists/{listId}/revision`
- `GET /api/household/{householdId}/inventories/{inventoryId}/revision`
- `GET /api/household/{householdId}/inventories/calendar/revision`

The calendar revision sits under the `inventories` group beside the existing
`MapGetExpiryCalendar()` (literal `calendar` segment), keeping `householdId` in
the path and auth identical to its siblings.

## Backend design

Each revision endpoint is a **handler-only read slice** (no aggregate method, no
domain mutation), mirroring `GetItems.cs` / `GetExpiryCalendar.cs`:

- Auth: `db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct)` →
  404 on no membership, exactly like the sibling read slices.
- Resource existence: same `AnyAsync` existence check the data endpoint already
  does (e.g. list exists + belongs to household + `IsActive`) → 404 otherwise.
- Returns a small DTO: `RevisionResponse(string Rev)` → `{ "rev": "..." }`.

### Token computation

Opaque, server-minted, never shown in UI. Format is an implementation detail the
client treats as a black box. Reference shape:

```
rev = $"{parentUpdatedAt.Ticks}.{maxItemUpdatedAtTicks}.{activeCount}"
```

- **List / inventory revision:** fetch the parent row's `UpdatedAt` (in the
  existing existence query) and aggregate over its active items:
  `db.ListItems.Where(i => i.ListId == listId && i.IsActive)` →
  `Max((DateTime?)i.UpdatedAt)` + `Count()`. Empty set → max `null` (encode as
  `0`), count `0`. Supported by `IX_ListItems_ListId_IsActive` /
  `IX_InventoryItems_InventoryId_IsActive`.
- **Calendar revision:** aggregate over the *exact same filter* the calendar
  query uses —
  `i.IsActive && i.ExpiryDate != null && i.Inventory.IsActive && i.Inventory.HouseholdId == householdId`
  — `Max(i.UpdatedAt)` + `Count()`. Scoping to the expiry filter means editing a
  *non-perishable* inventory item does **not** move the calendar token (no
  needless calendar refetch), while add/edit/remove-expiry/delete all do.
  Supported by `IX_InventoryItems_ExpiryDate_IsActive` plus the existing
  Inventory join. There is no parent "row" for the calendar — only the item
  aggregate — so the token is `{max}.{count}`.

### Registration

Add per-slice extension methods alongside the existing ones in `Program.cs`:

- `lists.MapGetListRevision();`
- `inventories.MapGetInventoryRevision();`
- `inventories.MapGetExpiryCalendarRevision();`

After adding endpoints/DTOs, regenerate the TS client with `npm run api` from
`ClientApp/` (emits `openapi.json`, regenerates `src/lib/api`).

## Frontend design

Three gate hooks, one per syncable page, each following the one-hook-per-file
TanStack Query convention (spread generated `*Options`, never hand-write
`queryFn`/`queryKey`):

- `useListRevision(householdId, listId)`
- `useInventoryRevision(householdId, inventoryId)`
- `useCalendarRevision(householdId)`

Each hook:

1. `useQuery({ ...getXRevisionOptions({ path }), enabled, refetchInterval: 2000, refetchIntervalInBackground: false, refetchOnWindowFocus: true })`.
   `enabled` guards every path id `> 0`.
2. Holds the last-seen `rev` in a ref. On each successful poll, if `rev` changed
   **and** no local mutation for this resource is in flight, invalidate the real
   data query.

### Cadence and focus-gating (the only real cost knob)

- `refetchInterval: 2000` — 2s while focused (user-chosen; trivially tunable).
- `refetchIntervalInBackground: false` — a hidden/backgrounded tab stops polling
  entirely. This is what protects Railway-sleep and overhead.
- `refetchOnWindowFocus: true` on the revision query — instant catch-up the
  moment the user tabs back, without waiting for the next tick.
- No exponential backoff in v1 — constant 2s, ~20-byte payload. Backoff is a
  noted future lever, not v1 scope.

### Local-edits-win mutation gate (the subtle bit)

The existing toggle/reorder/create/update hooks already do optimistic cache
writes via `onMutate`/`onError`/`onSettled`. A revision-triggered invalidation
must **not** clobber an in-flight optimistic mutation (it would flicker/revert
mid-action). The gate: before invalidating, skip if
`queryClient.isMutating({ ... }) > 0` for this resource. If a local mutation is
in flight, that tick's invalidation is skipped; the next poll (or the mutation's
own `onSettled` invalidation) catches up. Local edits stay authoritative;
remote changes pull only when the user is idle.

### Wiring

Each page component wires its gate hook in as a one-line side-effectful
subscriber. Rendering is unchanged — pages still read from the existing data
query:

- `ListViewPage` → `useListRevision(...)`
- inventory detail page → `useInventoryRevision(...)`
- `ExpiryCalendarPage` → `useCalendarRevision(...)`

A user can have the calendar open and an inventory detail open in separate tabs;
both poll independently at 2s. That is fine — still tiny, no shared state needed.

## Data flow

```
User B mutates item ──► SaveChangesAsync stamps UpdatedAt
                         (token's (max,count) tuple moves)

User A's tab (focused):
  every 2s ──► GET .../revision  ──► { rev }
                 rev unchanged? ──► do nothing (~20 bytes spent)
                 rev changed AND no local mutation in flight?
                     ──► invalidateQueries(real data key)
                         ──► one full fetch ──► UI updates
```

## Edge cases

- **Empty resource:** aggregate over zero rows → `max` null (encoded `0`),
  `count` 0 → stable token. Adding the first item moves it. ✓
- **Local user is the mutator:** their optimistic update already reflects the
  change; the mutation gate skips the redundant remote invalidation; the next
  poll's token matches their now-persisted state → no double fetch, no flicker.
- **Rename of the list/inventory by another user:** parent `UpdatedAt` is in the
  token → triggers refresh. (Calendar has no parent row — only items affect it,
  which matches what the calendar renders.)
- **Resource deleted out from under the viewer:** existence check → 404 from the
  revision endpoint; hook stops on error (no infinite error-poll — rely on
  TanStack Query default `retry` then the data query's own 404 handling drives
  the UI). Confirm the revision query does not retry-storm on 404.
- **Clock/precision:** token uses `DateTime.Ticks`; comparison is string
  equality, never ordering, so no clock-skew sensitivity.

## Testing

- **Backend (`Frigorino.Test` + `Frigorino.IntegrationTests`):** integration
  tests that the token is stable across no-op reads and **changes** after each
  mutation kind — add, edit, reorder, soft-delete, parent rename — for list and
  inventory; and that the calendar token moves on expiry add/remove but **not**
  on a non-perishable item edit. Membership 404 path covered like sibling slices.
- **Frontend:** no JS test runner exists. Verify manually via `/dev-up` + two
  browser sessions (or Playwright MCP): mutate in one, confirm the other updates
  within ~2s; confirm an in-flight local drag/edit is not clobbered by a
  concurrent remote change; confirm a backgrounded tab stops polling (network
  panel) and catches up on focus. Run `npm run build` before IT so new testids
  ship to `ClientApp/build`.
- **Full gate:** `dotnet test Application/Frigorino.sln` + frontend
  `lint`/`tsc`/`prettier`. `docker build` if the change set warrants it.

## Out of scope (v1)

- SignalR / WebSocket transport, presence ("N viewing"), per-field CRDT merge.
- Exponential backoff on the poll interval.
- A covering index `(ListId, IsActive) INCLUDE (UpdatedAt)` — premature at this
  scale and it slows every write; note only, do not add.
- Offline/conflict queue, cross-household sync.

## Future levers (noted, not built)

- Tune interval / add backoff if telemetry ever shows poll pressure.
- Covering index if a list ever grows large enough to make the heap touch matter.
- Swap the poll for SignalR push if a true sub-second co-editing requirement
  appears — the gate-hook seam (revision → invalidate data query) is the same
  shape a push subscriber would occupy.

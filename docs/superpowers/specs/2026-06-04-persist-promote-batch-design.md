# Persist the promote-to-inventory batch on the list

**Date:** 2026-06-04
**Branch:** `feat/persist-promote-batch`
**Status:** Design — approved forks, pending spec review
**Builds on:** `2026-05-31-promote-to-inventory-ux-design.md`, `2026-06-02-promote-adjust-quantity-design.md`

## Problem

Promotion of checked-off list items into inventory is a household activity split across
people, but the *pending batch* isn't shared. Today it lives in device-scoped
`localStorage` (`features/lists/promote/promotableStore.ts`, key `frigorino.promote.batch`).

Real-world break: **Person A** shops and checks items off in the store; **Person B** unpacks
the bags at home and wants to promote them into inventory — but Person B's app shows no
pending batch, because it only ever existed in Person A's browser.

**Goal:** lift the pending-promotion state out of the browser and into the database, scoped
to the shopping list, so any member sees and can act on the same batch. **Non-goal:** redesign
the promote UX — the existing sheet behavior is preserved exactly.

## Core insight

The checked-off items already persist server-side (they're list items with `Status == true`).
The only thing trapped in `localStorage` is *which checked items are still awaiting promotion*.
So this isn't a new batch entity — it's a **per-item promoted/skipped distinction** the server
doesn't model yet. Add that, and the per-list batch becomes a derived read ("checked
candidate items on this list not yet resolved") that every member sees identically. No batch
entity, no cross-device sync protocol.

## Preserved UX (no change)

The promote sheet (`PromoteReviewSheet.tsx`) keeps its current behavior — only the data source
moves from `localStorage` to the server:

1. The promote batch is **scoped per shopping list** (as today, keyed by `listId`).
2. The sheet is a list of promotable items (one row each).
3. Each row has a **selection checkbox**, on by default.
4. **Promote** adds only the *selected* rows to the *selected target inventory*.
5. **Clear All** removes all items from the sheet.
6. Each row has an **X** that removes that one item.
7. Closing the sheet changes nothing.

The behavioral shift is only in durability/sharing: X, Clear All, and Promote now write
**shared, durable** state (visible to every member) instead of mutating a device-local store.

## Data model

Three nullable columns on `ListItem` (`Frigorino.Domain/Entities/ListItem.cs`) — flat table,
no inheritance:

| Column | Type | Meaning |
|---|---|---|
| `PromotionExpiryHandling` | `ExpiryHandling?` (enum, stored int) | Stamped when an item is checked **and** found perishable. Non-null ⇒ promotion candidate. Captures the suggestion the checker saw. |
| `PromotionSuggestedExpiry` | `DateOnly?` | Suggested expiry captured at check time (null for `UserEntersFromPackage`). |
| `PromotionResolvedAt` | `DateTime?` | Stamped when the item is **promoted into inventory OR skipped** (X / Clear All). Non-null ⇒ resolved/done. |

**Pending predicate (per list):**
```
Status == true && PromotionExpiryHandling != null && PromotionResolvedAt == null
```
A pure column predicate — cheap to count, no Product-catalog join at read time.

**Single resolved stamp, deliberately:** promote and skip both mean "this pending item has
been dealt with." That's one coherent concept ("resolved"), not an overloaded field. Per-member
attribution of who promoted vs. skipped is out of scope (v1), so no discriminator column.

**Migration:** one EF migration adding the three columns plus a composite index
`(ListId, Status, PromotionResolvedAt)` to support the count and detail reads. Pre-existing
checked items default to null columns ⇒ they never retroactively flood the bar. No backfill
needed.

## Candidacy lifecycle (toggle)

`ToggleItemStatus` (`Frigorino.Features/Lists/Items/ToggleItemStatus.cs`) **already computes**
the `PromoteSuggestion` from the Product catalog when an item is checked (lines 66–75). We
persist what it already computes instead of only returning it:

- **Check (false → true):** slice computes the suggestion; a `List` aggregate method
  `ApplyPromotionSuggestion(itemId, handling, suggestedExpiry)` stamps
  `PromotionExpiryHandling` + `PromotionSuggestedExpiry` (or leaves null if non-perishable) and
  **resets `PromotionResolvedAt = null`**.
- **Uncheck (true → false):** `ToggleItemStatus` aggregate method **clears all three** promotion
  columns (pure domain, no Product lookup needed).

Consequence — the `resolved → uncheck → recheck` path: clearing on uncheck plus reset-on-check
means a re-purchased item that was already promoted/skipped becomes a **fresh pending candidate
again** when re-checked ("bought milk again, checked it off again" re-offers promotion; promoting
again adds a second inventory entry, which is correct — each shopping trip is its own promotion).
This preserves today's contract: *unchecking retracts the pending entry; re-checking
re-evaluates from scratch.*

The toggle endpoint keeps returning the `Promote` suggestion in its response (unchanged), so the
client can optimistically reflect a new candidate; it is no longer the source of truth for the
batch.

## Reads

- **Count on the list:** `ListResponse` (`Frigorino.Features/Lists/ListResponse.cs`) gains
  `PendingPromotionCount`, projected inline via the pending predicate. Drives the `PromoteBar`
  badge for **every** member on list open. Cheap (column predicate, no join).
- **Detail slice (lazy):** new read-only slice
  `GET /api/household/{householdId}/lists/{listId}/pending-promotions` →
  `[{ listItemId, text, quantity, expiryHandling, suggestedExpiry }]`, projected straight from
  the stored columns (no Product join). Fetched only when the sheet opens.

## Writes

Two new slices under `Frigorino.Features/Lists/Promote/`. Cross-aggregate (List + Inventory);
`Households/Members/AddMember.cs` is the precedent for a cross-aggregate slice.

### Promote (batch)
```
POST /api/household/{householdId}/lists/{listId}/promote
{ inventoryId, items: [ { listItemId, quantity?, expiryDate? } ] }
```
Single transaction. Per item: `Inventory.AddItem(text, quantity, expiryDate)` + stamp
`PromotionResolvedAt` on the source `ListItem` via a `List` aggregate method. `quantity` /
`expiryDate` carry the per-row edits from the sheet. **Idempotent:** an already-resolved item is
a no-op success — handles Person A and Person B racing. One `SaveChangesAsync`.

### Skip (X and Clear All)
```
POST /api/household/{householdId}/lists/{listId}/promote/skip
{ listItemIds: [ ... ] }
```
Stamps `PromotionResolvedAt` (no inventory write) on each id. **X** sends one id; **Clear All**
sends all currently-pending ids. Immediate and durable (shared), matching today's immediacy.
Idempotent (already-resolved ⇒ no-op).

### Domain methods (`Frigorino.Domain/Entities/List.cs`)
- `ApplyPromotionSuggestion(itemId, handling, suggestedExpiry)` — stamp candidacy + reset resolved.
- `ToggleItemStatus(itemId)` — extended to clear the three promotion columns on uncheck.
- `ResolvePromotion(itemId, resolvedAt)` — stamp resolved; already-resolved is a no-op success;
  validates the item exists / belongs to the list. Used by both Promote and Skip.

All return `FluentResults.Result`, dispatched by error type per the slice convention
(`EntityNotFoundError` → 404, etc.).

## Frontend

- **Delete** `features/lists/promote/promotableStore.ts` (the localStorage Zustand store) — no
  parallel cache (remove-dead-code).
- `PromoteBar.tsx` reads `PendingPromotionCount` from the `useList` query (count > 0 ⇒ visible).
- `PromoteReviewSheet.tsx`:
  - fetches `usePendingPromotions(listId)` (new query hook) on open; seeds row drafts from server
    data instead of the store.
  - **Promote** (`handleAdd`) → new `usePromoteListItems` mutation with the selected rows +
    `inventoryId`; on success invalidate `getList`, `pending-promotions`, and the target
    inventory's items query; toast unchanged.
  - **X** (`handleOmit`) → new `useSkipPromotion` mutation with `[itemId]`.
  - **Clear All** (`handleClearAll`) → `useSkipPromotion` with all pending ids.
- `useToggleListItemStatus.ts`: drop the `store.add` / `store.remove` side effect. Add a
  debounced invalidation of the `getList` query (alongside the existing items invalidation) so
  the bar count reconciles for the toggling member. (Optimistic `+1` on a check that returns
  `promote` is optional polish, not load-bearing.)

New hooks follow the API hook conventions (`features/lists/useList.ts` /
`features/lists/useDeleteList.ts` as templates); regenerate the client with `npm run api`.

## Cleanup interaction

No change to `DeleteInactiveItems` (`Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`).
Checked items — pending or resolved — purge at the household's `CheckedItemRetentionDays`
(default 30) window as today. That naturally bounds how long a stale pending batch survives,
which is the intended behavior (a month-old un-promoted check-off is stale).

## Out of scope (v1)

- Real-time push so Person B's open list updates the instant Person A checks an item off — normal
  query refetch / invalidation is enough; the batch is durable, not live.
- Per-member attribution of who promoted/skipped what.
- Partial-batch hand-off UI beyond "any member can promote/skip the remaining pending items."

## Verification

- Backend: `dotnet test Application/Frigorino.sln` (Test + IntegrationTests). New aggregate-method
  unit tests for `ApplyPromotionSuggestion`, the uncheck clear, and `ResolvePromotion` idempotency.
- Frontend: `npm run api` (regen client), `npm run lint`, `npm run tsc`, `npm run prettier`.
- Manual browser verify (two browser profiles / dev-up): Person A checks items off → Person B
  sees the bar and the pending batch; promote/skip/clear reconcile across both.
- `docker build -f Application/Dockerfile` as the final gate.

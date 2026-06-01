# Promote checked list items into inventory — Cycle 3 (UX) design

**Date:** 2026-05-31
**Status:** Approved design, ready for implementation plan
**Predecessors:** Cycle 1 (async runner), Cycle 2 (classification engine), Cycle 2.5 (quantity extraction) — all shipped. Tracking entry: `IDEAS.md` → "Promote checked list items into inventory (classifier-driven)".

## Why

Inventory only pays off if items land in it, and today the only path is manual entry *after* shopping — the worst possible moment, so most users skip it. Cycles 2/2.5 built the catalog knowledge (`Product` classification + structured `Quantity`) but it is invisible until inventory has items. Cycle 3 closes the loop: checking an item off a list passively offers to add it to inventory with a pre-filled expiry and quantity.

## Key decisions (from brainstorm)

1. **Passive + batch, not per-item interrupt.** Checking off an eligible item accumulates it; a sticky bar offers a single review sheet. Avoids modal whack-a-mole during rapid put-away. (Evolvable to per-item later.)
2. **Suggestion embedded in the toggle response**, accumulated client-side. No new GET endpoint. Accepts the documented eventual-consistency race (item added + checked off within the same ~3s window before classification finishes won't carry a suggestion that once; re-triggered on next reference).
3. **Eligibility = perishables only**: `ExpiryHandling ∈ { AiRecommendsShelfLife, UserEntersFromPackage }`. `NonPerishable` / `Unknown` are silently skipped (no suggestion, not counted).
4. **Inventory target = picker defaulting to newest inventory.** No `User` preference column, no migration. Single inventory → no picker.
5. **Per-row selection in the review sheet** so a mixed batch can split across inventories (some → Fridge, some → Pantry) in successive passes.
6. **Batch persisted in `localStorage`** (device-scoped), survives a mid-shop refresh. No DB state.
7. Reuse the existing `CreateInventoryItem` slice + `useCreateInventoryItem` hook verbatim — no new write API.
8. **Users can omit items** they don't want in inventory (per-row `✕` removes from the batch) — distinct from deselecting for a split pass; plus a **Clear all**. Closing the sheet keeps the batch.
9. **Recommended dates display human-readably** in the sheet via the existing inventory `getExpiryInfo` helper, kept consistent with the inventory list and updating live as the user edits the date.

## Backend

No new endpoint, no migration. One field added to the toggle response and a point lookup in the toggle handler.

### `ListItemResponse` (`Frigorino.Features/Lists/Items/ListItemResponse.cs`)

Add one optional field:

```csharp
public sealed record PromoteSuggestion(ExpiryHandling ExpiryHandling, DateOnly? SuggestedExpiry);

// ListItemResponse gains:
//   PromoteSuggestion? Promote = null
```

- `Promote == null` → item is not eligible (un-checked, NonPerishable, Unknown, or not-yet-classified).
- `Promote != null` with `SuggestedExpiry` set → `AiRecommendsShelfLife` (date pre-filled).
- `Promote != null` with `SuggestedExpiry == null` → `UserEntersFromPackage` (blank, user reads off package).

`ExpiryHandling` already serializes as a string name on the wire (existing `JsonStringEnumConverter`).

### `ToggleItemStatus` slice (`Frigorino.Features/Lists/Items/ToggleItemStatus.cs`)

After the status flip succeeds, **only when the new status is "checked/done"**:

1. `var normalized = ProductName.Normalize(item.Text);`
2. Point-lookup `Product` on the existing unique index `(HouseholdId, NormalizedName)`, filtered `IsActive`.
3. If found **and** `product.EffectiveExpiry.Handling ∈ { AiRecommendsShelfLife, UserEntersFromPackage }`:
   - `Promote = new PromoteSuggestion(handling, product.EffectiveExpiry.SuggestedExpiry(today))`.
   - `today` = server `DateOnly` (no new dependency; use the same clock approach as existing slices).
4. Otherwise `Promote = null`.

The handler already has `householdId` (route) and EF access. This adds one indexed read on the toggle path, as the sketch anticipated. Un-checking returns `Promote = null` (the frontend uses this to retract a pending entry).

### Notes / constraints

- Reuse `ProductName.Normalize` (lowercase + trim + collapse whitespace) so the lookup key matches the classifier's stored key exactly.
- No denormalization onto `ListItem`; the lookup stays a property of the product name.
- `CreateInventoryItemRequest(string Text, string? Quantity, DateOnly? ExpiryDate)` is the reuse target — inventory `Quantity` is **free-text string**, so the structured list-item `Quantity` is formatted to a display string client-side before promotion.

## Frontend

### Persistent batch store (`features/lists/promote/promotableStore.ts`)

Zustand store with the `persist` middleware → `localStorage["frigorino.promote.batch"]`.

- **Entry shape:** `{ itemId, listId, householdId, name, quantity: QuantityDto | null, expiryHandling, suggestedExpiry: string | null }`.
- **Actions:** `add(entry)`, `remove(itemId)`, `clear()`, selectors for "entries for list X" and count.
- **Rehydration:** on app boot the store loads from localStorage, so the sticky bar/sheet reappear after a refresh mid-shop. No data loss, no DB row.
- **Lifecycle:** an entry clears when its item is successfully **added** to an inventory, **omitted** (per-row `✕`), **un-checked** on the list, or via **Clear all**. Merely closing the review sheet does **not** clear entries — the bar persists.

### Toggle hook (`features/lists/items/useToggleListItemStatus.ts`)

Add an `onSuccess(data)` that **only** touches the store — **no query invalidation** (honors the existing deliberate "no `onSuccess` invalidate" rule; the `onMutate`/`onError`/`onSettled` optimistic flow is untouched):

```
onSuccess(data) {
  if (data.promote) store.add(entryFrom(data));
  else store.remove(data.id);   // un-check / now-ineligible retracts the entry
}
```

### Sticky bar (`features/lists/promote/PromoteBar.tsx`)

- Rendered on the list detail page, **sticky between the page header and the list**, dark theme.
- Subscribes to the store filtered by the current `listId`; hidden when count is 0.
- Content: *"N items ready for inventory"* + **Review** button → opens the review sheet.
- All copy via i18n keys; testids for IT.

### Review sheet (`features/lists/promote/PromoteReviewSheet.tsx`)

MUI bottom sheet / drawer. **Option B** stacked card rows.

- **Header:** title + subtitle ("Select items & a target; add the rest to another inventory after").
- **Inventory picker:** rendered only if `inventories.length > 1`; default = `inventories[0]` (newest, per existing `GetInventories` ordering). Single inventory → no picker, target implicit.
- **Rows (one card per batch entry for this list):**
  - **Selection checkbox**, selected by default (common case = everything to one inventory in one tap; deselect to split a mixed batch across targets).
  - Name (read-only).
  - **Provenance tag:** `AiRecommendsShelfLife` → **"Recommended"**; `UserEntersFromPackage` → **"Enter date"**. (i18n keys; not "AI".)
  - Editable **quantity** field, pre-filled by formatting the entry's `QuantityDto` → string with the list's existing quantity formatter.
  - Editable **expiry date** field: pre-filled for Recommended; blank ("tap to set") for Enter-date. See **Recommendation display** below for the readable hint shown alongside.
  - A per-row **omit** control (`✕`) that **removes the item from the batch entirely** — "I don't want this in inventory at all." Distinct from deselect (which keeps it for another pass). Omit is not a permanent suppression list: if the same name is checked off again later it re-enters the batch.

Two distinct "exclude" gestures, by design:
| Gesture | Meaning | Batch effect |
| --- | --- | --- |
| Deselect checkbox | "not this inventory / this pass" | stays in batch |
| Omit (`✕`) | "not going into inventory" | removed from batch |

- **Footer:** **"Add N to <Inventory>"** (N = selected count) as the primary action, plus a low-emphasis **"Clear all"** that empties the whole batch (gives up on every pending item, hides the bar).
- **Close behavior:** closing the sheet (backdrop / handle) **keeps the batch** — the sticky bar stays so the user can return. Only Add (removes the added items) and Clear all / per-row omit shrink the batch.
- **Add flow (split-target capable):** on Add, loop `useCreateInventoryItem.mutateAsync({ path: { householdId, inventoryId }, body: { Text, Quantity, ExpiryDate } })` over the *selected* rows; on each success `store.remove(itemId)`. The sheet stays open showing the remaining (unselected) rows so the user can switch the picker to another inventory and add those. Sheet closes when the batch for this list is empty.
- **No new generated API surface** beyond the `Promote` field.

### Recommendation display (readable expiry)

For a **Recommended** row the absolute date alone ("Jun 14, 2026") doesn't convey the shelf-life intent. Alongside the editable date field, show the human-readable hint from the existing inventory helper `getExpiryInfo(expiryDate, t)` (`src/utils/dateUtils.ts`) — the *same* logic the inventory list uses, so wording/colors stay consistent ("expires in 5 days", warning/info color, etc.). It returns `""` beyond 30 days, in which case only the absolute date shows. The hint is a pure function of the field value, so it **updates live as the user edits the date**. Enter-date rows show no hint until the user picks a date.

### Quantity formatting

Inventory `Quantity` is free-text; reuse the existing list-item `QuantityDto` → display-string formatter so "2 L", "4 pcs", etc. carry across. Editable, so the user can adjust before adding.

## Edge cases & races

- **Not-yet-classified at check-off:** `Promote == null`, item simply doesn't enter the batch this time. Re-triggered on next reference (cheap once cached). This is the accepted `List ↔ Product` eventual consistency.
- **Un-check after check-off:** retracts the pending entry (`store.remove`).
- **Omit vs deselect:** omit (`✕`) removes an item from the batch (not re-shown unless its name is checked off again); deselect only excludes it from the current add pass and it remains in the batch.
- **Item edited/text changed:** classification re-points by normalized name; out of scope for this cycle (entry already captured uses the name at check-off time).
- **Refresh mid-shop:** store rehydrates from localStorage; bar/sheet restored.
- **Stale localStorage entries:** entries persist until added/dismissed/un-checked. Acceptable for MVP (device-scoped, small). A TTL/cleanup is a deferred nicety, noted not built.
- **Inventory deleted while a batch targets it:** the picker lists only current active inventories; `CreateInventoryItem` 404s defensively if the target vanished mid-flow — surface a generic error, leave the entry in the batch.

## Testing

- **Backend unit (`Frigorino.Test`):** `ToggleItemStatus` attaches `Promote` when toggling a perishable item to done (Recommended → date set; UserEntersFromPackage → date null); returns `null` for un-check, NonPerishable, Unknown, and unclassified. `ExpiryProfile.SuggestedExpiry` is already covered.
- **Integration (Reqnroll, `Frigorino.IntegrationTests`):**
  1. Happy path: perishable item, classified → check off → sticky bar appears → review → item lands in inventory with the expiry.
  2. Non-perishable: checked off → no bar.
  3. Classification pending: checked off → no bar (documented eventual consistency).
  4. Omit: perishable item in the sheet → omit (`✕`) → not added, removed from the batch (bar count drops).
  - Assert on testids / `data-*` attributes, never translated text.
- No JS test runner exists; frontend correctness is covered via Reqnroll + Playwright.

## Out of scope (deferred)

- Per-item (non-batch) promote modal.
- `User.LastPromotedInventoryId` preference (picker defaults to newest instead).
- Cross-list batching (bar/store scoped to the current list for MVP).
- localStorage TTL / cleanup job.
- Learning from per-instance edits back into product overrides.
- Storage-location / category-driven inventory target selection.

## Impact

- Backend: 1 modified slice (`ToggleItemStatus`) + 1 modified response record. No migration, no new endpoint, no new package.
- Frontend: 1 persisted Zustand store, 1 sticky bar, 1 review sheet, 1 modified toggle hook. Reuses `useCreateInventoryItem`, `GetInventories`, the quantity formatter. `npm run api` regenerates the `Promote` field on `ListItemResponse`.

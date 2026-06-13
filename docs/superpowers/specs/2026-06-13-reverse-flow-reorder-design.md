# Reverse flow: inventory item → add to shopping list (re-order) design

**Date:** 2026-06-13
**Status:** Approved design, ready for implementation plan
**Tracking entry:** `IDEAS.md` → "Reverse flow: inventory item → add to shopping list (re-order)".
**Mirror of:** `2026-05-31-promote-to-inventory-ux-design.md` (promote: list → inventory). This is the smaller, single-item inverse: inventory → list.

## Why

Promote closes the **list → inventory** direction (shop → check off → lands in inventory). The natural mirror is **inventory → list**: an item is used up (or about to be) and the user wants a fresh order back on a shopping list. Today that's fully manual — remember the item, open the right list, retype its name + quantity. The data to pre-fill it ("Milk, 2 L") already lives on the inventory item (structured `Quantity`, mirroring `ListItem` since commit `4cc1ec0`), so the re-order should be one tap, not a retype.

## Key decisions (from brainstorm)

1. **No trigger, no lifecycle coupling.** Inventory items have *no* checked/consumed lifecycle — only an expiry date — and the user's real workflow is to delete an item when it's used up. This feature does **not** introduce a consume action and does **not** touch the inventory item at all (no consume, no delete, no flag). It is a standalone "re-order this" action. The user still deletes the item themselves when it's actually gone. (Supersedes the IDEAS entry's "finished it" trigger framing, which assumed a check-off concept that does not exist.)
2. **Single item, not batch.** The inverse of promote's batch review is a single-item action: re-order one inventory item at a time. Batch is explicitly out of scope (noted asymmetry vs promote).
3. **Entry point = the existing per-item kebab menu.** Every row already renders a "⋮" menu (`SortableListItem`, shared by lists and inventory) with Edit + Delete. Add an **"Add to list"** entry, surfaced **only for inventory rows** — no new affordance on the row.
4. **Reuse the promote sheet UI, simplified.** A bottom `Drawer` like `PromoteReviewSheet`, reduced to a single item: editable name, the shared quantity-draft editor, and a target-list picker. No expiry field (list items have none), no per-row checkboxes / select-all (single item).
5. **Quantity crosses over structured, not via text.** The inventory item's `Quantity` is written directly onto the new list item — the exact mechanism promote uses (it passes a structured `Quantity` into `inventory.AddItem`). No round-trip through text + LLM extraction.
6. **Target list = picker defaulting to newest list.** `GetLists` returns newest-first (`OrderByDescending(CreatedAt)`); default to `lists[0]`. Picker hidden when the household has only one list. No `User` preference column, no migration (mirrors promote's "newest inventory" default). A persisted "default list" preference is a future User-settings concern.
7. **Reuse the existing `CreateItem` slice + `useCreateListItem` hook** — no new write endpoint. The only backend change is one optional field on the existing request.

## Backend

No new endpoint, no migration. One optional field on `CreateItemRequest` and a branch in the handler.

### `CreateItem` slice (`Frigorino.Features/Lists/Items/CreateItem.cs`)

`CreateItemRequest` gains an optional structured quantity:

```csharp
public sealed record CreateItemRequest(string Text, string? Comment, QuantityDto? Quantity = null);
```

`QuantityDto` is the existing DTO already used by promote (`Frigorino.Features/Quantities`). It serializes the same on the wire.

Handler branches on whether a quantity was supplied:

- **`Quantity` provided** (re-order path):
  - `Quantity.Create(request.Quantity.Value, request.Quantity.Unit)` → on failure return `parsed.ToValidationProblem()` (same pattern as `PromoteListItems`).
  - Do **not** run `ItemTextRouter.Analyze` / extraction. Call `list.AddItem(request.Text.Trim(), quantity, request.Comment)` directly (`AddItem(string, Quantity?, string?)` already accepts the quantity — today's path just passes `null`).
  - Response: `ListItemResponse.From(...) with { ExtractionPending = false }`. Do **not** call `quantityTrigger.OnItemRouted` — there is nothing to extract.
- **`Quantity` null** (today's hand-typed path): unchanged. `ItemTextRouter.Analyze(text)`, `AddItem(analysis.CleanName, null, comment)`, async extraction enqueued via `quantityTrigger.OnItemRouted` exactly as now.

The `RankRetry.SaveWithRetryAsync` wrapper, membership check, and 404/validation handling are unchanged — the quantity branch lives inside the same save closure.

### Notes / constraints

- No change to `List.AddItem` (already takes an optional `Quantity`).
- No change to the inventory side — this feature never reads/writes inventory state beyond reading the source item's name + quantity client-side.
- Generated TS client picks up the optional `quantity` field on `npm run api`.

## Frontend

### Entry point — `SortableListItem` (`src/components/sortables/SortableListItem.tsx`)

Add an optional menu hook so the shared component stays list-agnostic:

- New optional prop, e.g. `extraMenuItems?: (item: T) => React.ReactNode` (or a typed `onAddToList?: (item: T) => void`), rendered above Delete in the existing `Menu`.
- `SortableList` forwards it through to each `SortableListItem`.
- **Inventory** `InventoryContainer` wires it to open the re-order sheet for the tapped item; the **lists** container leaves it undefined, so list rows are visually and behaviorally unchanged.
- Menu item: "playlist add" / add-to-cart icon + `t("reorder.addToList")`, `data-testid="add-to-list-button"`. Rendered only when the household has ≥1 list (hidden otherwise).

### Re-order sheet — new `src/features/inventories/reorder/ReorderSheet.tsx`

A bottom `Drawer` modeled on `PromoteReviewSheet`, for a single item:

- **Props:** `{ open, onClose, householdId, item: InventoryItemResponse }`.
- **Name:** editable `TextField`, prefilled from `item.text`.
- **Quantity:** the shared composer quantity-draft editor (value `TextField` + unit `Select`), prefilled via `quantityToDraft(item.quantity ?? null)`. Reuse — factor the value/unit editor out of `PromoteReviewSheet`'s `PromoteRow` into a small shared component (e.g. `QuantityDraftFields`) rather than copy-pasting; promote consumes the same component.
- **Target list:** `useHouseholdLists(householdId)` dropdown. Effective target `= picked ?? lists[0]?.id`. Picker hidden when `lists.length <= 1`. `data-testid="reorder-list-picker"`.
- **Confirm:** disabled while name empty, no target, quantity draft invalid (`isDraftValid`), or mutation pending. On click: `useCreateListItem` → `mutateAsync({ path: { householdId, listId: targetId }, body: { text: name.trim(), quantity: draftToQuantity(draft) } })`. On success: `toast.success(t("reorder.added", { name, list: targetName }))`, then `onClose()`. On failure: leave the sheet open for retry (mirror promote's `catch {}`).
- Inventory item is never mutated.

### Hooks

- `useCreateListItem` (`src/features/lists/items/useCreateListItem.ts`) already exists — reuse; no new query/mutation hook beyond what lists already expose.
- `useHouseholdLists` already exists — reuse.

## Testing

- **Backend unit (`Frigorino.Test`)** — `CreateItem` with a structured `Quantity`: item is created with that quantity, no extraction enqueued (`ExtractionPending == false`); invalid quantity → `ValidationProblem`; existing null-quantity path still routes through extraction.
- **Integration (`Frigorino.IntegrationTests`, Reqnroll + Playwright)** — open inventory, "⋮" → Add to list on an item, (pick list if >1), confirm, assert the new list item appears on the target list with the carried quantity, and the inventory item is unchanged. Assert on testids / `data-*`, never translated text.
- No JS test runner exists; rely on `npm run tsc` + lint + manual browser verify for the sheet.

## Out of scope (v1)

- Low-stock / par-level trigger (needs a threshold model first — separate IDEAS entry).
- Any auto-decrement or auto-delete of inventory stock on re-order (kept a separate concern).
- Cross-item / batch re-order (single-item only; asymmetry vs promote noted).
- Classifier-driven list suggestion (suggest *which* list by category).
- Persisted "default list" user preference (default stays "newest"; future User-settings work).

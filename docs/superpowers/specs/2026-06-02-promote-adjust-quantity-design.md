# Promote-to-inventory: editable quantity

**Date:** 2026-06-02
**Status:** Approved
**Scope:** Frontend-only (`Frigorino.Web/ClientApp`)

## Problem

Client feedback on the promote-to-inventory feature: when reviewing items to add to
inventory, the quantity is shown read-only (carried straight from the source list item).
But the real-world quantity often differs from what was on the list — the store had fewer
items, or you bought more than requested. Without the ability to adjust it during promote,
the inventory drifts from reality and is hard to keep correct.

## Solution

Make the per-row quantity editable in the promote review sheet
(`features/lists/promote/PromoteReviewSheet.tsx`), reusing the existing quantity-editing
primitives. No API, DTO, or DB change — `CreateInventoryItem` already accepts a `quantity`.

### Changes

1. **Row draft gains a quantity field.** `RowDraft` becomes
   `{ selected, expiry, quantity: QuantityDraft }`. Seeded with the existing
   `quantityToDraft(entry.quantity)` helper. For a quantity-less item this yields
   `EMPTY_QUANTITY_DRAFT`, which `draftToQuantity` converts back to `null` — so those
   items behave exactly as before.

2. **Inline editor, only for items that already have a quantity.** The read-only
   quantity `<Chip>` is replaced by an inline value `TextField` + unit-select `TextField`
   pair (same controls as the composer's `QuantityPanel`: `inputMode="decimal"`,
   comma→dot tolerance, `QUANTITY_UNIT_VALUES` for the unit dropdown). The block renders
   only when `entry.quantity` is truthy; quantity-less rows show no quantity field. The
   now-unused `formatQuantity` import is removed.

3. **Send the edited quantity.** `handleAdd` sends `draftToQuantity(draft.quantity)`
   instead of `entry.quantity`.

4. **Validation mirrors the composer.** Reuse `isDraftValid`. A new
   `hasRowInvalidQuantity` guard (alongside `hasRowMissingDate`) disables the **Add**
   button when any *selected* row holds a non-empty, unparseable/non-positive quantity.
   Empty stays valid (= clears the quantity), matching composer semantics.

## Testing

No JS test runner exists and the change is frontend-only; per client direction no
automated test is added for this case. Verification is manual via the dev stack /
Playwright: adjust a row's quantity up and down and confirm the created inventory item
reflects the edited value. The existing `Promote.feature` IT is unaffected (it does not
assert quantity values).

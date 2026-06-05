# Expiry calendar — inline item edit

**Date:** 2026-06-05
**Status:** Design approved, ready for implementation plan.
**Builds on:** `2026-06-04-inventory-calendar-spike-learnings.md` (resolves that doc's open question #3, "what a click does": the answer is **select → edit in place**).

## Goal

Let the user edit an inventory item directly from the expiry calendar, without leaving the calendar view. Mobile-first: the trigger and edit surface must be thumb-friendly on a phone, where calendar bars are thin (compact bars are a single day wide).

No backend changes. The calendar item response (`ExpiryCalendarItemResponse`) already carries everything the edit needs: `id`, `inventoryId`, `inventoryName`, `text`, `quantity`, `expiryDate`. Editing reuses the existing `Composer` and `useUpdateInventoryItem`.

## Interaction model

Tapping a bar selects it and raises a **minimal action bar**; committing to Edit **expands** that surface into the composer. The bar itself is never the edit target twice — a precise re-tap on a thin bar is the mobile failure mode we are avoiding.

### States

| From state | Action | Result |
| --- | --- | --- |
| Plain calendar | Tap any bar (compact **or** wide) | **Selected** — bar highlighted (others dim), slim action bar slides up from the bottom |
| Selected | Tap **Edit** | **Editing** — the surface expands into a bottom sheet hosting the composer, seeded from the item's current values |
| Editing | **Save** | Commits the update, then **collapses back to the minimal action bar; the item stays selected/highlighted** |
| Editing | **Cancel** | Collapses back to the minimal action bar, item stays selected/highlighted, no commit |
| Selected | Tap empty grid space, or tap the selected bar again | **Plain calendar** — selection cleared |

Save deliberately keeps the user in context: if they changed the expiry, the bar visibly moves/relabels while remaining selected, confirming the change landed. Selection is only dismissed by tapping away.

### Action bar contents

Minimal: **item name · expiry date · Edit button**. Nothing else for now. The name + date are what a compact bar can't render inline, so surfacing them here removes the need for a separate details view.

## What changes

This **replaces** the current dual path. Today:
- Compact (<4-day) bars open `CalendarItemDetailsSheet` on tap.
- Wide bars only toggle the focus-select highlight.

After this change, **every** bar goes through one path: tap → highlight + action bar → optional Edit. The existing `CalendarItemDetailsSheet` component is **removed** (its informational role is absorbed by the action bar). The `compact` distinction in `expiryCalendarEvents.ts` still governs *inline bar rendering* (name-only vs. full label) — that stays — but no longer governs the *tap behavior*.

## Components & data

- **`CalendarItemActionBar`** (new component, in `features/inventories/calendar/components/`). A bottom-anchored surface (MUI `Drawer anchor="bottom"` or `Paper`, matching the app's other bottom sheets — rounded top corners). One component with an `editing: boolean`:
  - `editing === false`: slim row — name, expiry date, Edit button.
  - `editing === true`: expanded — hosts the `Composer`.
  Keeping it one component (rather than two stacked drawers) makes the expand read as a single surface growing.
- **Composer reuse.** Same `Composer` with `features = [quantityComposerFeature, expiryFeature]` as `InventoryFooter`. Seed `initialDraft` from the selected item's `text`, `quantityToDraft(quantity)`, and `expiryDate`. On complete, map back with `draftToQuantity` and call the update with `{ text, quantity, clearQuantity: quantity === null, expiryDate: expiry ?? null }` — identical to `InventoryViewPage.handleUpdateItem`.
- **Selection state.** The page already holds `selectedId: number | null`. The full selected item (including `quantity`, which is **not** in `ExpiryEventProps`) is looked up from the `items` array that `useExpiryCalendar` already returns — keyed by `id`. No change to `ExpiryEventProps` or the event builder.
- **Edit mode flag.** A page-level `editing: boolean` (or deriving it from a separate `editingId`) drives the action bar's expanded state. Save sets `editing = false` but leaves `selectedId` set.

## Save & data refresh

`useUpdateInventoryItem` is reused as-is. On success it invalidates the **inventory-items** query key (`getInventoryItemsQueryKey`) — so navigating to the inventory page afterward refetches fresh data, no stale rows. This is the existing behavior; we get it for free by reusing the hook.

The hook does **not** know about the calendar, so the calendar's save handler additionally invalidates the **expiry-calendar** query key (`getExpiryCalendarQueryKey({ path: { householdId } })`) after `mutateAsync` resolves, so the bar updates immediately (e.g. an expiry change moves the bar). This calendar-specific invalidation stays **local to the calendar page** — it is not baked into the shared hook.

## i18n

All user-facing strings via `t()`. The action bar's Edit/Save/Cancel reuse existing `common.*` keys where they exist (`common.edit`, `common.save`, `common.cancel`). New keys only if needed, added to both `en` and `de` translation files.

## Testing

One integration test (Reqnroll + Playwright), asserting on **testids / `data-*` only** (never translated text):
1. Open the calendar with a seeded item that has an expiry.
2. Tap the item's bar → assert the action bar appears with the item selected.
3. Tap Edit → assert the composer is shown.
4. Change the expiry date, Save.
5. Assert the action bar collapsed back to minimal, the item is still selected, and the bar reflects the new expiry date (via the date stamp testid).

Testids to add: the action bar container, its Edit button, and an edit-mode marker, alongside the existing `cal-event-*` / `data-selected` hooks.

## Scope

**In scope:** `CalendarItemActionBar`, edit-via-composer, the expiry-calendar invalidation on save, i18n, the integration test, and removal of `CalendarItemDetailsSheet`.

**Out of scope (built to grow into, not added now):** delete, "mark cooked", add-to-shopping-list. The action bar is structured so these become additional buttons later without reworking the interaction model.

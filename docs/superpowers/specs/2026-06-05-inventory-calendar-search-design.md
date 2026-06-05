# In-view text search for Inventory & Expiry Calendar

**Date:** 2026-06-05
**Status:** Design approved, pending implementation plan

## Summary

Add a client-side text filter to two views — the inventory item list
(`InventoryViewPage`) and the expiry calendar (`ExpiryCalendarPage`). The filter
narrows what's on screen by hiding items whose text doesn't match. No backend
changes and no new endpoint: both views already fetch all of their items in a
single request, so filtering happens against the in-memory array.

This is deliberately **not** a global/full-app search. That direction was
considered and parked; the chosen scope is an in-view "narrow what I'm looking
at" filter.

## Scope

In scope:
- Inventory item list view — filter visible items by text.
- Expiry calendar view — filter visible calendar events by text.

Out of scope (explicitly):
- Global/cross-domain search (lists + inventories from one entry point).
- Backend query params, pagination, or server-side search.
- Searching list items, inventory/list names, or any field other than the
  inventory item `text`.

## UX

### Entry point
- A **search icon** in each page's header / action bar. Tapping it expands a
  text input; clearing or collapsing it removes the filter.
- Both views use the same entry-point treatment for consistency.

### Match semantics
- **Case-insensitive substring** match on `InventoryItemResponse.text`
  (calendar: `ExpiryCalendarItemResponse.text`).
- Non-matching items are **hidden** (not highlighted in place).
- Empty query = no filter (all items shown).

### Filter lifetime
- The filter is **ephemeral**: it resets when the user leaves the view. It is
  **not** persisted to localStorage (unlike inventory sort mode). Rationale: a
  filter is a transient "find this now" action; persisting it would silently
  hide items on the next visit.

### Empty state
- When the active filter matches nothing, show a simple "no items match"
  message rather than a blank list. (Rendered via `t()`; tests assert on
  testid/`data-*`, never the translated string.)

## Inventory page specifics

- Filtering composes with the existing sort modes (`custom`,
  `expiryDateAsc`, `expiryDateDesc`): filter first, then the existing sort
  applies to the survivors.
- **Drag-to-reorder is disabled while the filter is active** — handles are
  inert/hidden when the search box is non-empty, and re-enabled when the search
  is cleared. This avoids writing confusing `SortOrder` values for a partial
  list.

## Calendar page specifics

- The text filter **composes (AND)** with the existing expiry-level toggles
  (`CalendarLevelToggles`): an event renders only if it passes both the active
  level toggle and the text match.
- Non-matching events are simply not rendered on the calendar.

## Implementation shape

- **Shared component:** a small `SearchToggle` (icon → expanding input) reused
  by both views, so the two don't diverge. Owns its open/closed state and emits
  the current query string upward.
- **Inventory:** filter state + predicate live in `InventoryViewPage`. The
  items array is filtered before being handed to `InventoryContainer` /
  `SortableList`. The "disable drag" signal is derived from
  `query.length > 0`.
- **Calendar:** filter state lives in `ExpiryCalendarPage`. Items are filtered
  before FullCalendar events are built.
- The match predicate is a pure helper (e.g. `text.toLowerCase().includes(
  query.trim().toLowerCase())`) so it can be unit-tested independently and
  shared by both views.

### Key existing files

| Concern | File |
|---|---|
| Inventory page (filter host) | `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryViewPage.tsx` |
| Inventory list/drag | `.../features/inventories/items/components/InventoryContainer.tsx`, `.../components/sortables/SortableList.tsx` |
| Calendar page (filter host) | `.../features/inventories/calendar/pages/ExpiryCalendarPage.tsx` |
| Calendar level toggles | `.../features/inventories/calendar/components/CalendarLevelToggles.tsx` |
| Item text field (DTO) | `Application/Frigorino.Features/Inventories/Items/InventoryItemResponse.cs` (`Text`) |
| Calendar DTO | `Application/Frigorino.Features/Inventories/ExpiryCalendarItemResponse.cs` (`Text`) |

## Testing

- **No frontend JS test runner exists** (per CLAUDE.md), so the match predicate
  is verified through the integration tests below rather than a standalone unit
  test. It still lives as a small pure helper for clarity and reuse.
- **Integration (Reqnroll + Playwright):**
  - Inventory: open search, type a query, assert non-matching item testids
    disappear and matching ones remain; assert the drag handle is
    disabled/inert while the filter is active and active again once cleared.
  - Calendar: open search, type a query, assert non-matching events are gone and
    that the filter ANDs correctly with a level toggle.
  - All assertions target testids / `data-*` attributes, never translated text.

## Decisions log

- **Client-side, not server-side** — both views fetch all items already; instant
  filtering, no endpoint, no debounce/network.
- **Toggle-via-icon, not always-visible bar** — mobile-first PWA, preserve
  vertical space.
- **Hide non-matches, not highlight** — "narrow the view" intent.
- **Disable drag while filtering** — avoids partial-list `SortOrder` corruption.
- **Ephemeral filter** — resets on leaving the view.
- **Shared `SearchToggle` component** — avoid two divergent implementations.

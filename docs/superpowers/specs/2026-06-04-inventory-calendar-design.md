# Inventory expiry calendar — design

**Date:** 2026-06-04
**Status:** Design approved in brainstorm; ready for an implementation plan.
**Branch:** `feat/inventory-calendar` (worktree off `stage`).
**Builds on:** [`2026-06-04-inventory-calendar-spike-learnings.md`](2026-06-04-inventory-calendar-spike-learnings.md) — the spike that decided **FullCalendar** and validated the bar-rendering patterns. The spike code is gone by design; implement fresh from documented intent.

## Goal

A household-wide month calendar where each inventory item with an expiry renders as a multi-day **cook-by bar** spanning `[expiry − X … expiry]`, so overlapping windows pop out for "what do I need to use up this week." Mobile PWA target.

This is a **foundation pass** — get the calendar wired end-to-end (read → page → bars → focus-select) with production mechanics in place. Richer per-item UX (detail, edit, quick actions, re-order) is deliberately deferred to follow-up work once the setup exists.

## Decisions (from the focused brainstorm, 2026-06-04)

1. **Scope: household-wide.** One calendar across **all** inventories in the active household — the framing that matches meal planning ("you don't think per-fridge"). Bars carry a small inventory-name cue since items from different inventories are mixed.
2. **Window length `X`: fixed, configurable client constant.** A new `CALENDAR_WINDOW_DAYS` constant in `dateUtils.ts`, **decoupled** from `EXPIRY_THRESHOLDS` so it can be tuned independently. **No domain change** — `X` lives entirely on the client.
   - Note: `X` is *not* `ExpiryProfile.ShelfLifeDays`. `ShelfLifeDays` is lifespan-from-purchase (used at promote-time to suggest an expiry date); `X` is the cook-by planning tail before expiry. Different numbers, different concepts.
3. **Click: single-select focus.** Tapping a bar highlights that one item (focus ring on all its week-row segments) and dims the rest, so a cramped month view stays readable. Tapping it again or tapping empty space clears. **Single-select only** — no detail dialog, no edit navigation, no quick actions in v1.

## Architecture

### Backend — one new read slice

`Frigorino.Features/Inventories/GetInventoryCalendar.cs` (+ `InventoryCalendarItemResponse` DTO).

- **Route:** `GET /api/household/{householdId}/inventories/calendar`, registered on the existing `inventories` group in `Program.cs`. The `calendar` literal does not collide with the existing `{inventoryId:int}` routes (int route constraint).
- **Guard:** standard `db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct)` → `404` if not a member, mirroring `GetInventories` / `GetInventoryItems`.
- **Query:** EF projection over `InventoryItems` joined to active `Inventories` in the household, filtered to `IsActive && ExpiryDate != null`. One round-trip.
- **Response DTO** (`InventoryCalendarItemResponse`): `{ id, inventoryId, inventoryName, text, quantity (QuantityDto?), expiryDate (DateOnly) }`. `inventoryName` gives the per-bar cue for free; `expiryDate` is non-null by construction (items without expiry are filtered out).
- **No domain change.** The window `X` is applied on the client.

Why a dedicated slice (vs client-side aggregation of `GetInventories` + per-inventory `GetInventoryItems`): one query instead of N+1, no pulling non-expiring rows we'd discard, no duplicated filtering in TS — and it matches the vertical-slice conventions. Client regenerated via `npm run api`.

### Frontend — route, hook, page

- **Route:** `src/routes/inventories/calendar.tsx` — thin shell (`createFileRoute` + `requireAuth`), imports the page from `features/`. (`routeTree.gen.ts` regenerates automatically.)
- **Query hook:** `features/inventories/calendar/useInventoryCalendar.ts` — spreads the generated `getInventoryCalendarOptions({ path: { householdId } })` into `useQuery`, `enabled` guarded on `householdId > 0`, with a `staleTime`. No hand-written `queryFn`/`queryKey`.
- **Page:** `features/inventories/calendar/pages/InventoryCalendarPage.tsx` — `pageContainerSx` Container, owns `selectedId` React state, renders the FullCalendar.
- **Entry point:** a "Calendar" button in the Inventories index page (`/inventories`) header → navigates to `/inventories/calendar`.

### Calendar rendering (from validated spike patterns)

- `@fullcalendar/react` + `@fullcalendar/daygrid`, `initialView="dayGridMonth"`, `dayMaxEvents={false}` + `dayMaxEventRows={false}` so week rows grow to show **every** overlapping bar (the deciding factor over Schedule-X).
- **Event mapping:** each item → one all-day event, `start = expiry − X`, `end = expiry + 1` (FullCalendar all-day `end` is **exclusive**, so `+1` covers the expiry day). `X = CALENDAR_WINDOW_DAYS`. Parse dates with `parseLocalDate` (never `new Date("YYYY-MM-DD")` — UTC day shift).
- **Color:** existing `getExpiryLevel` bands (expired/critical → red, soon → amber, fresh → green), mapped to raw CSS hex (FullCalendar needs hex, not an MUI palette path).
- **`eventContent`:** stamp the expiry date on the bar tail (`isEnd`) + a `↩` continuation marker on wrapped segments (`!isStart`) so a long Mon→Sun span is identifiable on any week-row it touches. Label includes the inventory-name cue.
- **Vertical separation:** round bars into pills with a small `margin-bottom` on `.fc-daygrid-event-harness` so overlaps read as distinct rows.
- **Theming:** FullCalendar v6 auto-injects CSS; bridge its `--fc-*` variables to the MUI theme and style the toolbar chrome (`prev`/`next`/`today`) toward MUI — a small but real production task.

### Interaction — focus-select

- `eventClick` toggles a single `selectedId` in React state.
- `eventClassNames` applies a focus ring to the selected item's segments and a dim class to all others. Tapping the selected bar again, or tapping empty space, clears `selectedId`.
- Single-select only.

## Production mechanics (in scope)

- **i18n:** all copy via `t()`; translation keys added under `public/locales/{en,de}/translation.json`. No hardcoded English (the spike's English strings do not carry over).
- **Testids:** stable testids / `data-*` attributes on the page and bars for the integration test.
- **Integration test:** one Reqnroll + Playwright scenario driving the calendar (navigate from inventories → calendar, assert bars render, assert focus-select toggles a `data-*`/class). Assert on testids/`data-*`, **never** translated text. Requires `npm run build` so the SPA build picks up new testids.
- **MUI theming pass** for FullCalendar chrome (above).

## Out of scope (v1)

- Per-category or per-item window `X` (per-category would be a new domain concept; cook-by lead time doesn't obviously vary by `ProductCategory`).
- Detail dialog, navigate-to-edit, or quick actions on tap.
- The reverse re-order flow (inventory item → shopping list).
- Multi-select.
- A per-inventory calendar or inventory filter (household-wide only for now).

## Verification

- `dotnet test Application/Frigorino.sln` (unit + integration).
- Frontend: `npm run lint`, `npm run tsc`, `npm run prettier`.
- `docker build -f Application/Dockerfile -t frigorino .` as the final gate.
- Manual browser verify at a 390×844 phone viewport (the spike's date-parse / runtime-only class of bug passes tsc + lint and only surfaces in the browser).

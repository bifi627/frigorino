# Inventory expiry calendar — spike learnings

**Date:** 2026-06-04
**Status:** Throwaway spike complete. Library + approach decided (**FullCalendar**). Feature **not** built — open product questions remain (see end). This doc is the durable reference; the spike code is disposable and not merged.

## Goal

A client asked for a calendar view of an inventory showing each item's expiry. Refined during exploration into: render each item as a **multi-day "cook-by" bar** spanning `[expiry − X … expiry]` on a month grid, so **overlapping windows are visible at a glance** for cooking planning. Target is the mobile PWA.

## Decision: FullCalendar (`@fullcalendar/react` + `@fullcalendar/daygrid`)

Chosen over Schedule-X after building **both** side-by-side against real seeded data. The deciding factor was the client's own reference UX (a phone month-grid with spanning bars): FullCalendar reproduces it directly; Schedule-X does not without fighting its defaults.

| Dimension | FullCalendar | Schedule-X |
| --- | --- | --- |
| Show **all** overlapping bars (growing rows) | ✅ `dayMaxEvents={false}` + `dayMaxEventRows={false}` grows the week row to fit every bar | ❌ caps per cell, shows "+N event" overflow; `monthGridOptions.nEventsPerDay` only raises a **fixed** cap, not auto-grow |
| Dark theming | ⚠️ manual CSS-variable bridge; toolbar chrome still off-MUI | ✅ dark-native via `isDark: true`, zero CSS |
| Per-event custom render | ✅ `eventContent` → JSX; `isStart`/`isEnd` segment flags | ✅ `customComponents.monthGridEvent` → JSX; `hasStartDate` flag |
| Click / interactivity | ✅ `eventClick`, `eventMouseEnter`, `dateClick`; drag/resize via `@fullcalendar/interaction` | ✅ event modal plugin + click handlers |
| Setup friction | ✅ plug-and-play (4 packages) | ❌ Temporal peer-dep gotcha (see below); 9 packages |
| Bundle | 4 packages | 9 packages (incl. `temporal-polyfill`); prod gz delta not measured |

**Net:** for "see every overlapping window at once" on mobile, FullCalendar fits the requirement; Schedule-X's month grid hides overlap behind overflow links. Schedule-X wins on dark-native polish and the `hasStartDate` ergonomics, but that didn't outweigh the core requirement.

## What was validated

- **Data mapping.** `InventoryItem.expiryDate` is a nullable `DateOnly`, delivered over the wire as an ISO `"YYYY-MM-DD"` string. Map each item with an expiry to one event: `start = expiry − X`, `end = expiry + 1` (FullCalendar treats an all-day `end` as **exclusive**, so +1 makes the bar cover the expiry day). Items with no expiry are simply omitted. Parse dates in **local** time (`parseLocalDate` in `utils/dateUtils.ts`) — `new Date("YYYY-MM-DD")` is UTC and shifts the day in non-UTC zones.
- **Urgency color.** Reused the app's existing expiry bands (`getExpiryLevel` / `EXPIRY_THRESHOLDS` in `dateUtils.ts`): expired/critical → red, soon → amber, fresh → green. FullCalendar needs a raw CSS hex, not an MUI palette path.
- **Readability of long Mon→Sun spans** (a real client concern). Solved with `eventContent`: stamp the **expiry date on the bar's tail** (`isEnd`) and a `↩` continuation marker on wrapped segments (`!isStart`). A bar is then identifiable on any week-row it touches.
- **Highlighting items.** Mark items whose cook-by window is **active today** with 🔥 + bold + a white inset edge (via `eventClassNames`). The predicate is one line — trivially repointable (starred, low-stock, selected…).
- **Vertical separation.** Bars bled together by default; rounding them into pills with a small `margin-bottom` on `.fc-daygrid-event-harness` made overlaps read as distinct rows.
- **Interactivity.** `eventClick` → React `selectedId` state drives both a `cal-selected` ring and a MUI detail `<Dialog>`; closing clears selection. Confirmed the click target can equally navigate to edit / fire a quick action instead of opening a dialog.
- **Mobile.** Verified at a 390×844 phone viewport via Playwright. The growing-rows + vertical-scroll + truncating-labels layout holds up — the spanning-bar month view is mobile-viable, matching the client's reference.

## Gotchas worth remembering

- **Schedule-X needs the GLOBAL Temporal.** `temporal-polyfill` is a **peer** dependency; Schedule-X validates event dates against `globalThis.Temporal`. Creating dates from a named `import { Temporal } from "temporal-polyfill"` produces instances from a *duplicate* module copy → runtime error *"Event start time needs to be a Temporal.ZonedDateTime or Temporal.PlainDate."* Fix: `import "temporal-polyfill/global"` and build dates from the global, plus Vite `resolve.dedupe: ["temporal-polyfill"]` + `optimizeDeps.include`. Only relevant if Schedule-X is ever reconsidered. **This class of bug passes tsc + lint and only surfaces in the browser** — manual verify caught it.
- **FullCalendar `end` is exclusive** for all-day events (the `+1` above).
- **FullCalendar v6 auto-injects its CSS** — no manual stylesheet import; dark theming is done by overriding its `--fc-*` CSS variables.
- **FullCalendar's toolbar chrome** (`prev`/`next`/`today` buttons) is not MUI-styled out of the box — a real (small) theming task for production.

## Open questions (deferred — for the focused brainstorm before implementing)

1. **Window length `X`.** The spike hardcoded 3 days. Real options: a fixed global, **per-category via the existing `ExpiryProfile` / `ProductCategory`** domain concepts, or per-item. This is the one decision that touches the domain — the calendar itself is agnostic.
2. **Entry point & scope.** The spike used a per-inventory route (`/inventories/$id/calendar`) reached from a header button. Alternatives: a tab on the inventory view, or a **household-wide** calendar spanning all inventories (arguably the more useful "what do I cook this week" view).
3. **What a click does.** Detail dialog (spike) vs navigate-to-edit vs quick actions (mark cooked / re-order). Relates to [[Reverse flow: inventory item → add to shopping list (re-order)]]. **Resolved (2026-06-05):** select → edit in place via a bottom action bar. See `2026-06-05-calendar-inline-edit-design.md`.
4. **Production mechanics** (for the plan, not the brainstorm): i18n (spike used hardcoded English; app uses `t()` and tests assert on testids, never translated text), an integration test, FullCalendar chrome → MUI theming, and removing the Schedule-X experiment.

## Spike artifacts — deleted

The spike was built in worktree `spike/inventory-calendar` (off `stage`) and **deliberately discarded** — never committed, never merged. The touched files (`InventoryCalendarPage.tsx` + its FullCalendar wiring, the Schedule-X `…SxPage.tsx` comparison, the `calendar` / `calendar-sx` routes, the `InventoryViewPage.tsx` button, the `vite.config.ts` temporal dedupe, and the deps in `package.json`) no longer exist.

**Implement the real feature from this doc + a fresh plan — do NOT reconstruct or copy the spike code (it's gone by design).** The spike code carried throwaway shortcuts (hardcoded English, no i18n/tests, a demo dialog, Schedule-X leftovers) that must not become a baseline. Everything worth keeping — the library choice, the config knobs, the data mapping, the visual patterns, and the gotchas — is captured textually above precisely so the implementation starts from documented intent, not from disposable code.

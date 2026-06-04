# Inventory expiry calendar — personalized view settings

**Date:** 2026-06-04
**Status:** Design approved in brainstorm; ready for an implementation plan.
**Branch:** `feat/inventory-calendar` (worktree off `stage`) — continues the shipped calendar foundation.
**Builds on:** [`2026-06-04-inventory-calendar-design.md`](2026-06-04-inventory-calendar-design.md) — the foundation pass (household-wide read slice, FullCalendar `dayGridMonth`, cook-by bars, focus-select). This spec adds **user-tunable, browser-persisted view settings** on top of that page.

## Goal

Let the user personalize the expiry calendar to their own workflow — adjust how far ahead the cook-by bars reach, and filter which urgency levels show — so a cramped month stays readable during UAT without a one-size-fits-all default. Settings persist locally per device. **No backend change, no migration** — everything lives on the client, consistent with the foundation's "window `X` lives entirely on the client" decision.

This is a **UAT exploration pass**: the intent is to give the user every reasonable knob to find what works, not to lock down a minimal default.

## Decisions (from the focused brainstorm, 2026-06-04)

1. **Overflow behavior stays "Expanded".** Crowded days keep growing the week-row and scrolling the page (the foundation's `dayMaxEvents={false}` + `dayMaxEventRows={false}`). The compact "+N more" popover alternative was considered and **rejected** — the popover is day-scoped, so a multi-day cook-by *bar* collapses into a single-day entry on the crowded days it crosses, losing its "span" feel. Height is instead tamed by the two new levers below (shorter bars overlap less; filters drop whole items).
2. **Two settings, browser-persisted:** an adjustable **window length** and per-level **expiry filters**. Persisted in `localStorage` (per device); no cross-device sync in v1.
3. **Bars never extend into the past.** `barStart = max(expiry − windowDays, today)`. So an item expiring tomorrow with a 180-day window shows `[today … tomorrow]`, not a streak running ~6 months back. The clamp only engages when the window would reach before today; far-future items with a short window are unaffected (expires in 40 days, length 7 → still `[day 33 … day 40]`).
4. **"Full runway" (Max) mode.** A toggle that treats the window as unbounded: every not-yet-expired item's bar spans `[today … expiry]`. Still clamped to today, so it never overflows backward. This is the safe form of the original "set it to int max" idea — it surfaces the full remaining runway of everything regardless of how far out, accepting the extra height.
5. **Already-expired items render as a 1-day marker on their actual expiry date** (no backward tail). Truthful about when the item lapsed; visible by navigating to past months. Hidden when the `expired` level filter is off.
6. **All items show by default.** Decluttering is opt-in via the level filters and a shorter window — the default view is the full set.

## Architecture

Three small, independently-testable units, all under `features/inventories/calendar/`. The existing page, hook, and read slice are reused unchanged except where noted.

### 1. Settings store — `calendarViewSettings.ts`

A **Zustand store with the `persist` middleware** writing to `localStorage` under the key `frigorino-calendar-view`. Zustand is the sanctioned client-state layer (CLAUDE.md) and is already a dependency; it gives a reactive read in the page and reactive writes from the settings panel without a hand-rolled event bus. (A plain `localStorage` helper like `onboardingSkip.ts` was considered and rejected — it is not reactive across the panel ↔ page boundary.)

State shape:

```ts
interface CalendarViewSettings {
  windowDays: number;   // 1–180, default 7
  fullRunway: boolean;  // default false; true ⇒ bars span [today … expiry]
  levels: {
    expired: boolean;   // all default true
    critical: boolean;
    soon: boolean;
    fresh: boolean;
  };
}
```

Plus actions: `setWindowDays(n)`, `setFullRunway(b)`, `toggleLevel(level)`, `reset()`. `reset()` restores all defaults (`windowDays: 7`, `fullRunway: false`, all levels `true`).

`windowDays` is range-clamped to `[1, 180]` on write so a stale/corrupt persisted value can't produce a broken render. `persist` is configured with a `version` and a defensive `merge` so an older/partial stored object falls back to defaults for any missing keys.

### 2. Pure mapping changes — `expiryCalendarEvents.ts`

`buildExpiryEvents` stays pure: it gains a `settings: CalendarViewSettings` parameter (passed in by the page) so it remains deterministic and unit-testable without React or the clock. Behavior:

- **Level filter:** compute each item's `ExpiryLevel` (existing `getExpiryLevel`), and drop the item if `settings.levels[level]` is false. Filtering is purely client-side — the read slice already returns all items with an expiry, so there is **no backend change**.
- **Clamp + runway:** `windowStart = settings.fullRunway ? today : max(expiry − settings.windowDays, today)`. Bar = `[windowStart, expiry]`, all-day, exclusive `end = expiry + 1` (unchanged convention).
- **Expired items** (`expiry < today`): render a 1-day marker on the actual expiry date — `start = expiry`, `end = expiry + 1` — with no clamp/tail. (Still subject to the `expired` level filter.)
- `activeToday` continues to mean "today falls within `[windowStart, expiry]`"; under the clamp/runway, a bar that reaches today starts at today.

This keeps all date/window logic in one pure function with no React dependency.

### 3. UI — settings panel + page wiring

- **Entry point:** a filter/tune icon added to the calendar page header (`PageHeadActionBar` `directActions`, testid `calendar-settings-button`). It sits alongside the existing header; the foundation page currently passes `directActions={[]}`, so this is the first action there.
- **Panel:** an MUI `Drawer` (`anchor="bottom"`) — a mobile bottom sheet — containing:
  - **Window length:** a slider (`min=1, max=180`) + a numeric `TextField` (exact entry, also clamped to 1–180), kept in sync, with an **`∞ Max`** toggle (`Switch`/`Checkbox`). When Max is on, `fullRunway` is true and the slider + input are disabled and read "Max".
  - **Expiry levels:** four toggle chips — Expired / Critical / Soon / Fresh — each backed by `levels.*`. Chip color follows the level's palette band so the control reads as "what this hides/shows".
  - **Reset to defaults** button → `reset()`.
- **Page wiring (`ExpiryCalendarPage.tsx`):** read the store, pass `settings` into `buildExpiryEvents` (in the existing `useMemo`, with the settings as deps), and own the drawer open/close state. The settings button is rendered in the header so it stays reachable even when the current filters hide every bar.
- **Empty-state nuance:** when items exist but the active filters/length hide them all, the empty text reads "No items match your filters" (new i18n key) rather than the existing "No items with an expiry date yet." The page distinguishes the two by checking whether the *unfiltered* fetched list is non-empty.

### i18n

All new copy via `t()` under `inventory.calendar.settings.*` (panel title, window-length label, "Full runway"/Max label, the four level labels, reset, and the "no items match your filters" empty variant), added to both `en` and `de` translation files. Tests assert on testids / `data-*`, never translated text.

## Components / files

**Create:**
- `features/inventories/calendar/calendarViewSettings.ts` — Zustand persist store.
- `features/inventories/calendar/components/CalendarSettingsSheet.tsx` — the bottom-sheet panel.

**Modify:**
- `features/inventories/calendar/expiryCalendarEvents.ts` — `buildExpiryEvents` gains `settings`; clamp/runway/expired-marker/level-filter logic.
- `features/inventories/calendar/pages/ExpiryCalendarPage.tsx` — header settings button, drawer state, pass settings into the mapper, empty-state nuance.
- `public/locales/{en,de}/translation.json` — `inventory.calendar.settings.*` keys + the filtered empty variant.

No changes to the backend read slice, the generated client, the route, or the query hook.

## Data flow

1. Page mounts → Zustand reads persisted settings from `localStorage` (or defaults).
2. `useExpiryCalendar` fetches all items with an expiry (unchanged).
3. `buildExpiryEvents(items, today, levelColor, settings)` produces the visible events (filtered + clamped).
4. User opens the bottom sheet, changes a control → store updates → `persist` writes `localStorage` → the page's `useMemo` recomputes events → calendar re-renders.
5. Reload → step 1 restores the same settings.

## Error / edge handling

- **Corrupt/stale persisted value:** `windowDays` clamped to `[1, 180]`; `persist` `merge` fills missing keys from defaults; an unparseable blob falls back to defaults (store initializes clean).
- **`localStorage` unavailable** (privacy mode): Zustand `persist` degrades to in-memory state for the session — the calendar still works, settings just don't survive reload. No crash.
- **All levels off / everything filtered:** valid state → "No items match your filters" empty message; settings button stays in the header to recover.
- **Expired marker vs. clamp:** expired items bypass the clamp (they have no remaining window) and render on their real expiry date; guarded so `start < end` always holds.

## Testing

- **Unit (pure, no React):** `buildExpiryEvents` with a fixed `today` —
  - clamp: near/expired item + large window ⇒ `start === today`, never before;
  - far item + short window ⇒ unchanged future window;
  - `fullRunway` ⇒ every non-expired bar starts at `today`;
  - expired item ⇒ 1-day marker on its expiry date;
  - level filter ⇒ items of a disabled level are dropped; counts match.
- **Integration (Reqnroll + Playwright, assert on testids/`data-*`):** open the calendar → open the settings sheet → toggle a level off → assert the corresponding bar is gone; adjust the window length → assert it applies; reload the page → assert the changed setting persists. Requires `npm run build` before the UI test (harness serves `ClientApp/build`).

## Out of scope (v1)

- **Inventory filter** (show only chosen inventories) — natural next step, deferred.
- **Agenda / list view mode** — bigger lift; month grid only for now.
- **First-day-of-week** preference.
- **Server-side cross-device sync** of these settings — browser-only for now (mirrors the foundation's client-only window decision; could later fold into the existing `useUserSettings` server settings).
- **Compact "+N more" overflow** — explicitly rejected (see Decision 1).

## Verification

- `dotnet test Application/Frigorino.sln` (the new integration scenario runs under Testcontainers).
- Frontend: `npm run lint`, `npm run tsc`, `npm run prettier:check`.
- `docker build -f Application/Dockerfile -t frigorino .` as the final gate.
- Manual browser verify at a 390×844 phone viewport: open settings, drag the slider + type a value + toggle Max (confirm slider/input disable), toggle each level (confirm bars appear/disappear), confirm an item expiring far out shows its full bar capped at today, confirm expired items sit on their expiry date, and confirm settings survive a reload.

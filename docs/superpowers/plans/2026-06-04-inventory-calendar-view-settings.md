# Inventory Calendar — Personalized View Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add browser-persisted, user-tunable view settings to the expiry calendar — an adjustable cook-by window length (1–180 + "Full runway") and per-expiry-level filters — with bars that never extend into the past.

**Architecture:** A Zustand `persist` store (`localStorage`) holds the settings; the existing pure `buildExpiryEvents` mapper gains a `settings` param that applies the clamp/runway/expired-marker/level-filter logic; a MUI bottom-sheet panel (opened from a header icon) mutates the store. No backend, no migration, no API regeneration.

**Tech Stack:** React 19 + TanStack Query/Router + MUI + Zustand `^5.0.6` (`persist` + `createJSONStorage`); Reqnroll + Playwright integration test.

**Spec:** `docs/superpowers/specs/2026-06-04-inventory-calendar-view-settings-design.md`.

**Conventions to honor (CLAUDE.md / project memory):**
- Client state = Zustand; never a third state layer. Spread generated query options for server state (unchanged here).
- i18n: all copy via `t()`; tests assert on testids / `data-*`, never translated text.
- Use `npm run` scripts (`tsc` / `lint` / `fix` / `build`), never raw `npx`.
- Integration harness serves `ClientApp/build` — run `npm run build` before the UI integration test.
- **No JS test runner exists.** The pure mapper logic is verified via the Playwright integration scenario (level filter + persistence) plus the manual browser verify; frontend per-task gate is `npm run tsc` (+ `npm run lint`).
- All shell commands run from the worktree root: `C:/Repositories/frigorino/.claude/worktrees/feat-inventory-calendar`. ClientApp lives at `Application/Frigorino.Web/ClientApp/`.
- No Co-Authored-By / "Generated with Claude" commit trailers.

---

## File Structure

**Frontend (create):**
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/calendarViewSettings.ts` — Zustand persist store + types + defaults.
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarSettingsSheet.tsx` — the bottom-sheet panel.

**Frontend (modify):**
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/expiryCalendarEvents.ts` — `buildExpiryEvents` gains `settings`; clamp / full-runway / expired-marker / level-filter.
- `Application/Frigorino.Web/ClientApp/src/utils/dateUtils.ts` — remove the now-superseded `CALENDAR_WINDOW_DAYS` constant.
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx` — read store + pass settings (Task 2); header settings icon, drawer state, render sheet, filtered empty-state (Task 3).
- `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` + `public/locales/de/translation.json` — `inventory.calendar.settings.*` + `inventory.calendar.emptyFiltered`.

**Integration tests (modify):**
- `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature` — add the settings/persistence scenario.
- `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs` — add the new step bindings.

---

## Task 1: Settings store (Zustand persist)

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/calendarViewSettings.ts`

- [ ] **Step 1: Create the store**

Create `calendarViewSettings.ts`:

```typescript
import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import type { ExpiryLevel } from "../../../utils/dateUtils";

// Per-level visibility. Keyed by ExpiryLevel so the four keys always match the bands in dateUtils.
export type CalendarLevelFilters = Record<ExpiryLevel, boolean>;

export interface CalendarViewSettings {
    windowDays: number; // cook-by bar length in days
    fullRunway: boolean; // true ⇒ bars span [today … expiry], ignoring windowDays
    levels: CalendarLevelFilters;
}

export const CALENDAR_WINDOW_MIN = 1;
export const CALENDAR_WINDOW_MAX = 180;

export const DEFAULT_CALENDAR_VIEW_SETTINGS: CalendarViewSettings = {
    windowDays: 30,
    fullRunway: false,
    levels: { expired: true, critical: true, soon: true, fresh: true },
};

// Keep windowDays inside [MIN, MAX] so a stale/corrupt persisted or typed value can't break the render.
const clampWindow = (n: number): number => {
    if (!Number.isFinite(n)) {
        return DEFAULT_CALENDAR_VIEW_SETTINGS.windowDays;
    }
    return Math.min(
        CALENDAR_WINDOW_MAX,
        Math.max(CALENDAR_WINDOW_MIN, Math.round(n)),
    );
};

interface CalendarViewState extends CalendarViewSettings {
    setWindowDays: (n: number) => void;
    setFullRunway: (b: boolean) => void;
    toggleLevel: (level: ExpiryLevel) => void;
    reset: () => void;
}

export const useCalendarViewSettings = create<CalendarViewState>()(
    persist(
        (set) => ({
            ...DEFAULT_CALENDAR_VIEW_SETTINGS,
            setWindowDays: (n) => set({ windowDays: clampWindow(n) }),
            setFullRunway: (b) => set({ fullRunway: b }),
            toggleLevel: (level) =>
                set((state) => ({
                    levels: { ...state.levels, [level]: !state.levels[level] },
                })),
            reset: () => set({ ...DEFAULT_CALENDAR_VIEW_SETTINGS }),
        }),
        {
            name: "frigorino-calendar-view",
            version: 1,
            storage: createJSONStorage(() => localStorage),
            // Defensive merge: an older/partial stored object falls back to defaults for missing keys,
            // and windowDays is re-clamped on hydration.
            merge: (persisted, current) => {
                const p = (persisted ?? {}) as Partial<CalendarViewSettings>;
                return {
                    ...current,
                    ...p,
                    windowDays: clampWindow(p.windowDays ?? current.windowDays),
                    levels: { ...current.levels, ...(p.levels ?? {}) },
                };
            },
        },
    ),
);
```

- [ ] **Step 2: Type-check**

Run (from `ClientApp/`):

```bash
npm run tsc
```

Expected: PASS. (`ExpiryLevel` resolves from `dateUtils`; zustand `persist`/`createJSONStorage` resolve from `zustand/middleware`. The curried `create<T>()(persist(...))` form is required by zustand v5.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/calendarViewSettings.ts
git commit -m "feat(client): calendar view settings store (persisted)"
```

---

## Task 2: Apply settings in the pure mapper + wire the page to read them

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/expiryCalendarEvents.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/utils/dateUtils.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx`

- [ ] **Step 1: Rewrite `buildExpiryEvents` to take settings**

Replace the entire contents of `expiryCalendarEvents.ts` with:

```typescript
import type { ExpiryCalendarItemResponse } from "../../../lib/api";
import {
    getExpiryLevel,
    parseLocalDate,
    type ExpiryLevel,
} from "../../../utils/dateUtils";
import type { CalendarViewSettings } from "./calendarViewSettings";

export interface ExpiryEventProps {
    itemId: number;
    inventoryId: number;
    inventoryName: string;
    expiryDate: string;
    activeToday: boolean;
}

export interface ExpiryCalendarEvent {
    id: string;
    title: string;
    start: string; // YYYY-MM-DD, inclusive
    end: string; // YYYY-MM-DD, EXCLUSIVE (FullCalendar all-day convention)
    allDay: true;
    backgroundColor: string;
    borderColor: string;
    extendedProps: ExpiryEventProps;
}

const MS_PER_DAY = 1000 * 60 * 60 * 24;

function toIsoDate(date: Date): string {
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function addDays(date: Date, days: number): Date {
    const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    copy.setDate(copy.getDate() + days);
    return copy;
}

function wholeDayDiff(from: Date, to: Date): number {
    return Math.round((to.getTime() - from.getTime()) / MS_PER_DAY);
}

// Maps inventory items (each with an expiry) to all-day cook-by bars, applying the user's
// view settings. Rules (see the view-settings design):
//   • Level filter: items whose ExpiryLevel is toggled off are dropped.
//   • Bars never extend into the past: barStart = max(expiry - windowDays, today).
//   • Full runway: barStart = today (bar spans the full remaining runway to expiry).
//   • Expired items (expiry < today) have no remaining window → a 1-day marker on the expiry date.
// FullCalendar's all-day `end` is EXCLUSIVE, so end = expiry + 1 covers the expiry day itself.
// `levelColor` keeps bar color in sync with the MUI theme; `todayIso` and `settings` are passed in
// so the mapping stays pure and deterministic.
export function buildExpiryEvents(
    items: ExpiryCalendarItemResponse[],
    todayIso: string,
    levelColor: (level: ExpiryLevel) => string,
    settings: CalendarViewSettings,
): ExpiryCalendarEvent[] {
    const today = parseLocalDate(todayIso);
    const events: ExpiryCalendarEvent[] = [];

    for (const item of items) {
        const expiry = parseLocalDate(item.expiryDate);
        const daysUntil = wholeDayDiff(today, expiry);
        const level = getExpiryLevel(daysUntil);

        if (!settings.levels[level]) {
            continue;
        }

        const color = levelColor(level);

        let windowStart: Date;
        if (daysUntil < 0) {
            // Already expired: 1-day marker on the actual expiry date, no backward tail.
            windowStart = expiry;
        } else if (settings.fullRunway) {
            windowStart = today;
        } else {
            const rawStart = addDays(expiry, -settings.windowDays);
            // Clamp the start to today so the bar never extends into the past.
            windowStart =
                rawStart.getTime() > today.getTime() ? rawStart : today;
        }

        const activeToday =
            wholeDayDiff(windowStart, today) >= 0 &&
            wholeDayDiff(today, expiry) >= 0;

        events.push({
            id: String(item.id),
            title: item.text,
            start: toIsoDate(windowStart),
            end: toIsoDate(addDays(expiry, 1)),
            allDay: true,
            backgroundColor: color,
            borderColor: color,
            extendedProps: {
                itemId: item.id,
                inventoryId: item.inventoryId,
                inventoryName: item.inventoryName,
                expiryDate: item.expiryDate,
                activeToday,
            },
        });
    }

    return events;
}
```

- [ ] **Step 2: Remove the superseded constant**

In `dateUtils.ts`, delete the `CALENDAR_WINDOW_DAYS` block (its default now lives in `DEFAULT_CALENDAR_VIEW_SETTINGS.windowDays`). Remove these lines:

```typescript
// Cook-by planning window for the expiry calendar: how many days BEFORE an item's expiry
// its bar begins. Deliberately decoupled from EXPIRY_THRESHOLDS so the bar length (planning
// horizon) and the urgency color bands can be tuned independently.
export const CALENDAR_WINDOW_DAYS = 7;
```

- [ ] **Step 3: Wire the page to read the store and pass settings**

In `ExpiryCalendarPage.tsx`, add the store import alongside the other relative imports (after the `useExpiryCalendar` import):

```tsx
import { useCalendarViewSettings } from "../calendarViewSettings";
```

Then, inside the component, after the `selectedId` state line (`const [selectedId, setSelectedId] = useState<number | null>(null);`), add the three settings reads:

```tsx
    const windowDays = useCalendarViewSettings((s) => s.windowDays);
    const fullRunway = useCalendarViewSettings((s) => s.fullRunway);
    const levels = useCalendarViewSettings((s) => s.levels);
```

And replace the existing `events` memo:

```tsx
    const events = useMemo(
        () => buildExpiryEvents(items ?? [], todayIsoDate(), levelColor),
        [items, levelColor],
    );
```

with:

```tsx
    const events = useMemo(
        () =>
            buildExpiryEvents(items ?? [], todayIsoDate(), levelColor, {
                windowDays,
                fullRunway,
                levels,
            }),
        [items, levelColor, windowDays, fullRunway, levels],
    );
```

- [ ] **Step 4: Type-check + lint**

Run (from `ClientApp/`):

```bash
npm run tsc && npm run lint
```

Expected: both PASS. (No references to `CALENDAR_WINDOW_DAYS` remain — it was only used by the mapper. If lint flags formatting, run `npm run fix` then re-run `npm run tsc`.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/expiryCalendarEvents.ts \
        Application/Frigorino.Web/ClientApp/src/utils/dateUtils.ts \
        Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx
git commit -m "feat(client): apply view settings (window/runway/expired/level filter) to calendar bars"
```

---

## Task 3: Settings bottom-sheet + header entry + i18n + filtered empty state

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarSettingsSheet.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add the English i18n keys**

In `public/locales/en/translation.json`, inside the existing `"inventory"."calendar"` object, add a sibling `"emptyFiltered"` key and a `"settings"` sub-object. The calendar object currently has `title` / `empty` / `failedToLoad`; add after them (mind the commas so the JSON stays valid):

```json
            "emptyFiltered": "No items match your filters.",
            "settings": {
                "title": "Calendar view",
                "windowLength": "Cook-by window",
                "days": "days",
                "fullRunway": "Full runway",
                "levels": "Show levels",
                "level": {
                    "expired": "Expired",
                    "critical": "Critical",
                    "soon": "Soon",
                    "fresh": "Fresh"
                },
                "reset": "Reset to defaults"
            }
```

- [ ] **Step 2: Add the German i18n keys**

In `public/locales/de/translation.json`, inside `"inventory"."calendar"`, add the same structure:

```json
            "emptyFiltered": "Keine Artikel entsprechen deinen Filtern.",
            "settings": {
                "title": "Kalenderansicht",
                "windowLength": "Verbrauchsfenster",
                "days": "Tage",
                "fullRunway": "Volle Laufzeit",
                "levels": "Stufen anzeigen",
                "level": {
                    "expired": "Abgelaufen",
                    "critical": "Kritisch",
                    "soon": "Bald",
                    "fresh": "Frisch"
                },
                "reset": "Auf Standard zurücksetzen"
            }
```

- [ ] **Step 3: Create the settings sheet**

Create `components/CalendarSettingsSheet.tsx`:

```tsx
import { Close } from "@mui/icons-material";
import {
    Box,
    Button,
    Chip,
    Drawer,
    FormControlLabel,
    IconButton,
    Slider,
    Stack,
    Switch,
    TextField,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ExpiryLevel } from "../../../../utils/dateUtils";
import {
    CALENDAR_WINDOW_MAX,
    CALENDAR_WINDOW_MIN,
    useCalendarViewSettings,
} from "../calendarViewSettings";

const LEVEL_KEYS: ExpiryLevel[] = ["expired", "critical", "soon", "fresh"];

// Chip color per level mirrors the urgency bands so the control reads as "what this shows/hides".
const LEVEL_CHIP_COLOR: Record<ExpiryLevel, "error" | "warning" | "success"> = {
    expired: "error",
    critical: "error",
    soon: "warning",
    fresh: "success",
};

interface CalendarSettingsSheetProps {
    open: boolean;
    onClose: () => void;
}

export const CalendarSettingsSheet = ({
    open,
    onClose,
}: CalendarSettingsSheetProps) => {
    const { t } = useTranslation();
    const windowDays = useCalendarViewSettings((s) => s.windowDays);
    const fullRunway = useCalendarViewSettings((s) => s.fullRunway);
    const levels = useCalendarViewSettings((s) => s.levels);
    const setWindowDays = useCalendarViewSettings((s) => s.setWindowDays);
    const setFullRunway = useCalendarViewSettings((s) => s.setFullRunway);
    const toggleLevel = useCalendarViewSettings((s) => s.toggleLevel);
    const reset = useCalendarViewSettings((s) => s.reset);

    return (
        <Drawer
            anchor="bottom"
            open={open}
            onClose={onClose}
            data-testid="calendar-settings-sheet"
            slotProps={{
                paper: {
                    sx: { borderTopLeftRadius: 16, borderTopRightRadius: 16 },
                },
            }}
        >
            <Box sx={{ p: 2, maxWidth: 600, mx: "auto", width: "100%" }}>
                <Stack
                    direction="row"
                    sx={{
                        alignItems: "center",
                        justifyContent: "space-between",
                        mb: 1,
                    }}
                >
                    <Typography variant="h6">
                        {t("inventory.calendar.settings.title")}
                    </Typography>
                    <IconButton
                        onClick={onClose}
                        size="small"
                        aria-label="close"
                        data-testid="calendar-settings-close"
                    >
                        <Close />
                    </IconButton>
                </Stack>

                <Typography variant="subtitle2" sx={{ mt: 1 }}>
                    {t("inventory.calendar.settings.windowLength")}
                </Typography>
                <Stack
                    direction="row"
                    spacing={2}
                    sx={{ alignItems: "center", mb: 1 }}
                >
                    <TextField
                        size="small"
                        type="number"
                        value={fullRunway ? "" : windowDays}
                        disabled={fullRunway}
                        onChange={(e) => setWindowDays(Number(e.target.value))}
                        slotProps={{
                            htmlInput: {
                                min: CALENDAR_WINDOW_MIN,
                                max: CALENDAR_WINDOW_MAX,
                                inputMode: "numeric",
                                "data-testid": "calendar-window-input",
                            },
                        }}
                        sx={{ width: 96 }}
                    />
                    <Typography variant="body2" color="text.secondary">
                        {t("inventory.calendar.settings.days")}
                    </Typography>
                    <FormControlLabel
                        sx={{ ml: "auto" }}
                        data-testid="calendar-fullrunway-toggle"
                        control={
                            <Switch
                                checked={fullRunway}
                                onChange={(e) =>
                                    setFullRunway(e.target.checked)
                                }
                            />
                        }
                        label={t("inventory.calendar.settings.fullRunway")}
                    />
                </Stack>
                <Slider
                    value={fullRunway ? CALENDAR_WINDOW_MAX : windowDays}
                    disabled={fullRunway}
                    min={CALENDAR_WINDOW_MIN}
                    max={CALENDAR_WINDOW_MAX}
                    onChange={(_, v) => setWindowDays(v as number)}
                    data-testid="calendar-window-slider"
                    sx={{ mb: 2 }}
                />

                <Typography variant="subtitle2">
                    {t("inventory.calendar.settings.levels")}
                </Typography>
                <Stack
                    direction="row"
                    sx={{ flexWrap: "wrap", gap: 1, my: 1 }}
                >
                    {LEVEL_KEYS.map((lvl) => (
                        <Chip
                            key={lvl}
                            label={t(`inventory.calendar.settings.level.${lvl}`)}
                            color={levels[lvl] ? LEVEL_CHIP_COLOR[lvl] : "default"}
                            variant={levels[lvl] ? "filled" : "outlined"}
                            onClick={() => toggleLevel(lvl)}
                            data-testid={`calendar-level-${lvl}`}
                            data-active={levels[lvl] ? "true" : "false"}
                        />
                    ))}
                </Stack>

                <Button
                    fullWidth
                    onClick={reset}
                    sx={{ mt: 1 }}
                    data-testid="calendar-settings-reset"
                >
                    {t("inventory.calendar.settings.reset")}
                </Button>
            </Box>
        </Drawer>
    );
};
```

- [ ] **Step 4: Add the header icon, drawer state, sheet render, and filtered empty state to the page**

In `ExpiryCalendarPage.tsx`:

(a) Add the icon + sheet imports. Add near the top with the other imports:

```tsx
import { Tune } from "@mui/icons-material";
```
```tsx
import { CalendarSettingsSheet } from "../components/CalendarSettingsSheet";
```

(b) Add the drawer open state next to `selectedId`:

```tsx
    const [settingsOpen, setSettingsOpen] = useState(false);
```

(c) Replace the header's empty `directActions={[]}` with the settings button:

```tsx
                directActions={[
                    {
                        icon: <Tune />,
                        onClick: () => setSettingsOpen(true),
                        testId: "calendar-settings-button",
                    },
                ]}
```

(d) Replace the single empty-state block:

```tsx
                {!isLoading && !error && events.length === 0 && (
                    <Typography
                        variant="body2"
                        sx={{
                            color: "text.secondary",
                            textAlign: "center",
                            py: 4,
                        }}
                        data-testid="calendar-empty"
                    >
                        {t("inventory.calendar.empty")}
                    </Typography>
                )}
```

with two variants (filtered vs. genuinely empty):

```tsx
                {!isLoading &&
                    !error &&
                    events.length === 0 &&
                    (items?.length ?? 0) > 0 && (
                        <Typography
                            variant="body2"
                            sx={{
                                color: "text.secondary",
                                textAlign: "center",
                                py: 4,
                            }}
                            data-testid="calendar-empty-filtered"
                        >
                            {t("inventory.calendar.emptyFiltered")}
                        </Typography>
                    )}
                {!isLoading &&
                    !error &&
                    events.length === 0 &&
                    (items?.length ?? 0) === 0 && (
                        <Typography
                            variant="body2"
                            sx={{
                                color: "text.secondary",
                                textAlign: "center",
                                py: 4,
                            }}
                            data-testid="calendar-empty"
                        >
                            {t("inventory.calendar.empty")}
                        </Typography>
                    )}
```

(e) Render the sheet just before the closing `</>` of the returned fragment (after the `</Container>`):

```tsx
            <CalendarSettingsSheet
                open={settingsOpen}
                onClose={() => setSettingsOpen(false)}
            />
```

- [ ] **Step 5: Type-check + lint**

Run (from `ClientApp/`):

```bash
npm run tsc && npm run lint
```

Expected: both PASS. (If lint flags import ordering/formatting, run `npm run fix` then re-run `npm run tsc`.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarSettingsSheet.tsx \
        Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx \
        Application/Frigorino.Web/ClientApp/public/locales/en/translation.json \
        Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(client): calendar settings bottom-sheet (window, runway, level filters)"
```

---

## Task 4: Integration test — level filter hides items and persists

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs`

- [ ] **Step 1: Build the SPA so the harness serves the new UI + testids**

Run (from `ClientApp/`):

```bash
npm run build
```

Expected: PASS — outputs to `ClientApp/build` (the integration harness serves this, not live source).

- [ ] **Step 2: Add the scenario to the feature file**

Append this scenario to `ExpiryCalendar.feature` (after the existing focus-select scenario; the `Background` already logs in with an active household):

```gherkin
  Scenario: Filtering a level hides matching items and persists across reload
    Given an inventory "Fridge" has an item "Milk" expiring in 2 days
    And an inventory "Fridge" has an item "Rice" expiring in 40 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Rice"
    When I open the calendar settings
    And I turn off the "fresh" level filter
    Then the calendar does not show the item "Rice"
    When I reload the calendar page
    Then the calendar does not show the item "Rice"
    And the calendar shows the item "Milk"
```

(Milk at 2 days = `critical`; Rice at 40 days = `fresh`. Turning off `fresh` hides Rice but keeps Milk. The `Given … expiring in … days` step is the shared binding from `ExpiryCalendarApiSteps`.)

- [ ] **Step 3: Add the new step bindings**

In `ExpiryCalendarSteps.cs`, add these methods inside the existing `ExpiryCalendarSteps` class (the file has no `using` directives — `GlobalUsings.cs` provides `Microsoft.Playwright` + the infrastructure namespace, matching the existing steps):

```csharp
    [When("I open the calendar settings")]
    public async Task WhenIOpenTheCalendarSettings()
    {
        await ctx.Page.GetByTestId("calendar-settings-button").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-settings-sheet"))
            .ToBeVisibleAsync();
    }

    [When("I turn off the {string} level filter")]
    public async Task WhenITurnOffTheLevelFilter(string level)
    {
        await ctx.Page.GetByTestId($"calendar-level-{level}").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId($"calendar-level-{level}"))
            .ToHaveAttributeAsync("data-active", "false");
    }

    [Then("the calendar does not show the item {string}")]
    public async Task ThenTheCalendarDoesNotShowTheItem(string itemText)
    {
        // Filtered-out items are not rendered at all, so the locator resolves to zero elements.
        await Assertions.Expect(ctx.Page.GetByTestId($"cal-event-{itemText}"))
            .ToHaveCountAsync(0);
    }

    [When("I reload the calendar page")]
    public async Task WhenIReloadTheCalendarPage()
    {
        await ctx.Page.ReloadAsync(
            new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await ctx.Page.WaitForURLAsync("**/inventories/calendar");
    }
```

- [ ] **Step 4: Run the ExpiryCalendar tests to verify they pass**

Run (from repo root):

```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~ExpiryCalendar" 2>&1 | tail -40
```

Expected: all ExpiryCalendar scenarios PASS (2 API + 2 UI = the prior focus-select scenario plus the new filter/persistence one). Confirm via the actual "Passed!/Bestanden!" summary line — a piped command's exit code reflects `tail`, not `dotnet test`. (Docker Desktop must be running for Testcontainers.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature \
        Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs
git commit -m "test(it): calendar level filter hides items and persists across reload"
```

---

## Task 5: Final verification, manual check, finish branch

**Files:** none (verification only).

- [ ] **Step 1: Full backend + integration test run**

Run (from repo root):

```bash
dotnet test Application/Frigorino.sln 2>&1 | tail -25
```

Expected: PASS for `Frigorino.Test` + `Frigorino.IntegrationTests`. Confirm via the "Passed!" summary, not a piped exit code. (Note: the `UndoRestoresADeletedListItemViaTheToast` list scenario is a known pre-existing flake unrelated to this work — if it is the only failure, re-run it in isolation with `--filter "FullyQualifiedName~UndoRestoresADeletedListItem"` to confirm it passes on retry.)

- [ ] **Step 2: Frontend verification gate**

Run (from `ClientApp/`):

```bash
npm run lint && npm run tsc && npm run prettier:check
```

Expected: all PASS. If `prettier:check` fails, run `npm run prettier` and re-commit.

- [ ] **Step 3: Docker build (drift gate)**

Run (from repo root):

```bash
docker build -f Application/Dockerfile -t frigorino .
```

Expected: PASS. (No project added, so no Dockerfile change — this confirms the SPA still publishes cleanly. If the Docker daemon is unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 4: Manual browser verify (catches runtime-only bugs)**

Bring up the dev stack (`/dev-up`) and, at a 390×844 phone viewport, on `/inventories/calendar` (seed several items across urgency levels and far-future dates):
- Open the settings icon → the bottom sheet appears.
- Drag the slider and type a value in the number box → bars change length; values stay in sync and clamp to 1–180.
- Toggle **Full runway** → slider + input disable and read "Max"; every not-yet-expired bar now starts at today.
- Confirm an item expiring far out (e.g. 60 days) with a large window shows a bar that **starts at today, never before** (clamp).
- Confirm an **already-expired** item shows as a 1-day marker on its expiry date.
- Toggle each level chip off/on → matching bars disappear/reappear; with everything filtered out, the "No items match your filters" message shows (testid `calendar-empty-filtered`).
- **Reset to defaults** → window 30, all levels on, runway off.
- Reload the page → the last settings persist.

- [ ] **Step 5: Finish the branch**

Use the `superpowers:finishing-a-development-branch` skill to integrate `feat/inventory-calendar` (the project promotes via `stage`, per project memory). No `IDEAS.md` entry to remove for this increment.

---

## Self-Review

**Spec coverage:**
- Overflow stays Expanded (no compact mode) → no change to `dayMaxEvents`/`dayMaxEventRows`; nothing in the plan reintroduces a popover. ✓
- Two browser-persisted settings (window length + level filters) → Task 1 store (`persist` → `localStorage` key `frigorino-calendar-view`). ✓
- Clamp `barStart = max(expiry − windowDays, today)` → Task 2 mapper (`rawStart.getTime() > today.getTime() ? rawStart : today`). ✓
- Full runway (Max) = bars span today→expiry, clamped → Task 2 (`fullRunway ? today : …`) + Task 3 toggle. ✓
- Expired items = 1-day marker on expiry date → Task 2 (`daysUntil < 0 ⇒ windowStart = expiry`). ✓
- All items show by default; declutter via filters/length → defaults all levels true, windowDays 30. ✓
- UI: header icon → bottom sheet with slider+input+Max toggle, four level chips, reset → Task 3. ✓
- Length range 1–180 + number input + Max toggle (Max separate, disables slider/input) → Task 1 (`CALENDAR_WINDOW_MIN/MAX`, clamp) + Task 3 (disabled when `fullRunway`). ✓
- Default windowDays 30, all levels true → `DEFAULT_CALENDAR_VIEW_SETTINGS`. ✓
- Filtered empty-state ("No items match your filters") → Task 3 (d) + i18n `emptyFiltered`. ✓
- i18n en+de via `t()` → Task 3 Steps 1–2. ✓
- Persistence + level filter integration test (assert on `data-*`/testids) → Task 4. ✓
- No backend/migration/API regen → only frontend + IT files touched. ✓
- Corrupt/missing persisted value handling → `clampWindow` + `merge` (Task 1). ✓
- Out-of-scope items (inventory filter, agenda view, first-day-of-week, server sync, compact overflow) → not implemented. ✓
- **Testing deviation noted:** spec lists pure unit tests, but no JS test runner exists; the mapper logic is covered via the Task 4 integration scenario + Task 5 manual verify (called out in the header). ✓

**Placeholder scan:** No TBD/TODO; every code step shows full content. ✓

**Type consistency:** `CalendarViewSettings` / `CalendarLevelFilters` (Task 1) consumed by `buildExpiryEvents(items, todayIso, levelColor, settings)` (Task 2) and the page call site (Task 2 Step 3) and the sheet (Task 3); `ExpiryLevel` keys match the `levels` record keys; `useCalendarViewSettings` selectors (`windowDays`/`fullRunway`/`levels`/`setWindowDays`/`setFullRunway`/`toggleLevel`/`reset`) match the store definition; `CALENDAR_WINDOW_MIN`/`CALENDAR_WINDOW_MAX` defined in Task 1, used in Task 3; testids `calendar-settings-button` / `calendar-settings-sheet` / `calendar-level-{lvl}` / `data-active` defined in Task 3 and asserted in Task 4. ✓

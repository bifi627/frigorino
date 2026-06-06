# Keyboard-or-Pick Expiry Dates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace both native `<input type="date">` expiry controls with one shared MUI X `DatePicker` so users can either type the date with the keyboard or pick it from a calendar.

**Architecture:** Add `@mui/x-date-pickers` + the `date-fns` adapter, wrap the app in a language-reactive `LocalizationProvider`, and build one shared `ExpiryDatePicker` wrapper that converts between the app's `"YYYY-MM-DD"` string model and the picker's `Date | null`. Both call sites (composer expiry feature, promotion review sheet) consume the wrapper.

**Tech Stack:** React 19, TypeScript, MUI v9 (`@mui/material`), `@mui/x-date-pickers@^9.4.0`, `date-fns@^4.4.0`, i18next, Vite, Reqnroll + Playwright (integration tests).

---

## Critical constraints (read before starting)

- **No JS/TS unit-test runner exists** in this repo (CLAUDE.md: "There is no frontend (JS) test runner configured"). Do **not** add Vitest/Jest. The test layer for frontend behavior is the **integration tests** (Reqnroll + Playwright, `Frigorino.IntegrationTests`) plus `tsc` + `eslint` + `prettier`. TDD here means: change the integration scenario/step first (red), implement, rebuild SPA, run IT (green).
- **The IT harness serves `ClientApp/build`, not live source** — after ANY React edit you MUST run `npm run build` from `ClientApp/` or the integration tests run against stale UI.
- **Keep the `"YYYY-MM-DD"` string model end-to-end.** All DTOs, drafts, and server payloads stay strings. Only the new wrapper touches `Date` objects.
- **Test locale = English.** `i18n` has `fallbackLng: "en"` and the Playwright browser's `navigator` language is `en-US`, so in tests the field mask is `MM/dd/yyyy`. German users get `dd.MM.yyyy` at runtime.
- **Never assert on translated text** in IT — target testids / `data-*` (project rule).
- Commit after each task. Branch is already `feat/keyboard-expiry-dates`.

## File structure

| File | Responsibility | Action |
|------|----------------|--------|
| `ClientApp/package.json` + `package-lock.json` | Declare the two new deps | Modify |
| `ClientApp/src/utils/dateUtils.ts` | Add `formatIsoDate(date)` (Date → local `YYYY-MM-DD`, invalid → `null`) | Modify |
| `ClientApp/src/common/AppLocalizationProvider.tsx` | Wrap children in `LocalizationProvider` with adapter locale derived from `i18n.language` | Create |
| `ClientApp/src/main.tsx` | Mount `AppLocalizationProvider` inside `ThemeProvider` | Modify |
| `ClientApp/src/components/ExpiryDatePicker.tsx` | Shared `DatePicker` wrapper: `string\|null` ↔ `Date`, clearable, testid, error/helperText | Create |
| `ClientApp/src/components/composer/features/expiryFeature.tsx` | Use `ExpiryDatePicker`; drop Today + Clear buttons | Modify |
| `ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx` | Use `ExpiryDatePicker` in `PromoteRow` | Modify |
| `IntegrationTests/Shared/ComposerSteps.cs` | Type into the masked field instead of `FillAsync(iso)` | Modify |
| `IntegrationTests/Slices/Lists/PromoteSteps.cs` | Clear the masked field via keyboard | Modify |

---

## Task 1: Add dependencies

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/package.json`
- Modify: `Application/Frigorino.Web/ClientApp/package-lock.json`

- [ ] **Step 1: Install the two packages (adds to package.json + lockfile)**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm install @mui/x-date-pickers@^9.4.0 date-fns@^4.4.0
```

(Adding deps → `npm install` is correct here, not `npm ci`.)

- [ ] **Step 2: Verify the declared ranges match the pinning rule**

Run: `grep -E '"@mui/x-date-pickers"|"date-fns"' package.json`
Expected: caret-minor ranges, e.g. `"@mui/x-date-pickers": "^9.4.0"` and `"date-fns": "^4.4.0"`. If npm wrote a different style, edit `package.json` to caret form and re-run `npm install` to refresh the lockfile.

- [ ] **Step 3: Verify the project still type-checks and lints**

Run: `npm run tsc && npm run lint`
Expected: both PASS (no usages yet; this just confirms the install didn't break resolution).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/package.json Application/Frigorino.Web/ClientApp/package-lock.json
git commit -m "build: add @mui/x-date-pickers + date-fns for expiry date entry"
```

---

## Task 2: Add `formatIsoDate` to dateUtils

The wrapper needs Date → `"YYYY-MM-DD"`. Reuse the existing `parseLocalDate` for the reverse. `formatIsoDate` must be local-time (no UTC shift, mirroring `todayIsoDate`) and must return `null` for an invalid/`null` Date (a partially-typed picker value produces `Invalid Date`).

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/utils/dateUtils.ts`

> No JS test runner exists, so this function is verified by `tsc` here and exercised end-to-end by the IT date scenarios in Task 7.

- [ ] **Step 1: Add the function** directly after the existing `todayIsoDate()` (around line 70):

```ts
// Date -> "YYYY-MM-DD" in LOCAL time, the inverse of parseLocalDate. Returns null for a
// null/invalid Date (e.g. a half-typed value from a date picker field), so callers can keep
// emitting null until the user finishes a valid date. Local-time on purpose — never UTC,
// which would shift the day in non-UTC timezones (same reasoning as todayIsoDate).
export function formatIsoDate(date: Date | null): string | null {
    if (date === null || Number.isNaN(date.getTime())) {
        return null;
    }
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
```

- [ ] **Step 2: Verify type-check passes**

Run (from `ClientApp/`): `npm run tsc`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/utils/dateUtils.ts
git commit -m "feat: add formatIsoDate (Date -> local YYYY-MM-DD)"
```

---

## Task 3: Language-reactive LocalizationProvider

`@mui/x-date-pickers` requires a `LocalizationProvider` ancestor. The adapter locale must follow the app language so the field mask/format is `dd.MM.yyyy` for German and `MM/dd/yyyy` for English, matching the recent native-calendar i18n fix.

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/common/AppLocalizationProvider.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/main.tsx`

- [ ] **Step 1: Create the provider component**

```tsx
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDateFns } from "@mui/x-date-pickers/AdapterDateFnsV3";
import { de, enUS } from "date-fns/locale";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

// Map the active i18next language (normalized to "de"/"en" by load: "languageOnly") to a
// date-fns locale. The locale drives the DatePicker field's mask + display format, so German
// users type/see dd.MM.yyyy and everyone else MM/dd/yyyy. Re-renders on language switch
// because useTranslation subscribes to i18next.
export const AppLocalizationProvider = ({
    children,
}: {
    children: ReactNode;
}) => {
    const { i18n } = useTranslation();
    const adapterLocale = i18n.language.startsWith("de") ? de : enUS;
    return (
        <LocalizationProvider
            dateAdapter={AdapterDateFns}
            adapterLocale={adapterLocale}
        >
            {children}
        </LocalizationProvider>
    );
};
```

> **Adapter import path note:** with `date-fns` v4 the adapter is exported as `AdapterDateFnsV3` (v3/v4 share the modern API; the legacy `AdapterDateFns` export targets date-fns v2). If `tsc` reports the import is missing, fall back to `@mui/x-date-pickers/AdapterDateFns` — verify against the installed package's `package.json` `exports`. Do NOT guess silently; let `tsc` confirm.

- [ ] **Step 2: Mount it in `main.tsx`** — wrap the `Suspense`/`RouterProvider` subtree, inside `ThemeProvider`. Add the import near the other imports:

```tsx
import { AppLocalizationProvider } from "./common/AppLocalizationProvider";
```

Then change the render tree so the provider wraps the existing `<Suspense>...</Suspense>` block (the one rendering `<RouterProvider />`):

```tsx
<ThemeProvider theme={appTheme}>
    <CssBaseline />
    <AppLocalizationProvider>
        <Suspense
            fallback={
                <Box
                    sx={{
                        display: "flex",
                        justifyContent: "center",
                        alignItems: "center",
                        minHeight: "100vh",
                    }}
                >
                    <CircularProgress />
                </Box>
            }
        >
            <RouterProvider router={router} />
        </Suspense>
    </AppLocalizationProvider>
    {/* GlobalStyles + Toaster stay where they are, inside ThemeProvider */}
```

Leave the existing `GlobalStyles` and `Toaster` siblings unchanged (they can stay outside `AppLocalizationProvider`).

- [ ] **Step 3: Verify type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/common/AppLocalizationProvider.tsx Application/Frigorino.Web/ClientApp/src/main.tsx
git commit -m "feat: app-wide LocalizationProvider keyed to i18n language"
```

---

## Task 4: Shared `ExpiryDatePicker` wrapper

One component both call sites use. Keeps the string model on the outside, `Date` only inside.

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/ExpiryDatePicker.tsx`

- [ ] **Step 1: Create the component**

```tsx
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import { formatIsoDate, parseLocalDate } from "../utils/dateUtils";

interface ExpiryDatePickerProps {
    /** Calendar date as "YYYY-MM-DD", or null when unset. */
    value: string | null;
    /** Emits a valid "YYYY-MM-DD", or null while empty/incomplete. */
    onChange: (value: string | null) => void;
    label?: string;
    placeholder?: string;
    disabled?: boolean;
    error?: boolean;
    helperText?: string;
    fullWidth?: boolean;
    /** Forwarded to the underlying <input> so Playwright can target/type into it. */
    dataTestId?: string;
}

// Single source of truth for expiry entry: a typeable masked field AND a calendar popover.
// Converts the app's "YYYY-MM-DD" string at the boundary so no Date ever leaks into state
// (avoids UTC day-shift bugs). Emits null until the typed value is a complete valid date.
export const ExpiryDatePicker = ({
    value,
    onChange,
    label,
    placeholder,
    disabled,
    error,
    helperText,
    fullWidth,
    dataTestId,
}: ExpiryDatePickerProps) => {
    const dateValue = value ? parseLocalDate(value) : null;
    return (
        <DatePicker
            label={label}
            value={dateValue}
            disabled={disabled}
            onChange={(next) => onChange(formatIsoDate(next))}
            slotProps={{
                field: { clearable: true },
                textField: {
                    fullWidth,
                    size: "small",
                    placeholder,
                    error,
                    helperText,
                    slotProps: {
                        htmlInput: dataTestId
                            ? { "data-testid": dataTestId }
                            : undefined,
                    },
                },
            }}
        />
    );
};
```

> **Verify-as-you-go:** the exact `slotProps` shape for clearable + htmlInput testid is MUI-version-sensitive. If `tsc` rejects `slotProps.field.clearable` or the nested `htmlInput`, consult `@mui/x-date-pickers` v9 docs (DatePicker `slotProps`, "Clearable behavior") via context7 and adjust — keep clearable enabled and keep `data-testid` on the `<input>` element (not the root), because the IT calls `.FillAsync`/typing on it.

- [ ] **Step 2: Verify type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/ExpiryDatePicker.tsx
git commit -m "feat: shared ExpiryDatePicker wrapper (string<->Date, type or pick)"
```

---

## Task 5: Use `ExpiryDatePicker` in the composer expiry feature

Replace the native date `TextField` in `ExpiryPanel`. Per the approved design, **remove the custom "Today" quick-button and the manual "Clear" button** — the picker's calendar covers "today" and `clearable` covers clearing. The toggle/chip stay as-is.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/composer/features/expiryFeature.tsx`

- [ ] **Step 1: Replace the `ExpiryPanel` body.** Swap the current `ExpiryPanel` (lines ~56-100) for:

```tsx
const ExpiryPanel = ({
    value,
    setValue,
    disabled,
}: FeatureSlot<string | null>) => {
    const { t } = useTranslation();
    return (
        <Box
            sx={{ width: "100%", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <ExpiryDatePicker
                fullWidth
                value={value}
                onChange={setValue}
                placeholder={t("common.date")}
                disabled={disabled}
                dataTestId="composer-expiry-input"
            />
        </Box>
    );
};
```

- [ ] **Step 2: Fix imports.** At the top of the file:
  - Add: `import { ExpiryDatePicker } from "../../ExpiryDatePicker";`
  - Remove now-unused: `Clear`, `Today` from `@mui/icons-material`; `IconButton`, `TextField` from `@mui/material` (keep `Box`, `Chip`); and `todayIsoDate` from the dateUtils import (keep `parseLocalDate`, still used by `formatForDisplay`).

  Resulting import lines should be:

```tsx
import { CalendarToday } from "@mui/icons-material";
import { Box, Chip } from "@mui/material";
import { useTranslation } from "react-i18next";
import { parseLocalDate } from "../../../utils/dateUtils";
import { ExpiryDatePicker } from "../../ExpiryDatePicker";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";
```

  (`CalendarToday` is still used by `ExpiryToggle`/`ExpiryChip`.)

- [ ] **Step 3: Verify type-check + lint** (catches any remaining unused import)

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS, no `no-unused-vars`.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/features/expiryFeature.tsx
git commit -m "feat: composer expiry uses ExpiryDatePicker (type or pick); drop Today/Clear buttons"
```

---

## Task 6: Use `ExpiryDatePicker` in the promotion sheet

Replace the native date `TextField` in `PromoteRow`, preserving the existing error state (`expiryMissing`) and helper text (the missing-date message vs. the `getExpiryInfo` human-readable hint).

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx`

- [ ] **Step 1: Replace the date `TextField`** (currently lines ~409-428) with:

```tsx
<ExpiryDatePicker
    fullWidth
    value={draft.expiry || null}
    onChange={(v) => onChange({ expiry: v ?? "" })}
    label={t("promote.expiry")}
    error={expiryMissing}
    helperText={
        expiryMissing
            ? t("promote.expiryRequired")
            : info?.humanReadable || " "
    }
    dataTestId={`promote-row-expiry-${entry.text}`}
/>
```

  Note the `draft.expiry` model stays a string (`""` when empty), so map `null ↔ ""` at this boundary.

- [ ] **Step 2: Fix imports.** Add `import { ExpiryDatePicker } from "../../../components/ExpiryDatePicker";`. Then check whether `TextField` is still used elsewhere in the file (it IS — the inventory picker and the quantity field use it), so **keep** the `TextField` import. Run lint to confirm nothing went unused.

- [ ] **Step 3: Verify type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx
git commit -m "feat: promotion sheet expiry uses ExpiryDatePicker (type or pick)"
```

---

## Task 7: Update integration tests for the masked field (red → green)

The native input accepted a raw `YYYY-MM-DD` via `FillAsync`. The MUI X field is a masked, segmented input that expects the locale format (`MM/dd/yyyy` in the en test environment) and does not clear via `FillAsync("")`. Two steps must change. This is the TDD layer for this feature.

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Shared/ComposerSteps.cs` (around lines 37-41)
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/PromoteSteps.cs` (around lines 83-86)

- [ ] **Step 1: Rebuild the SPA so the IT harness sees the new UI** (the harness serves `ClientApp/build`):

Run from `Application/Frigorino.Web/ClientApp/`: `npm run build`
Expected: build succeeds, `ClientApp/build` regenerated.

- [ ] **Step 2: Run the affected scenarios FIRST to watch them fail against the new UI** (confirms the steps are now wrong, not silently passing):

Run from repo root:
```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Promote|FullyQualifiedName~ListItem|FullyQualifiedName~InventoryItem"
```
Expected: the expiry-setting / clear-expiry scenarios FAIL (timeout filling the masked field).

- [ ] **Step 3: Update the composer expiry step.** The native input testid was `composer-panel-expiry input`; the new field is reachable the same way (the panel wrapper testid is unchanged), and also directly via `composer-expiry-input`. Replace `WhenISetTheExpiryDateTo`:

```csharp
[When("I set the expiry date to {string}")]
public async Task WhenISetTheExpiryDateTo(string isoDate)
{
    // The MUI X DatePicker renders a masked, segmented field (MM/dd/yyyy in the en test
    // env), not a raw native date input — so type the digits in locale order rather than
    // FillAsync-ing the ISO string. parts = [yyyy, MM, dd] -> "MMddyyyy".
    var parts = isoDate.Split('-');
    var digits = $"{parts[1]}{parts[2]}{parts[0]}";
    var input = ctx.Page.GetByTestId("composer-expiry-input");
    await input.ClickAsync();
    await input.PressSequentiallyAsync(digits);
}
```

- [ ] **Step 4: Update the promote clear-expiry step.** Replace `WhenIClearTheExpiryDateFor`:

```csharp
[When("I clear the expiry date for {string}")]
public async Task WhenIClearTheExpiryDateFor(string itemText)
{
    // Masked DatePicker field: select-all + Delete clears every section. FillAsync("")
    // does not work on a segmented field.
    var input = ctx.Page.GetByTestId($"promote-row-expiry-{itemText}");
    await input.ClickAsync();
    await input.PressAsync("ControlOrMeta+a");
    await input.PressAsync("Delete");
}
```

> If any other step types an expiry date (grep `set the expiry|expiry date to|promote-row-expiry` across `IntegrationTests`), apply the same masked-field pattern.

- [ ] **Step 5: Run the scenarios again — expect green:**

Run from repo root:
```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Promote|FullyQualifiedName~ListItem|FullyQualifiedName~InventoryItem"
```
Expected: PASS. If a masked-field interaction is still flaky, verify the exact key chord / digit order against the rendered field via the Playwright snapshot before adjusting — do not loosen assertions.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Shared/ComposerSteps.cs Application/Frigorino.IntegrationTests/Slices/Lists/PromoteSteps.cs
git commit -m "test: drive MUI X expiry field via masked keyboard input in IT"
```

---

## Task 8: Manual browser verification + full verification gate

Static checks and IT pass, but plan-baked DOM/runtime details (mask behavior, calendar popover, clearable, German format) need a real-browser look. Then run the full gate.

**Files:** none (verification only).

- [ ] **Step 1: Bring up the dev stack** via the `/dev-up` skill, then drive the SPA with Playwright MCP (point the browser at the printed SPA URL; authenticated as `dev@frigorino.local`).

- [ ] **Step 2: Verify the composer expiry** on a list: open the expiry panel, (a) **type** a date with the keyboard and confirm it sticks; (b) open the **calendar** popover and pick a date; (c) clear it via the clear affordance. Confirm the saved item shows the expected expiry.

- [ ] **Step 3: Verify the promotion sheet expiry**: trigger a promotion, type a date into a row, confirm Add is enabled only when a selected row has a date, and the human-readable hint updates live.

- [ ] **Step 4: Verify German format**: switch app language to German (settings) and confirm the field mask/display is `dd.MM.yyyy` and typing works in that order.

- [ ] **Step 5: Tear down** only if the user asks (do not auto `/dev-down`).

- [ ] **Step 6: Full verification gate** (final, from repo root):

```bash
dotnet test Application/Frigorino.sln
```
And from `ClientApp/`:
```bash
npm run lint && npm run tsc && npm run prettier && npm run build
```
And from repo root (catches SPA/Dockerfile drift):
```bash
docker build -f Application/Dockerfile -t frigorino .
```
Expected: all PASS. Capture the pass/fail lines — do not trust a tail-piped exit code (read actual results).

- [ ] **Step 7: Final commit (if prettier/build produced changes)**

```bash
git add -A
git commit -m "chore: prettier + build artifacts for expiry date picker" || echo "nothing to commit"
```

---

## Self-review notes (author)

- **Spec coverage:** deps (T1) ✓; `formatIsoDate` (T2) ✓; language-reactive LocalizationProvider (T3) ✓; shared `ExpiryDatePicker` keeping string model (T4) ✓; composer panel + drop Today/Clear (T5) ✓; promote row preserve error/helper (T6) ✓; testing incl. IT + manual + full gate (T7, T8) ✓; out-of-scope (calendar/display) untouched ✓.
- **No JS unit test for `formatIsoDate`** is intentional — repo has no JS test runner; it's covered by `tsc` and the IT date scenarios. Called out in constraints.
- **Type consistency:** `formatIsoDate(date: Date | null): string | null` used in T4 wrapper; `parseLocalDate` reused unchanged; `ExpiryDatePicker` prop names (`value`/`onChange`/`dataTestId`/`error`/`helperText`/`fullWidth`/`label`/`placeholder`) consistent across T4/T5/T6.
- **Version-sensitive spots flagged** with explicit "verify with tsc / context7" notes: adapter import path (T3) and `slotProps` clearable/htmlInput shape (T4).

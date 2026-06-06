# Keyboard-or-Pick Expiry Dates — Design

**Date:** 2026-06-06
**Status:** Approved (design); pending implementation plan

## Problem

Users gave feedback that they want to **type** expiry dates with the keyboard rather
than being forced through a picker. The app currently uses the native
`<input type="date">` for every expiry entry. On desktop that input is already typeable
(segmented MM/DD/YYYY fields), but on mobile — the primary target for this household PWA —
tapping it opens the OS picker with no way to just type the digits.

Goal: give users **both** options at every expiry-entry point — a typeable field *and* a
calendar picker — with one consistent control.

## Scope

Two — and only two — actual date-entry controls exist in the SPA:

1. **Composer expiry** — `components/composer/features/expiryFeature.tsx` (`ExpiryPanel`),
   reused for list-item and inventory-item creation/editing.
2. **Promotion sheet expiry** — `features/lists/promote/PromoteReviewSheet.tsx`
   (`PromoteRow`).

Everything else under the expiry grep (calendar page, summary cards, `getExpiryInfo`,
chips, colors) only **reads/formats** expiry and is out of scope.

## Approach

Replace both native `<input type="date">` controls with a single shared MUI X
`DatePicker`, which provides a typeable masked text field **and** a calendar popover
button in one component.

### 1. Dependencies (caret-minor, per the project pinning rule)

- `@mui/x-date-pickers` `^9.4.0` — verified current `latest`; its peer deps accept
  `@mui/material ^9` (the project's version), `date-fns ^4`, and React 19.
- `date-fns` `^4.4.0` — the date adapter.
  - Chosen over dayjs/luxon/moment: tree-shakeable pure functions, matches the existing
    `Intl` / `parseLocalDate` style in `dateUtils.ts`, no global mutation.

Run via `npm install` (adding deps), then verify the lockfile.

### 2. Localization (mirrors the recent native-calendar i18n fix)

- Wrap the app root in:

  ```tsx
  <LocalizationProvider dateAdapter={AdapterDateFns} adapterLocale={locale}>
  ```

  where `locale` is derived from the current `i18next` language (`de` → `date-fns/locale/de`,
  else `enUS` → `date-fns/locale/en-US`) and updates when the language switches.
- The adapter locale drives the field mask/format automatically:
  `dd.MM.yyyy` for German, `MM/dd/yyyy` for English — so typing matches each language's
  expectation. No explicit `format` prop needed unless a deviation is wanted.

### 3. Keep the `YYYY-MM-DD` string model (no UTC drift)

Data stays as `"YYYY-MM-DD"` strings end-to-end (DTOs, drafts, server). The DatePicker
operates on `Date | null`, so conversion happens **only** inside the shared wrapper:

- string → Date: reuse existing `parseLocalDate(value)` (local midnight — already
  UTC-safe).
- Date → string: add `formatIsoDate(date: Date): string` to `utils/dateUtils.ts` — local,
  zero-padded `YYYY-MM-DD`, a sibling of the existing `todayIsoDate()`. Guard against
  invalid dates (a partially-typed value yields an `Invalid Date`) by returning `null`
  upstream until the date parses.

### 4. One shared component: `ExpiryDatePicker`

New component (e.g. `components/composer/components/ExpiryDatePicker.tsx` or a shared
`components/` location — finalized in the plan) wrapping `DatePicker`:

- Props: `value: string | null`, `onChange: (v: string | null) => void`, `label?`,
  `disabled?`, `error?`, `helperText?`, `fullWidth?`, `dataTestId`.
- Converts string ↔ Date internally; emits `null` while the typed value is incomplete /
  invalid, and the ISO string once it parses.
- Forwards `dataTestId` and `error`/`helperText` via `slotProps.textField` so Playwright
  can target/type into the rendered field.
- `clearable` enabled (replaces a manual clear affordance).

Both call sites consume it:

- **`expiryFeature.tsx` `ExpiryPanel`** — replace the `TextField type="date"` with
  `ExpiryDatePicker`. **Remove the custom "Today" quick-button** (picker's calendar covers
  jumping to today) and the manual "Clear" button (replaced by the picker's `clearable`).
  Net: the panel simplifies to just the date control.
- **`PromoteReviewSheet.tsx` `PromoteRow`** — drop-in replace the `TextField type="date"`,
  preserving the existing error state (`expiryMissing`) and `helperText`
  (missing-date message vs. `getExpiryInfo` human-readable hint).

### 5. Provider wiring

Add `LocalizationProvider` at the app root (where the MUI `ThemeProvider` / i18n provider
live — confirmed in the plan, likely `main.tsx` or `App.tsx`). It must re-derive
`adapterLocale` from `i18next.language` so a language switch re-localizes open/future
pickers.

## Testing

- Integration tests currently type into / assert on the native input's `data-testid`. With
  MUI X the testid moves onto the rendered text field (`slotProps.textField`). The typeable
  field makes keyboard-entry IT **easier**, not harder.
- Re-verify the affected Playwright/Reqnroll steps (composer expiry, promotion sheet
  expiry). Per project rules: assert on testids/`data-*`, never translated text; the IT
  harness serves `ClientApp/build`, so rebuild the SPA after the React edits.
- Unit: add coverage for `formatIsoDate` (round-trips with `parseLocalDate`, handles
  invalid dates).
- Final gate: `npm run lint` + `npm run tsc` + prettier; `dotnet test Application/Frigorino.sln`
  (covers IT); `docker build` to catch SPA/Dockerfile drift.

## Out of scope

- The read-only expiry **calendar** page and its event/color code.
- All expiry **display/formatting** (`getExpiryInfo`, chips, summary cards).
- Any change to the wire format or backend DTOs.

## Open questions

None outstanding. (date-fns confirmed; custom "Today" button removed by decision.)

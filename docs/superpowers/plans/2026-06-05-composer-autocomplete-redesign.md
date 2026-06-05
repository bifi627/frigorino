# Composer Autocomplete Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the composer's autocomplete dropdown into a floating, rounded, green-bordered menu with contains-search + match highlighting, and replace the misleading red "duplicate" button state with calm, intent-specific affordances.

**Architecture:** The composer is a generic component (`components/composer/`) configured per feature by `ListFooter`/`InventoryFooter` through the `useItemComposer` hook. Three concerns change: (1) suggestion **matching** moves from prefix to substring; (2) the **dropdown** gets a custom floating surface + per-row highlight; (3) the **exact-match state** drops the `error`/red color and instead drives three intents — allow+warn (inventory), block+warn (active list item), restore (completed list item) — via a `tone` field on `DuplicateResult` plus a restore mode on `SendButton`.

**Tech Stack:** React 19, TypeScript, MUI `^9.0.1` (`Autocomplete` with `slotProps.paper`/`listbox`), i18next (en/de), Vite. No JS test runner exists in this repo (per `CLAUDE.md`) — frontend tasks verify via `npm run tsc`, `npm run lint`, `npm run build`, and a manual browser pass through dev-up.

---

## File Structure

- **Create** `Application/Frigorino.Web/ClientApp/src/components/composer/highlightMatch.tsx` — pure helper that bolds matched substrings in a suggestion label.
- **Modify** `src/components/composer/types.ts` — add `tone` to `DuplicateResult`.
- **Modify** `src/hooks/useItemComposer.ts` — `startsWith` → `includes`.
- **Modify** `src/components/composer/components/SendButton.tsx` — drop `duplicate`/`error`; add `restore` mode (amber + Replay icon).
- **Modify** `src/components/composer/components/ComposerTextField.tsx` — contains filter, floating dropdown surface, highlighted rows with right-aligned secondary info, toned helper text.
- **Modify** `src/components/composer/Composer.tsx` — derive `restore`/`tone`, amber pill border on any exact-match state, pass new props down.
- **Modify** `src/features/inventories/items/components/InventoryFooter.tsx` — allow duplicates (warn, no block).
- **Modify** `src/features/lists/items/components/ListFooter.tsx` — reworded messages + tone; keep restore-vs-block split.
- **Modify** `public/locales/en/translation.json` + `public/locales/de/translation.json` — new keys, remove dead `alreadyExists`.

---

### Task 1: Add `tone` to `DuplicateResult`

**Files:**
- Modify: `src/components/composer/types.ts:86-93`

- [ ] **Step 1: Add the `tone` field**

Replace the existing `DuplicateResult` interface (currently lines 86-93):

```ts
export interface DuplicateResult {
    /** Already-localized message shown inline under the field. */
    message: string;
    /** When true, send is disabled and the text completion is prevented. */
    block?: boolean;
    /** When set, hitting send fires this instead of completing (e.g. "uncheck existing"). */
    onResolve?: () => void;
    /**
     * Visual tone for the inline message and pill border. "warning" (orange) for
     * already-exists notices and blocks; "success" (green) for the resolvable
     * restore case. Defaults to "warning" when omitted.
     */
    tone?: "warning" | "success";
}
```

- [ ] **Step 2: Type-check**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc`
Expected: PASS (no errors; field is optional so existing callers still compile).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/types.ts
git commit -m "feat(composer): add tone to DuplicateResult"
```

---

### Task 2: Create the highlight helper

**Files:**
- Create: `src/components/composer/highlightMatch.tsx`

- [ ] **Step 1: Write the helper**

```tsx
import { Box } from "@mui/material";
import type { ReactNode } from "react";

/**
 * Splits `label` around every case-insensitive occurrence of `query` and bolds
 * the matched runs while muting the rest. Used by the suggestion dropdown so the
 * typed term is highlighted wherever it appears — start, middle, or end — to
 * match the "contains" search in useItemComposer. Returns the plain label when
 * the query is empty.
 */
export function highlightMatch(label: string, query: string): ReactNode {
    const needle = query.trim();
    if (!needle) {
        return label;
    }
    const lowerLabel = label.toLowerCase();
    const lowerNeedle = needle.toLowerCase();
    const parts: ReactNode[] = [];
    let cursor = 0;
    let key = 0;
    let index = lowerLabel.indexOf(lowerNeedle);
    while (index !== -1) {
        if (index > cursor) {
            parts.push(
                <Box component="span" key={key++} sx={{ color: "text.secondary" }}>
                    {label.slice(cursor, index)}
                </Box>,
            );
        }
        parts.push(
            <Box
                component="span"
                key={key++}
                sx={{ color: "text.primary", fontWeight: 600 }}
            >
                {label.slice(index, index + needle.length)}
            </Box>,
        );
        cursor = index + needle.length;
        index = lowerLabel.indexOf(lowerNeedle, cursor);
    }
    if (cursor < label.length) {
        parts.push(
            <Box component="span" key={key++} sx={{ color: "text.secondary" }}>
                {label.slice(cursor)}
            </Box>,
        );
    }
    return parts;
}
```

- [ ] **Step 2: Type-check**

Run from `ClientApp/`: `npm run tsc`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/highlightMatch.tsx
git commit -m "feat(composer): add substring highlight helper"
```

---

### Task 3: Switch suggestion matching to contains

**Files:**
- Modify: `src/hooks/useItemComposer.ts:45-61`

- [ ] **Step 1: Replace `startsWith` with `includes`**

In `useItemComposer.ts`, inside `suggestions.getItems`, change the filter predicate (currently line 51) from:

```ts
                            item.text.toLowerCase().startsWith(q),
```

to:

```ts
                            item.text.toLowerCase().includes(q),
```

- [ ] **Step 2: Type-check**

Run from `ClientApp/`: `npm run tsc`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/hooks/useItemComposer.ts
git commit -m "feat(composer): contains search for suggestions"
```

---

### Task 4: Rework SendButton (no red, add restore mode)

**Files:**
- Modify: `src/components/composer/components/SendButton.tsx` (whole file)

- [ ] **Step 1: Replace the component**

Replace the entire file contents:

```tsx
import { Replay, Send } from "@mui/icons-material";
import { IconButton } from "@mui/material";
import { useTranslation } from "react-i18next";

interface SendButtonProps {
    onClick: () => void;
    disabled: boolean;
    editing: boolean;
    /** Resolvable duplicate (e.g. re-add a completed list item) — shows a restore icon. */
    restore: boolean;
    title?: string;
}

export const SendButton = ({
    onClick,
    disabled,
    editing,
    restore,
    title,
}: SendButtonProps) => {
    const { t } = useTranslation();
    // Red is reserved for expiry status elsewhere; the composer never uses it.
    // Restore and edit both read as the amber "active" accent; plain add is primary.
    const color = editing || restore ? "warning" : "primary";
    const Icon = restore ? Replay : Send;
    const label = restore
        ? t("common.restore")
        : editing
          ? t("common.update")
          : t("common.add");
    return (
        <IconButton
            data-testid="autocomplete-input-submit-button"
            onClick={onClick}
            disabled={disabled}
            color={color}
            title={title ?? label}
            sx={{
                minWidth: 44,
                minHeight: 44,
                bgcolor: disabled ? "transparent" : `${color}.main`,
                color: disabled ? "action.disabled" : "common.white",
                "&:hover": {
                    bgcolor: disabled ? "transparent" : `${color}.dark`,
                },
                "&:disabled": {
                    bgcolor: "transparent",
                    color: "action.disabled",
                },
                transition: "all 0.2s ease",
            }}
        >
            <Icon />
        </IconButton>
    );
};

SendButton.displayName = "SendButton";
```

- [ ] **Step 2: Type-check (expect a Composer error)**

Run from `ClientApp/`: `npm run tsc`
Expected: FAIL — `Composer.tsx` still passes `duplicate={...}` and not `restore`. This is fixed in Task 6. (If you prefer a green build per commit, do Task 6 before committing this one — but they are split for review clarity.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/components/SendButton.tsx
git commit -m "feat(composer): restore mode + drop red on send button"
```

---

### Task 5: Redesign the dropdown + toned helper in ComposerTextField

**Files:**
- Modify: `src/components/composer/components/ComposerTextField.tsx` (whole file)

- [ ] **Step 1: Replace the component**

Replace the entire file contents:

```tsx
import {
    alpha,
    Autocomplete,
    Box,
    createFilterOptions,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { highlightMatch } from "../highlightMatch";
import type { Suggestion, SuggestionsConfig } from "../types";

interface ComposerTextFieldProps {
    text: string;
    onTextChange: (value: string) => void;
    onEnter: () => void;
    inputRef: React.RefObject<HTMLInputElement | null>;
    placeholder: string;
    disabled: boolean;
    /** Inline status under the field (e.g. duplicate notice). */
    message?: string;
    /** Color of the inline status: orange notice/block vs green restore. */
    messageTone?: "warning" | "success";
    suggestions?: SuggestionsConfig;
}

export const ComposerTextField = ({
    text,
    onTextChange,
    onEnter,
    inputRef,
    placeholder,
    disabled,
    message,
    messageTone = "warning",
    suggestions,
}: ComposerTextFieldProps) => {
    const { t } = useTranslation();
    const minChars = suggestions?.minChars ?? 3;

    // Default matchFrom is "any" (substring) — pairs with the contains filter in
    // useItemComposer so middle/end matches surface and get highlighted.
    const filter = useMemo(
        () =>
            createFilterOptions<Suggestion>({
                stringify: (option) => option.label,
                limit: 5,
            }),
        [],
    );

    const options = useMemo(
        () =>
            suggestions && text.trim().length >= minChars
                ? suggestions.getItems(text)
                : [],
        [suggestions, text, minChars],
    );

    const helperColor =
        messageTone === "success" ? "success.main" : "warning.main";

    return (
        <Box sx={{ flex: 1 }}>
            <Autocomplete
                data-testid="autocomplete-input-textfield"
                freeSolo
                options={options}
                getOptionLabel={(option) =>
                    typeof option === "string" ? option : option.label
                }
                filterOptions={(opts, params) =>
                    params.inputValue.trim().length < minChars
                        ? []
                        : filter(opts, params)
                }
                inputValue={text}
                onInputChange={(_, value, reason) => {
                    // Only react to real typing. MUI fires a "reset" event on
                    // mount (and on selection) that would otherwise wipe a
                    // seeded value when editing an item; selections are handled
                    // in onChange below.
                    if (reason === "input") {
                        onTextChange(value);
                    }
                }}
                onChange={(_, value) => {
                    if (value && typeof value !== "string") {
                        if (suggestions?.onSelect) {
                            suggestions.onSelect(value);
                        } else {
                            onTextChange(value.label);
                        }
                    }
                }}
                noOptionsText={
                    text.trim().length >= minChars
                        ? t("common.noMatchingItems")
                        : t("common.typeAtLeastCharacters")
                }
                renderOption={(props, option) => (
                    <Box
                        component="li"
                        {...props}
                        key={option.id}
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: 1,
                            mx: 0.5,
                            px: 1.5,
                            py: 1,
                            borderRadius: 2,
                            "&:hover, &.Mui-focused": {
                                bgcolor: (theme) =>
                                    alpha(theme.palette.primary.main, 0.14),
                            },
                        }}
                    >
                        <Box
                            sx={{
                                flex: 1,
                                minWidth: 0,
                                display: "flex",
                                alignItems: "center",
                                gap: 0.5,
                            }}
                        >
                            <Typography variant="body2" component="span" noWrap>
                                {highlightMatch(option.label, text)}
                            </Typography>
                            {option.badge}
                        </Box>
                        {option.secondaryLabel && (
                            <Typography
                                variant="caption"
                                sx={{
                                    color: "text.disabled",
                                    whiteSpace: "nowrap",
                                    flexShrink: 0,
                                }}
                            >
                                {option.secondaryLabel}
                            </Typography>
                        )}
                    </Box>
                )}
                renderInput={(params) => (
                    <TextField
                        {...params}
                        fullWidth
                        // Render as a single-row <textarea> rather than an
                        // <input>. Android keyboards (e.g. SwiftKey) only show
                        // their autofill toolbar (passwords/cards/addresses) for
                        // single-line <input> fields; a textarea is never treated
                        // as autofillable, so this suppresses that bar. Enter is
                        // already intercepted below to submit (preventDefault), so
                        // no newline is inserted.
                        multiline
                        minRows={1}
                        maxRows={1}
                        variant="outlined"
                        placeholder={placeholder}
                        disabled={disabled}
                        inputRef={inputRef}
                        helperText={message}
                        onKeyDown={(event) => {
                            if (event.key === "Enter" && !event.shiftKey) {
                                event.preventDefault();
                                event.stopPropagation();
                                onEnter();
                            }
                        }}
                        slotProps={{
                            ...params.slotProps,
                            input: {
                                ...params.slotProps.input,
                                sx: {
                                    "& .MuiOutlinedInput-notchedOutline": {
                                        border: "none",
                                    },
                                    "& .MuiInputBase-input": { py: 1 },
                                },
                            },
                            formHelperText: {
                                sx: { color: helperColor, ml: 1.5, mt: 0.25 },
                            },
                        }}
                        sx={{ "& .MuiOutlinedInput-root": { p: 0 } }}
                    />
                )}
                slotProps={{
                    paper: {
                        sx: {
                            mt: 0.75,
                            bgcolor: "background.default",
                            border: "1px solid",
                            borderColor: (theme) =>
                                alpha(theme.palette.primary.main, 0.55),
                            borderRadius: 3,
                            boxShadow: 8,
                            overflow: "hidden",
                        },
                    },
                    listbox: { sx: { py: 0.5 } },
                }}
                sx={{
                    "& .MuiAutocomplete-popupIndicator": { display: "none" },
                    "& .MuiAutocomplete-clearIndicator": { display: "none" },
                }}
            />
        </Box>
    );
};

ComposerTextField.displayName = "ComposerTextField";
```

- [ ] **Step 2: Type-check (expect a Composer error)**

Run from `ClientApp/`: `npm run tsc`
Expected: FAIL — `Composer.tsx` still passes `errorMessage` (renamed to `message`/`messageTone`) and `duplicate` on SendButton. Fixed in Task 6.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/components/ComposerTextField.tsx
git commit -m "feat(composer): floating dropdown, highlight, toned helper"
```

---

### Task 6: Wire Composer to new props (tone, restore, amber border)

**Files:**
- Modify: `src/components/composer/Composer.tsx:58-62`, `:264-271`, `:280-289`, `:333-340`

- [ ] **Step 1: Derive restore + tone alongside `blocked`**

Replace the `dup`/`blocked` block (currently lines 58-62):

```ts
    const dup = useMemo(
        () => (duplicate && trimmed ? duplicate.check(trimmed) : null),
        [duplicate, trimmed],
    );
    const blocked = Boolean(dup?.block);
    const restore = Boolean(dup?.onResolve);
    const messageTone = dup?.tone ?? "warning";
```

- [ ] **Step 2: Make the pill border amber on any exact-match state**

In the input-surface `Box` sx (currently lines 264-271), replace the `borderColor` and the hover `borderColor` so `dup` also triggers the warning border:

```ts
                        border: "1px solid",
                        borderColor:
                            isEditing || dup ? "warning.main" : "primary.200",
                        transition: "border-color 0.2s ease",
                        "&:hover, &:focus-within": {
                            borderColor:
                                isEditing || dup
                                    ? "warning.dark"
                                    : "primary.main",
                        },
```

- [ ] **Step 3: Pass `message`/`messageTone` to the field**

Replace the `<ComposerTextField ... errorMessage={dup?.message} ... />` props (currently lines 280-289):

```tsx
                    <ComposerTextField
                        text={text}
                        onTextChange={setText}
                        onEnter={completeText}
                        inputRef={inputRef}
                        placeholder={fieldPlaceholder}
                        disabled={disabled}
                        message={dup?.message}
                        messageTone={messageTone}
                        suggestions={suggestions}
                    />
```

- [ ] **Step 4: Pass `restore` to SendButton**

Replace the `<SendButton ... duplicate={Boolean(dup)} />` (currently lines 333-340):

```tsx
                <SendButton
                    onClick={completeText}
                    disabled={
                        !trimmed || disabled || blocked || !modifiersValid
                    }
                    editing={isEditing}
                    restore={restore}
                />
```

- [ ] **Step 5: Type-check**

Run from `ClientApp/`: `npm run tsc`
Expected: PASS — Tasks 4 and 5 errors now resolve.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/Composer.tsx
git commit -m "feat(composer): wire tone/restore state, amber border on duplicate"
```

---

### Task 7: Inventory allows duplicates (warn, no block)

**Files:**
- Modify: `src/features/inventories/items/components/InventoryFooter.tsx:65-71`

- [ ] **Step 1: Replace `onDuplicate`**

Replace the `onDuplicate` callback (currently lines 65-71). The `match` param is dropped (a no-arg function is assignable to the `(match) => DuplicateResult` slot), so no unused-var lint error:

```tsx
        const onDuplicate = useCallback(
            (): DuplicateResult => ({
                message: t("common.alreadyInInventory"),
                tone: "warning",
            }),
            [t],
        );
```

- [ ] **Step 2: Type-check**

Run from `ClientApp/`: `npm run tsc`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryFooter.tsx
git commit -m "feat(inventory): allow duplicate items with a warning"
```

---

### Task 8: List messages + tone

**Files:**
- Modify: `src/features/lists/items/components/ListFooter.tsx:78-92`

- [ ] **Step 1: Replace `onDuplicate`**

Replace the `onDuplicate` callback (currently lines 78-92):

```tsx
        const onDuplicate = useCallback(
            (match: ListItemResponse): DuplicateResult => {
                if (match.status && !editingItem) {
                    return {
                        message: t("common.alreadyCompletedRestore"),
                        onResolve: () => onUncheckExisting(match.id),
                        tone: "success",
                    };
                }
                return {
                    message: t("common.alreadyOnList"),
                    block: true,
                    tone: "warning",
                };
            },
            [editingItem, onUncheckExisting, t],
        );
```

- [ ] **Step 2: Type-check**

Run from `ClientApp/`: `npm run tsc`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListFooter.tsx
git commit -m "feat(list): reworded duplicate notices with tone"
```

---

### Task 9: i18n keys (en + de) and remove dead key

**Files:**
- Modify: `public/locales/en/translation.json` (the `common` object, near lines 47-52)
- Modify: `public/locales/de/translation.json` (the `common` object, near lines 47-52)

- [ ] **Step 1: Confirm `alreadyExists` is now unused**

Run from `ClientApp/`:
```bash
grep -rn "alreadyExists" src
```
Expected: no matches (Tasks 7 and 8 removed both usages). If any remain, do not delete the key in Step 3.

- [ ] **Step 2: Add new keys in `en/translation.json`**

In the `common` object, remove the `"alreadyExists": "already exists",` line and add these keys (keep JSON valid — mind trailing commas). Leave `"completed"` in place (used elsewhere in list UI):

```json
        "alreadyInInventory": "Already in your inventory",
        "alreadyOnList": "Already on your list",
        "alreadyCompletedRestore": "Already done — tap to add back to the list",
        "restore": "Restore",
```

- [ ] **Step 3: Add new keys in `de/translation.json`**

In the `common` object, remove the `"alreadyExists": "bereits vorhanden",` line and add (casual/informal voice, matching the app):

```json
        "alreadyInInventory": "Schon im Vorrat",
        "alreadyOnList": "Schon auf der Liste",
        "alreadyCompletedRestore": "Schon erledigt – tippen zum Zurückholen",
        "restore": "Zurückholen",
```

- [ ] **Step 4: Verify JSON validity + tsc**

Run from `ClientApp/`:
```bash
node -e "require('./public/locales/en/translation.json'); require('./public/locales/de/translation.json'); console.log('json ok')"
npm run tsc
```
Expected: `json ok` and tsc PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(i18n): composer duplicate/restore strings (en+de)"
```

---

### Task 10: Verification + manual pass

**Files:** none (verification only)

- [ ] **Step 1: Frontend gates**

Run from `ClientApp/`:
```bash
npm run lint
npm run tsc
npm run prettier
npm run build
```
Expected: all PASS; `build/` is produced (the integration harness serves `ClientApp/build`).

- [ ] **Step 2: Backend/IT suite**

Run from repo root:
```bash
dotnet test Application/Frigorino.sln
```
Expected: PASS. (No IT step asserts on the old red/blocked-button behavior — confirmed during planning — but run the full suite to catch any composer-driven regressions.)

- [ ] **Step 3: Manual browser verification (dev-up)**

Bring up the stack (`/dev-up`), open the SPA, and verify in a **list** and an **inventory**:
  1. Type ≥3 chars that match the **middle/end** of an existing item (e.g. add an item "Buttermilch", then type "milch") → it appears in the dropdown with "milch" bolded mid-word.
  2. Dropdown is a floating, rounded, green-bordered menu separated from the pill; rows show muted quantity/expiry on the right; no leading icons; hover is a green tint.
  3. **Inventory** — type the exact name of an existing item → orange pill border + orange "Schon im Vorrat" helper, **green enabled** send; pressing it adds a second copy.
  4. **List**, item present & unchecked → type its exact name → orange border + orange "Schon auf der Liste", send **disabled**.
  5. **List**, item present & checked off → type its exact name → amber border, **amber Replay** button + green "Schon erledigt …" helper; pressing it returns the item to the list.
  6. No red/error state appears anywhere in the composer.

- [ ] **Step 4: Final commit (if prettier reformatted anything)**

```bash
git add -A
git commit -m "chore(composer): formatting" || echo "nothing to format"
```

---

## Notes for the implementer

- **MUI version is `^9.0.1`.** Only `slotProps.paper`/`listbox`/`popper` are exposed on Autocomplete in the installed typings — there is no `popupIndicator`/`clearIndicator` slot, so the indicators stay hidden via the top-level `sx` selectors (kept from the original component).
- **`borderRadius: 3`** on the dropdown paper is chosen to match the composer pill (`Composer.tsx:261`), keeping the corners visually consistent regardless of the exact px the theme resolves.
- **No JS unit tests** — there is no frontend test runner in this repo. `highlightMatch` is a pure function; correctness is covered by `tsc` + the manual pass. Do not scaffold a test runner for this change.
- **`tone` defaults to "warning"** in both `Composer` and `ComposerTextField`, so any future `DuplicateResult` without an explicit tone renders orange.

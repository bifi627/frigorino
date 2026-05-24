# Composer Value Chips, Single-Panel State & Mobile Targets — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the composer's toggle-morphing value display with editable chips above the input, make feature panels mutually exclusive (one open at a time), and bring all input-row controls up to mobile touch-target size.

**Architecture:** Frontend-only change inside `ClientApp/src/components/composer/`. The generic `Composer` already owns layout; `useComposerState` owns text/values/panel state. We (1) collapse the per-feature `open` boolean map into a single `openId`, (2) add an optional `renderChip` hook to the modifier descriptor and render a chip row in `Composer`, and (3) move value display out of the toggles into chips. No backend, type-signature, or consumer-API changes — `ListFooter`/`InventoryFooter` get the new behavior for free.

**Tech Stack:** React 19, TypeScript, MUI v9, react-i18next. No frontend test runner exists (per CLAUDE.md), so verification per task is `npm run tsc` + `npm run lint`; the integration suite (`dotnet test Application/Frigorino.sln`) + manual `/dev-up` browser verification are the final gate. All frontend commands run from `Application/Frigorino.Web/ClientApp/`.

**Out of scope (design-only per the spec):** `pin()`/overflow `+` menu and the `ActionFeature`-owns-caption contract — not built here. `exclusive()` is dropped entirely. See `docs/superpowers/specs/2026-05-24-composer-feature-scalability-design.md`.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `src/components/composer/types.ts` | Public feature/prop types | Add optional `renderChip?` to `ModifierFeature` |
| `src/components/composer/hooks/useComposerState.ts` | text/values/panel state | `open` map → single `openId`; drop auto-open-on-seed; export `isModifierValueEmpty` |
| `src/components/composer/Composer.tsx` | Layout shell | Consume `openId`; render chip row; mobile-size the discard button |
| `src/components/composer/components/SendButton.tsx` | Send button | Mobile touch-target size |
| `src/components/composer/features/quantityFeature.tsx` | Quantity modifier | Icon-only toggle + highlight; add `renderChip`; add panel Clear; sizing |
| `src/components/composer/features/expiryFeature.tsx` | Expiry modifier | Icon-only toggle + highlight; add `renderChip`; sizing |

`ListFooter.tsx` / `InventoryFooter.tsx`: **no edits** — behavior changes through the composer internals; verified manually in Task 6.

---

## Task 1: Add `renderChip` to the modifier descriptor

**Files:**
- Modify: `src/components/composer/types.ts:13-21`

- [ ] **Step 1: Add the optional `renderChip` hook**

In `types.ts`, the `ModifierFeature` interface currently ends with `renderToggle` / `renderPanel`. Add `renderChip` after `renderPanel`:

```ts
/** A feature that augments a text completion with a typed value under its id. */
export interface ModifierFeature<Id extends string, V> {
    kind: "modifier";
    id: Id;
    initial: V;
    /** Optional emptiness test; used to decide whether a value renders a chip. */
    isEmpty?: (value: V) => boolean;
    renderToggle?: (slot: FeatureSlot<V>) => ReactNode;
    renderPanel?: (slot: FeatureSlot<V>) => ReactNode;
    /** Optional chip shown above the field when the value is non-empty; tapping it opens the panel. */
    renderChip?: (slot: FeatureSlot<V>) => ReactNode;
}
```

`ReactNode` is already imported at the top of the file. No other change.

- [ ] **Step 2: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc` then `npm run lint`
Expected: both clean (this is an additive optional field; nothing consumes it yet).

- [ ] **Step 3: Commit**

```bash
git add src/components/composer/types.ts
git commit -m "feat(composer): add optional renderChip hook to modifier descriptor"
```

---

## Task 2: Single-panel state (`openId`) + remove auto-open-on-seed

**Files:**
- Modify: `src/components/composer/hooks/useComposerState.ts` (whole file)

This collapses the `open: Record<string, boolean>` map into a single `openId: string | null` so only one panel is open at a time, removes the behavior that auto-opens panels for seeded values (those now surface as chips), and exports the emptiness helper for `Composer` to reuse.

- [ ] **Step 1: Rewrite `useComposerState.ts`**

Replace the entire file with:

```ts
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { AnyFeature, AnyModifierFeature } from "../types";

type ValuesMap = Record<string, unknown>;

interface InitialDraft {
    text?: string;
    values?: Record<string, unknown>;
}

interface UseComposerStateArgs {
    features: readonly AnyFeature[];
    initialDraft?: InitialDraft;
}

const onlyModifiers = (features: readonly AnyFeature[]): AnyModifierFeature[] =>
    features.filter((f): f is AnyModifierFeature => f.kind === "modifier");

export const isModifierValueEmpty = (
    feature: AnyModifierFeature,
    value: unknown,
): boolean => {
    if (feature.isEmpty) {
        return feature.isEmpty(value);
    }
    return value === undefined || value === null || value === "";
};

export function useComposerState({ features, initialDraft }: UseComposerStateArgs) {
    const modifiers = useMemo(() => onlyModifiers(features), [features]);

    const seedValues = useCallback((): ValuesMap => {
        const map: ValuesMap = {};
        for (const f of modifiers) {
            const seeded = initialDraft?.values?.[f.id];
            map[f.id] = seeded !== undefined ? seeded : f.initial;
        }
        return map;
    }, [modifiers, initialDraft]);

    const [text, setText] = useState<string>(() => initialDraft?.text ?? "");
    const [values, setValues] = useState<ValuesMap>(seedValues);
    const [openId, setOpenId] = useState<string | null>(null);
    const inputRef = useRef<HTMLInputElement>(null);

    // Re-seed text + values whenever a new draft object is supplied (e.g. editing a new item).
    // Seeded values surface as chips, so no panel is auto-opened.
    const draftRef = useRef<InitialDraft | undefined>(initialDraft);
    useEffect(() => {
        if (draftRef.current === initialDraft) {
            return;
        }
        draftRef.current = initialDraft;
        setText(initialDraft?.text ?? "");
        setValues(seedValues());
        setOpenId(null);
    }, [initialDraft, seedValues]);

    const setValue = useCallback((id: string, value: unknown) => {
        setValues((prev) => ({ ...prev, [id]: value }));
    }, []);

    const toggleOpen = useCallback((id: string) => {
        setOpenId((prev) => (prev === id ? null : id));
    }, []);

    const openPanel = useCallback((id: string) => {
        setOpenId(id);
    }, []);

    const focusInput = useCallback(() => {
        inputRef.current?.focus();
    }, []);

    const reset = useCallback(() => {
        setText("");
        const cleared: ValuesMap = {};
        for (const f of modifiers) {
            cleared[f.id] = f.initial;
        }
        setValues(cleared);
        setOpenId(null);
    }, [modifiers]);

    return {
        text,
        setText,
        values,
        setValue,
        openId,
        openPanel,
        toggleOpen,
        inputRef,
        focusInput,
        reset,
    };
}
```

Notes: the `modifiers` dependency dropped out of the effect because it is no longer read there; `seedValues` already depends on `modifiers`, so the effect stays correct.

- [ ] **Step 2: Update `Composer.tsx` to consume `openId`**

In `src/components/composer/Composer.tsx`, the hook is destructured around line 35. Change `open, toggleOpen` to `openId, openPanel, toggleOpen`:

```tsx
    const { text, setText, values, setValue, openId, openPanel, toggleOpen, inputRef, focusInput, reset } =
        useComposerState({ features: featureList, initialDraft });
```

Then update `slotFor` (around line 114) — replace `open: Boolean(open[feature.id])` with `open: openId === feature.id`:

```tsx
    const slotFor = (feature: AnyModifierFeature): FeatureSlot<unknown> => ({
        value: values[feature.id],
        setValue: (value) => setValue(feature.id, value),
        open: openId === feature.id,
        toggleOpen: () => toggleOpen(feature.id),
        disabled,
    });
```

And update the panel `Collapse` (around line 150) — replace `in={Boolean(open[feature.id])}` with `in={openId === feature.id}`:

```tsx
            {modifierFeatures.map((feature) =>
                feature.renderPanel ? (
                    <Collapse
                        key={feature.id}
                        className="composer-panel"
                        in={openId === feature.id}
                    >
                        <Box sx={{ mb: 0.5 }}>{feature.renderPanel(slotFor(feature))}</Box>
                    </Collapse>
                ) : null,
            )}
```

`openPanel` is destructured now but unused until Task 3 — that is a lint error (`@typescript-eslint/no-unused-vars`). To keep this step's commit clean, add a temporary reference: it is wired for real in Task 3, so **do Step 2 and Task 3's chip row in the same working session and run lint once at the end of Task 3.** If committing Task 2 alone, instead omit `openPanel` from the destructure here and add it in Task 3.

> Practical guidance: omit `openPanel` from the destructure in this task (keep `openId, toggleOpen`), and add `openPanel` to the destructure in Task 3 when the chip row that uses it lands. This keeps every commit lint-clean.

So for Task 2, the destructure is:

```tsx
    const { text, setText, values, setValue, openId, toggleOpen, inputRef, focusInput, reset } =
        useComposerState({ features: featureList, initialDraft });
```

- [ ] **Step 3: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc` then `npm run lint`
Expected: both clean. Behavior now: opening one panel closes any other; editing an item no longer auto-opens panels (values not yet visible as chips until Task 3 — interim state).

- [ ] **Step 4: Commit**

```bash
git add src/components/composer/hooks/useComposerState.ts src/components/composer/Composer.tsx
git commit -m "feat(composer): make feature panels mutually exclusive via single openId"
```

---

## Task 3: Chip row in `Composer` + mobile-size the discard button

**Files:**
- Modify: `src/components/composer/Composer.tsx`

- [ ] **Step 1: Import the emptiness helper**

At the top of `Composer.tsx`, the hook is imported on line 8 (`import { useComposerState } from "./hooks/useComposerState";`). Change it to also import the helper:

```tsx
import { isModifierValueEmpty, useComposerState } from "./hooks/useComposerState";
```

- [ ] **Step 2: Add `openPanel` back to the destructure**

```tsx
    const { text, setText, values, setValue, openId, openPanel, toggleOpen, inputRef, focusInput, reset } =
        useComposerState({ features: featureList, initialDraft });
```

- [ ] **Step 3: Render the chip row**

Insert the chip row immediately after the `EditHeader` block (after the `{isEditing && editing && (...)}` block, before the `{modifierFeatures.map(... renderPanel)}` Collapse block):

```tsx
            {modifierFeatures.some(
                (feature) =>
                    feature.renderChip &&
                    openId !== feature.id &&
                    !isModifierValueEmpty(feature, values[feature.id]),
            ) && (
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5, mb: 0.5 }}>
                    {modifierFeatures.map((feature) => {
                        if (
                            !feature.renderChip ||
                            openId === feature.id ||
                            isModifierValueEmpty(feature, values[feature.id])
                        ) {
                            return null;
                        }
                        return (
                            <Box
                                key={feature.id}
                                className="composer-panel"
                                role="button"
                                onClick={() => openPanel(feature.id)}
                                sx={{ cursor: "pointer", display: "inline-flex", alignItems: "center" }}
                            >
                                {feature.renderChip(slotFor(feature))}
                            </Box>
                        );
                    })}
                </Box>
            )}
```

Notes: chips are hidden for a feature whose panel is currently open (`openId === feature.id`) to avoid duplicating the live editor. The `composer-panel` class keeps the existing `handleContainerClick` (line 92) from stealing focus when a chip is tapped.

- [ ] **Step 4: Mobile-size the discard button**

The discard `IconButton` (around line 162) currently has no min size. Add `minWidth`/`minHeight` to its `sx`:

```tsx
                {trimmed && (
                    <IconButton
                        onClick={handleDiscard}
                        title={t("common.discardInput")}
                        sx={{
                            minWidth: 44,
                            minHeight: 44,
                            color: "text.secondary",
                            bgcolor: "action.hover",
                            "&:hover": { color: "error.main", bgcolor: "error.50" },
                        }}
                    >
                        <Delete />
                    </IconButton>
                )}
```

- [ ] **Step 5: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc` then `npm run lint`
Expected: both clean. No chips render yet (no feature defines `renderChip`), but the wiring compiles.

- [ ] **Step 6: Commit**

```bash
git add src/components/composer/Composer.tsx
git commit -m "feat(composer): render editable value chips above the input"
```

---

## Task 4: Quantity feature — icon-only toggle, chip, panel Clear

**Files:**
- Modify: `src/components/composer/features/quantityFeature.tsx` (whole file)

- [ ] **Step 1: Rewrite `quantityFeature.tsx`**

Replace the entire file with (toggle no longer shows the value; new `QuantityChip`; panel gains a Clear button; all icon buttons sized ≥44):

```tsx
/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { Add, Clear, Remove, ShoppingBag } from "@mui/icons-material";
import { Box, Button, ButtonGroup, Chip, IconButton, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const QuantityToggle = ({ value, open, toggleOpen }: FeatureSlot<string>) => (
    <IconButton
        onClick={toggleOpen}
        sx={{
            minWidth: 44,
            minHeight: 44,
            color: value || open ? "primary.main" : "inherit",
        }}
    >
        <ShoppingBag fontSize="small" />
    </IconButton>
);

const QuantityChip = ({ value }: FeatureSlot<string>) => (
    <Chip
        size="small"
        icon={<ShoppingBag fontSize="small" />}
        label={value}
        sx={{ minHeight: 32 }}
    />
);

const QuantityPanel = ({ value, setValue, disabled }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    return (
        <Box
            sx={{ display: "flex", gap: 0.75, alignItems: "center", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <TextField
                fullWidth
                variant="outlined"
                placeholder={t("common.quantity")}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                disabled={disabled}
                size="small"
            />
            <ButtonGroup variant="outlined" size="small">
                {[1, 2, 5].map((num) => (
                    <Button
                        key={num}
                        onClick={() => setValue(num.toString())}
                        variant={value === num.toString() ? "contained" : "outlined"}
                        size="small"
                        sx={{ minWidth: 44, minHeight: 44 }}
                    >
                        {num}
                    </Button>
                ))}
            </ButtonGroup>
            <IconButton
                onClick={() => {
                    const current = parseInt(value) || 0;
                    if (current > 0) setValue((current - 1).toString());
                }}
                disabled={!value || parseInt(value) <= 0}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Remove fontSize="small" />
            </IconButton>
            <IconButton
                onClick={() => {
                    const current = parseInt(value) || 0;
                    setValue((current + 1).toString());
                }}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Add fontSize="small" />
            </IconButton>
            <IconButton
                onClick={() => setValue("")}
                disabled={!value}
                title={t("common.clear")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Clear fontSize="small" />
            </IconButton>
        </Box>
    );
};

export const quantityFeature = defineModifier({
    id: "quantity",
    initial: "" as string,
    isEmpty: (value) => value.trim() === "",
    renderToggle: (slot) => <QuantityToggle {...slot} />,
    renderPanel: (slot) => <QuantityPanel {...slot} />,
    renderChip: (slot) => <QuantityChip {...slot} />,
});
```

Notes: `Typography` is no longer used (the value moved to the chip) and is dropped from the import. `Clear` and `Chip` are added.

- [ ] **Step 2: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc` then `npm run lint`
Expected: both clean. The quantity toggle now shows just the bag icon (highlighted when set/open); a set quantity shows as a chip above the field; the panel has a Clear button.

- [ ] **Step 3: Commit**

```bash
git add src/components/composer/features/quantityFeature.tsx
git commit -m "feat(composer): quantity value as editable chip with panel clear"
```

---

## Task 5: Expiry feature — icon-only toggle, chip

**Files:**
- Modify: `src/components/composer/features/expiryFeature.tsx` (whole file)

- [ ] **Step 1: Rewrite `expiryFeature.tsx`**

Replace the entire file with (toggle icon-only + highlight; new `ExpiryChip` reusing `formatForDisplay`; panel already had Clear — buttons sized ≥44):

```tsx
/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { CalendarToday, Clear, Today } from "@mui/icons-material";
import { Box, Chip, IconButton, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const formatForDisplay = (date: Date | null) =>
    date ? date.toLocaleDateString("de-DE", { day: "2-digit", month: "2-digit" }) : "";

const formatForInput = (date: Date | null) =>
    date ? date.toISOString().split("T")[0] : "";

const ExpiryToggle = ({ value, open, toggleOpen }: FeatureSlot<Date | null>) => (
    <IconButton
        onClick={toggleOpen}
        sx={{
            minWidth: 44,
            minHeight: 44,
            color: value || open ? "primary.main" : "inherit",
        }}
    >
        <CalendarToday fontSize="small" />
    </IconButton>
);

const ExpiryChip = ({ value }: FeatureSlot<Date | null>) => (
    <Chip
        size="small"
        icon={<CalendarToday fontSize="small" />}
        label={formatForDisplay(value)}
        sx={{ minHeight: 32 }}
    />
);

const ExpiryPanel = ({ value, setValue, disabled }: FeatureSlot<Date | null>) => {
    const { t } = useTranslation();
    const handleChange = (dateString: string) => {
        if (!dateString) {
            setValue(null);
            return;
        }
        const date = new Date(dateString);
        setValue(isNaN(date.getTime()) ? null : date);
    };
    return (
        <Box
            sx={{ display: "flex", alignItems: "center", gap: 0.75, width: "100%", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <TextField
                fullWidth
                variant="outlined"
                placeholder={t("common.date")}
                type="date"
                value={formatForInput(value)}
                onChange={(e) => handleChange(e.target.value)}
                disabled={disabled}
                size="small"
            />
            <IconButton
                onClick={() => setValue(new Date())}
                title={t("common.setToday")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Today fontSize="small" />
            </IconButton>
            <IconButton
                onClick={() => setValue(null)}
                disabled={!value}
                title={t("common.clear")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Clear fontSize="small" />
            </IconButton>
        </Box>
    );
};

export const expiryFeature = defineModifier({
    id: "expiry",
    initial: null as Date | null,
    isEmpty: (value) => value === null,
    renderToggle: (slot) => <ExpiryToggle {...slot} />,
    renderPanel: (slot) => <ExpiryPanel {...slot} />,
    renderChip: (slot) => <ExpiryChip {...slot} />,
});
```

Notes: `Typography` dropped from the import (value moved to the chip); `Chip` added. `formatForDisplay` is now shared by the chip.

- [ ] **Step 2: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc` then `npm run lint`
Expected: both clean.

- [ ] **Step 3: Commit**

```bash
git add src/components/composer/features/expiryFeature.tsx
git commit -m "feat(composer): expiry value as editable chip, icon-only toggle"
```

---

## Task 6: Send button sizing + full verification

**Files:**
- Modify: `src/components/composer/components/SendButton.tsx:29-35`

- [ ] **Step 1: Mobile-size the send button**

In `SendButton.tsx`, add `minWidth`/`minHeight` to the `IconButton` `sx`:

```tsx
            sx={{
                minWidth: 44,
                minHeight: 44,
                bgcolor: disabled ? "transparent" : `${color}.main`,
                color: disabled ? "action.disabled" : "common.white",
                "&:hover": { bgcolor: disabled ? "transparent" : `${color}.dark` },
                "&:disabled": { bgcolor: "transparent", color: "action.disabled" },
                transition: "all 0.2s ease",
            }}
```

- [ ] **Step 2: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc` then `npm run lint`
Expected: both clean.

- [ ] **Step 3: Run the integration suite (regression gate)**

Run (from repo root): `dotnet test Application/Frigorino.sln`
Expected: green. The list/inventory add/edit/duplicate/undo Playwright flows assert on `data-testid="autocomplete-input-textfield"` / `autocomplete-input-submit-button`, which are unchanged — they must still pass. (If a known-flaky undo/toast test fails, re-run it in isolation to confirm it is unrelated.)

- [ ] **Step 4: Manual browser verification at mobile width**

Bring up the stack (`/dev-up`) and drive it with Playwright MCP at ~390px width. Verify for **both** the list page and the inventory page:
- Type text + set a quantity → quantity shows as a chip above the field; toggle icon highlighted; only the bag icon (no number) in the toggle.
- Tap the quantity chip → its panel reopens with the value; tap Clear in the panel → chip disappears.
- (Inventory) set an expiry date → date chip appears; open the quantity panel, then the expiry panel → opening the second closes the first (one panel at a time).
- Edit an existing item with quantity/expiry → values appear as chips, no panel auto-opens.
- Tap targets (toggles, send, discard, chips, panel buttons) are comfortable to hit at mobile width.
- Send / update / duplicate-resolve / discard still behave as before.

Report what was verified; if anything can't be checked, say so explicitly (don't claim success).

- [ ] **Step 5: Commit**

```bash
git add src/components/composer/components/SendButton.tsx
git commit -m "feat(composer): mobile touch-target size for send button"
```

---

## Self-Review

**Spec coverage:**
- Build-now #1 (value chips, tap-to-edit, clear-in-panel) → Tasks 1, 3, 4, 5. ✓
- Build-now #2 (one panel at a time) → Task 2. ✓
- Build-now #3 (mobile touch targets) → discard (Task 3), panel buttons (Tasks 4/5), send (Task 6), toggles (Tasks 4/5). ✓
- `renderChip` descriptor hook → Task 1. ✓
- Consumer migration (no edits, behavior-only) → verified in Task 6 Step 4. ✓
- Out of scope (`pin`/overflow/`ActionFeature` caption/`exclusive()`) → not present in any task. ✓

**Type consistency:** `openId` / `openPanel` / `toggleOpen` names match between `useComposerState.ts` (Task 2) and `Composer.tsx` (Tasks 2, 3). `isModifierValueEmpty` exported in Task 2, imported in Task 3. `renderChip` signature `(slot: FeatureSlot<V>) => ReactNode` matches between Task 1 (type) and Tasks 4/5 (usage). `FeatureSlot` shape (`value/setValue/open/toggleOpen/disabled`) is unchanged, so feature render functions keep their existing signatures.

**Placeholder scan:** No TBD/TODO; every code step shows full code; commands and expected outcomes are explicit. The one cross-task ordering subtlety (`openPanel` unused between Task 2 and Task 3) is resolved by omitting it from the Task 2 destructure and adding it in Task 3 — both commits stay lint-clean.

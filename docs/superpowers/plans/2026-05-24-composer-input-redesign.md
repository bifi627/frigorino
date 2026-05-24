# Composer Input Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the list/item-coupled `AddInput` with a generic, reusable WhatsApp-style `Composer` whose features are self-contained descriptors and whose completion is a typed discriminated union; migrate `ListFooter` and `InventoryFooter` to it and delete the old component.

**Architecture:** A generic `Composer<F>` owns the text + per-feature state and emits one typed `onComplete` union. Features are descriptors (`defineModifier` / `defineAction`) that each bundle their toggle button, panel, state shape, and contribution to the payload. Three opt-in, consumer-driven built-ins (edit mode, autocomplete suggestions, duplicate detection) keep the core free of any list/API knowledge.

**Tech Stack:** React 19, TypeScript 6, MUI v9, react-i18next, Vite. Spec: `docs/superpowers/specs/2026-05-24-composer-input-redesign-design.md`.

---

## Verification model (read first)

This repo has **no frontend unit-test runner** (per `CLAUDE.md`; the Jest mention in `knowledge/Frontend_Architecture.md` is aspirational). Do **not** invent one. The per-task verification harness is therefore:

- `npm run tsc` — the typed `onComplete` payload is the primary correctness surface, so type-checking carries real weight.
- `npm run lint` — `eslint .`; warnings do not fail it, but fix any **errors**.

All frontend commands run from `Application/Frigorino.Web/ClientApp`. Behavior preservation is gated at the end by the existing Reqnroll/Playwright integration suite (`dotnet test Application/Frigorino.sln`) plus manual Playwright-MCP verification via `/dev-up`. Tasks follow **build → type-check/lint → commit**, not red-green unit TDD.

Commits must not include Co-Authored-By / "Generated with" trailers (repo `attribution` is empty by design).

## File structure

Create under `src/components/composer/`:

| File | Responsibility |
|---|---|
| `types.ts` | Public types: feature descriptors, `FeatureSlot`, the inferred `Completion` union, `ComposerProps`, suggestion/duplicate/editing config. |
| `defineFeature.ts` | `defineModifier` / `defineAction` helpers that capture literal `id` + value/payload types. |
| `hooks/useComposerState.ts` | Owns text, per-feature value map, per-feature open map; seeding from `initialDraft`; reset. |
| `components/EditHeader.tsx` | Generic edit-mode header (icon + label + close). |
| `components/SendButton.tsx` | Send/update icon button (keeps `autocomplete-input-submit-button` testid). |
| `components/ComposerTextField.tsx` | The text input (MUI Autocomplete, keeps `autocomplete-input-textfield` testid); renders suggestions; shows duplicate error inline. |
| `Composer.tsx` | The shell: edit header + feature panels + input row (discard · toggles · actions · text field · send); assembles + emits the typed completion. |
| `features/quantityFeature.tsx` | Self-contained quantity modifier (toggle + panel UI moved from old `QuantityPanel`). |
| `features/expiryFeature.tsx` | Self-contained expiry-date modifier (UI moved from old `DateInputPanel`). |
| `index.ts` | Barrel: `Composer`, `defineModifier`/`defineAction`, the two features, public types. |

Modify:
- `src/features/lists/items/components/ListFooter.tsx`
- `src/features/inventories/items/components/InventoryFooter.tsx`
- `public/locales/en/translation.json`, `public/locales/de/translation.json`

Delete (after migration): `src/components/inputs/` (entire folder).

---

## Task 1: Composer types + define helpers

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/types.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/defineFeature.ts`

- [ ] **Step 1: Write `types.ts`**

```ts
import type { ReactNode } from "react";

/** State handed to a modifier feature's render functions. */
export interface FeatureSlot<V> {
    value: V;
    setValue: (value: V) => void;
    open: boolean;
    toggleOpen: () => void;
    disabled: boolean;
}

/** A feature that augments a text completion with a typed value under its id. */
export interface ModifierFeature<Id extends string, V> {
    kind: "modifier";
    id: Id;
    initial: V;
    /** Optional emptiness test; used to decide whether a seeded value opens its panel. */
    isEmpty?: (value: V) => boolean;
    renderToggle?: (slot: FeatureSlot<V>) => ReactNode;
    renderPanel?: (slot: FeatureSlot<V>) => ReactNode;
}

/** Context handed to an action feature's trigger. */
export interface ActionContext<P> {
    complete: (payload: P) => void;
    disabled: boolean;
}

/** A feature that IS its own completion (e.g. pick a document). */
export interface ActionFeature<Id extends string, P> {
    kind: "action";
    id: Id;
    renderTrigger: (ctx: ActionContext<P>) => ReactNode;
}

/* eslint-disable @typescript-eslint/no-explicit-any -- type-level only: `any` lets a
   heterogeneous features array hold differently-typed features. The real value/payload
   types are recovered by inference in Completion below, so consumers never see `any`. */
export type AnyModifierFeature = ModifierFeature<string, any>;
export type AnyActionFeature = ActionFeature<string, any>;
/* eslint-enable @typescript-eslint/no-explicit-any */

export type AnyFeature = AnyModifierFeature | AnyActionFeature;

/** Map of each modifier feature's id -> its value type. */
export type ModifierValues<F extends readonly AnyFeature[]> = {
    [M in Extract<F[number], AnyModifierFeature> as M["id"]]: M extends ModifierFeature<
        string,
        infer V
    >
        ? V
        : never;
};

/** The text-send completion: text + mode + all modifier values. */
export type TextCompletion<F extends readonly AnyFeature[]> = {
    kind: "text";
    mode: "create" | "edit";
    text: string;
} & ModifierValues<F>;

/** One completion variant per action feature. */
export type ActionCompletion<A extends AnyActionFeature> =
    A extends ActionFeature<infer Id, infer P> ? { kind: Id } & P : never;

/** Full discriminated-union completion for a features tuple. */
export type Completion<F extends readonly AnyFeature[]> =
    | TextCompletion<F>
    | ActionCompletion<Extract<F[number], AnyActionFeature>>;

export interface Suggestion {
    id: string | number;
    label: string;
    secondaryLabel?: string;
    badge?: ReactNode;
}

export interface SuggestionsConfig {
    getItems: (query: string) => Suggestion[];
    minChars?: number;
    onSelect?: (suggestion: Suggestion) => void;
}

export interface DuplicateResult {
    /** Already-localized message shown inline under the field. */
    message: string;
    /** When true, send is disabled and the text completion is prevented. */
    block?: boolean;
    /** When set, hitting send fires this instead of completing (e.g. "uncheck existing"). */
    onResolve?: () => void;
}

export interface DuplicateConfig {
    check: (text: string) => DuplicateResult | null;
}

export interface EditingConfig {
    active: boolean;
    onCancel: () => void;
    label?: string;
}

export interface ComposerProps<F extends readonly AnyFeature[]> {
    features?: F;
    onComplete: (completion: Completion<F>) => void;
    placeholder?: string;
    disabled?: boolean;
    editing?: EditingConfig;
    initialDraft?: { text?: string; values?: Partial<ModifierValues<F>> };
    suggestions?: SuggestionsConfig;
    duplicate?: DuplicateConfig;
}
```

- [ ] **Step 2: Write `defineFeature.ts`**

```ts
import type { ActionFeature, ModifierFeature } from "./types";

export function defineModifier<const Id extends string, V>(
    feature: Omit<ModifierFeature<Id, V>, "kind">,
): ModifierFeature<Id, V> {
    return { ...feature, kind: "modifier" };
}

export function defineAction<const Id extends string, P>(
    feature: Omit<ActionFeature<Id, P>, "kind">,
): ActionFeature<Id, P> {
    return { ...feature, kind: "action" };
}
```

- [ ] **Step 3: Type-check + lint**

Run (from `Application/Frigorino.Web/ClientApp`): `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/types.ts Application/Frigorino.Web/ClientApp/src/components/composer/defineFeature.ts
git commit -m "feat(composer): add typed feature descriptors and completion types"
```

---

## Task 2: Composer state hook

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/hooks/useComposerState.ts`

- [ ] **Step 1: Write `useComposerState.ts`**

```ts
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { AnyFeature, AnyModifierFeature } from "../types";

type ValuesMap = Record<string, unknown>;
type OpenMap = Record<string, boolean>;

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

const isValueEmpty = (feature: AnyModifierFeature, value: unknown): boolean => {
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
    const [open, setOpen] = useState<OpenMap>({});
    const inputRef = useRef<HTMLInputElement>(null);

    // Re-seed text + values whenever a new draft object is supplied (e.g. editing a new item).
    const draftRef = useRef<InitialDraft | undefined>(initialDraft);
    useEffect(() => {
        if (draftRef.current === initialDraft) {
            return;
        }
        draftRef.current = initialDraft;
        setText(initialDraft?.text ?? "");
        const nextValues = seedValues();
        setValues(nextValues);
        const nextOpen: OpenMap = {};
        for (const f of modifiers) {
            if (!isValueEmpty(f, nextValues[f.id])) {
                nextOpen[f.id] = true;
            }
        }
        setOpen(nextOpen);
    }, [initialDraft, seedValues, modifiers]);

    const setValue = useCallback((id: string, value: unknown) => {
        setValues((prev) => ({ ...prev, [id]: value }));
    }, []);

    const toggleOpen = useCallback((id: string) => {
        setOpen((prev) => ({ ...prev, [id]: !prev[id] }));
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
        setOpen({});
    }, [modifiers]);

    return {
        text,
        setText,
        values,
        setValue,
        open,
        toggleOpen,
        inputRef,
        focusInput,
        reset,
    };
}
```

- [ ] **Step 2: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors (an `exhaustive-deps` *warning* is acceptable).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/hooks/useComposerState.ts
git commit -m "feat(composer): add composer state hook with draft seeding and reset"
```

---

## Task 3: Presentational components

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/components/EditHeader.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/components/SendButton.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/components/ComposerTextField.tsx`

- [ ] **Step 1: Write `EditHeader.tsx`**

```tsx
import { Close, Edit } from "@mui/icons-material";
import { Box, IconButton, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

interface EditHeaderProps {
    label?: string;
    onCancel: () => void;
}

export const EditHeader = ({ label, onCancel }: EditHeaderProps) => {
    const { t } = useTranslation();
    return (
        <Box
            sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                px: 0.5,
                py: 0.25,
                bgcolor: "warning.50",
                borderRadius: 1,
                mb: 0.5,
            }}
        >
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <Edit fontSize="small" color="warning" />
                <Typography variant="caption" sx={{ color: "warning.dark" }}>
                    {label ?? t("common.edit")}
                </Typography>
            </Box>
            <IconButton
                size="small"
                onClick={onCancel}
                aria-label={t("common.cancel")}
                sx={{ color: "warning.dark", "&:hover": { bgcolor: "warning.100" } }}
            >
                <Close fontSize="small" />
            </IconButton>
        </Box>
    );
};

EditHeader.displayName = "EditHeader";
```

- [ ] **Step 2: Write `SendButton.tsx`**

```tsx
import { Send } from "@mui/icons-material";
import { IconButton } from "@mui/material";
import { useTranslation } from "react-i18next";

interface SendButtonProps {
    onClick: () => void;
    disabled: boolean;
    editing: boolean;
    duplicate: boolean;
    title?: string;
}

export const SendButton = ({
    onClick,
    disabled,
    editing,
    duplicate,
    title,
}: SendButtonProps) => {
    const { t } = useTranslation();
    const color = duplicate ? "error" : editing ? "warning" : "primary";
    return (
        <IconButton
            data-testid="autocomplete-input-submit-button"
            onClick={onClick}
            disabled={disabled}
            color={color}
            title={title ?? (editing ? t("common.update") : t("common.add"))}
            sx={{
                bgcolor: disabled ? "transparent" : `${color}.main`,
                color: disabled ? "action.disabled" : "common.white",
                "&:hover": { bgcolor: disabled ? "transparent" : `${color}.dark` },
                "&:disabled": { bgcolor: "transparent", color: "action.disabled" },
                transition: "all 0.2s ease",
            }}
        >
            <Send />
        </IconButton>
    );
};

SendButton.displayName = "SendButton";
```

- [ ] **Step 3: Write `ComposerTextField.tsx`**

```tsx
import {
    Autocomplete,
    Box,
    createFilterOptions,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { Suggestion, SuggestionsConfig } from "../types";

interface ComposerTextFieldProps {
    text: string;
    onTextChange: (value: string) => void;
    onEnter: () => void;
    inputRef: React.RefObject<HTMLInputElement | null>;
    placeholder: string;
    disabled: boolean;
    errorMessage?: string;
    suggestions?: SuggestionsConfig;
}

export const ComposerTextField = ({
    text,
    onTextChange,
    onEnter,
    inputRef,
    placeholder,
    disabled,
    errorMessage,
    suggestions,
}: ComposerTextFieldProps) => {
    const { t } = useTranslation();
    const minChars = suggestions?.minChars ?? 3;

    const filter = useMemo(
        () =>
            createFilterOptions<Suggestion>({
                stringify: (option) => option.label,
                matchFrom: "start",
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
                onInputChange={(_, value) => onTextChange(value)}
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
                    <Box component="li" {...props} key={option.id}>
                        <Box
                            sx={{
                                display: "flex",
                                flexDirection: "column",
                                width: "100%",
                            }}
                        >
                            <Typography variant="body2">
                                {option.label}
                                {option.badge}
                            </Typography>
                            {option.secondaryLabel && (
                                <Typography
                                    variant="caption"
                                    sx={{ color: "text.secondary" }}
                                >
                                    {option.secondaryLabel}
                                </Typography>
                            )}
                        </Box>
                    </Box>
                )}
                renderInput={(params) => (
                    <TextField
                        {...params}
                        fullWidth
                        variant="outlined"
                        placeholder={placeholder}
                        disabled={disabled}
                        inputRef={inputRef}
                        error={Boolean(errorMessage)}
                        helperText={errorMessage}
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
                        }}
                        sx={{ "& .MuiOutlinedInput-root": { p: 0 } }}
                    />
                )}
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

- [ ] **Step 4: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/components
git commit -m "feat(composer): add edit header, send button, and text field components"
```

---

## Task 4: Composer shell + barrel

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/Composer.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/index.ts`

- [ ] **Step 1: Write `Composer.tsx`**

```tsx
import { Delete } from "@mui/icons-material";
import { Box, Collapse, IconButton, Paper } from "@mui/material";
import { useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { ComposerTextField } from "./components/ComposerTextField";
import { EditHeader } from "./components/EditHeader";
import { SendButton } from "./components/SendButton";
import { useComposerState } from "./hooks/useComposerState";
import type {
    AnyActionFeature,
    AnyFeature,
    AnyModifierFeature,
    Completion,
    ComposerProps,
    FeatureSlot,
} from "./types";

export function Composer<const F extends readonly AnyFeature[] = []>({
    features,
    onComplete,
    placeholder,
    disabled = false,
    editing,
    initialDraft,
    suggestions,
    duplicate,
}: ComposerProps<F>) {
    const { t } = useTranslation();

    const featureList = useMemo<readonly AnyFeature[]>(
        () => features ?? [],
        [features],
    );

    const { text, setText, values, setValue, open, toggleOpen, inputRef, focusInput, reset } =
        useComposerState({ features: featureList, initialDraft });

    const isEditing = editing?.active ?? false;
    const trimmed = text.trim();

    const dup = useMemo(
        () => (duplicate && trimmed ? duplicate.check(trimmed) : null),
        [duplicate, trimmed],
    );
    const blocked = Boolean(dup?.block);

    const completeText = useCallback(() => {
        if (!trimmed) {
            return;
        }
        if (dup) {
            if (dup.onResolve) {
                dup.onResolve();
                reset();
                requestAnimationFrame(focusInput);
                return;
            }
            if (dup.block) {
                return;
            }
        }
        const completion = {
            kind: "text",
            mode: isEditing ? "edit" : "create",
            text: trimmed,
            ...values,
        } as Completion<F>;
        onComplete(completion);
        reset();
        requestAnimationFrame(focusInput);
    }, [trimmed, dup, isEditing, values, onComplete, reset, focusInput]);

    const completeAction = useCallback(
        (id: string, payload: Record<string, unknown>) => {
            onComplete({ kind: id, ...payload } as Completion<F>);
            reset();
        },
        [onComplete, reset],
    );

    const handleDiscard = () => {
        reset();
        focusInput();
    };

    const handleCancelEdit = () => {
        reset();
        editing?.onCancel();
        focusInput();
    };

    const handleContainerClick = (event: React.MouseEvent) => {
        if ((event.target as HTMLElement).closest(".composer-panel")) {
            return;
        }
        focusInput();
    };

    const modifierFeatures = useMemo(
        () =>
            featureList.filter(
                (f): f is AnyModifierFeature => f.kind === "modifier",
            ),
        [featureList],
    );
    const actionFeatures = useMemo(
        () =>
            featureList.filter(
                (f): f is AnyActionFeature => f.kind === "action",
            ),
        [featureList],
    );

    const slotFor = (feature: AnyModifierFeature): FeatureSlot<unknown> => ({
        value: values[feature.id],
        setValue: (value) => setValue(feature.id, value),
        open: Boolean(open[feature.id]),
        toggleOpen: () => toggleOpen(feature.id),
        disabled,
    });

    const fieldPlaceholder =
        placeholder ??
        (isEditing ? t("common.editItem") : t("common.addItemPlaceholder"));

    return (
        <Paper
            elevation={3}
            onClick={handleContainerClick}
            sx={{
                width: "100%",
                p: 1,
                bgcolor: "background.paper",
                border: "1px solid",
                borderColor: isEditing ? "warning.main" : "primary.200",
                cursor: "text",
                transition: "all 0.3s ease",
                "&:hover, &:focus-within": {
                    borderColor: isEditing ? "warning.dark" : "primary.main",
                    boxShadow: 3,
                },
            }}
        >
            {isEditing && editing && (
                <EditHeader label={editing.label} onCancel={handleCancelEdit} />
            )}

            {modifierFeatures.map((feature) =>
                feature.renderPanel ? (
                    <Collapse
                        key={feature.id}
                        className="composer-panel"
                        in={Boolean(open[feature.id])}
                    >
                        <Box sx={{ mb: 0.5 }}>{feature.renderPanel(slotFor(feature))}</Box>
                    </Collapse>
                ) : null,
            )}

            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                {trimmed && (
                    <IconButton
                        onClick={handleDiscard}
                        title={t("common.discardInput")}
                        sx={{
                            color: "text.secondary",
                            bgcolor: "action.hover",
                            "&:hover": { color: "error.main", bgcolor: "error.50" },
                        }}
                    >
                        <Delete />
                    </IconButton>
                )}

                {modifierFeatures.map((feature) =>
                    feature.renderToggle ? (
                        <Box key={feature.id} className="composer-panel">
                            {feature.renderToggle(slotFor(feature))}
                        </Box>
                    ) : null,
                )}

                {actionFeatures.map((feature) => (
                    <Box key={feature.id} className="composer-panel">
                        {feature.renderTrigger({
                            complete: (payload) =>
                                completeAction(
                                    feature.id,
                                    payload as Record<string, unknown>,
                                ),
                            disabled,
                        })}
                    </Box>
                ))}

                <ComposerTextField
                    text={text}
                    onTextChange={setText}
                    onEnter={completeText}
                    inputRef={inputRef}
                    placeholder={fieldPlaceholder}
                    disabled={disabled}
                    errorMessage={dup?.message}
                    suggestions={suggestions}
                />

                <SendButton
                    onClick={completeText}
                    disabled={!trimmed || disabled || blocked}
                    editing={isEditing}
                    duplicate={Boolean(dup)}
                />
            </Box>
        </Paper>
    );
}
```

- [ ] **Step 2: Write `index.ts`** (features added in Task 6)

```ts
export { Composer } from "./Composer";
export { defineAction, defineModifier } from "./defineFeature";
export type {
    ActionFeature,
    Completion,
    ComposerProps,
    DuplicateConfig,
    DuplicateResult,
    EditingConfig,
    FeatureSlot,
    ModifierFeature,
    Suggestion,
    SuggestionsConfig,
} from "./types";
```

- [ ] **Step 3: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/Composer.tsx Application/Frigorino.Web/ClientApp/src/components/composer/index.ts
git commit -m "feat(composer): add composer shell and barrel export"
```

---

## Task 5: i18n keys

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

Only two new keys are needed (`date`, `setToday`); all other strings reuse existing `common.*` keys.

- [ ] **Step 1: Add keys to `en/translation.json`**

Find the line `"undo": "Undo"` (last key in the `common` object) and replace it with:

```json
        "undo": "Undo",
        "date": "Date",
        "setToday": "Today"
```

- [ ] **Step 2: Add keys to `de/translation.json`**

Find the line `"undo": "Rückgängig"` (last key in the `common` object) and replace it with:

```json
        "undo": "Rückgängig",
        "date": "Datum",
        "setToday": "Heute"
```

- [ ] **Step 3: Verify JSON validity + lint**

Run: `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors. (eslint does not parse JSON, but tsc/build will fail later if the JSON is malformed — keep commas correct.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(composer): add date and setToday i18n keys"
```

---

## Task 6: Quantity and expiry features

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/features/quantityFeature.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/features/expiryFeature.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/components/composer/index.ts`

The toggle/panel UI is moved here from the old `QuantityPanel.tsx` / `DateInputPanel.tsx`, with hardcoded strings replaced by `t()`.

- [ ] **Step 1: Write `quantityFeature.tsx`**

```tsx
/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components, mirroring the
   existing pattern in components/inputs/context/AddInputContext.tsx. */
import { Add, Remove, ShoppingBag } from "@mui/icons-material";
import {
    Box,
    Button,
    ButtonGroup,
    IconButton,
    TextField,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const QuantityToggle = ({ value, open, toggleOpen }: FeatureSlot<string>) => (
    <IconButton onClick={toggleOpen} size="small">
        {value ? (
            <Typography
                variant="caption"
                sx={{ fontWeight: "bold", color: "primary.main", minWidth: "30px" }}
            >
                {value}
            </Typography>
        ) : (
            <ShoppingBag
                fontSize="small"
                sx={{ color: open ? "primary.main" : "inherit" }}
            />
        )}
    </IconButton>
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
                        sx={{ minWidth: 40 }}
                    >
                        {num}
                    </Button>
                ))}
            </ButtonGroup>
            <IconButton
                size="small"
                onClick={() => {
                    const current = parseInt(value) || 0;
                    if (current > 0) setValue((current - 1).toString());
                }}
                disabled={!value || parseInt(value) <= 0}
            >
                <Remove fontSize="small" />
            </IconButton>
            <IconButton
                size="small"
                onClick={() => {
                    const current = parseInt(value) || 0;
                    setValue((current + 1).toString());
                }}
            >
                <Add fontSize="small" />
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
});
```

- [ ] **Step 2: Write `expiryFeature.tsx`**

```tsx
/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components, mirroring the
   existing pattern in components/inputs/context/AddInputContext.tsx. */
import { CalendarToday, Clear, Today } from "@mui/icons-material";
import { Box, IconButton, TextField, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const formatForDisplay = (date: Date | null) =>
    date ? date.toLocaleDateString("de-DE", { day: "2-digit", month: "2-digit" }) : "";

const formatForInput = (date: Date | null) =>
    date ? date.toISOString().split("T")[0] : "";

const ExpiryToggle = ({ value, open, toggleOpen }: FeatureSlot<Date | null>) => (
    <IconButton onClick={toggleOpen} size="small">
        {value ? (
            <Typography
                variant="caption"
                sx={{ fontWeight: "bold", color: "primary.main", minWidth: "30px" }}
            >
                {formatForDisplay(value)}
            </Typography>
        ) : (
            <CalendarToday
                fontSize="small"
                sx={{ color: open ? "primary.main" : "inherit" }}
            />
        )}
    </IconButton>
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
                size="small"
                onClick={() => setValue(new Date())}
                title={t("common.setToday")}
            >
                <Today fontSize="small" />
            </IconButton>
            <IconButton
                size="small"
                onClick={() => setValue(null)}
                disabled={!value}
                title={t("common.clear")}
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
});
```

- [ ] **Step 3: Add feature exports to `index.ts`**

Add these two lines after the `export { defineAction, defineModifier } from "./defineFeature";` line:

```ts
export { quantityFeature } from "./features/quantityFeature";
export { expiryFeature } from "./features/expiryFeature";
```

- [ ] **Step 4: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/features Application/Frigorino.Web/ClientApp/src/components/composer/index.ts
git commit -m "feat(composer): add quantity and expiry modifier features"
```

---

## Task 7: Migrate ListFooter

**Files:**
- Modify (full rewrite): `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListFooter.tsx`

Behavior to preserve: quantity panel, autocomplete from existing items, duplicate detection, "re-adding a checked item unchecks it" (only when **not** editing), edit seeding, scroll-to-last on add.

- [ ] **Step 1: Replace the file contents**

```tsx
import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    quantityFeature,
    type DuplicateConfig,
    type Suggestion,
    type SuggestionsConfig,
} from "../../../../components/composer";
import type { ListItemResponse } from "../../../../lib/api";

interface ListFooterProps {
    editingItem: ListItemResponse | null;
    existingItems: ListItemResponse[];
    onAddItem: (data: string, quantity?: string) => void;
    onUpdateItem: (data: string, quantity?: string) => void;
    onCancelEdit: () => void;
    onUncheckExisting: (itemId: number) => void;
    isLoading: boolean;
    onScrollToLastUnchecked: () => void;
}

export const ListFooter = memo(
    ({
        editingItem,
        existingItems,
        onAddItem,
        onUpdateItem,
        onCancelEdit,
        onUncheckExisting,
        isLoading,
        onScrollToLastUnchecked,
    }: ListFooterProps) => {
        const { t } = useTranslation();

        const toSuggestion = (item: ListItemResponse): Suggestion => ({
            id: item.id,
            label: item.text,
            secondaryLabel: item.quantity ?? undefined,
            badge: item.status ? (
                <Chip
                    label="✓"
                    size="small"
                    color="success"
                    variant="outlined"
                    sx={{ ml: 1, height: 16, fontSize: "0.7rem" }}
                />
            ) : undefined,
        });

        const suggestions = useMemo<SuggestionsConfig>(
            () => ({
                getItems: (query) => {
                    const q = query.trim().toLowerCase();
                    return existingItems
                        .filter(
                            (item) =>
                                item.id !== editingItem?.id &&
                                item.text.toLowerCase().startsWith(q),
                        )
                        .map(toSuggestion);
                },
            }),
            [existingItems, editingItem?.id],
        );

        const duplicate = useMemo<DuplicateConfig>(
            () => ({
                check: (text) => {
                    const needle = text.trim().toLowerCase();
                    const match = existingItems.find(
                        (item) =>
                            item.text.toLowerCase() === needle &&
                            item.id !== editingItem?.id,
                    );
                    if (!match) {
                        return null;
                    }
                    if (match.status && !editingItem) {
                        return {
                            message: `"${match.text}" ${t("common.alreadyExists")} (${t("common.completed")})`,
                            onResolve: () => onUncheckExisting(match.id),
                        };
                    }
                    return {
                        message: `"${match.text}" ${t("common.alreadyExists")}`,
                        block: true,
                    };
                },
            }),
            [existingItems, editingItem, onUncheckExisting, t],
        );

        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: { quantity: editingItem.quantity ?? "" },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: { kind: "text"; mode: "create" | "edit"; text: string; quantity: string }) => {
                if (r.kind !== "text") {
                    return;
                }
                if (r.mode === "edit") {
                    onUpdateItem(r.text, r.quantity || undefined);
                } else {
                    onAddItem(r.text, r.quantity || undefined);
                    onScrollToLastUnchecked();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLastUnchecked],
        );

        return (
            <Container
                maxWidth="sm"
                sx={{
                    flexShrink: 0,
                    px: 3,
                    py: 2,
                    borderTop: 1,
                    borderColor: "divider",
                    bgcolor: "background.paper",
                }}
            >
                <Composer
                    features={[quantityFeature]}
                    disabled={isLoading}
                    editing={{ active: Boolean(editingItem), onCancel: onCancelEdit }}
                    initialDraft={initialDraft}
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

ListFooter.displayName = "ListFooter";
```

> Note on `handleComplete`'s parameter type: it is written explicitly to match the
> `Composer`'s inferred text-completion shape for `features={[quantityFeature]}`
> (`{ kind: "text"; mode: "create" | "edit"; text: string; quantity: string }`). If tsc
> reports a mismatch, let the parameter be inferred instead by inlining the arrow into
> `onComplete={(r) => { ... }}` and removing the standalone `handleComplete` — inference
> will supply the exact `Completion` union. Keep the body identical.

- [ ] **Step 2: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListFooter.tsx
git commit -m "refactor(lists): migrate ListFooter to the composer"
```

---

## Task 8: Migrate InventoryFooter

**Files:**
- Modify (full rewrite): `Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryFooter.tsx`

Same as ListFooter plus the expiry feature. `expiryDate` arrives from the API as an ISO string and goes back to the consumer as `Date | null`.

- [ ] **Step 1: Replace the file contents**

```tsx
import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    expiryFeature,
    quantityFeature,
    type DuplicateConfig,
    type Suggestion,
    type SuggestionsConfig,
} from "../../../../components/composer";
import type { InventoryItemResponse } from "../../../../lib/api";

interface InventoryFooterProps {
    editingItem: InventoryItemResponse | null;
    existingItems: InventoryItemResponse[];
    onAddItem: (data: string, quantity?: string, expiryDate?: Date) => void;
    onUpdateItem: (data: string, quantity?: string, expiryDate?: Date) => void;
    onCancelEdit: () => void;
    onUncheckExisting: (itemId: number) => void;
    isLoading: boolean;
    onScrollToLastUnchecked: () => void;
}

export const InventoryFooter = memo(
    ({
        editingItem,
        existingItems,
        onAddItem,
        onUpdateItem,
        onCancelEdit,
        isLoading,
        onScrollToLastUnchecked,
    }: InventoryFooterProps) => {
        const { t } = useTranslation();

        const toSuggestion = (item: InventoryItemResponse): Suggestion => ({
            id: item.id,
            label: item.text,
            secondaryLabel: item.quantity ?? undefined,
            badge: item.isExpiring ? (
                <Chip
                    label="!"
                    size="small"
                    color="warning"
                    variant="outlined"
                    sx={{ ml: 1, height: 16, fontSize: "0.7rem" }}
                />
            ) : undefined,
        });

        const suggestions = useMemo<SuggestionsConfig>(
            () => ({
                getItems: (query) => {
                    const q = query.trim().toLowerCase();
                    return existingItems
                        .filter(
                            (item) =>
                                item.id !== editingItem?.id &&
                                item.text.toLowerCase().startsWith(q),
                        )
                        .map(toSuggestion);
                },
            }),
            [existingItems, editingItem?.id],
        );

        const duplicate = useMemo<DuplicateConfig>(
            () => ({
                check: (text) => {
                    const needle = text.trim().toLowerCase();
                    const match = existingItems.find(
                        (item) =>
                            item.text.toLowerCase() === needle &&
                            item.id !== editingItem?.id,
                    );
                    if (!match) {
                        return null;
                    }
                    return {
                        message: `"${match.text}" ${t("common.alreadyExists")}`,
                        block: true,
                    };
                },
            }),
            [existingItems, editingItem?.id, t],
        );

        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: {
                              quantity: editingItem.quantity ?? "",
                              expiry: editingItem.expiryDate
                                  ? new Date(editingItem.expiryDate)
                                  : null,
                          },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: {
                kind: "text";
                mode: "create" | "edit";
                text: string;
                quantity: string;
                expiry: Date | null;
            }) => {
                if (r.kind !== "text") {
                    return;
                }
                if (r.mode === "edit") {
                    onUpdateItem(r.text, r.quantity || undefined, r.expiry ?? undefined);
                } else {
                    onAddItem(r.text, r.quantity || undefined, r.expiry ?? undefined);
                    onScrollToLastUnchecked();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLastUnchecked],
        );

        return (
            <Container
                maxWidth="sm"
                sx={{
                    flexShrink: 0,
                    px: 3,
                    py: 2,
                    borderTop: 1,
                    borderColor: "divider",
                    bgcolor: "background.paper",
                }}
            >
                <Composer
                    features={[quantityFeature, expiryFeature]}
                    disabled={isLoading}
                    editing={{ active: Boolean(editingItem), onCancel: onCancelEdit }}
                    initialDraft={initialDraft}
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

InventoryFooter.displayName = "InventoryFooter";
```

> Same inference note as Task 7: if the explicit `handleComplete` parameter type drifts
> from the inferred `Completion`, inline the arrow into `onComplete` and let tsc infer it.
> The old `InventoryFooter` dropped `onUncheckExisting` for inventories (no checkbox
> semantics), so it is intentionally omitted from the destructure here.

- [ ] **Step 2: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors. (`onUncheckExisting` is now unused by the parent that renders this footer; if eslint flags the prop, leave the interface as-is — it matches the parent's call site — and confirm the parent still compiles.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryFooter.tsx
git commit -m "refactor(inventories): migrate InventoryFooter to the composer"
```

---

## Task 9: Delete the legacy inputs folder

**Files:**
- Delete: `Application/Frigorino.Web/ClientApp/src/components/inputs/` (entire folder)

- [ ] **Step 1: Confirm nothing still imports it**

Run (from repo root): `grep -rn "components/inputs" Application/Frigorino.Web/ClientApp/src`
Expected: **no matches** (Tasks 7 and 8 removed the only two importers).

If there are matches, stop and migrate those importers before deleting.

- [ ] **Step 2: Delete the folder**

```bash
git rm -r Application/Frigorino.Web/ClientApp/src/components/inputs
```

- [ ] **Step 3: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: tsc exits 0; no eslint errors.

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(composer): remove legacy AddInput component"
```

---

## Task 10: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Production build**

Run (from `Application/Frigorino.Web/ClientApp`): `npm run build`
Expected: `tsc -b` clean + `vite build` succeeds, no type or build errors.

- [ ] **Step 2: Integration suite (behavior gate)**

Run (from repo root): `dotnet test Application/Frigorino.sln`
Expected: all tests pass. These Reqnroll/Playwright scenarios add list/inventory items through the composer via the `autocomplete-input-textfield` / `autocomplete-input-submit-button` testids; green here proves the testids and add/edit flows survived the rewrite.

If Docker/Testcontainers reports the daemon is unreachable, ask the user to start Docker Desktop rather than skipping.

- [ ] **Step 3: Manual verification via the dev stack**

Bring up the stack with the `/dev-up` skill, then drive the SPA with Playwright MCP and confirm:
- Lists: type an item + Enter → added; open quantity toggle → set quantity → add → quantity persists.
- Lists: type an existing checked item → inline duplicate message; send → it gets unchecked (not a new row).
- Lists: edit an item (pencil) → text + quantity seeded, header shows; save → updates; cancel → clears.
- Inventories: quantity **and** expiry now have **separate** toggles; set both → add → both persist; edit seeds both.
- Autocomplete dropdown appears after 3 chars and filtering works.

Capture findings; if anything regresses, fix in a follow-up commit referencing the failing behavior.

- [ ] **Step 4: Final commit (only if Step 3 required fixes)**

```bash
git add -A
git commit -m "fix(composer): address manual-verification findings"
```

---

## Notes / decisions carried from the spec

- **No frontend unit runner** — verification is tsc + lint + integration suite + manual MCP (see top).
- **Per-feature toggles** — inventory intentionally shows two toggles now (user-approved UX change).
- **`alert()` removed** — duplicate feedback is inline `helperText` via the `duplicate` config.
- **i18n** — core/feature strings come from `t()`; only `common.date` + `common.setToday` are new.
- **Testids preserved** — `autocomplete-input-textfield`, `autocomplete-input-submit-button`.
- **The one runtime↔type bridge** — `Composer` builds the completion object and casts it to
  `Completion<F>`; this single localized cast is expected and is the only place `as` is used
  to assemble the union.
```

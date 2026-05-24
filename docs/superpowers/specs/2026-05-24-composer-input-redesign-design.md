# Composer input redesign — design

- **Date:** 2026-05-24
- **Status:** Draft (design); awaiting user review
- **Branch:** TBD (off `stage`)

## Summary

The app's main "add item" input (`src/components/inputs/AddInput.tsx`, referred to informally as
"ListInput") is being reimplemented from scratch as a generic, reusable, **WhatsApp-style composer**:
a plain text field by default, with toggle buttons that extend it with additional features. The new
component knows nothing about lists, items, or the API — it collects input and emits a **typed
completion event**; the consuming feature owns the workflow that follows.

The two existing consumers — `ListFooter` (quantity) and `InventoryFooter` (quantity + expiry date) —
migrate to it, and the old `src/components/inputs/` folder is deleted.

## Goals

- A generic input core, **decoupled from list / list-item** concepts, reusable across the app.
- **Extensible:** adding a new feature (a future photo, document, location, …) is one new self-contained
  file dropped into a `features` array — no edits to the core or to other consumers.
- **Typesafe:** the completion payload handed to the consumer is fully typed; a consumer cannot misread
  a field or forget one.
- **Simple consumer interface:** the consumer declares the features it wants, supplies data/callbacks
  for the opt-in built-ins, and reacts to one `onComplete` callback.
- Preserve today's behaviors: quantity (lists + inventories), expiry date (inventories), autocomplete
  suggestions, duplicate detection, edit mode.

## Non-goals / out of scope

- **Photo / document attach features.** The approved `rich-list-items` spec
  (`2026-05-23-rich-list-items-design.md`) calls for a WhatsApp-style attach affordance. This redesign
  makes the extension model accommodate such **action features** cleanly, but it does not implement
  photo/document upload — that stays in the rich-list-items work. Quantity + expiry are the v1 proving
  features.
- New backend endpoints or DTO changes. This is a frontend-only change.
- A frontend unit-test runner (none exists in the repo; see Testing).

## Key decisions & rationale

1. **Generic core + opt-in built-ins** (chosen over a pure plugin core or a thin core that pushes
   everything to the consumer). The core understands text + a features array, and additionally
   understands three optional, consumer-driven built-ins: edit mode, autocomplete suggestions,
   duplicate detection. *Rationale: list-like consumers get those behaviors without re-implementing
   them, while the core still carries no list/API knowledge because the built-ins are driven entirely
   by consumer-supplied data and callbacks.*
2. **Completion is a typed discriminated union**, not a flat payload. The user's own examples ("text
   send" vs "document selected") are different completion shapes. *Rationale: a discriminated union is
   the typesafe, scalable way to model "the component can finish in several distinct ways," and lets
   the consumer `switch` on `kind`.*
3. **Two feature roles:**
   - **Modifier features** (quantity, expiry) augment a *text* completion with a typed value under
     their `id`; they do not complete on their own.
   - **Action features** (future photo/document) *are* the completion; selecting one emits its own
     completion variant and bypasses the text payload.
   *Rationale: this is the minimal model that covers both existing needs and the rich-list-items
   direction without special-casing.*
4. **The composer owns each feature's state and assembles the payload** (chosen over the consumer
   owning panel state, which is closer to today). *Rationale: this is what removes the duplicated
   quantity/date state-wiring currently hand-rolled in both footers, and is what lets the consumer
   receive a single typed payload instead of smuggling extra fields through a closure.*
5. **Per-feature toggles**, not a shared toggle. Each feature owns its own toggle button + panel.
   *Rationale: consistent and scalable as features multiply. This is a small UX change for inventories,
   which today open quantity + date panels from one combined toggle; the user accepted it.*
6. **The core hardcodes no strings and no list knowledge.** Suggestions data, duplicate predicates, and
   all display strings come from the consumer or from `t()` inside feature files. *Rationale: keeps the
   core i18n-neutral and genuinely reusable.*

## Architecture

New folder, replacing `src/components/inputs/`:

```
src/components/composer/
  Composer.tsx           // shell: text row + send + per-feature toggles + expandable panels
  types.ts               // public types: ComposerProps, feature descriptors, completion union
  defineFeature.ts       // defineModifier / defineAction helpers (hide generics from feature files)
  components/
    ComposerTextField.tsx // text input (autocomplete-capable when suggestions provided)
    SendButton.tsx        // send/update affordance
    EditHeader.tsx        // generic edit header (icon + label + close)
    Suggestions.tsx       // suggestion dropdown
  features/
    quantityFeature.tsx   // self-contained modifier
    expiryFeature.tsx     // self-contained modifier
  hooks/
    useComposerState.ts   // text + per-feature value/open state, seeding, reset
```

### Layout

WhatsApp-style, matching today's visual shape but theme-driven (no inline `borderRadius: 2`, no
hand-rolled `boxShadow`; uses `theme.ts` tokens, MUI size props, `<Paper>`):

```
┌─────────────────────────────────────────────┐
│ [edit header — only in edit mode]             │
│ [feature panel(s) — expanded when toggled]    │
│ [🗑 when text] [toggleA][toggleB] [ text … ] [▶]│
└─────────────────────────────────────────────┘
```

## Feature model

A feature is one self-contained file created via a typed helper so the file itself needs no generics.

### Modifier feature

```ts
export interface ModifierFeature<Id extends string, V> {
  kind: "modifier";
  id: Id;
  initial: V;
  isEmpty?: (value: V) => boolean;          // empty → omitted from "active modifiers"; default falsy check
  renderToggle?: (s: FeatureSlot<V>) => React.ReactNode;
  renderPanel?:  (s: FeatureSlot<V>) => React.ReactNode;
}

export interface FeatureSlot<V> {
  value: V;
  setValue: (v: V) => void;
  open: boolean;            // is this feature's panel expanded?
  toggleOpen: () => void;
}
```

Example (`quantityFeature.tsx`):

```ts
export const quantityFeature = defineModifier({
  id: "quantity",
  initial: "" as string,
  isEmpty: (v) => v.trim() === "",
  renderToggle: ({ value, open, toggleOpen }) => (
    <QuantityToggle value={value} active={open} onToggle={toggleOpen} />
  ),
  renderPanel: ({ value, setValue }) => <QuantityPanel value={value} onChange={setValue} />,
});
```

### Action feature

```ts
export interface ActionFeature<Kind extends string, Payload> {
  kind: "action";
  id: Kind;
  renderTrigger: (ctx: { complete: (payload: Payload) => void }) => React.ReactNode;
}
```

(No action features ship in v1; `documentFeature` / `photoFeature` are illustrative for the
rich-list-items work.)

### Typed completion

The composer derives the completion union from the features array:

```ts
type TextCompletion<Mods>   = { kind: "text"; mode: "create" | "edit"; text: string } & Mods;
type ActionCompletion<...>  = { kind: <actionId>; ...<payload> };
type Completion = TextCompletion<...> | ActionCompletion<...>;

onComplete: (c: Completion) => void;
```

`Mods` is the map of each modifier's `id → V` (e.g. `{ quantity: string; expiry: Date | null }`),
derived from the features tuple. **All the type derivation lives in `types.ts` + `defineFeature.ts`;**
feature files and consumers stay plain.

> Implementation note: the values map is derived from a `readonly` features tuple via a mapped type
> over the modifier members. The plan should verify this compiles cleanly against the repo's TS
> config and `npm run tsc`; if the inference proves fragile, fall back to a consumer-declared
> `Modifiers` type parameter on `Composer` (slightly more verbose for the consumer, no behavior
> change). Either way the consumer reads `r.quantity` / `r.expiry` directly.

## Opt-in built-ins (consumer-driven, list-agnostic)

A consumer that passes none of these gets a plain text-and-send box.

### Edit mode

```ts
editing?: { active: boolean; onCancel: () => void; label?: string };
initialDraft?: { text?: string; values?: Partial<Mods> };
```

- When `editing.active`, the composer renders a generic `EditHeader` (edit icon + `label` + close →
  `onCancel`) and the send button switches to its "update" affordance (color/title).
- The composer seeds text + each modifier's value from `initialDraft` when it changes identity, and
  resets to `initial` values after a completion.
- The `text` completion carries `mode: "create" | "edit"` so the consumer's `onComplete` branches
  without re-reading its own editing state.
- The list-specific "Completed" chip in today's edit header is dropped (minor; can return later as an
  optional header slot if missed).

### Autocomplete suggestions

```ts
suggestions?: {
  getItems: (query: string) => Suggestion[];   // consumer sources + filters
  minChars?: number;                            // default 3
  onSelect?: (s: Suggestion) => void;           // default: fill the text field
};
type Suggestion = { id: string | number; label: string; secondaryLabel?: string; badge?: React.ReactNode };
```

The composer renders the dropdown (MUI Autocomplete internally, as today); the consumer maps its own
data into `Suggestion[]` (e.g. a list's completed flag → a `badge`). Decoupled from `ListItem`.

### Duplicate detection

```ts
duplicate?: { check: (text: string) => DuplicateResult | null };
type DuplicateResult = {
  message: string;          // already localized by the consumer
  block?: boolean;          // true → prevent the text completion
  onResolve?: () => void;   // optional action fired on send instead of completing
};
```

- Replaces today's `alert()` with the existing inline `helperText` error styling on the text field.
- If `block` is set, send does not complete while the text is a duplicate.
- The list-specific "re-adding a checked item unchecks it instead" becomes the consumer's `onResolve`:
  when present, hitting send on a duplicate fires `onResolve()` rather than completing.
- All strings arrive localized, so the core stays i18n-neutral.

## i18n

- The core hardcodes no strings: placeholder, edit label, button titles arrive via props or `t()`.
- The hardcoded German in the date/edit panels ("Bearbeiten", "Datum", "Heute", "Löschen") and the
  English alert/title strings are replaced with real `t()` keys added to `public/locales/{en,de}/translation.json`.
- Feature files use `t()` directly.
- **Testids preserved:** `autocomplete-input-textfield` and `autocomplete-input-submit-button` are kept
  so the Reqnroll/Playwright integration steps that target them keep working. The plan greps for these
  (and any other references into `components/inputs/`) before changing or removing anything, and
  updates references in lockstep. Tests assert on testids / `data-*`, never translated text.

## Consumer migration

### ListFooter

```tsx
<Composer
  features={[quantityFeature]}
  editing={{ active: !!editingItem, onCancel: onCancelEdit }}
  initialDraft={editingItem ? { text: editingItem.text ?? "", values: { quantity: editingItem.quantity ?? "" } } : undefined}
  suggestions={{ getItems: (q) => existingItems.filter(/* startsWith, ≠ editing */).map(toSuggestion) }}
  duplicate={{ check: (text) => findDuplicate(text) /* returns message + onResolve (uncheck) */ }}
  onComplete={(r) => {
    if (r.kind !== "text") return;
    r.mode === "edit" ? onUpdateItem(r.text, r.quantity) : onAddItem(r.text, r.quantity);
    onScrollToLastUnchecked();
  }}
/>
```

### InventoryFooter

Same shape with `features={[quantityFeature, expiryFeature]}` and `onComplete` reading `r.quantity`
and `r.expiry`. Per-feature toggles mean inventory now shows a quantity toggle and an expiry toggle
(accepted UX change).

The per-consumer quantity/date `useState`, the `Collapse`/panel wiring, the `secondaryText` mapping,
and the closure-smuggling of extra fields into `onAdd` all disappear. The existing-items → suggestion
transform replaces the `mappedExistingItems` mapping.

### Cleanup

Delete `src/components/inputs/` once both consumers compile against the new component. Grep for all
importers first (currently only `ListFooter` and `InventoryFooter` import `AddInput`; `QuantityPanel`
is imported by both footers, `DateInputPanel` by `InventoryFooter`) and confirm nothing else references
the folder before removal. The reusable bits (`QuantityPanel`, `QuantityToggle`, `DateInputPanel`,
`DateToggle`) move into the new feature files.

## Error handling

- Duplicate / blocked submit: inline `helperText` on the text field; no `alert()`.
- Empty text + no active action feature: send is disabled (as today).
- Action-feature errors (future photo/document upload) are the action feature's / consumer's concern,
  surfaced through the consumer's workflow — out of scope here.

## Testing & verification

No frontend unit-test runner exists (per CLAUDE.md — the Jest mention in
`knowledge/Frontend_Architecture.md` is aspirational). The gate is:

- `npm run tsc` — the typed `onComplete` payload is the primary correctness surface, so type-check
  carries real weight.
- `npm run lint`.
- **Manual verification via `/dev-up` + Playwright MCP:** lists add/edit + quantity; inventory add/edit
  + quantity + expiry; inline duplicate message; uncheck-on-re-add; autocomplete suggestions;
  edit-mode seeding + cancel; per-feature toggle open/close.
- **`dotnet test Application/Frigorino.sln`** — the Reqnroll + Playwright integration tests drive this
  input when adding list/inventory items, so they are the regression gate for the testid/behavior
  changes. Preserve testids accordingly.

## Migration / rollout

Frontend-only; no DB migration, no backend changes, no new project, so the Dockerfile is expected
unchanged (confirm only if a broader verification pass is requested). Ship on a feature branch off
`stage`; do not push probe commits to `stage` (it hosts a live client).

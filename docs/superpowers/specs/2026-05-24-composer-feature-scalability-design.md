# Composer feature scalability, value chips & attachment direction — design

- **Date:** 2026-05-24
- **Status:** Approved (design); implementation plan pending
- **Branch:** `feat/composer-input-redesign` (off `stage`) — builds directly on the just-shipped generic `Composer`.

## Summary

The generic `Composer` (`ClientApp/src/components/composer/`) shipped as a faithful
re-implementation of the old list input: text field + a flat row of always-visible feature
toggles (quantity, expiry), where a set value is shown by the toggle morphing into the value.

Team review raised three questions: **(1)** how does this scale to many features, **(2)** features
are purely additive (multiple panels open at once) — how do we get exclusivity, and **(3)** the UI
mirrored the old design — how do we improve it. This design answers all three and, in doing so,
resolves how image/document items (a separate, already-approved feature) will plug into the same
input without polluting the text-item domain model.

The work splits into a small **buildable-now** slice that improves the real consumers today, and a
set of **contracts locked on paper** that give the future attachment work a clean home.

## Motivation

- **Scalability:** the toggle row (`Composer.tsx`) renders every feature inline. ~3–4 icons is the
  mobile ceiling; the type machinery already scales (`ModifierValues<F>` / `Completion<F>` are O(n)
  mapped types), but the *layout* does not.
- **Exclusivity:** `useComposerState` holds panel open-state as a `Record<id, boolean>`, so any number
  of panels stack vertically. There is no notion of "only one open" or "these are alternatives".
- **UX:** values displayed by morphing the toggle icon is cramped and doesn't scale; there is no
  WhatsApp-style attach affordance; touch targets use `size="small"` throughout (borderline on mobile).

## Scope

### Build now (improves the current list & inventory inputs immediately)

1. **Value chips above the input.** A set modifier value renders as a chip in a row above the text
   field, not by morphing its toggle. Tapping a chip re-opens that feature's panel to edit; the value
   is removed via a **clear control inside the panel** (no tiny chip ✕ — see Mobile).
2. **One panel open at a time (panel-exclusivity).** Opening any panel closes whatever was open.
   Replaces the `open` boolean map with a single `openPanelId`.
3. **Mobile-first touch targets.** Every interactive control (toggles, send, discard, chips, panel
   buttons) meets a ~44px minimum hit area. Bump the current `size="small"` icon buttons where they
   fall short.

### Design now, build with the attachment feature (contracts only — no production code yet)

4. **`pin()` placement + overflow "+" menu.** `pin(feature)` keeps a feature inline; bare features
   collapse behind a single "+" attach button (popover on desktop, bottom-sheet on mobile). The "+"
   has no real content until attachments land, so it is specified here but not rendered for the
   current all-pinned consumers.
5. **`ActionFeature`-owns-its-flow contract.** An action feature owns its trigger, its capture UI, and
   its completion payload (including any caption). The composer core never routes the shared text
   field into an action. This is what lets image/document carry a caption without overloading the
   text-item's `text`.

### Out of scope

- **`exclusive()` value-exclusive modifier grouping.** Dropped as YAGNI. Image/document are *item
  types* (distinct completions via action features), not exclusive modifiers on a text item, so they
  are mutually exclusive **by nature** — no grouping primitive is needed. (This reverses an earlier
  idea from discussion; see Key decisions #1.)
- **Building the image/document features themselves.** They belong to `feat/rich-list-items`
  (`docs/superpowers/specs/2026-05-23-rich-list-items-design.md`).
- **Backend / storage / DTO changes.** None — this is a frontend composer refactor.

## Key decisions & rationale

1. **Attachments are item types (actions), not exclusive modifiers.** An image item *is* a photo with
   an optional caption; it is not a text item that "has" an image. This matches the approved
   rich-list-items design (`Type ∈ {Text, Image, Document}`, flat model). Modeling them as
   `ActionFeature`s (each producing its own `Completion` variant) makes exclusivity inherent and keeps
   the text-item completion clean. *Avoids "dirty invariants" on the shared completion object.*
2. **Caption lives in the action's own surface, never the main text field.** The composer text field
   is bound to exactly one meaning: a text item's primary content. Picking Photo/Document launches the
   action's self-contained flow (pick file → preview surface with its own caption field → confirm),
   which emits `{ kind: "image", file, caption }`. Structural separation, not a mode flag on a shared
   field.
3. **Chips for set values, not toggle-morphing.** Chips scale to many simultaneous values, read
   clearly, and give a natural tap-to-edit target. The toggle returns to a stable icon that simply
   highlights when its value is set.
4. **One panel at a time.** Matches the "+"-menu mental model and prevents the input from growing
   vertically as features multiply.
5. **Placement is a consumer decision, declared at the call site** via `pin()` wrappers in the
   `features` array — not baked into feature descriptors (a feature is reusable; where it sits is the
   consumer's call). Inference still flows through the wrappers.
6. **Buildable-now slice deliberately excludes `pin`/overflow rendering** because all current consumers
   pin everything — there is nothing to overflow until attachments exist. Building the menu now would
   be speculative. The contract is specified so the attachment work drops in without redesign.

## Architecture

### State (`hooks/useComposerState.ts`)

- Replace `open: Record<string, boolean>` + `toggleOpen(id)` with `openPanelId: string | null` +
  `openPanel(id)` / `closePanel()`. `toggleOpen(id)` becomes "open this, closing any other".
- `values` map, draft re-seeding, and reset are unchanged.
- **Chip derivation:** a feature contributes a chip when its value is non-empty, using the existing
  optional `isEmpty(value)` predicate on the descriptor (already defined on quantity/expiry).

### Rendering (`Composer.tsx`)

Top-to-bottom inside the `Paper`:

1. `EditHeader` (unchanged, edit mode only).
2. **Chip row** — for each modifier whose value is non-empty, a chip: `feature.renderChip?(slot)` (new
   optional descriptor hook) or a default chip showing the value. Tap → `openPanel(feature.id)`.
3. **Single panel area** — renders only the feature whose id === `openPanelId` (Collapse). Replaces
   the "map every open panel" loop.
4. **Bar row** — discard button (when text present) · modifier toggles (all rendered inline today;
   the `pin`/overflow split formalizes this later) · `[+ attach]` (only when overflow features exist —
   none today) · `ComposerTextField` · `SendButton`.

`renderToggle` stops displaying the value; it shows the feature icon, highlighted (e.g.
`color="primary"`) when `!isEmpty(value)` or when its panel is open. The value lives in the chip.

### Feature descriptor (`types.ts`)

- Add optional `renderChip?(slot: FeatureSlot<V>): ReactNode` to `ModifierFeature`. If absent, the
  composer renders a default chip from the stringified value + the feature's icon.
- `ActionFeature` keeps `renderTrigger(ctx)`; document in the type that the action owns its full
  capture flow and emits the complete payload (caption included). No structural change needed now.
- `pin()` / overflow types: specify a `Placement` notion and a `pin(feature)` wrapper that preserves
  the wrapped feature's `Id`/`V` so `Completion<F>` inference is unaffected. **Specified, implemented
  with the attachment work.**

### Panels gain a clear control

- `expiryFeature` panel already has a Clear button — keep it; it becomes the canonical "remove value"
  action (chip has no ✕).
- `quantityFeature` panel gains an equivalent clear/remove control so a set quantity can be removed
  from inside its panel.

## Interaction flows (build-now)

- **Set a value:** tap toggle → panel opens (any other panel closes) → enter value → value appears as
  a chip above the field; toggle icon highlights.
- **Edit a value:** tap its chip → its panel re-opens with the current value.
- **Remove a value:** open the panel (via toggle or chip) → tap Clear → chip disappears.
- **Send:** unchanged — `completeText` emits `{ kind:"text", mode, text, ...values }`.

## Type model (unchanged for consumers)

`Completion<F>`, `ModifierValues<F>`, `TextCompletion<F>` are untouched. Adding `renderChip` is
additive. When action features arrive, `Completion<F>` already unions in `ActionCompletion<...>`, so
`{ kind:"image", ... }` variants appear automatically from the features tuple.

## Consumer migration

- **`ListFooter.tsx`** — no API change (quantity stays inline/pinned). Behavior change: a set quantity
  now shows as a chip instead of morphing the toggle. `handleComplete` unchanged.
- **`InventoryFooter.tsx`** — same: quantity + expiry stay inline; their set values become chips;
  panel-exclusivity applies. `handleComplete` unchanged.
- Both keep `data-testid="autocomplete-input-textfield"` / `autocomplete-input-submit-button` so
  existing integration steps keep working.

## Mobile / touch targets

- Minimum ~44px hit area on toggles, send, discard, chips, and panel buttons. Where MUI `size="small"`
  yields a smaller box, set explicit `minWidth`/`minHeight` (or drop to default size) per the theme.
- Chips are sized as comfortable single tap targets (the whole chip body opens the panel); removal is
  the in-panel Clear, so there is no small secondary target to miss.
- The "+" overflow (future) opens a **bottom-sheet on mobile** rather than a tight popover.

## Testing / verification

- No frontend unit runner. Verify with `npm run tsc` + `npm run lint`, the integration suite
  (`dotnet test Application/Frigorino.sln`: Reqnroll + Playwright + Testcontainers — assert on
  testids/`data-*`, never translated text), and **manual `/dev-up` + Playwright MCP at mobile width**.
- Manual checklist: set/edit/remove quantity via chip; set/edit/remove expiry via chip; opening one
  panel closes the other; tap targets comfortable at ~390px width; existing add/edit/duplicate/undo
  flows for both list and inventory unaffected.

## Future (enabled by this design, built elsewhere)

- Image/document action features (camera/library + document picker) with their own preview+caption
  surface, emitting `{ kind:"image"|"document", file, caption }` — built on `feat/rich-list-items`.
- The "+" overflow menu becomes live the moment a consumer passes a non-pinned (e.g. action) feature.

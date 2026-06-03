# List item comments / hints — Design

**Date:** 2026-06-03
**Status:** Approved (pending spec review)
**Scope:** List items only. InventoryItem mirror is explicitly deferred (see Out of scope).

## Problem

A `ListItem` today is a name (`Text`) plus an optional structured quantity (`QuantityValue` / `QuantityUnit`). When a user wants to attach context — *"the blue one"*, *"brand X, not the store brand"*, *"for Anna's party"*, *"ask the butcher"* — there is nowhere to put it, so it gets crammed into `Text`. That pollutes the name everywhere it is reused: the checked-off display, the quantity-extraction router (`ItemTextRouter`), and the promote-to-inventory pre-fill all then carry the note as if it were part of the product name.

A dedicated free-text **comment** keeps the *what* (item name, machine-parseable) separate from the *note about it* (human prose). This is the clean-domain-separation principle: one field, one meaning. The comment is a distinct concept from both the item name and from any future rich-attachment caption — it is a standalone hint on a plain text item.

## Decisions (locked during brainstorming)

1. **Model as one nullable column** — `Comment string?` on the flat `ListItem` table, with a `CommentMaxLength` source-of-truth const. One EF migration, one nullable column. No new entity, no inheritance.
2. **Set/clear via the `List` aggregate**, not the handler — thread comment through the existing `AddItem` / `UpdateItem` aggregate methods.
3. **Reuse the existing write slices** — extend `CreateItem` and `UpdateItem` + `ListItemResponse`; no new endpoint.
4. **Comment is settable at create AND edit.** (`CreateItemRequest` and `List.AddItem` both gain a comment.)
5. **Comment semantics follow `Description`, not quantity:** `null = preserve`, empty/whitespace = clear-to-null, otherwise trim + length-validate. **No `clearComment` flag** (quantity needs `clearQuantity` only because it is a structured value object).
6. **Frontend uses the existing composer feature framework** — a new `commentComposerFeature` defined via `defineModifier`, mirroring `quantityComposerFeature`.
7. **List display:** a small-text, muted, single-line truncated comment **preview** under the item name (an at-a-glance indicator that a comment exists), **tap to expand** the full multiline text in-place — no edit mode required.

## Backend

### Entity — `Frigorino.Domain/Entities/ListItem.cs`

```csharp
// Source of truth, mirroring TextMaxLength.
public const int CommentMaxLength = 500;
public string? Comment { get; set; }
```

`CommentMaxLength = 500` matches `TextMaxLength`. (Open to a shorter cap like 250 — does not change the design.)

### EF — `ListItemConfiguration`

Add `HasMaxLength(ListItem.CommentMaxLength)` for the `Comment` property, the same way `Text` reads its const. One migration: a single nullable column, no backfill, reversible.

### Aggregate — `Frigorino.Domain/Entities/List.cs`

Thread comment through the two existing item methods (no new methods):

- **`AddItem(string text, Quantity? quantity, string? comment)`**
  - Trim comment; empty/whitespace → `null`.
  - Length-validate (> `CommentMaxLength` → `Error` with `Property = nameof(ListItem.Comment)`).
  - Set `item.Comment` on the new `ListItem`.

- **`UpdateItem(int itemId, string? text, Quantity? quantity, bool clearQuantity, bool? status, string? comment)`**
  - `comment is null` → preserve existing value.
  - empty/whitespace → clear to `null`.
  - otherwise trim, length-validate, assign.
  - **No-op guard** extended: a request that sets *only* a comment must be valid. Current guard `text is null && quantity is null && !clearQuantity && status is null` becomes `... && comment is null`.

- Add a shared `ValidateComment(string? comment)` helper alongside `ValidateItemText`.

- **`ApplyExtractedQuantity`** is untouched — extraction only rewrites name + quantity; comments are out of its concern.

**The router never sees the comment.** `ItemTextRouter.Analyze` runs on `Text` only. Comment is human prose, deliberately excluded from quantity extraction — the whole point of the feature.

### Slices — `Frigorino.Features/Lists/Items/`

- **`CreateItem.cs`**: `CreateItemRequest(string Text, string? Comment)`; pass `request.Comment` into `list.AddItem(analysis.CleanName, analysis.Quantity, request.Comment)`. Router still analyzes `Text` only; `ExtractionPending` logic unchanged.
- **`UpdateItem.cs`**: `UpdateItemRequest(string? Text, QuantityDto? Quantity, bool? ClearQuantity, bool? Status, string? Comment)`; pass `request.Comment` into `list.UpdateItem(...)`. The comment never participates in the `textChangedWithoutQuantityIntent` router gate.
- **`ListItemResponse.cs`**: add `string? Comment` to the positional record, to `From(...)`, and to the EF-translatable `ToProjection` expression (plain property read — EF-safe). Read slices (`GetItem` / `GetItems`) surface it automatically.

Error dispatch is unchanged — over-length comment is a generic `Error` with `Property` metadata → `ValidationProblem`; `EntityNotFoundError` → 404. No new error types.

### API client regeneration

`npm run api` from `ClientApp/` after backend changes (build-time MSBuild target emits `openapi.json`, regenerates the TS client + tanstack hooks). No hand-edits to generated code under `src/lib/api/`.

## Frontend

### New composer feature — `components/composer/features/commentComposerFeature.tsx`

Defined via `defineModifier`, mirroring `quantityComposerFeature`. Draft is a plain `string` (simpler than quantity's struct):

- `id: "comment"`
- `initial: ""`
- `isEmpty: (v) => v.trim() === ""`
- `isValid: (v) => v.trim().length <= COMMENT_MAX_LENGTH` (client mirror of the backend const)
- `renderToggle` — a note icon (e.g. MUI `Notes` / `StickyNote2`), highlighted when a comment exists or the panel is open (same pattern as `QuantityToggle`).
- `renderPanel` — a **`multiline`** `TextField` (placeholder = `t("lists.commentPlaceholder")`) with a clear `IconButton`; input carries `data-testid="composer-comment"`.
- `renderChip` — a small chip previewing the saved hint, clickable to reopen the panel.

### Composer wiring — `ListFooter.tsx`

Comment is set at both create and edit:

- `EDIT_FEATURES = [quantityComposerFeature, commentComposerFeature]`
- `ADD_FEATURES = [commentComposerFeature]` (replaces the current `NO_FEATURES = []`; add-mode gets comment but stays quantity-free, since extraction owns quantity at add time).
- `initialDraft` seeds `values.comment` from `editingItem.comment ?? ""`.
- `handleComplete` reads `r.comment` and forwards it; `onAddItem` / `onUpdateItem` signatures extend to carry the comment string.

### Page — `ListViewPage.tsx`

`handleAddItem` / `handleUpdateItem` thread the comment into the mutation `body.comment`.

### Optimistic update — `useUpdateListItem.ts`

Extend `onMutate` to apply comment with domain-matching semantics, for both the list-cache and detail-cache writes:

```ts
comment:
    variables.body.comment == null
        ? item.comment
        : (variables.body.comment.trim() || null)
```

(`null`/absent = preserve — matching the domain's `null = preserve`; empty/whitespace = clear; otherwise set.) `onError` rollback and `onSuccess` invalidation are unchanged.

### List display — `ListItemContent.tsx`

The item `Text` rendering (with link/image parsing) is untouched. The `secondary` slot becomes a small column:

- existing quantity chip row (unchanged), **plus**
- a comment **preview** line, only when `item.comment` is present:
  - small muted text — `Typography variant="caption" color="text.secondary"`, font size ~`0.7rem` (exact sizing is adjustable polish), prefixed by a subtle note icon.
  - single line, truncated with ellipsis — for most short hints this shows the whole comment.
  - **tap toggles** the full text in-place: expanded state uses `whiteSpace: "pre-wrap"` and `wordBreak: "break-word"` so multiline hints render with their line breaks preserved.
  - `data-testid={`list-item-comment-${item.id}`}`.

Local expand/collapse is component state on the row; it does not enter edit mode and does not collide with the existing long-press-to-copy or quantity-chip-to-edit behaviors.

## i18n

Add keys to `public/locales/{en,de}/translation.json`:

- `lists.comment` — label / aria
- `lists.commentPlaceholder` — panel TextField placeholder
- `lists.addComment` — toggle aria-label

`common.clear` already exists. Tests never assert on translated text.

## Testing

- **Domain unit tests** (`Frigorino.Test`, xUnit + FakeItEasy):
  - `List.AddItem` with comment: trims, empty → null, over-length → fail with `Property = Comment`.
  - `List.UpdateItem` comment: set / preserve-on-null / clear-on-empty/whitespace / over-length fail.
  - A comment-only update is **not** rejected by the no-op guard.
- **Integration** (`Frigorino.IntegrationTests`, Reqnroll + Playwright + Postgres Testcontainers):
  - Scenario: add an item with a comment and/or edit an item to add a comment; assert via `data-testid` (`composer-comment`, `list-item-comment-<id>`) — never translated text.
  - Requires `npm run build` first so the testids land in `ClientApp/build` (the IT harness serves the built SPA, not live source).

**Verification gate:** `dotnet test Application/Frigorino.sln` (full SLN — Test + IntegrationTests) + frontend `npm run lint` / `npm run tsc` / prettier + `docker build` as the final drift check.

## Impact / cost

Small and reversible. One EF migration (1 nullable column + a length const), `Comment` threaded through two aggregate methods + the `CreateItem` / `UpdateItem` slices + `ListItemResponse`, regenerated API client, one new composer feature, and display tweaks in `ListItemContent`. No new dependencies, no new endpoint.

## Out of scope

- **InventoryItem mirror.** `InventoryItem` shares the structured-quantity shape with `ListItem` (commit `4cc1ec0`); a note field is just as useful there ("expires soon", "opened"). Deferred to a separate fast-follow spec. Noted asymmetry rather than silent omission.
- **Promote-to-inventory carry-over.** Until inventory has a comment field, a promoted list item's comment simply does not travel. Revisit when the inventory mirror is specced.
- Threaded / multi-author comments (this is a single free-text hint per item, not a discussion).
- @-mentions or per-comment notifications.
- Rich formatting (plain text only).
- Comment history / edit trail.

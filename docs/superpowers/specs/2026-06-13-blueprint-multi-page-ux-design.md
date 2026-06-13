# Blueprint management → list/inventory multi-page UX

**Date:** 2026-06-13
**Branch:** `feat/category-blueprints`
**Status:** Approved design — ready for implementation plan

## Goal

Rework the category-blueprint management UX from a single all-in-one page
(`BlueprintsPage` rendering stacked `BlueprintCard`s) into the same multi-page
structure used by lists and inventories: an overview list, a details surface for
arranging categories, and separate create/edit pages for the title.

Out of scope: the **apply-blueprint-to-a-list** flow. That already lives on the
list side (`ListViewPage` → `ApplyBlueprintDialog`) and is not touched here.

## Decisions (resolved during brainstorming)

1. **Routing:** stays nested under `/household/blueprints/*` (reached from the
   household settings page, as today). No new bottom-nav entry.
2. **New blueprint starting categories:** pre-filled with the full default aisle
   order (`ALL_AISLES` from `features/blueprints/aisles.ts`). User prunes/reorders
   on the details page.
3. **Save model:** details page (category ordering) keeps the implicit debounced
   auto-save; create/edit-title pages are explicit Save forms (like lists).
4. **Single-blueprint fetch:** add a `GetBlueprint` endpoint + `useSortBlueprint`
   hook so details/edit pages are deep-linkable and refresh-safe (symmetric with
   `useList`).
5. **Overview row interaction:** tapping a row opens the details page; the overflow
   menu holds Edit (title), Duplicate, Delete.
6. **Delete:** keeps the existing undo-toast flow (`useDeleteSortBlueprint`), NOT
   the type-the-name `ConfirmDialog` that lists use — blueprints already have undo.

## Routing

Convert the flat `routes/household/blueprints.tsx` into a directory mirroring
`routes/lists/`. `routeTree.gen.ts` regenerates via the router vite plugin.

| Route | Component | Purpose |
|---|---|---|
| `/household/blueprints/` (index) | `BlueprintsPage` (rewrite) | Overview — all blueprints as summary cards |
| `/household/blueprints/create` | `BlueprintCreatePage` (new) | Title form → create, then navigate to details |
| `/household/blueprints/$blueprintId/view` | `BlueprintViewPage` (new) | Details — category ordering, auto-saves |
| `/household/blueprints/$blueprintId/edit` | `BlueprintEditPage` (new) | Title form + delete |

Route shells are thin `createFileRoute` + `requireAuth` wrappers importing the
page from `features/blueprints/pages/` (canonical shape: `routes/lists/...`). The
existing settings link `to="/household/blueprints"` resolves to the index route —
no change needed.

## Frontend surfaces

### 1. Overview — `BlueprintsPage` (rewritten)

Mirrors `ListsPage`:
- `PageHeadActionBar` titled `blueprints.manage`, section `household`, with a
  single direct Add action → navigate `/household/blueprints/create`.
- `Stack` of new `BlueprintSummaryCard` components (one per blueprint from
  `useSortBlueprints`).
- Empty state: a centered card prompting creation (mirrors lists' empty state).
- Loading: `CircularProgress`; error: `Alert`.

**`BlueprintSummaryCard` (new)** — mirrors `ListSummaryCard`: shows the blueprint
name and a `Chip` with the aisle count (`{count} aisles`), plus a `MoreVert`
overflow button. Row click → navigate to `/$blueprintId/view`. `data-testid`
`blueprint-item-${name}` and a menu button testid.

**`BlueprintActionsMenu` (new)** — mirrors `ListActionsMenu`, three items:
- **Edit** → navigate `/$blueprintId/edit`
- **Duplicate** → `useCreateSortBlueprint` with `{ name: "${name} ${copySuffix}",
  categories }` (current duplicate behavior), then toast success
- **Delete** → `useDeleteSortBlueprint` (undo-toast)

### 2. Create — `BlueprintCreatePage` + `BlueprintCreateForm` (new)

Mirrors `CreateListPage` / `CreateListForm`:
- ArrowBack header + title-only form (single `TextField`, `common.name`).
- Submit: `useCreateSortBlueprint` with `{ name: name.trim(), categories: ALL_AISLES }`,
  then `navigate({ to: "/household/blueprints/$blueprintId/view", params })` to the
  new blueprint's details page to arrange aisles.
- Validation: non-empty trimmed name required.

### 3. Details — `BlueprintViewPage` (new)

The category-ordering surface:
- Loads the blueprint via `useSortBlueprint(householdId, blueprintId)`.
- `PageHeadActionBar` titled with the blueprint name, section `household`, with an
  **Edit** direct action → `/$blueprintId/edit`.
- Body: the existing `BlueprintEditor` (touch-drag fix retained), wired to local
  `included` state seeded from the loaded blueprint (keyed by blueprint id so a
  different blueprint remounts/reseeds).
- **Auto-save moves here** from `BlueprintCard`, simplified: name is fixed on this
  page, so the debounced snapshot-guarded effect saves
  `{ name: blueprint.name, categories: included }` via `useUpdateSortBlueprint`.
  Keep the `lastSaved` snapshot-ref comparison (StrictMode-safe; no save on mount,
  no refetch loop) and hold off while `included.length === 0`. Surface failures via
  `toast.error`. Show a small `CircularProgress` (`blueprint-saving` testid) while
  the update is pending.
- Loading / not-found / no-household states mirror `ListEditPage`.

### 4. Edit — `BlueprintEditPage` + `BlueprintEditForm` (new)

Mirrors `ListEditPage` / `EditListForm`:
- Loads via `useSortBlueprint`; `PageHeadActionBar` titled `blueprints.editTitle`
  with a Delete menu action.
- `BlueprintEditForm`: title `TextField` seeded once from `blueprint.name` (keyed
  by id), explicit Save/Cancel. Save calls `useUpdateSortBlueprint` with
  `{ name: trimmed, categories: blueprint.categories }` — Update requires both, so
  the title-only edit resends the current category order — then `router.history.back()`.
- Delete: `useDeleteSortBlueprint` (undo-toast) → navigate back to the overview.

## Backend

### `GetBlueprint` slice (new)

`Application/Frigorino.Features/Households/Blueprints/GetBlueprint.cs`:
- `MapGet("/{blueprintId:int}", Handle)`, `.WithName("GetBlueprint")`,
  `Produces<SortBlueprintResponse>()` + 404.
- Handler: resolve membership via `db.FindActiveMembershipAsync` (404 if none),
  then project the single active blueprint by id/householdId into
  `SortBlueprintResponse` (same projection as `GetBlueprints`, ordered categories);
  404 if not found.
- Register `blueprints.MapGetBlueprint()` in `Program.cs` next to the other
  blueprint slice registrations.

No domain or migration changes. `SortBlueprintResponse` already exists.

## Hooks

- **Add** `useSortBlueprint.ts` — query hook mirroring `useList`: spreads
  `getBlueprintOptions({ path: { householdId, blueprintId } })`, `enabled` guarded
  on both ids `> 0`, sets a `staleTime`.
- **Keep** `useSortBlueprints`, `useCreateSortBlueprint`, `useUpdateSortBlueprint`,
  `useDeleteSortBlueprint`, `useRestoreSortBlueprint`, `useApplyBlueprint`.

## Components removed / added

- **Remove** `components/BlueprintCard.tsx` (its responsibilities split across the
  new pages/forms). Verify no other references before deleting.
- **Add** `components/BlueprintSummaryCard.tsx`, `components/BlueprintActionsMenu.tsx`,
  `components/BlueprintCreateForm.tsx`, `components/BlueprintEditForm.tsx`.
- **Keep** `components/BlueprintEditor.tsx`, `components/ApplyBlueprintDialog.tsx`.

## i18n

Add keys under `blueprints.*` (and reuse `common.*` where possible), e.g.
`createBlueprint`, `editTitle`, `blueprintName`, `blueprintNameRequired`,
`aisleCount` (with `{{count}}`), plus any new page titles. Add to both
`public/locales/en/translation.json` and `de/translation.json`. Tests assert on
testids/`data-*`, never translated text.

## Verification

1. `npm run api` (new endpoint → regenerate the TS client).
2. `npm run tsc`, `npm run lint`, prettier.
3. `dotnet build` clean; full `dotnet test Application/Frigorino.sln`.
4. `docker build` as the final gate (Docker daemon must be running).
5. Manual browser verify (dev-up): create → arrange (incl. mobile drag) →
   auto-save → rename → duplicate → delete + undo, with deep-link/refresh on the
   details and edit pages.

## Risks / notes

- `routeTree.gen.ts` is generated — do not hand-edit; converting the flat route
  file to a directory regenerates it.
- The Update endpoint takes both name and categories; both the title edit (resends
  categories) and the details auto-save (resends name) must send the full pair.
- Auto-save snapshot-ref must stay StrictMode-safe (no mount-time save, no
  invalidation→refetch→resave loop).

# Long-title header overflow — design

**Date:** 2026-06-13
**Branch:** `fix/header-title-overflow`
**Bug:** "Long list/inventory titles overflow and push the action buttons off-screen" (BUGS.md)

## Problem

On detail pages, a long list/inventory/blueprint/household title does not wrap or
truncate. In `PageHeadActionBar` the title `Box` has `flex: 1` but **no
`min-width: 0`**, so a long `<h5>` cannot shrink below its content width — it
expands the flex row and pushes the trailing action buttons (edit / sort /
search / overflow) past the right edge of the viewport, where they are
unreachable.

`PageHeadActionBar` (`src/components/shared/PageHeadActionBar.tsx`) is the single
header component shared by **12 pages**. Only some of them have long,
user-content titles; the overview pages have short static titles.

## Decisions (from brainstorming)

- **Title behaviour:** wrap to multiple lines, never ellipsis-truncate. "If the
  text still doesn't fit, it grows downward."
- **Action behaviour on the detail/view pages:** *always in menu* — the header
  keeps only `back · section-icon · title · ⋮`, and every action lives in the
  overflow menu. This is the simplest, most scalable shape: adding a future
  action is just another menu item and the header geometry never changes.
- **Scope:** `PageHeadActionBar` only. Summary cards (`ListSummaryCard`,
  `InventorySummaryCard`, `BlueprintSummaryCard`) and the dashboard
  (`WelcomePage`) rows have the same class of bug but a different structure and
  are deferred to a separate pass.
- **Scope split within the shared component:** the *view/detail* pages go
  always-in-menu; the *overview* pages keep their primary `+` / settings as
  direct buttons (static titles, no overflow risk, and a visible "create" CTA
  matters).

## Design

### 1. Core overflow-safety fix — `PageHeadActionBar` (universal)

Applies to every page that uses the component, including those left otherwise
unchanged (e.g. `ManageHouseholdPage`, whose title is a user-named household).

In the header row (`PageHeadActionBar.tsx` ~lines 98–170):

- **Header row container:** change `alignItems: "center"` → `alignItems: "flex-start"`,
  so the back button, section icon, and actions pin to the **first line** of the
  title when it wraps (rather than centering against a tall multi-line block).
- **Title `Box`** (currently `sx={{ flex: 1 }}`): add `minWidth: 0` and
  `wordBreak: "break-word"`. `min-width: 0` lets the flex item shrink so the
  title wraps; `word-break` covers a single very long unbroken token.
- **Actions `Box`** (currently `sx={{ display: "flex", gap: 1, ml: "auto" }}`):
  add `flexShrink: 0` so the action cluster keeps its intrinsic width and is
  never compressed or pushed off-screen.
- **Back button + section icon:** already fixed-size; the section icon already
  has `flexShrink: 0`. Add `flexShrink: 0` to the back `IconButton` for symmetry
  so it can never be squeezed.

Net effect: a long title wraps and increases the header's height; the action
cluster stays pinned top-right and reachable. This change alone resolves the
reported bug for all pages.

No public API change in this step; it is pure styling.

### 2. Always-in-menu — the detail/view pages

Move each view page's direct actions into `menuActions` (set
`directActions={[]}`). Each relocated action gains a `text` label (required for a
menu item) and keeps its existing `icon`, `onClick`, and `testId` **unchanged**.
Behaviour of every action is preserved exactly — only its presentation moves from
an icon button to a menu row.

- **`InventoryViewPage`** (`features/inventories/pages/InventoryViewPage.tsx`):
  relocate Edit, Sort (the `getSortModeIcon(sortMode)` cycle), Search.
  - The Sort item keeps its cycle-on-tap `handleToggleSortMode` behaviour and
    surfaces the current mode via `secondaryText` (e.g. "Manual" / "A→Z"), so the
    menu row communicates state that the bare icon used to.
- **`ListViewPage`** (`features/lists/pages/ListViewPage.tsx`):
  relocate Edit, Reorder (the drag-handle toggle, testid `list-toggle-drag-handles`),
  Search. The existing "Sort by category" menu item (opens the sort dialog)
  stays. Suggested order: Edit, Reorder, Search, Sort by category.
- **`BlueprintViewPage`** (`features/blueprints/pages/BlueprintViewPage.tsx`):
  relocate the single Edit action.
- **`ManageHouseholdPage`**: already menu-only (delete). No change beyond the
  core fix in §1.

Resulting view-page header: `back · section-icon · wrapping-title · ⋮`.

### 3. Unchanged by design

- **Overview pages** — `ListsPage`, `InventoriesPage`, `BlueprintsPage`,
  `ExpiryCalendarPage`: keep their direct `+` / calendar / settings / search
  buttons. Static titles, no overflow, and the primary action should stay
  visible. They still inherit the §1 overflow-safety fix.
- **Edit pages + Settings** — `ListEditPage`, `InventoryEditPage`,
  `BlueprintEditPage`, `UserSettingsPage`: already `directActions={[]}`; no
  change.
- **Summary cards + dashboard rows:** deferred (separate bug entry).

### 4. i18n

Add the menu-item labels in both `public/locales/en/translation.json` and
`public/locales/de/translation.json`. Reuse existing keys where present
(`common.edit`, `common.delete`, and a search label if one already exists);
add new keys for "reorder items" and the inventory sort-mode labels. Exact key
names and reuse are determined during implementation by inspecting the existing
translation files. No hard-coded user-facing strings.

## Testing

- **Unit:** `PageHeadActionBar` is presentational; no new unit tests.
- **Integration (the real risk):** moving Search / Edit / Reorder from direct
  buttons into the `⋮` menu changes how they are reached. The testids survive
  (placed on each `MenuItem`), but every existing Playwright step that clicks a
  relocated action directly now needs an **"open the header `⋮` menu first"**
  step. Audit `Frigorino.IntegrationTests` for the affected testids —
  `inventory-search-button`, `list-search-button`, `list-toggle-drag-handles`,
  and any edit-from-header steps — and insert the menu-open step before each.
  Add a focused assertion that a long title wraps without the `⋮` leaving the
  viewport, asserting on testids only (never translated text).
- **Manual:** verify in the running dev stack with a deliberately long title on
  an inventory and a list (title wraps, `⋮` reachable, menu actions work), plus
  a short title (header still looks right), at phone width.

## Verification

- Frontend: `npm run tsc`, `npm run lint`, `npm run prettier` (from `ClientApp/`).
- Regenerate API client is **not** needed (no backend/DTO change).
- Full `dotnet test Application/Frigorino.sln` to run the integration suite after
  the IT step updates.

## Out of scope / follow-ups

- Summary-card and dashboard title overflow (same bug class, different markup).
- Any redesign of the inventory sort UX (cycle vs. picker) — behaviour is
  preserved as-is, only relocated.

# Long-title Header Overflow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop long list/inventory/blueprint/household titles from pushing the header action buttons off-screen, and move the detail/view pages' actions into the `⋮` overflow menu so the header stays compact and scalable.

**Architecture:** One shared header component, `PageHeadActionBar`, gets a universal overflow-safety fix (title `min-width:0` + wrap, actions `flex-shrink:0`, row top-aligned). On top of that, the three *view* pages (inventory, list, blueprint) relocate their direct action buttons into the existing `⋮` menu (set `directActions={[]}`, populate `menuActions` with labels). Overview/edit/settings pages are unchanged. Three integration-test steps that clicked relocated buttons directly are updated to open the header menu first.

**Tech Stack:** React 19 + MUI v6 + TanStack Router (SPA), i18next (en/de), Reqnroll + Playwright integration tests.

**Branch:** `fix/header-title-overflow` (already created off `stage`). Spec: `docs/superpowers/specs/2026-06-13-header-title-overflow-design.md`.

**Conventions to honor (from repo memory/CLAUDE.md):**
- Frontend verify = `npm run tsc` + `npm run lint` + `npm run prettier` from `Application/Frigorino.Web/ClientApp/`.
- Integration harness serves the SPA from `ClientApp/build` — run `npm run build` after React edits before running IT.
- Tests assert on testids / `data-*`, never translated text.
- No `Co-Authored-By` trailers in commits.

---

### Task 1: Core overflow-safety fix in `PageHeadActionBar`

Pure styling change to the shared header. Makes every page overflow-safe. No API change.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/shared/PageHeadActionBar.tsx`

- [ ] **Step 1: Top-align the header row**

In the header row `Box` (currently lines ~98-104), change `alignItems`:

```tsx
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "flex-start",
                            gap: 2,
                        }}
                    >
```

- [ ] **Step 2: Make the back button non-shrinkable**

The back `IconButton` (currently line ~105):

```tsx
                        <IconButton
                            onClick={handleBack}
                            sx={{ p: 1, flexShrink: 0 }}
                        >
                            <ArrowBack />
                        </IconButton>
```

- [ ] **Step 3: Let the title shrink and wrap**

The title `Box` (currently line ~123, `sx={{ flex: 1 }}`):

```tsx
                        <Box sx={{ flex: 1, minWidth: 0, wordBreak: "break-word" }}>
```

Leave the inner `Typography` as-is — with the parent now allowed to shrink, it wraps by default (no `noWrap`).

- [ ] **Step 4: Pin the actions cluster**

The actions `Box` (currently line ~143, `sx={{ display: "flex", gap: 1, ml: "auto" }}`):

```tsx
                        <Box sx={{ display: "flex", gap: 1, ml: "auto", flexShrink: 0 }}>
```

- [ ] **Step 5: Type-check and lint**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run tsc && npm run lint
```

Expected: both pass, no errors.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/shared/PageHeadActionBar.tsx
git commit -m "fix: keep header title from pushing actions off-screen"
```

---

### Task 2: Add i18n keys for the relocated menu actions

New labels needed: an inventory "Sort order" item with per-mode secondary text, and a list "Reorder items" item. Edit/Search/Delete reuse existing `common.*` keys.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add English keys**

Into the existing `"inventory"` object in `en/translation.json`, add:

```json
    "sortOrder": "Sort order",
    "sortManual": "Manual order",
    "sortExpiryAsc": "Soonest expiry first",
    "sortExpiryDesc": "Latest expiry first",
```

Into the existing `"lists"` object in `en/translation.json`, add:

```json
    "reorderItems": "Reorder items",
```

- [ ] **Step 2: Add German keys**

Into the existing `"inventory"` object in `de/translation.json`, add:

```json
    "sortOrder": "Sortierung",
    "sortManual": "Manuelle Reihenfolge",
    "sortExpiryAsc": "Bald ablaufende zuerst",
    "sortExpiryDesc": "Spät ablaufende zuerst",
```

Into the existing `"lists"` object in `de/translation.json`, add:

```json
    "reorderItems": "Einträge umsortieren",
```

- [ ] **Step 3: Validate JSON**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
node -e "JSON.parse(require('fs').readFileSync('public/locales/en/translation.json','utf8')); JSON.parse(require('fs').readFileSync('public/locales/de/translation.json','utf8')); console.log('OK')"
```

Expected: prints `OK`.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "i18n: labels for header sort/reorder menu actions"
```

---

### Task 3: `InventoryViewPage` — move actions into the `⋮` menu

Relocate Edit, Sort (cycle), Search from `directActions` into `menuActions`. Sort keeps its cycle-on-tap behavior and shows the current mode via `secondaryText`. Add a `menuButtonTestId` so the IT can open the menu.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryViewPage.tsx`

- [ ] **Step 1: Render sort-mode icons at menu size**

Update `getSortModeIcon` (currently lines ~130-140) so its icons match the small menu-item icons:

```tsx
    const getSortModeIcon = (mode: SortMode) => {
        switch (mode) {
            case "expiryDateAsc":
                return <Schedule fontSize="small" />;
            case "expiryDateDesc":
                return (
                    <Schedule
                        fontSize="small"
                        style={{ transform: "scaleY(-1)" }}
                    />
                );
            case "custom":
            default:
                return <DragIndicator fontSize="small" />;
        }
    };
```

- [ ] **Step 2: Add a sort-mode label helper**

Add this just below `getSortModeIcon` (uses the keys from Task 2):

```tsx
    const getSortModeLabel = (mode: SortMode) => {
        switch (mode) {
            case "expiryDateAsc":
                return t("inventory.sortExpiryAsc");
            case "expiryDateDesc":
                return t("inventory.sortExpiryDesc");
            case "custom":
            default:
                return t("inventory.sortManual");
        }
    };
```

- [ ] **Step 3: Replace `directActions`/`menuActions`**

Replace the block currently at lines ~201-210 (`const directActions = [...]; const menuActions: HeadNavigationAction[] = [];`) with:

```tsx
    const directActions: HeadNavigationAction[] = [];
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("common.edit"),
            icon: <Edit fontSize="small" />,
            onClick: handleEdit,
            testId: "inventory-edit-button",
        },
        {
            text: t("inventory.sortOrder"),
            secondaryText: getSortModeLabel(sortMode),
            icon: getSortModeIcon(sortMode),
            onClick: handleToggleSortMode,
            testId: "inventory-sort-toggle",
        },
        {
            text: t("common.search"),
            icon: <Search fontSize="small" />,
            onClick: handleToggleSearch,
            testId: "inventory-search-button",
        },
    ];
```

- [ ] **Step 4: Pass `directActions={[]}` and a menu testid to the header**

Update the `<PageHeadActionBar>` usage (currently lines ~221-227):

```tsx
            <PageHeadActionBar
                title={inventory.name || t("inventory.untitledInventory")}
                subtitle={inventory.description || undefined}
                section="inventory"
                directActions={directActions}
                menuActions={menuActions}
                menuButtonTestId="inventory-header-menu-toggle"
            />
```

- [ ] **Step 5: Type-check and lint**

Run from `ClientApp/`:

```bash
npm run tsc && npm run lint
```

Expected: pass. (`HeadNavigationAction` is already imported in this file.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryViewPage.tsx
git commit -m "fix: move inventory header actions into overflow menu"
```

---

### Task 4: `ListViewPage` — move actions into the `⋮` menu

Relocate Edit, Reorder (drag-handle toggle), Search into `menuActions` alongside the existing "Sort by category". Add a `menuButtonTestId`.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx`

- [ ] **Step 1: Replace `directActions`/`menuActions`**

Replace the block currently at lines ~306-326 with (keep the existing testids; preserve handler references `handleEdit`, `handleToggleDragHandles`, `handleToggleSearch`, `setSortDialogOpen`):

```tsx
    const directActions: HeadNavigationAction[] = [];
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("common.edit"),
            icon: <Edit fontSize="small" />,
            onClick: handleEdit,
            testId: "list-edit-button",
        },
        {
            text: t("lists.reorderItems"),
            icon: <DragHandle fontSize="small" />,
            onClick: handleToggleDragHandles,
            testId: "list-toggle-drag-handles",
        },
        {
            text: t("common.search"),
            icon: <Search fontSize="small" />,
            onClick: handleToggleSearch,
            testId: "list-search-button",
        },
        {
            text: t("blueprints.sortByCategory"),
            icon: <Sort fontSize="small" />,
            onClick: () => setSortDialogOpen(true),
            testId: "list-sort-by-category",
        },
    ];
```

- [ ] **Step 2: Pass `directActions={[]}` and a menu testid to the header**

Update the `<PageHeadActionBar>` usage (currently lines ~337-343):

```tsx
            <PageHeadActionBar
                title={list.name || t("lists.untitledList")}
                subtitle={list.description || undefined}
                section="lists"
                directActions={directActions}
                menuActions={menuActions}
                menuButtonTestId="list-header-menu-toggle"
            />
```

- [ ] **Step 3: Type-check and lint**

Run from `ClientApp/`:

```bash
npm run tsc && npm run lint
```

Expected: pass. (`Edit`, `DragHandle`, `Search`, `Sort`, and `HeadNavigationAction` are already imported in this file.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx
git commit -m "fix: move list header actions into overflow menu"
```

---

### Task 5: `BlueprintViewPage` — move Edit into the `⋮` menu

Relocate the single Edit action into the menu and add a `menuButtonTestId`.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/blueprints/pages/BlueprintViewPage.tsx`

- [ ] **Step 1: Replace the `<PageHeadActionBar>` usage**

Replace the block currently at lines ~157-175 with:

```tsx
            <PageHeadActionBar
                title={blueprint.name}
                section="blueprints"
                maxWidth="md"
                directActions={[]}
                menuActions={[
                    {
                        text: t("common.edit"),
                        icon: <Edit fontSize="small" />,
                        onClick: () =>
                            navigate({
                                to: "/household/blueprints/$blueprintId/edit",
                                params: {
                                    blueprintId: blueprint.id.toString(),
                                },
                            }),
                        testId: "blueprint-edit-title",
                    },
                ]}
                menuButtonTestId="blueprint-header-menu-toggle"
            />
```

- [ ] **Step 2: Type-check and lint**

Run from `ClientApp/`:

```bash
npm run tsc && npm run lint
```

Expected: pass. (`Edit` is already imported in this file.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/blueprints/pages/BlueprintViewPage.tsx
git commit -m "fix: move blueprint header edit into overflow menu"
```

---

### Task 6: Update integration-test steps to open the header menu first

Three existing steps click a now-relocated action directly. Each must open the header `⋮` menu (the testids added in Tasks 3-4) before clicking the menu item. MUI renders the menu in a portal; clicking the same testid still works once the menu is open.

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Inventories/InventorySearchSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/ListSearchSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/ListItemSteps.cs`

- [ ] **Step 1: Rebuild the SPA so the IT harness serves the new markup**

The integration harness serves `ClientApp/build`, not live source. Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run build
```

Expected: `tsc -b && vite build` completes, writes `ClientApp/build`.

- [ ] **Step 2: Open the inventory menu before clicking Search**

In `InventorySearchSteps.cs`, replace the body of `WhenIOpenTheInventorySearch` (currently line 9):

```csharp
    [When("I open the inventory search")]
    public async Task WhenIOpenTheInventorySearch()
    {
        await ctx.Page.GetByTestId("inventory-header-menu-toggle").ClickAsync();
        await ctx.Page.GetByTestId("inventory-search-button").ClickAsync();
    }
```

- [ ] **Step 3: Open the list menu before clicking Search**

In `ListSearchSteps.cs`, replace the body of `WhenIOpenTheListSearch` (currently line 21):

```csharp
    [When("I open the list search")]
    public async Task WhenIOpenTheListSearch()
    {
        await ctx.Page.GetByTestId("list-header-menu-toggle").ClickAsync();
        await ctx.Page.GetByTestId("list-search-button").ClickAsync();
    }
```

- [ ] **Step 4: Open the list menu before toggling drag handles**

In `ListItemSteps.cs`, update `WhenIEnableDragHandles` (currently lines 75-82):

```csharp
    [When("I enable drag handles")]
    public async Task WhenIEnableDragHandles()
    {
        await ctx.Page.GetByTestId("list-header-menu-toggle").ClickAsync();
        await ctx.Page.GetByTestId("list-toggle-drag-handles").ClickAsync();
        // Wait for at least one drag handle to render so the next "I drag" step doesn't race
        // the toggle re-render.
        await ctx.Page.Locator("[data-testid^='drag-handle-item-']").First.WaitForAsync();
    }
```

- [ ] **Step 5: Run the inventory + list integration scenarios**

Run the affected feature areas first for a fast signal:

```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Inventor|FullyQualifiedName~List"
```

Expected: PASS. Confirm the run actually executed scenarios (check the "Passed!" total is non-zero) — per repo note, a too-narrow filter can report green without running anything.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Inventories/InventorySearchSteps.cs Application/Frigorino.IntegrationTests/Slices/Lists/ListSearchSteps.cs Application/Frigorino.IntegrationTests/Slices/Lists/ListItemSteps.cs
git commit -m "test: open header menu before clicking relocated actions"
```

---

### Task 7: Manual verification, BUGS.md cleanup, full verify

CSS wrap behavior is verified manually in the running app (Playwright cannot reliably assert horizontal off-screen overflow — a manual browser check is the net). Then remove the bug entry and run the full gate.

**Files:**
- Modify: `BUGS.md`

- [ ] **Step 1: Bring up the dev stack (if not already up)**

```bash
powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1
```

Read `.dev/stack.json` for the Vite URL.

- [ ] **Step 2: Manually verify in the browser (phone width ~390px)**

Using the running SPA (seed via the UI or API — dev data is disposable):
- Give a list (and an inventory) a very long name (e.g. "Weekly Groceries For The Whole Extended Family"). Open it.
  - Expected: the title **wraps** to multiple lines; the `⋮` button stays on-screen and is clickable; opening it shows Edit / Reorder (lists) / Search / Sort.
- Open an inventory with a long name.
  - Expected: title wraps; `⋮` opens Edit / Sort order (with current mode as secondary text) / Search.
- Check a **short** title on each.
  - Expected: header looks correct, back button + section icon + `⋮` aligned to the title's first line.
- Confirm an **overview** page (Lists / Inventories) is unchanged — the primary `+` is still a visible button.

- [ ] **Step 3: Remove the bug entry from BUGS.md**

Delete the section `## Long list/inventory titles overflow and push the action buttons off-screen` and its body (the paragraphs through "...vs. wrapping the title while keeping the actions pinned and reachable.").

- [ ] **Step 4: Frontend verify**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run tsc && npm run lint && npm run prettier
```

Expected: all pass / clean.

- [ ] **Step 5: Full solution test gate**

```bash
dotnet test Application/Frigorino.sln
```

Expected: PASS (runs `Frigorino.Test` + `Frigorino.IntegrationTests`). If the backend dev server is holding locked `bin/Debug` DLLs, tear down the dev stack first (`scripts/dev-down.ps1`) and re-run.

- [ ] **Step 6: Commit**

```bash
git add BUGS.md
git commit -m "docs(bugs): remove fixed long-title header overflow entry"
```

---

## Notes for the implementer

- **Do not** touch `ListsPage`, `InventoriesPage`, `BlueprintsPage`, `ExpiryCalendarPage` (overview pages keep their `+`/settings buttons), nor the edit/settings pages (already menu-only). They inherit the Task 1 fix for free.
- **Summary cards** (`ListSummaryCard`/`InventorySummaryCard`/`BlueprintSummaryCard`) and the dashboard rows are a separate, deferred bug — out of scope here.
- The `⋮` menu only renders when `menuActions.length > 0` (see `PageHeadActionBar.tsx` line ~158). After Tasks 3-5 every view page has menu actions, so the toggle is always present.
- Keep all existing testids exactly as written so unmodified IT steps keep matching; only the *interaction path* (open menu first) changes.

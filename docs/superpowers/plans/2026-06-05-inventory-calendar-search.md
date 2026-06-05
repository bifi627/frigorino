# In-view Item Search (Inventory + Expiry Calendar) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a toggle-via-icon text filter to the inventory item list and the expiry calendar that hides items whose text doesn't match a case-insensitive substring query, client-side.

**Architecture:** Both views already fetch all their items in one request, so filtering is pure client-side against the in-memory array — no backend, no new endpoint, no debounce. A shared `SearchInputRow` component renders the expanding input; each page owns its `searchOpen`/`searchQuery` state and mounts a search-toggle icon through the existing `PageHeadActionBar` `directActions` mechanism. A pure `matchesQuery` helper does the matching and is reused by both views.

**Tech Stack:** React 19, TypeScript, MUI, TanStack Router/Query, i18next; Reqnroll + Playwright integration tests.

**Testing approach (read before starting):** The frontend has **no JS test runner** (per CLAUDE.md), so the match logic is verified through the integration suite, not a unit test. The integration suite is slow (needs `npm run build` + Postgres Testcontainers), so per-task checks are `npm run tsc` + `npm run lint`, with a **manual browser verification** (dev-up + Playwright MCP) after Tasks 3 and 4, and the full integration scenarios added in Task 5. This matches the project's verification-by-confidence convention. Run all frontend npm commands from `Application/Frigorino.Web/ClientApp/`.

**Spec:** `docs/superpowers/specs/2026-06-05-inventory-calendar-search-design.md`

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `src/utils/searchUtils.ts` | Pure `matchesQuery(text, query)` predicate | Create |
| `src/components/shared/SearchInputRow.tsx` | Shared expanding search input row (field + clear button + autofocus) | Create |
| `public/locales/en/translation.json` | `inventory.searchPlaceholder`, `inventory.noSearchMatches` | Modify |
| `public/locales/de/translation.json` | Same keys (German) | Modify |
| `src/features/inventories/pages/InventoryViewPage.tsx` | Own search state, mount toggle + `SearchInputRow`, pass query down | Modify |
| `src/features/inventories/items/components/InventoryContainer.tsx` | Filter items, disable drag while filtering, no-match empty state | Modify |
| `src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx` | Own search state, mount toggle + `SearchInputRow`, filter items before building events | Modify |
| `Application/Frigorino.IntegrationTests/Slices/Inventories/InventorySearch.feature` | BDD scenarios for inventory + calendar search | Create |
| `Application/Frigorino.IntegrationTests/Slices/Inventories/InventorySearchSteps.cs` | New Playwright steps for search | Create |

---

## Task 1: Search predicate helper + i18n keys

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/utils/searchUtils.ts`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Create the pure predicate helper**

Create `src/utils/searchUtils.ts`:

```ts
/**
 * Case-insensitive substring match for the in-view item search.
 * An empty or whitespace-only query matches everything (no filter).
 */
export const matchesQuery = (text: string, query: string): boolean => {
    const trimmed = query.trim().toLowerCase();
    if (trimmed.length === 0) {
        return true;
    }
    return text.toLowerCase().includes(trimmed);
};
```

- [ ] **Step 2: Add the English i18n keys**

In `public/locales/en/translation.json`, inside the `"inventory"` object, add these two keys (next to `"noItems"`):

```json
        "searchPlaceholder": "Search items",
        "noSearchMatches": "No items match your search.",
```

- [ ] **Step 3: Add the German i18n keys**

In `public/locales/de/translation.json`, inside the `"inventory"` object, add:

```json
        "searchPlaceholder": "Artikel suchen",
        "noSearchMatches": "Keine Artikel entsprechen deiner Suche.",
```

> Note: `common.search` ("Search"/"Suchen") and `inventory.calendar.emptyFiltered` ("No items match your filters.") already exist and are reused — do not re-add them.

- [ ] **Step 4: Verify it type-checks and lints**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc && npm run lint`
Expected: both pass with no errors. (Also confirms the JSON edits are valid.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/utils/searchUtils.ts Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(search): add matchesQuery helper and search i18n keys"
```

---

## Task 2: Shared SearchInputRow component

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/shared/SearchInputRow.tsx`

- [ ] **Step 1: Create the component**

Create `src/components/shared/SearchInputRow.tsx`:

```tsx
import { Close } from "@mui/icons-material";
import {
    Container,
    IconButton,
    InputAdornment,
    TextField,
} from "@mui/material";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { featureContentPx } from "../../theme";

interface SearchInputRowProps {
    open: boolean;
    query: string;
    onQueryChange: (value: string) => void;
    onClose: () => void;
    placeholder?: string;
    /** Test-id stem: renders `${testIdPrefix}-input` and `${testIdPrefix}-clear`. */
    testIdPrefix: string;
}

export const SearchInputRow = ({
    open,
    query,
    onQueryChange,
    onClose,
    placeholder,
    testIdPrefix,
}: SearchInputRowProps) => {
    const { t } = useTranslation();
    const inputRef = useRef<HTMLInputElement>(null);

    // rAF so focus lands after the row mounts (mirrors the composer autofocus pattern).
    useEffect(() => {
        if (!open) {
            return;
        }
        const id = requestAnimationFrame(() => inputRef.current?.focus());
        return () => cancelAnimationFrame(id);
    }, [open]);

    if (!open) {
        return null;
    }

    return (
        <Container
            maxWidth="sm"
            sx={{ px: featureContentPx, pb: 1, flexShrink: 0 }}
        >
            <TextField
                inputRef={inputRef}
                value={query}
                onChange={(event) => onQueryChange(event.target.value)}
                placeholder={placeholder ?? t("common.search")}
                size="small"
                fullWidth
                slotProps={{
                    htmlInput: { "data-testid": `${testIdPrefix}-input` },
                    input: {
                        endAdornment: (
                            <InputAdornment position="end">
                                <IconButton
                                    size="small"
                                    onClick={onClose}
                                    data-testid={`${testIdPrefix}-clear`}
                                    aria-label={t("common.search")}
                                >
                                    <Close fontSize="small" />
                                </IconButton>
                            </InputAdornment>
                        ),
                    },
                }}
            />
        </Container>
    );
};
```

- [ ] **Step 2: Verify it type-checks and lints**

Run: `npm run tsc && npm run lint`
Expected: both pass. (The component is not yet imported anywhere — that's fine; tsc covers it.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/shared/SearchInputRow.tsx
git commit -m "feat(search): add shared SearchInputRow component"
```

---

## Task 3: Wire search into the inventory view

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryViewPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryContainer.tsx`

- [ ] **Step 1: Add filtering + drag-disable + no-match state to InventoryContainer**

In `InventoryContainer.tsx`:

(a) Add imports at the top (after the existing imports):

```tsx
import { Container, Paper, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { matchesQuery } from "../../../../utils/searchUtils";
```

(Replace the existing `import { Container } from "@mui/material";` line with the combined `Container, Paper, Typography` import above.)

(b) Add `searchQuery` to the props interface:

```tsx
interface InventoryContainerProps {
    householdId: number;
    inventoryId: number;
    editingItem: InventoryItemResponse | null;
    onEdit: (item: InventoryItemResponse) => void;
    sortMode?: SortMode;
    searchQuery?: string;
}
```

(c) Destructure `searchQuery = ""` in the component params (alongside `sortMode = "custom"`).

(d) Inside the component body, after the `useInventoryItems` / mutation hooks, compute the filtered list and flags:

```tsx
        const { t } = useTranslation();
        const trimmedQuery = searchQuery.trim();
        const filterActive = trimmedQuery.length > 0;
        const visibleItems = filterActive
            ? items.filter((item) => matchesQuery(item.text, trimmedQuery))
            : items;
        const showNoMatches =
            filterActive && !isLoading && !error && visibleItems.length === 0;
```

(e) In the JSX, change the `SortableList` block so it renders the no-match message instead when `showNoMatches`, passes `visibleItems`, and disables drag handles while filtering. Replace the existing `<SortableList ... />` element with:

```tsx
                {showNoMatches ? (
                    <Paper
                        elevation={0}
                        data-testid="inventory-search-no-results"
                        sx={{
                            p: 3,
                            textAlign: "center",
                            border: "2px dashed",
                            borderColor: "divider",
                            mx: 1,
                        }}
                    >
                        <Typography variant="body2" color="text.secondary">
                            {t("inventory.noSearchMatches")}
                        </Typography>
                    </Paper>
                ) : (
                    <SortableList
                        items={visibleItems}
                        isLoading={isLoading}
                        error={error}
                        onReorder={async (itemId, afterId) => {
                            await reorderMutation.mutateAsync({
                                path: { householdId, inventoryId, itemId },
                                body: { afterId },
                            });
                        }}
                        onToggleStatus={async () => {}}
                        onEdit={onEdit}
                        onDelete={async (itemId) => {
                            await deleteMutation.mutateAsync({
                                path: { householdId, inventoryId, itemId },
                            });
                        }}
                        editingItem={editingItem}
                        showDragHandles={sortMode === "custom" && !filterActive}
                        sortMode={sortMode}
                        renderContent={(item) => (
                            <InventoryItemContent item={item} />
                        )}
                    />
                )}
```

- [ ] **Step 2: Add search state + toggle + SearchInputRow to InventoryViewPage**

In `InventoryViewPage.tsx`:

(a) Add to imports:

```tsx
import { DragIndicator, Edit, Schedule, Search } from "@mui/icons-material";
import { SearchInputRow } from "../../../components/shared/SearchInputRow";
```

(Replace the existing `import { DragIndicator, Edit, Schedule } from "@mui/icons-material";` line with the `Search`-inclusive one above.)

(b) Add state next to the existing `useState` calls (after `sortMode`):

```tsx
    const [searchOpen, setSearchOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");
```

(c) Add a toggle handler next to `handleToggleSortMode`:

```tsx
    const handleToggleSearch = useCallback(() => {
        setSearchOpen((prev) => {
            // Clear the query when collapsing so the filter resets (ephemeral by design).
            if (prev) {
                setSearchQuery("");
            }
            return !prev;
        });
    }, []);
```

(d) Add the search icon to `directActions` (append as the third entry):

```tsx
    const directActions = [
        { icon: <Edit />, onClick: handleEdit },
        { icon: getSortModeIcon(sortMode), onClick: handleToggleSortMode },
        {
            icon: <Search />,
            onClick: handleToggleSearch,
            testId: "inventory-search-button",
        },
    ];
```

(e) Render `SearchInputRow` between `PageHeadActionBar` and `InventoryContainer`, and pass `searchQuery` into the container:

```tsx
            <PageHeadActionBar
                title={inventory.name || t("inventory.untitledInventory")}
                subtitle={inventory.description || undefined}
                section="inventory"
                directActions={directActions}
                menuActions={menuActions}
            />

            <SearchInputRow
                open={searchOpen}
                query={searchQuery}
                onQueryChange={setSearchQuery}
                onClose={handleToggleSearch}
                placeholder={t("inventory.searchPlaceholder")}
                testIdPrefix="inventory-search"
            />

            <InventoryContainer
                ref={scrollContainerRef}
                householdId={householdId}
                inventoryId={inventoryId}
                editingItem={editingItem}
                onEdit={setEditingItem}
                sortMode={sortMode}
                searchQuery={searchQuery}
            />
```

- [ ] **Step 3: Verify type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: both pass.

- [ ] **Step 4: Manual browser verification**

Bring up the dev stack (use the `/dev-up` skill) and, via Playwright MCP at the printed SPA URL:
1. Open an inventory with several items (seed a few if empty).
2. Tap the search icon (`inventory-search-button`) → the input row appears and is focused.
3. Type a substring matching one item → non-matching `toggle-item-*` rows disappear, the matching one remains; the `drag-handle-item-*` handles are gone while filtering.
4. Type a non-matching string → `inventory-search-no-results` message appears.
5. Tap the clear (X) button → filter resets, all items return, drag handles reappear (in custom sort mode).

Expected: all of the above behave as described. Fix any runtime issues before committing.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryViewPage.tsx Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryContainer.tsx
git commit -m "feat(search): filter inventory items via header search toggle"
```

---

## Task 4: Wire search into the expiry calendar

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx`

- [ ] **Step 1: Add imports**

Add to `ExpiryCalendarPage.tsx` imports:

```tsx
import { Search, Tune } from "@mui/icons-material";
import { SearchInputRow } from "../../../../components/shared/SearchInputRow";
import { matchesQuery } from "../../../../utils/searchUtils";
```

(Replace the existing `import { Tune } from "@mui/icons-material";` line with the `Search, Tune` one above.)

- [ ] **Step 2: Add search state**

After the existing `const [settingsOpen, setSettingsOpen] = useState(false);` line, add:

```tsx
    const [searchOpen, setSearchOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");

    const handleToggleSearch = useCallback(() => {
        setSearchOpen((prev) => {
            if (prev) {
                setSearchQuery("");
            }
            return !prev;
        });
    }, []);
```

Add `useCallback` to the existing React import (`import { useCallback, useEffect, useMemo, useState } from "react";`).

- [ ] **Step 3: Filter items before building events**

Replace the existing `events` memo so it builds from a search-filtered list:

```tsx
    const trimmedSearch = searchQuery.trim();
    const searchedItems = useMemo(() => {
        if (trimmedSearch.length === 0) {
            return items ?? [];
        }
        return (items ?? []).filter((item) =>
            matchesQuery(item.text, trimmedSearch),
        );
    }, [items, trimmedSearch]);

    const events = useMemo(
        () =>
            buildExpiryEvents(searchedItems, todayIsoDate(), levelColor, {
                windowDays,
                fullRunway,
                levels,
            }),
        [searchedItems, levelColor, windowDays, fullRunway, levels],
    );
```

> The existing `emptyFiltered` empty-state (`events.length === 0 && (items?.length ?? 0) > 0`) already covers "search matched nothing" — no new empty-state code needed. Selection auto-clearing also already handles an item leaving the calendar (it reads `items`, the unfiltered list, so a selected-but-filtered-out item simply dims with the rest until cleared — acceptable).

- [ ] **Step 4: Add the search toggle to the header and mount SearchInputRow**

Add the search icon to the `PageHeadActionBar` `directActions` (append after the `Tune` action):

```tsx
                directActions={[
                    {
                        icon: <Tune />,
                        onClick: () => setSettingsOpen(true),
                        testId: "calendar-settings-button",
                    },
                    {
                        icon: <Search />,
                        onClick: handleToggleSearch,
                        testId: "calendar-search-button",
                    },
                ]}
```

Then mount `SearchInputRow` immediately after the closing `/>` of `PageHeadActionBar` (before the `<Container maxWidth="sm" sx={pageContainerSx}>`):

```tsx
            <SearchInputRow
                open={searchOpen}
                query={searchQuery}
                onQueryChange={setSearchQuery}
                onClose={handleToggleSearch}
                placeholder={t("inventory.searchPlaceholder")}
                testIdPrefix="calendar-search"
            />
```

- [ ] **Step 5: Verify type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: both pass.

- [ ] **Step 6: Manual browser verification**

With the dev stack up, via Playwright MCP:
1. Open the expiry calendar (seed 2+ items with expiry dates if empty).
2. Tap `calendar-search-button` → input appears, focused.
3. Type a substring matching one item → only that `cal-event-*` bar renders; the others disappear.
4. Type a non-matching string → the `calendar-empty-filtered` message shows.
5. Confirm search ANDs with a level toggle: with a query active, toggling off the matching item's level removes it too.
6. Tap clear → all events return.

Expected: behaves as described. Fix runtime issues before committing.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx
git commit -m "feat(search): filter expiry calendar items via header search toggle"
```

---

## Task 5: Integration tests

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/InventorySearch.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/InventorySearchSteps.cs`

> Reuses existing steps: `there is an inventory named {string}`, `there is an inventory named {string} with item {string}`, `the inventory {string} also has item {string}`, `I open the inventory {string}`, `I open the inventories overview`, `I open the expiry calendar from the header`, `the calendar shows the item {string}`, `the calendar does not show the item {string}`, `an inventory {string} has an item {string} expiring in {int} days`. Only the search-specific steps below are new.

- [ ] **Step 1: Write the feature file**

Create `InventorySearch.feature`:

```gherkin
Feature: Inventory and Calendar Item Search

  Background:
    Given I am logged in with an active household

  Scenario: Searching the inventory hides non-matching items
    Given there is an inventory named "Pantry" with item "Flour"
    And the inventory "Pantry" also has item "Sugar"
    When I open the inventory "Pantry"
    And I open the inventory search
    And I search the inventory for "Flo"
    Then "Flour" appears in the inventory
    And "Sugar" no longer appears in the inventory

  Scenario: Searching disables drag handles, clearing restores them
    Given there is an inventory named "Pantry" with item "Flour"
    When I open the inventory "Pantry"
    Then the inventory item "Flour" shows a drag handle
    When I open the inventory search
    And I search the inventory for "Flour"
    Then the inventory item "Flour" shows no drag handle
    When I clear the inventory search
    Then the inventory item "Flour" shows a drag handle

  Scenario: A non-matching inventory search shows the no-results message
    Given there is an inventory named "Pantry" with item "Flour"
    When I open the inventory "Pantry"
    And I open the inventory search
    And I search the inventory for "zzz"
    Then the inventory search shows no results

  Scenario: Searching the calendar hides non-matching items
    Given an inventory "Fridge" has an item "Milk" expiring in 10 days
    And an inventory "Fridge" has an item "Rice" expiring in 10 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Milk"
    When I open the calendar search
    And I search the calendar for "Mil"
    Then the calendar shows the item "Milk"
    And the calendar does not show the item "Rice"
```

- [ ] **Step 2: Write the new step definitions**

Create `InventorySearchSteps.cs`:

```csharp
namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class InventorySearchSteps(ScenarioContextHolder ctx)
{
    [When("I open the inventory search")]
    public async Task WhenIOpenTheInventorySearch()
    {
        await ctx.Page.GetByTestId("inventory-search-button").ClickAsync();
    }

    [When("I search the inventory for {string}")]
    public async Task WhenISearchTheInventoryFor(string query)
    {
        await ctx.Page.GetByTestId("inventory-search-input").FillAsync(query);
    }

    [When("I clear the inventory search")]
    public async Task WhenIClearTheInventorySearch()
    {
        await ctx.Page.GetByTestId("inventory-search-clear").ClickAsync();
    }

    [Then("the inventory item {string} shows a drag handle")]
    public async Task ThenTheInventoryItemShowsADragHandle(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"drag-handle-item-{itemText}"))
            .ToBeVisibleAsync();
    }

    [Then("the inventory item {string} shows no drag handle")]
    public async Task ThenTheInventoryItemShowsNoDragHandle(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"drag-handle-item-{itemText}"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the inventory search shows no results")]
    public async Task ThenTheInventorySearchShowsNoResults()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("inventory-search-no-results"))
            .ToBeVisibleAsync();
    }

    [When("I open the calendar search")]
    public async Task WhenIOpenTheCalendarSearch()
    {
        await ctx.Page.GetByTestId("calendar-search-button").ClickAsync();
    }

    [When("I search the calendar for {string}")]
    public async Task WhenISearchTheCalendarFor(string query)
    {
        await ctx.Page.GetByTestId("calendar-search-input").FillAsync(query);
    }
}
```

- [ ] **Step 3: Build the SPA so the new test-ids are served**

The integration harness serves the SPA from `ClientApp/build`, not live source. Run (from `Application/Frigorino.Web/ClientApp/`):

`npm run build`
Expected: build succeeds, `build/` updated.

- [ ] **Step 4: Run the new integration scenarios**

Run (from repo root): `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~InventorySearch"`
Expected: all five scenarios PASS. (Requires Docker Desktop running for Testcontainers — if it errors with daemon-unreachable, ask the user to start it.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Inventories/InventorySearch.feature Application/Frigorino.IntegrationTests/Slices/Inventories/InventorySearchSteps.cs
git commit -m "test(search): integration scenarios for inventory + calendar search"
```

---

## Task 6: Full verification + branch wrap-up

**Files:** none (verification only)

- [ ] **Step 1: Frontend verification gate**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run lint && npm run tsc && npm run prettier`
Expected: lint clean, type-check clean, prettier writes/reports no diffs. Commit any prettier-only changes if produced:

```bash
git commit -am "style: prettier" || true
```

- [ ] **Step 2: Full solution test run**

Run (from repo root): `dotnet test Application/Frigorino.sln`
Expected: all tests pass (Frigorino.Test + Frigorino.IntegrationTests). Capture the pass/fail summary lines — do not trust a piped tail exit code.

- [ ] **Step 3: Docker build (drift gate)**

Run (from repo root): `docker build -f Application/Dockerfile -t frigorino .`
Expected: image builds successfully (catches any SPA/pipeline drift before Railway does). If the Docker daemon is unreachable, ask the user to start Docker Desktop.

- [ ] **Step 4: Tear-down note**

If the dev stack was brought up for manual verification and is no longer needed, leave it to the user (do not auto `/dev-down`). The branch `feat/inventory-search` is ready; use the `superpowers:finishing-a-development-branch` skill to decide on merge/PR back toward `stage`.

---

## Self-Review notes

- **Spec coverage:** toggle-via-icon entry point (Tasks 3/4), case-insensitive substring hide (Task 1 + 3/4), client-side only (no backend tasks), drag disabled while filtering (Task 3, `showDragHandles={sortMode === "custom" && !filterActive}`), ephemeral reset-on-close (toggle handlers clear `searchQuery`), inventory no-match empty state (Task 3, `inventory-search-no-results`), calendar reuses existing `emptyFiltered` (Task 4 note), shared component (Task 2), tests targeting testids not translated text (Task 5). All spec points map to a task.
- **Type consistency:** `matchesQuery(text, query)` signature is identical across helper, InventoryContainer, and ExpiryCalendarPage. `SearchInputRow` prop names (`open`, `query`, `onQueryChange`, `onClose`, `placeholder`, `testIdPrefix`) are used identically at both call sites. Test-id stems (`inventory-search`, `calendar-search`) match between component usage and step definitions.
- **No placeholders:** every code step contains complete code.

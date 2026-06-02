# UX Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply visual direction "A — subtle & semantic" across the SPA: a real theme palette, a calmer Promote bar, clearer dashboard counts, a colored earliest-expiry chip on inventories, no strikethrough on checked items, and one standardized page header with consistent action placement.

**Architecture:** Mostly surgical edits to existing React/MUI components plus one EF projection field. A shared `PageHeadActionBar` already exists and is used by the two "view" pages; the work standardizes the remaining pages onto it (extending it minimally to preserve test ids and support a wider container). One backend DTO gains an `EarliestExpiryDate` field via the existing EF projection; the TS client is regenerated from the OpenAPI spec.

**Tech Stack:** React 19, MUI v7, TanStack Router/Query, TypeScript, i18next; .NET 10 minimal-API vertical slices, EF Core (Postgres), xUnit + FakeItEasy.

**Conventions (read before starting):**
- **No frontend test runner exists.** Frontend tasks verify with `npm run tsc` + `npm run lint` (run from `Application/Frigorino.Web/ClientApp/`). Only the backend task is TDD.
- Run all `npm` commands from `Application/Frigorino.Web/ClientApp/`. Run `dotnet` from repo root.
- Commit after every task. Keep each task build-green (tsc + lint pass).
- C# brace style: always block `{}`. Never assert on translated text in tests.
- Spec: `docs/superpowers/specs/2026-06-02-ux-alignment-design.md`.

---

## Phase A — Theme & quick wins

### Task 1: Theme palette

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/theme.ts`

- [ ] **Step 1: Add the palette**

Replace the `createTheme({...})` call so it defines an explicit primary/secondary (today it relies on MUI dark defaults: light-blue primary + pink secondary). Full new file content:

```ts
import { createTheme, responsiveFontSizes } from "@mui/material/styles";

export const appTheme = responsiveFontSizes(
    createTheme({
        palette: {
            mode: "dark",
            // Fresh-green primary suits a food/fridge app; used sparingly per direction A.
            primary: { main: "#43A047" },
            // Warm amber replaces the default clashing pink.
            secondary: { main: "#FFB300" },
        },
        shape: { borderRadius: 8 },
        components: {
            MuiButton: {
                styleOverrides: {
                    root: { textTransform: "none" },
                },
            },
        },
    }),
);

export const pageContainerSx = {
    py: { xs: 2, sm: 3 },
    px: { xs: 1, sm: 2 },
};
```

- [ ] **Step 2: Type-check & lint**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS (no errors).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/theme.ts
git commit -m "feat(theme): add green/amber palette (direction A)"
```

---

### Task 2: Promote bar — soft tinted banner

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteBar.tsx`

- [ ] **Step 1: Restyle the Paper + button**

Change the import line 2 to also import `alpha`:

```tsx
import { Button, Paper, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
```

Replace the `<Paper ...>` opening tag's `sx` (currently `bgcolor: "primary.main", color: "primary.contrastText"`) and the icon + button. The new `<Paper>` block (replacing lines 32–61) is:

```tsx
            <Paper
                elevation={0}
                data-testid="promote-bar"
                data-count={entries.length}
                sx={{
                    mx: 3,
                    mb: 1,
                    px: 1.5,
                    py: 1,
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                    bgcolor: (theme) => alpha(theme.palette.primary.main, 0.15),
                    color: "text.primary",
                    border: "1px solid",
                    borderColor: (theme) =>
                        alpha(theme.palette.primary.main, 0.3),
                    borderLeft: "3px solid",
                    borderLeftColor: "primary.main",
                }}
            >
                <Inventory2Outlined
                    fontSize="small"
                    sx={{ color: "primary.main" }}
                />
                <Typography variant="body2" sx={{ flex: 1 }}>
                    {t("promote.barReady", { count: entries.length })}
                </Typography>
                <Button
                    size="small"
                    variant="contained"
                    color="primary"
                    data-testid="promote-bar-review"
                    onClick={() => setOpen(true)}
                >
                    {t("promote.review")}
                </Button>
            </Paper>
```

(Key changes: tinted background + left accent instead of solid fill; icon tinted primary; button `color="secondary"` → `color="primary"`. The `data-testid`/`data-count` attributes are preserved.)

- [ ] **Step 2: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteBar.tsx
git commit -m "feat(promote): calmer tinted promote bar (direction A)"
```

---

### Task 3: Remove strikethrough from checked-item quantity chip

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx`

Background: the only "checked" strikethrough in the app is on the quantity chip here (the item text has none; the row already greys out via `opacity: 0.7` in `SortableListItem`). Remove the chip's strikethrough entirely.

- [ ] **Step 1: Drop the `textDecoration` rule**

In the `<Chip ... sx={{ ... }}>` (lines 56–61), replace:

```tsx
                            sx={{
                                height: 20,
                                textDecoration: item.status
                                    ? "line-through"
                                    : "none",
                            }}
```

with:

```tsx
                            sx={{
                                height: 20,
                            }}
```

- [ ] **Step 2: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS. (`item.status` is still used? No — confirm no now-unused vars; `item` is still used elsewhere, so no unused-import errors.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx
git commit -m "fix(lists): remove strikethrough on checked-item quantity chip"
```

---

## Phase B — Dashboard count clarity

### Task 4: Shopping-list count = "open" (total in subtitle)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/dashboard/WelcomePage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

Background: today the dashboard list row builds `count = "${checkedCount}/${uncheckedCount} Artikel"` and the card renders that same string **twice** (chip label + secondary line); the computed `status` is never shown. Fix: chip = open count, secondary line = total. "Open" = `uncheckedCount` (items not yet checked off); total = `uncheckedCount + checkedCount`.

- [ ] **Step 1: Add translation keys**

In `en/translation.json`, inside the `"dashboard"` object (currently has `items`/`articles`/`expiring`), add two keys:

```json
        "open": "open",
        "itemsTotal": "items total",
```

In `de/translation.json`, inside `"dashboard"`, add:

```json
        "open": "offen",
        "itemsTotal": "Artikel gesamt",
```

(Place them next to the existing `expiring` key; keep valid JSON — mind the trailing commas.)

- [ ] **Step 2: Change the list mapping**

In `WelcomePage.tsx`, replace the list `.map(...)` (lines 134–141):

```tsx
                  ? lists.map((list) => ({
                        name: list.name || "Unnamed List",
                        count: `${list.checkedCount}/${list.uncheckedCount} ${t("dashboard.articles")}`,
                        status: new Date(list.createdAt!).toLocaleDateString(
                            "de-DE",
                        ),
                        id: list.id,
                    }))
```

with:

```tsx
                  ? lists.map((list) => ({
                        name: list.name || "Unnamed List",
                        count: `${list.uncheckedCount} ${t("dashboard.open")}`,
                        status: `${list.uncheckedCount + list.checkedCount} ${t("dashboard.itemsTotal")}`,
                        id: list.id,
                    }))
```

- [ ] **Step 3: Render the subtitle from `status`, not `count`**

In the card body, change the duplicated render (line 503):

```tsx
                                                        secondary={item.count}
```

to:

```tsx
                                                        secondary={item.status}
```

(This makes the chip show `count` and the subtitle show `status` for every collection — fixing the duplication. Inventory rows now show their existing "N expiring"/"current" status; recipe/empty/loading rows show their helper text.)

- [ ] **Step 4: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/dashboard/WelcomePage.tsx Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(dashboard): show open count + total instead of confusing duplicate"
```

---

## Phase C — Inventory earliest-expiry chip

### Task 5: Backend — add `EarliestExpiryDate` to `InventoryResponse`

**Files:**
- Modify: `Application/Frigorino.Features/Inventories/InventoryResponse.cs`
- Test: `Application/Frigorino.Test/Features/InventoryResponseProjectionTests.cs` (create)

The projection is an EF `Expression`; we test it by compiling it and applying it to in-memory entities (the entities are POCOs with public setters). Using `OrderBy().Select().FirstOrDefault()` (not `Min`) so LINQ-to-objects and EF SQL agree on the empty case (→ `null`).

- [ ] **Step 1: Write the failing test**

Create `Application/Frigorino.Test/Features/InventoryResponseProjectionTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Features.Inventories;

namespace Frigorino.Test.Features
{
    public class InventoryResponseProjectionTests
    {
        private static Inventory BuildInventory(params InventoryItem[] items)
        {
            return new Inventory
            {
                Id = 1,
                Name = "Fridge",
                Description = null,
                HouseholdId = 1,
                CreatedByUser = new User
                {
                    ExternalId = "u1",
                    Name = "Tester",
                    Email = "t@example.com",
                },
                InventoryItems = items.ToList(),
            };
        }

        [Fact]
        public void EarliestExpiryDate_IsMinAmongActiveItemsWithDate()
        {
            var inventory = BuildInventory(
                new InventoryItem { IsActive = true, ExpiryDate = new DateOnly(2026, 7, 1) },
                new InventoryItem { IsActive = true, ExpiryDate = new DateOnly(2026, 6, 10) },
                new InventoryItem { IsActive = true, ExpiryDate = null });

            var result = InventoryResponse.ToProjection.Compile().Invoke(inventory);

            Assert.Equal(new DateOnly(2026, 6, 10), result.EarliestExpiryDate);
        }

        [Fact]
        public void EarliestExpiryDate_IgnoresInactiveItems()
        {
            var inventory = BuildInventory(
                new InventoryItem { IsActive = false, ExpiryDate = new DateOnly(2026, 1, 1) },
                new InventoryItem { IsActive = true, ExpiryDate = new DateOnly(2026, 6, 10) });

            var result = InventoryResponse.ToProjection.Compile().Invoke(inventory);

            Assert.Equal(new DateOnly(2026, 6, 10), result.EarliestExpiryDate);
        }

        [Fact]
        public void EarliestExpiryDate_IsNullWhenNoItemHasDate()
        {
            var inventory = BuildInventory(
                new InventoryItem { IsActive = true, ExpiryDate = null });

            var result = InventoryResponse.ToProjection.Compile().Invoke(inventory);

            Assert.Null(result.EarliestExpiryDate);
        }
    }
}
```

- [ ] **Step 2: Run the test, verify it fails to compile**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventoryResponseProjectionTests"`
Expected: FAIL — `InventoryResponse` has no `EarliestExpiryDate` member (compile error).

- [ ] **Step 3: Add the field to `InventoryResponse`**

In `InventoryResponse.cs`, add the record parameter, the `From` argument, and the projection line.

Record header — add `DateOnly? EarliestExpiryDate` after `ExpiringItems`:

```csharp
    public sealed record InventoryResponse(
        int Id,
        string Name,
        string? Description,
        int HouseholdId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        InventoryCreatorResponse CreatedByUser,
        int TotalItems,
        int ExpiringItems,
        DateOnly? EarliestExpiryDate)
    {
```

`From` factory — add the matching argument. Change its signature and body:

```csharp
        public static InventoryResponse From(Inventory inventory, User creator, int totalItems, int expiringItems, DateOnly? earliestExpiryDate)
        {
            return new InventoryResponse(
                inventory.Id,
                inventory.Name,
                inventory.Description,
                inventory.HouseholdId,
                inventory.CreatedAt,
                inventory.UpdatedAt,
                new InventoryCreatorResponse(creator.ExternalId, creator.Name, creator.Email),
                totalItems,
                expiringItems,
                earliestExpiryDate);
        }
```

`ToProjection` — add the earliest-expiry subquery as the final constructor argument (after the `ExpiringItems` count line):

```csharp
        public static readonly Expression<Func<Inventory, InventoryResponse>> ToProjection = i => new InventoryResponse(
            i.Id,
            i.Name,
            i.Description,
            i.HouseholdId,
            i.CreatedAt,
            i.UpdatedAt,
            new InventoryCreatorResponse(i.CreatedByUser.ExternalId, i.CreatedByUser.Name, i.CreatedByUser.Email),
            i.InventoryItems.Count(x => x.IsActive),
            i.InventoryItems.Count(x => x.IsActive && x.ExpiryDate.HasValue && x.ExpiryDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(ExpiringWithinDays)),
            i.InventoryItems
                .Where(x => x.IsActive && x.ExpiryDate != null)
                .OrderBy(x => x.ExpiryDate)
                .Select(x => x.ExpiryDate)
                .FirstOrDefault());
```

> Note: if any other call site uses `InventoryResponse.From` (grep before assuming none), update it to pass the new argument. The read slices use `ToProjection`, not `From`.

- [ ] **Step 4: Check for other `From` callers**

Run: `grep -rn "InventoryResponse.From" Application/`
If any non-test caller exists, add a 5th argument (the earliest expiry, or `null` if not readily available) so it compiles.

- [ ] **Step 5: Run the test, verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventoryResponseProjectionTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Inventories/InventoryResponse.cs Application/Frigorino.Test/Features/InventoryResponseProjectionTests.cs
git commit -m "feat(inventories): expose earliest expiry date on inventory response"
```

---

### Task 6: Regenerate the API client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/api/*` and `src/lib/openapi.json`

- [ ] **Step 1: Regenerate**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run api`
Expected: rebuilds the backend, emits `openapi.json`, regenerates the TS client. `InventoryResponse` in `src/lib/api/types.gen.ts` now includes `earliestExpiryDate: null | string;`.

- [ ] **Step 2: Verify the new field is present**

Run: `grep -n "earliestExpiryDate" src/lib/api/types.gen.ts`
Expected: one match in the `InventoryResponse` type.

- [ ] **Step 3: Type-check**

Run: `npm run tsc`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/api Application/Frigorino.Web/ClientApp/src/lib/openapi.json
git commit -m "chore(api): regenerate client with earliestExpiryDate"
```

---

### Task 7: Colored earliest-expiry chip on the inventory list

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/components/InventorySummaryCard.tsx`

Reuse `getExpiryInfo` (relative text, e.g. "expires in 2 days") and `getExpiryColor` (red/orange/yellow/green) from `utils/dateUtils.ts`. Show the chip only when `earliestExpiryDate` is present. For dates >30 days out `getExpiryInfo` returns an empty `humanReadable`, so fall back to the formatted date.

- [ ] **Step 1: Add imports**

At the top of `InventorySummaryCard.tsx`, after the existing `@mui/material` import, add the dateUtils import (and `useTranslation`):

```tsx
import { useTranslation } from "react-i18next";
import {
    formatLocalDate,
    getExpiryColor,
    getExpiryInfo,
} from "../../../utils/dateUtils";
```

- [ ] **Step 2: Build the chip inside the component**

Inside `InventorySummaryCard`, before the `return`, add (the `translateKey` wrapper mirrors `InventoryItemContent` because `getExpiryInfo` expects a `(key: string) => string`):

```tsx
    const { t } = useTranslation();
    // getExpiryInfo expects a plain (key) => string; the i18n t has stricter overloads.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const translateKey = (key: string): string => t(key as any);

    const expiry = inventory.earliestExpiryDate;
    const expiryLabel = expiry
        ? getExpiryInfo(expiry, translateKey).humanReadable ||
          formatLocalDate(expiry)
        : null;
```

- [ ] **Step 3: Render the chip in the `secondaryAction`**

In the `secondaryAction` `<Box>`, add the expiry chip **before** the existing date `<Chip>` (so order is: expiry chip, date chip, menu button):

```tsx
                                {expiry && expiryLabel && (
                                    <Chip
                                        label={expiryLabel}
                                        size="small"
                                        variant="outlined"
                                        data-testid={`inventory-earliest-expiry-${inventory.name}`}
                                        sx={{
                                            color: getExpiryColor(expiry),
                                            borderColor: getExpiryColor(expiry),
                                        }}
                                    />
                                )}
```

- [ ] **Step 4: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/components/InventorySummaryCard.tsx
git commit -m "feat(inventories): colored earliest-expiry chip on inventory cards"
```

---

## Phase D — Standardized page header & action placement

### Task 8: Extend `PageHeadActionBar` (maxWidth + menu test ids)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/shared/PageHeadActionBar.tsx`

Two additions, both backward-compatible: (1) optional `maxWidth` so wider (`md`) form pages match their content; (2) apply `action.testId` to menu items + an optional `menuButtonTestId` on the overflow button, so pages migrating off custom menus keep their integration-test ids.

- [ ] **Step 1: Extend the props interface**

Add `Breakpoint` to the `@mui/material` import and extend `HeadNavigationProps`:

```tsx
import {
    Box,
    type Breakpoint,
    Container,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Typography,
} from "@mui/material";
```

```tsx
export interface HeadNavigationProps {
    title: string;
    subtitle?: string;
    menuActions: HeadNavigationAction[];
    directActions: HeadNavigationAction[];
    maxWidth?: Breakpoint;
    menuButtonTestId?: string;
}
```

- [ ] **Step 2: Use the new props**

Destructure them with a default:

```tsx
    ({
        title,
        subtitle,
        menuActions,
        directActions,
        maxWidth = "sm",
        menuButtonTestId,
    }: HeadNavigationProps) => {
```

Use `maxWidth` on the `<Container maxWidth="sm" ...>` → `<Container maxWidth={maxWidth} ...>`.

Add the test id to the overflow `<IconButton onClick={handleMenuOpen} ...>` (the one rendering `<MoreVert />`):

```tsx
                                <IconButton
                                    onClick={handleMenuOpen}
                                    data-testid={menuButtonTestId}
                                    sx={{
                                        bgcolor: "grey.100",
                                        color: "grey.700",
                                        "&:hover": { bgcolor: "grey.200" },
                                    }}
                                >
                                    <MoreVert />
                                </IconButton>
```

Add the test id to each menu `<MenuItem>`:

```tsx
                        {menuActions.map((action, index) => (
                            <MenuItem
                                key={index}
                                onClick={() => handleMenuAction(action)}
                                data-testid={action.testId}
                            >
                                {action.icon && (
                                    <ListItemIcon>{action.icon}</ListItemIcon>
                                )}
                                <ListItemText
                                    primary={action.text}
                                    secondary={action.secondaryText}
                                />
                            </MenuItem>
                        ))}
```

- [ ] **Step 3: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/shared/PageHeadActionBar.tsx
git commit -m "feat(header): add maxWidth + menu test ids to PageHeadActionBar"
```

---

### Task 9: Slim overview action menus to delete-only

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/components/ListActionsMenu.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/components/InventoryActionsMenu.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListsPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoriesPage.tsx`

Both menus currently show a permanently `disabled` "Edit" item (dead UI). Remove it and its now-unused `onEdit` prop. Done together with the pages that pass `onEdit`, so the build stays green.

- [ ] **Step 1: `ListActionsMenu.tsx` — drop Edit**

Replace the file body so the import drops `Edit`, the props drop `onEdit`, and the disabled Edit `MenuItem` is gone:

```tsx
import { Delete } from "@mui/icons-material";
import { Menu, MenuItem } from "@mui/material";
import { useTranslation } from "react-i18next";

interface ListActionsMenuProps {
    anchorEl: HTMLElement | null;
    onClose: () => void;
    onDelete: () => void;
    isDeleting?: boolean;
}

export const ListActionsMenu = ({
    anchorEl,
    onClose,
    onDelete,
    isDeleting = false,
}: ListActionsMenuProps) => {
    const { t } = useTranslation();

    return (
        <Menu
            anchorEl={anchorEl}
            open={Boolean(anchorEl)}
            onClose={onClose}
            elevation={4}
            slotProps={{ paper: { sx: { minWidth: 160 } } }}
        >
            <MenuItem
                onClick={onDelete}
                disabled={isDeleting}
                data-testid="delete-list-button"
                sx={{ color: "error.main" }}
            >
                <Delete fontSize="small" sx={{ mr: 1 }} />
                {t("common.delete")}
            </MenuItem>
        </Menu>
    );
};
```

- [ ] **Step 2: `InventoryActionsMenu.tsx` — drop Edit**

Same change for inventories:

```tsx
import { Delete } from "@mui/icons-material";
import { Menu, MenuItem } from "@mui/material";
import { useTranslation } from "react-i18next";

interface InventoryActionsMenuProps {
    anchorEl: HTMLElement | null;
    onClose: () => void;
    onDelete: () => void;
    isDeleting?: boolean;
}

export const InventoryActionsMenu = ({
    anchorEl,
    onClose,
    onDelete,
    isDeleting = false,
}: InventoryActionsMenuProps) => {
    const { t } = useTranslation();

    return (
        <Menu
            anchorEl={anchorEl}
            open={Boolean(anchorEl)}
            onClose={onClose}
            elevation={4}
            slotProps={{ paper: { sx: { minWidth: 160 } } }}
        >
            <MenuItem
                onClick={onDelete}
                disabled={isDeleting}
                data-testid="delete-inventory-button"
                sx={{ color: "error.main" }}
            >
                <Delete fontSize="small" sx={{ mr: 1 }} />
                {t("common.delete")}
            </MenuItem>
        </Menu>
    );
};
```

- [ ] **Step 3: `ListsPage.tsx` — remove the `onEdit` wiring**

Delete the now-unused handler (line 71):

```tsx
    const handleEditList = () => handleMenuClose();
```

And remove the `onEdit` prop from the `<ListActionsMenu>` usage:

```tsx
            <ListActionsMenu
                anchorEl={anchorEl}
                onClose={handleMenuClose}
                onDelete={handleDeleteList}
                isDeleting={deleteListMutation.isPending}
            />
```

- [ ] **Step 4: `InventoriesPage.tsx` — remove the `onEdit` wiring**

Delete the handler (line 72):

```tsx
    const handleEditInventory = () => handleMenuClose();
```

And the `onEdit` prop from `<InventoryActionsMenu>`:

```tsx
            <InventoryActionsMenu
                anchorEl={anchorEl}
                onClose={handleMenuClose}
                onDelete={handleDeleteInventory}
                isDeleting={deleteInventoryMutation.isPending}
            />
```

- [ ] **Step 5: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/components/ListActionsMenu.tsx Application/Frigorino.Web/ClientApp/src/features/inventories/components/InventoryActionsMenu.tsx Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListsPage.tsx Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoriesPage.tsx
git commit -m "refactor(menus): remove dead disabled Edit from overview menus"
```

---

### Task 10: Migrate Lists & Inventories overview pages to the shared header

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListsPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoriesPage.tsx`

Replace each page's hand-rolled header `<Box>` (back + title + labeled Create button) with `<PageHeadActionBar>`; Create becomes the primary inline (`Add`) action. The back button is built in (uses `router.history.back()`).

- [ ] **Step 1: `ListsPage.tsx` — imports**

Change the imports: drop `ArrowBack` and `IconButton` (no longer used in the header); add `PageHeadActionBar`. New top imports:

```tsx
import { Add } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    Stack,
    Typography,
} from "@mui/material";
```

Add after the existing `pageContainerSx` import:

```tsx
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
```

- [ ] **Step 2: `ListsPage.tsx` — replace the header**

Replace the main return's outer wrapper + header `<Box>` (lines 89–126, from `<Container ...>` through the closing `</Box>` of the header) so the page renders the shared header above the content Container:

```tsx
    return (
        <>
            <PageHeadActionBar
                title={t("lists.shoppingLists")}
                directActions={[{ icon: <Add />, onClick: handleCreateList }]}
                menuActions={[]}
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
```

Then, at the very end of the component's main return, close the fragment — change the final `</Container>` (the one wrapping the content, after `<ListActionsMenu .../>`) to:

```tsx
            </Container>
        </>
    );
```

(The `!householdId` early-return Container is unchanged. `handleBack` is still used there, so keep it.)

- [ ] **Step 3: `InventoriesPage.tsx` — imports**

Same import change: drop `ArrowBack`/`IconButton`, add `PageHeadActionBar`:

```tsx
import { Add } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    Stack,
    Typography,
} from "@mui/material";
```

```tsx
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
```

- [ ] **Step 4: `InventoriesPage.tsx` — replace the header**

Replace the main return wrapper + header `<Box>` (lines 90–127):

```tsx
    return (
        <>
            <PageHeadActionBar
                title={t("inventory.inventories")}
                directActions={[
                    { icon: <Add />, onClick: handleCreateInventory },
                ]}
                menuActions={[]}
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
```

And close the fragment at the end (after `<InventoryActionsMenu .../>`):

```tsx
            </Container>
        </>
    );
```

- [ ] **Step 5: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS (no unused `ArrowBack`/`IconButton`).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListsPage.tsx Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoriesPage.tsx
git commit -m "feat(header): standardize Lists/Inventories overview headers"
```

---

### Task 11: Migrate List & Inventory edit pages to the shared header

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListEditPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryEditPage.tsx`

Each edit page has a custom header (back + title + a `MoreVert` menu whose only item is Delete → opens a confirm dialog). Replace with `<PageHeadActionBar maxWidth="md">` where Delete is a single `menuAction`. The pages keep `maxWidth="md"` content, so pass `maxWidth="md"` to the header.

- [ ] **Step 1: `ListEditPage.tsx` — imports**

Replace the icon + MUI imports (drop `ArrowBack`, `IconButton`, `ListItemIcon`, `ListItemText`, `Menu`, `MenuItem`, `MoreVert`, and `Typography` — Typography was only in the header; keep `Delete` for the menu action icon) and the router import (drop `useRouter` — the header owns back). Add the header import:

```tsx
import { Delete } from "@mui/icons-material";
import { Alert, Box, Container, Skeleton } from "@mui/material";
import { useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
```

(Keep the existing `useCurrentHouseholdWithDetails`, `DeleteListConfirmDialog`, `EditListForm`, `useList` imports.)

- [ ] **Step 2: `ListEditPage.tsx` — remove menu state/handlers, add menu action**

Delete the menu anchor state and handlers (lines 30, 51–53): remove `const [menuAnchor, setMenuAnchor] = ...`, `handleMenuClick`, `handleMenuClose`. Also remove `const router = useRouter();` (line 24) and `handleBack` (line 50) — both now unused (the header owns back). Keep `deleteDialogOpen` state. Change `handleDeleteClick` to just open the dialog:

```tsx
    const handleDeleteClick = () => setDeleteDialogOpen(true);
```

- [ ] **Step 3: `ListEditPage.tsx` — replace header + menu markup**

Replace the final return's header `<Box>` (lines 110–144) and the `<Menu>` block (lines 148–173) with the shared header. The return becomes:

```tsx
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("lists.deleteList"),
            icon: <Delete fontSize="small" />,
            onClick: handleDeleteClick,
        },
    ];

    return (
        <>
            <PageHeadActionBar
                title={t("lists.editList")}
                maxWidth="md"
                directActions={[]}
                menuActions={menuActions}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <EditListForm householdId={householdId} list={list} />

                {list.id && (
                    <DeleteListConfirmDialog
                        open={deleteDialogOpen}
                        onClose={() => setDeleteDialogOpen(false)}
                        householdId={householdId}
                        listId={list.id}
                        listName={listName}
                    />
                )}
            </Container>
        </>
    );
```

(The loading/error/no-household/no-list early returns keep their own `<Container maxWidth="md">` — unchanged.)

- [ ] **Step 4: `InventoryEditPage.tsx` — imports**

(Same rationale as ListEditPage: drop `Typography` and `useRouter`.)

```tsx
import { Delete } from "@mui/icons-material";
import { Alert, Box, Container, Skeleton } from "@mui/material";
import { useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
```

(Keep `useCurrentHouseholdWithDetails`, `DeleteInventoryConfirmDialog`, `EditInventoryForm`, `InventorySettingsCard`, `useInventory`.)

- [ ] **Step 5: `InventoryEditPage.tsx` — remove menu state/handlers**

Remove `menuAnchor` state, `handleMenuClick`, `handleMenuClose`, `handleBack`, and `const router = useRouter();` (all now unused); simplify delete:

```tsx
    const handleDeleteClick = () => setDeleteDialogOpen(true);
```

- [ ] **Step 6: `InventoryEditPage.tsx` — replace header + menu markup**

Replace the header `<Box>` (lines 115–147) and the `<Menu>` block (lines 161–186):

```tsx
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("inventory.deleteInventory"),
            icon: <Delete fontSize="small" />,
            onClick: handleDeleteClick,
        },
    ];

    return (
        <>
            <PageHeadActionBar
                title={t("inventory.editInventory")}
                maxWidth="md"
                directActions={[]}
                menuActions={menuActions}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <EditInventoryForm
                    householdId={householdId}
                    inventory={inventory}
                />

                {inventory.id && (
                    <InventorySettingsCard
                        householdId={householdId}
                        inventoryId={inventory.id}
                    />
                )}

                {inventory.id && (
                    <DeleteInventoryConfirmDialog
                        open={deleteDialogOpen}
                        onClose={() => setDeleteDialogOpen(false)}
                        householdId={householdId}
                        inventoryId={inventory.id}
                        inventoryName={inventoryName}
                    />
                )}
            </Container>
        </>
    );
```

- [ ] **Step 7: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS. (`Box`/`Skeleton` are still used by the loading-skeleton early returns; `Typography` is NOT used after the header is removed, which is why it was dropped from the imports.)

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListEditPage.tsx Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryEditPage.tsx
git commit -m "feat(header): standardize List/Inventory edit headers"
```

---

### Task 12: Migrate Manage-household page to the shared header (preserve test ids)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/households/pages/ManageHouseholdPage.tsx`

Integration tests click `household-manage-menu-toggle` and `household-manage-menu-delete`, so pass `menuButtonTestId="household-manage-menu-toggle"` and the delete action `testId="household-manage-menu-delete"`. The delete menu item only shows for owners.

- [ ] **Step 1: Imports**

Replace the icon + MUI imports (drop `ArrowBack`, `IconButton`, `ListItemIcon`, `ListItemText`, `Menu`, `MenuItem`, `MoreVert`, and `Typography` — Typography was only in the header; keep `Delete`) and drop the `useNavigate` router import entirely (the back button moves into the header). Add the header import:

```tsx
import { Delete } from "@mui/icons-material";
import { Alert, Box, Container, Skeleton } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
```

(Keep the remaining domain imports: `useCurrentHouseholdWithDetails`, `DeleteHouseholdDialog`, `HouseholdSettingsCard`, `HouseholdSummaryCard`, `HouseholdRoleValue`, `roleRank`, `MembersPanel`, `useHouseholdMembers`.)

- [ ] **Step 2: Remove menu anchor state + unused navigate**

Delete `const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);` (line 30) and `const navigate = useNavigate();` (line 27) — `navigate` was only used by the old back button. Keep `deleteDialogOpen`.

- [ ] **Step 3: Replace header + menu markup**

Replace the header wrapper `<Box sx={{ mb... }}>` containing the inner header `<Box>` + `HouseholdSummaryCard` (lines 91–134) and the `<Menu>` block (lines 146–177). Build the menu action conditionally for owners and render via the shared header:

```tsx
    const menuActions: HeadNavigationAction[] = isOwner
        ? [
              {
                  text: t("household.deleteHousehold"),
                  icon: <Delete fontSize="small" />,
                  onClick: () => setDeleteDialogOpen(true),
                  testId: "household-manage-menu-delete",
              },
          ]
        : [];

    return (
        <>
            <PageHeadActionBar
                title={t("household.householdManagement")}
                maxWidth="md"
                directActions={[]}
                menuActions={menuActions}
                menuButtonTestId="household-manage-menu-toggle"
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    <HouseholdSummaryCard
                        householdName={householdName}
                        memberCount={members?.length ?? 0}
                        userRole={userRole}
                    />
                </Box>

                <MembersPanel
                    householdId={currentHousehold.householdId}
                    currentUserRole={userRole}
                />

                <HouseholdSettingsCard
                    householdId={currentHousehold.householdId}
                    canManage={canManageSettings}
                />

                <DeleteHouseholdDialog
                    open={deleteDialogOpen}
                    onClose={() => setDeleteDialogOpen(false)}
                    householdId={currentHousehold.householdId}
                    householdName={householdName}
                />
            </Container>
        </>
    );
```

> Note: when `isOwner` is false, `menuActions` is empty and `PageHeadActionBar` renders no overflow button — matching today's behavior (the menu toggle only showed for owners). The IT asserts the toggle is hidden for non-owners (`HouseholdSteps.cs:91`); that still holds.

- [ ] **Step 4: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS. (`Box`/`Skeleton`/`Typography`/`Alert` still used in early returns — keep.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/households/pages/ManageHouseholdPage.tsx
git commit -m "feat(header): standardize manage-household header (keep test ids)"
```

---

### Task 13: Add a back button to User Settings via the shared header

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/settings/pages/UserSettingsPage.tsx`

The settings page is the only one with no back navigation (just an `h5` title). Add the shared header above the content; drop the bare title.

- [ ] **Step 1: Add the import**

After the `pageContainerSx` import, add:

```tsx
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
```

- [ ] **Step 2: Replace the title with the header**

Change the start of the returned JSX. Replace:

```tsx
    return (
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Typography variant="h5" sx={{ mb: { xs: 2, sm: 3 } }}>
                {t("settings.userSettings")}
            </Typography>
```

with:

```tsx
    return (
        <>
            <PageHeadActionBar
                title={t("settings.userSettings")}
                directActions={[]}
                menuActions={[]}
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
```

Then close the fragment at the very end — change the final `</Container>` to:

```tsx
            </Container>
        </>
    );
```

- [ ] **Step 3: Remove the now-unused `Typography` import**

`Typography` is still used inside the notifications card (`<Typography variant="h6">`), so **keep** the import. (Verify with `npm run lint`.)

- [ ] **Step 4: Type-check & lint**

Run: `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/settings/pages/UserSettingsPage.tsx
git commit -m "feat(settings): add back navigation via shared header"
```

---

## Phase E — Verification

### Task 14: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Frontend gate**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run lint && npm run tsc && npm run prettier`
Expected: all PASS. (If `prettier` rewrites files, commit the formatting: `git commit -am "style: prettier"`.)

- [ ] **Step 2: Build the SPA into `ClientApp/build`**

Integration tests serve `ClientApp/build`, so rebuild it (also catches any vite/tsc build break the dev type-check missed).
Run: `npm run build`
Expected: PASS, outputs to `build/`.

- [ ] **Step 3: Full solution tests**

Run (from repo root): `dotnet test Application/Frigorino.sln`
Expected: PASS (includes the new `InventoryResponseProjectionTests` and the household/settings integration tests that exercise the migrated headers). Capture `${PIPESTATUS[0]}` / read the pass-fail summary — do not trust a piped tail.

- [ ] **Step 4: Manual mobile-first sweep (dev-up + browser)**

Bring up the stack (`/dev-up`) and visually confirm on a phone-width viewport:
- Promote bar reads as a calm tinted banner with a green button (add a perishable, check it off to trigger it).
- Dashboard shopping-list rows show "N open" chip + "M items total" subtitle (no `6/1`).
- Inventories page shows a colored earliest-expiry chip per inventory; none when an inventory has no dated items.
- Checked list items grey out with **no** strikethrough on text or quantity chip.
- Every page (lists, inventories, list/inventory edit, manage household, **user settings**) has a back button in a consistent header; overview "create" is the green inline action; edit/manage delete lives in the overflow `⋮`.

- [ ] **Step 5: (Optional, final gate) Docker build**

If the Dockerfile or project set changed (it didn't here), run `docker build -f Application/Dockerfile -t frigorino .`. Not required for this UI-only change, but run it before promoting if in doubt.

---

## Self-review notes (author)

- **Spec coverage:** §1 palette → Task 1; §2 promote bar → Task 2; §3 list count → Task 4; §4 expiry chip → Tasks 5–7; §5 strikethrough → Task 3; §6 header/menus → Tasks 8–13. All covered.
- **Deferred (per spec, not in plan):** icon-set refinement, bottom-nav/mobile-nav rework, dashboard layout changes. Dashboard inventory expiry is surfaced as text via Task 4's `status` render; the *colored chip* requirement is delivered on the Inventories page (the canonical "inventory lists"), matching the spec's primary target.
- **Type consistency:** `EarliestExpiryDate` (C# `DateOnly?`) ↔ generated `earliestExpiryDate: null | string` ↔ consumed by `getExpiryInfo`/`getExpiryColor`/`formatLocalDate` (all take `string`). `HeadNavigationAction.testId` is reused for menu items. `maxWidth` typed as MUI `Breakpoint`.
- **Build-green ordering:** Task 9 changes the menu components and their callers together; Task 6 regenerates the client before Task 7 consumes the new field.
```

# Recipe view/edit refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the recipe `/view` page a clean read-only recipe and move all editing (metadata + ingredients) onto `/edit`, which saves implicitly as you type.

**Architecture:** Pure frontend restructure of the recipes feature (no backend/API/DB/DTO changes). `RecipeViewPage` sheds its editing machinery (composer, drag-reorder, per-item actions) and renders a new read-only ingredient list; `RecipeEditPage` absorbs that machinery plus an auto-saving metadata form. Display-only servings scaling stays on the view.

**Tech Stack:** React 19, MUI, TanStack Router/Query, hey-api generated client, i18next (en/de); Reqnroll + Playwright + Postgres Testcontainers for integration tests.

**Spec:** `docs/superpowers/specs/2026-06-14-recipe-view-edit-refinement-design.md`

**Branch:** continue on `feat/recipe-metadata-servings` (already checked out).

**Conventions (apply to every task):**
- Frontend tooling only via npm scripts, run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc`, `npm run lint`, `npm run build`. Never raw `npx tsc/eslint`.
- Tests assert on testids / `data-*`, never translated text.
- C# block-style braces.
- No Co-Authored-By trailers in commits.
- The IT harness serves `ClientApp/build` — run `npm run build` before any IT run after frontend edits.

**File map:**
- Create: `features/recipes/items/components/RecipeViewItem.tsx` (read-only ingredient row).
- Create: `features/recipes/items/components/RecipeViewList.tsx` (read-only ingredient list).
- Modify: `features/recipes/pages/RecipeViewPage.tsx` (→ read-only).
- Modify: `features/recipes/pages/RecipeEditPage.tsx` (→ metadata + ingredient editor).
- Modify: `features/recipes/components/EditRecipeForm.tsx` (→ implicit save).
- Modify: `features/recipes/components/CreateRecipeForm.tsx` (→ navigate to `/edit`, `replace`).
- Modify: `features/recipes/items/components/RecipeContainer.tsx` (`scrollable` prop, drop `multiplier`).
- Modify: `features/recipes/items/components/RecipeItemContent.tsx` (drop scaling/`multiplier`).
- Modify: `components/common/ItemQuantityChip.tsx` (drop unused `color`).
- Modify: `public/locales/{en,de}/translation.json` (new keys; drop replaced key).
- Modify: `IntegrationTests/Slices/Recipes/Recipes.feature` + `RecipeSteps.cs` + `Infrastructure/TestApiClient.cs`.

Routes (`routes/recipes/$recipeId/{view,edit}.tsx`) need **no change** — both already mount their page component.

---

### Task 1: i18n keys

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add the new keys under `recipes` (en)**

In the `recipes` object add (keep existing keys; `servingsFrom` stays for now — Task 7 removes it after the view stops using it):

```json
"ingredientsHeading": "Ingredients",
"servingsFor": "for {{count}} servings",
"scaledFrom": "scaled from {{count}}",
"emptyIngredients": "No ingredients yet — tap edit to add some.",
"servingsRange": "Servings must be between 1 and 99"
```

- [ ] **Step 2: Add a `saved` key under `common` (en)**

In the `common` object of `en/translation.json` add (next to the existing `saving`):

```json
"saved": "Saved"
```

- [ ] **Step 3: Add the German equivalents**

In `de/translation.json`, under `recipes`:

```json
"ingredientsHeading": "Zutaten",
"servingsFor": "für {{count}} Portionen",
"scaledFrom": "skaliert von {{count}}",
"emptyIngredients": "Noch keine Zutaten — zum Bearbeiten tippen.",
"servingsRange": "Portionen müssen zwischen 1 und 99 liegen"
```

Under `common` in `de/translation.json`:

```json
"saved": "Gespeichert"
```

- [ ] **Step 4: Verify JSON is valid**

Run from `ClientApp/`: `node -e "require('./public/locales/en/translation.json');require('./public/locales/de/translation.json');console.log('ok')"`
Expected: `ok`

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(recipes): i18n keys for read-only view + edit auto-save"
```

---

### Task 2: Read-only ingredient components

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeViewItem.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeViewList.tsx`

These are the read-only rendering. They are wired into the page in Task 3.

- [ ] **Step 1: Create `RecipeViewItem.tsx`**

Quantity-column (left) + name (right) read-only row. Quantity is **text** (not a chip). Scaling cue: accented scaled value + struck original beneath.

```tsx
import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { formatQuantity, scaleQuantity } from "../../../../components/composer";
import type { RecipeItemResponse } from "../../../../lib/api";

interface Props {
    item: RecipeItemResponse;
    // Display-only scale factor for quantities. 1 = unscaled (default).
    multiplier?: number;
}

export function RecipeViewItem({ item, multiplier = 1 }: Props) {
    const { t } = useTranslation();
    const isScaled = multiplier !== 1 && !!item.quantity;
    const displayQuantity =
        item.quantity && isScaled
            ? scaleQuantity(item.quantity, multiplier)
            : item.quantity;

    return (
        <Box
            data-testid={`recipe-item-${item.id}`}
            sx={{
                display: "flex",
                gap: 1.5,
                py: 1,
                alignItems: "baseline",
                borderBottom: 1,
                borderColor: "divider",
            }}
        >
            <Box sx={{ flex: "0 0 84px", minWidth: 84 }}>
                {displayQuantity ? (
                    <Box sx={{ display: "flex", flexDirection: "column" }}>
                        <Typography
                            component="span"
                            data-testid={`recipe-item-quantity-${item.text}`}
                            variant="body2"
                            sx={{
                                fontWeight: 600,
                                color: isScaled ? "success.dark" : "success.main",
                            }}
                        >
                            {formatQuantity(t, displayQuantity)}
                        </Typography>
                        {isScaled && item.quantity ? (
                            <Typography
                                component="span"
                                data-testid={`recipe-item-quantity-base-${item.id}`}
                                variant="caption"
                                color="text.disabled"
                                sx={{
                                    textDecoration: "line-through",
                                    fontSize: "0.7rem",
                                }}
                            >
                                {formatQuantity(t, item.quantity)}
                            </Typography>
                        ) : null}
                    </Box>
                ) : null}
            </Box>
            <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography
                    variant="body2"
                    sx={{ fontWeight: 500, wordBreak: "break-word" }}
                >
                    {item.text}
                </Typography>
                {item.comment ? (
                    <Typography
                        data-testid={`recipe-item-comment-${item.id}`}
                        variant="caption"
                        color="text.secondary"
                        sx={{
                            display: "block",
                            fontStyle: "italic",
                            fontSize: "0.7rem",
                            whiteSpace: "pre-wrap",
                            wordBreak: "break-word",
                        }}
                    >
                        {item.comment}
                    </Typography>
                ) : null}
            </Box>
        </Box>
    );
}
```

- [ ] **Step 2: Create `RecipeViewList.tsx`**

Fetches its own items (mirrors `RecipeContainer`), applies the search filter, renders empty / no-match states, and maps to `RecipeViewItem`. It is the scroll region.

```tsx
import { Box, CircularProgress, Container, Paper, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { RecipeItemResponse } from "../../../../lib/api";
import { featureContentPx } from "../../../../theme";
import { matchesQuery } from "../../../../utils/searchUtils";
import { useRecipeItems } from "../useRecipeItems";
import { RecipeViewItem } from "./RecipeViewItem";

interface RecipeViewListProps {
    householdId: number;
    recipeId: number;
    searchQuery?: string;
    multiplier?: number;
}

// Search across text AND comment so ingredient notes match too (mirrors RecipeContainer).
const searchableText = (item: RecipeItemResponse): string =>
    [item.text, item.comment].filter(Boolean).join(" ");

export function RecipeViewList({
    householdId,
    recipeId,
    searchQuery = "",
    multiplier = 1,
}: RecipeViewListProps) {
    const { data: items = [], isLoading, error } = useRecipeItems(
        householdId,
        recipeId,
    );
    const { t } = useTranslation();

    const trimmedQuery = searchQuery.trim();
    const filterActive = trimmedQuery.length > 0;
    const visibleItems = filterActive
        ? items.filter((item) =>
              matchesQuery(searchableText(item), trimmedQuery),
          )
        : items;

    const showNoMatches =
        filterActive && !isLoading && !error && visibleItems.length === 0;
    const showEmpty =
        !filterActive && !isLoading && !error && items.length === 0;

    return (
        <Container
            maxWidth="sm"
            data-testid="recipe-items"
            sx={{
                flex: 1,
                overflow: "auto",
                px: featureContentPx,
                py: 1,
                minHeight: 0,
            }}
        >
            {isLoading ? (
                <Box sx={{ textAlign: "center", py: 4 }}>
                    <CircularProgress />
                </Box>
            ) : null}

            {showEmpty ? (
                <Paper
                    elevation={0}
                    data-testid="recipe-empty"
                    sx={{
                        p: 3,
                        textAlign: "center",
                        border: "2px dashed",
                        borderColor: "divider",
                        mx: 1,
                    }}
                >
                    <Typography variant="body2" color="text.secondary">
                        {t("recipes.emptyIngredients")}
                    </Typography>
                </Paper>
            ) : null}

            {showNoMatches ? (
                <Paper
                    elevation={0}
                    data-testid="recipe-search-no-results"
                    sx={{
                        p: 3,
                        textAlign: "center",
                        border: "2px dashed",
                        borderColor: "divider",
                        mx: 1,
                    }}
                >
                    <Typography variant="body2" color="text.secondary">
                        {t("recipes.noSearchMatches")}
                    </Typography>
                </Paper>
            ) : null}

            {!isLoading && !error
                ? visibleItems.map((item) => (
                      <RecipeViewItem
                          key={item.id}
                          item={item}
                          multiplier={multiplier}
                      />
                  ))
                : null}
        </Container>
    );
}
```

- [ ] **Step 3: Type-check + lint**

Run from `ClientApp/`: `npm run tsc` then `npm run lint`
Expected: both pass (the components are not yet imported anywhere — that's fine; no unused-export rule).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeViewItem.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeViewList.tsx
git commit -m "feat(recipes): read-only ingredient list + row components"
```

---

### Task 3: Convert `RecipeViewPage` to read-only

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeViewPage.tsx`

Replace the whole file. Removes `editingItem`, item add/update handlers, `useCreateRecipeItem`/`useUpdateRecipeItem`, extraction polling, `RecipeFooter`, the edit-capable `RecipeContainer`, and `scrollToLastItem`. Keeps search + scaling state. Adds the description band, the "Zutaten" heading, the pencil edit as a direct action (was a menu item), and `RecipeViewList`.

- [ ] **Step 1: Replace file contents**

```tsx
import { Add, Edit, Remove, Search } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    IconButton,
    Stack,
    Typography,
} from "@mui/material";
import { useParams, useRouter } from "@tanstack/react-router";
import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { SearchInputRow } from "../../../components/shared/SearchInputRow";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { RecipeViewList } from "../items/components/RecipeViewList";
import { useRecipeRevision } from "../items/useRecipeRevision";
import { useRecipe } from "../useRecipe";

export const RecipeViewPage = () => {
    const router = useRouter();
    const { t } = useTranslation();
    const { recipeId: recipeIdParam } = useParams({
        from: "/recipes/$recipeId/view",
    });
    const recipeId = parseInt(recipeIdParam);

    const [searchOpen, setSearchOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");
    // Display-only scaling. targetServings overrides the base; null = no override (shows base).
    const [targetServings, setTargetServings] = useState<number | null>(null);

    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: recipe,
        isLoading: recipeLoading,
        error: recipeError,
    } = useRecipe(householdId, recipeId, householdId > 0);

    useRecipeRevision(householdId, recipeId);

    const handleEdit = useCallback(() => {
        router.navigate({
            to: "/recipes/$recipeId/edit",
            params: { recipeId: recipeId.toString() },
        });
    }, [router, recipeId]);

    const handleToggleSearch = useCallback(() => {
        setSearchOpen((prev) => {
            // Clear the query when collapsing so the filter resets (ephemeral by design).
            if (prev) {
                setSearchQuery("");
            }
            return !prev;
        });
    }, []);

    const baseServings = recipe?.servings ?? null;
    const effectiveServings = targetServings ?? baseServings;
    const isScaled =
        baseServings != null &&
        effectiveServings != null &&
        effectiveServings !== baseServings;
    const multiplier =
        baseServings && effectiveServings
            ? effectiveServings / baseServings
            : 1;

    const stepServings = (delta: number) => {
        if (baseServings == null || effectiveServings == null) return;
        const next = Math.min(99, Math.max(1, effectiveServings + delta));
        setTargetServings(next);
    };

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="warning">
                    {t("common.pleaseSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    if (recipeLoading) {
        return (
            <Container maxWidth="sm" sx={{ py: 4, textAlign: "center" }}>
                <CircularProgress />
                <Typography variant="body2" sx={{ mt: 2 }}>
                    {t("recipes.loadingRecipe")}
                </Typography>
            </Container>
        );
    }

    if (recipeError || !recipe) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="error" sx={{ mb: 2 }}>
                    {t("recipes.failedToLoadRecipe")}
                </Alert>
            </Container>
        );
    }

    const directActions: HeadNavigationAction[] = [
        {
            icon: <Edit fontSize="small" />,
            onClick: handleEdit,
            testId: "recipe-edit-button",
        },
    ];
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("common.search"),
            icon: <Search fontSize="small" />,
            onClick: handleToggleSearch,
            testId: "recipe-search-button",
        },
    ];

    return (
        <Box
            sx={{
                height: "calc(100dvh - 56px)",
                display: "flex",
                flexDirection: "column",
                overflow: "hidden",
            }}
        >
            <PageHeadActionBar
                title={recipe.name || t("recipes.untitledRecipe")}
                section="recipes"
                directActions={directActions}
                menuActions={menuActions}
                menuButtonTestId="recipe-header-menu-toggle"
            />

            {recipe.description ? (
                <Container
                    maxWidth="sm"
                    sx={{ px: 2, pb: 1.5, flexShrink: 0 }}
                >
                    <Typography
                        data-testid="recipe-description"
                        variant="body2"
                        sx={{
                            color: "text.secondary",
                            fontStyle: "italic",
                            whiteSpace: "pre-wrap",
                            wordBreak: "break-word",
                            lineHeight: 1.5,
                        }}
                    >
                        {recipe.description}
                    </Typography>
                </Container>
            ) : null}

            <SearchInputRow
                open={searchOpen}
                query={searchQuery}
                onQueryChange={setSearchQuery}
                onClose={handleToggleSearch}
                placeholder={t("recipes.searchPlaceholder")}
                testIdPrefix="recipe-search"
            />

            <Container
                maxWidth="sm"
                sx={{ px: 2, pt: 1, pb: 0.5, flexShrink: 0 }}
            >
                <Stack
                    direction="row"
                    sx={{
                        alignItems: "center",
                        justifyContent: "space-between",
                    }}
                >
                    <Typography
                        variant="overline"
                        color="text.secondary"
                        sx={{ fontWeight: 700, letterSpacing: 1 }}
                    >
                        {t("recipes.ingredientsHeading")}
                    </Typography>
                    {baseServings != null ? (
                        <Stack
                            direction="row"
                            sx={{ alignItems: "center" }}
                            spacing={0.5}
                        >
                            {isScaled ? (
                                <Button
                                    size="small"
                                    onClick={() => setTargetServings(null)}
                                    data-testid="recipe-servings-reset"
                                >
                                    {t("recipes.resetServings")}
                                </Button>
                            ) : null}
                            <IconButton
                                size="small"
                                onClick={() => stepServings(-1)}
                                disabled={
                                    effectiveServings != null &&
                                    effectiveServings <= 1
                                }
                                data-testid="recipe-servings-decrement"
                            >
                                <Remove fontSize="small" />
                            </IconButton>
                            <Typography
                                variant="body2"
                                sx={{
                                    minWidth: 20,
                                    textAlign: "center",
                                    fontWeight: 600,
                                }}
                                data-testid="recipe-servings-value"
                            >
                                {effectiveServings}
                            </Typography>
                            <IconButton
                                size="small"
                                onClick={() => stepServings(1)}
                                disabled={
                                    effectiveServings != null &&
                                    effectiveServings >= 99
                                }
                                data-testid="recipe-servings-increment"
                            >
                                <Add fontSize="small" />
                            </IconButton>
                        </Stack>
                    ) : null}
                </Stack>
                {baseServings != null ? (
                    <Typography
                        variant="caption"
                        color="text.secondary"
                        data-testid="recipe-servings-subline"
                    >
                        {isScaled
                            ? t("recipes.scaledFrom", { count: baseServings })
                            : t("recipes.servingsFor", { count: baseServings })}
                    </Typography>
                ) : null}
            </Container>

            <RecipeViewList
                householdId={householdId}
                recipeId={recipeId}
                searchQuery={searchQuery}
                multiplier={multiplier}
            />
        </Box>
    );
};
```

- [ ] **Step 2: Type-check + lint + build**

Run from `ClientApp/`: `npm run tsc` && `npm run lint` && `npm run build`
Expected: all pass.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeViewPage.tsx
git commit -m "feat(recipes): make recipe view read-only with description band + ingredients heading"
```

---

### Task 4: Move the ingredient editor onto `RecipeEditPage`

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeContainer.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeItemContent.tsx`

The edit page becomes the single editing surface: metadata form (Task 5 converts it; here just embed it as-is) + the interactive `RecipeContainer` + the `RecipeFooter` composer. The metadata form + item list share one outer scroll region; the composer is pinned.

- [ ] **Step 1: Add a `scrollable` prop to `RecipeContainer` and drop `multiplier`**

`RecipeContainer` is currently its own scroll region. On the edit page it must flow inside the page's outer scroll. Add `scrollable?: boolean` (default `true`, preserving any standalone use) that toggles the scroll sx, and remove the now-unused `multiplier` prop (scaling lives only in the read-only view).

Replace the props interface and the destructure + the `Container` sx + the `renderContent`:

```tsx
interface RecipeContainerProps {
    householdId: number;
    recipeId: number;
    editingItem: RecipeItemResponse | null;
    onEdit: (item: RecipeItemResponse) => void;
    isExtracting?: boolean;
    extractingItemId?: number | null;
    searchQuery?: string;
    // When false, the container flows in its parent's scroll instead of being its own
    // scroll region (used on the edit page where it shares scroll with the metadata form).
    scrollable?: boolean;
}
```

Destructure (drop `multiplier`, add `scrollable = true`):

```tsx
        {
            householdId,
            recipeId,
            editingItem,
            onEdit,
            isExtracting,
            extractingItemId,
            searchQuery = "",
            scrollable = true,
        },
        ref,
```

Container `sx` — make the scroll properties conditional:

```tsx
                sx={{
                    ...(scrollable
                        ? { flex: 1, overflow: "auto", minHeight: 0 }
                        : {}),
                    px: featureContentPx,
                    py: 0,
                }}
```

`renderContent` — drop the `multiplier` prop:

```tsx
                        renderContent={(item) => (
                            <RecipeItemContent item={item} />
                        )}
```

- [ ] **Step 2: Strip scaling from `RecipeItemContent`**

The edit-side list never scales. Remove `multiplier`, `scaleQuantity`, the struck-original branch, and the `color` on the chip. Replace the whole file:

```tsx
import { Box, ListItemText, Typography } from "@mui/material";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import type { RecipeItemResponse } from "../../../../lib/api";

interface Props {
    item: RecipeItemResponse;
}

export function RecipeItemContent({ item }: Props) {
    return (
        <ListItemText
            data-testid={`recipe-item-${item.id}`}
            slotProps={{ secondary: { component: "div" } }}
            primary={
                <Typography
                    variant="body2"
                    sx={{
                        fontWeight: 500,
                        wordBreak: "break-word",
                    }}
                >
                    {item.text}
                </Typography>
            }
            secondary={
                item.quantity || item.comment ? (
                    <Box
                        sx={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 0.25,
                        }}
                    >
                        {item.comment ? (
                            <Typography
                                component="div"
                                data-testid={`recipe-item-comment-${item.id}`}
                                variant="caption"
                                color="text.secondary"
                                sx={{
                                    fontSize: "0.7rem",
                                    fontStyle: "italic",
                                    whiteSpace: "pre-wrap",
                                    wordBreak: "break-word",
                                }}
                            >
                                {item.comment}
                            </Typography>
                        ) : null}
                        {item.quantity ? (
                            <Box
                                sx={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: 0.5,
                                }}
                            >
                                <ItemQuantityChip
                                    quantity={item.quantity}
                                    testId={`recipe-item-quantity-${item.text}`}
                                />
                            </Box>
                        ) : null}
                    </Box>
                ) : null
            }
        />
    );
}
```

- [ ] **Step 3: Replace `RecipeEditPage.tsx`**

```tsx
import { Delete } from "@mui/icons-material";
import {
    Alert,
    Box,
    Container,
    Skeleton,
} from "@mui/material";
import { useParams } from "@tanstack/react-router";
import { useCallback, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import type { QuantityDto, RecipeItemResponse } from "../../../lib/api";
import { featureContentPx, pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { DeleteRecipeConfirmDialog } from "../components/DeleteRecipeConfirmDialog";
import { EditRecipeForm } from "../components/EditRecipeForm";
import { RecipeContainer } from "../items/components/RecipeContainer";
import { RecipeFooter } from "../items/components/RecipeFooter";
import { useCreateRecipeItem } from "../items/useCreateRecipeItem";
import { useRecipeExtractionPoll } from "../items/useRecipeExtractionPoll";
import { useRecipeItems } from "../items/useRecipeItems";
import { useRecipeRevision } from "../items/useRecipeRevision";
import { useUpdateRecipeItem } from "../items/useUpdateRecipeItem";
import { useRecipe } from "../useRecipe";

export const RecipeEditPage = () => {
    const { recipeId: recipeIdParam } = useParams({
        from: "/recipes/$recipeId/edit",
    });
    const { t } = useTranslation();
    const recipeId = parseInt(recipeIdParam, 10);

    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [editingItem, setEditingItem] = useState<RecipeItemResponse | null>(
        null,
    );
    const [pendingExtraction, setPendingExtraction] = useState<{
        id: number;
        extractionPending: boolean;
    } | null>(null);

    const {
        currentHousehold,
        isLoading: householdLoading,
        error: householdError,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: recipe,
        isLoading: recipeLoading,
        error: recipeError,
    } = useRecipe(householdId, recipeId, hasActiveHousehold && !isNaN(recipeId));

    const { data: items = [] } = useRecipeItems(householdId, recipeId, !!recipe);
    useRecipeRevision(householdId, recipeId);

    const createMutation = useCreateRecipeItem();
    const updateMutation = useUpdateRecipeItem();

    const { isExtracting, extractingItemId } = useRecipeExtractionPoll(
        householdId,
        recipeId,
        pendingExtraction?.id ?? null,
        pendingExtraction?.extractionPending ?? false,
    );

    const scrollToLastItem = useCallback(() => {
        if (scrollContainerRef.current) {
            const listItems =
                scrollContainerRef.current.querySelectorAll(
                    ".MuiListItem-root",
                );
            const lastItem = listItems[listItems.length - 1];
            if (lastItem) {
                lastItem.scrollIntoView({
                    behavior: "smooth",
                    block: "center",
                });
            }
        }
    }, []);

    const handleAddItem = useCallback(
        async (text: string, comment: string | null) => {
            if (!householdId) return;
            try {
                const created = await createMutation.mutateAsync({
                    path: { householdId, recipeId },
                    body: { text, comment },
                });
                setPendingExtraction({
                    id: created.id,
                    extractionPending: created.extractionPending,
                });
            } catch {
                // createMutation.onError rolls back the optimistic item.
            }
        },
        [createMutation, householdId, recipeId],
    );

    const handleUpdateItem = useCallback(
        (text: string, quantity: QuantityDto | null, comment: string | null) => {
            if (editingItem?.id && householdId) {
                updateMutation.mutate({
                    path: { householdId, recipeId, itemId: editingItem.id },
                    body: {
                        text,
                        quantity,
                        clearQuantity: quantity === null,
                        comment,
                    },
                });
                setEditingItem(null);
            }
        },
        [editingItem, updateMutation, householdId, recipeId],
    );

    const isLoading = householdLoading || recipeLoading;
    const error = householdError || recipeError;

    if (isLoading) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    <Skeleton
                        variant="rectangular"
                        height={40}
                        sx={{ mb: 1 }}
                    />
                    <Skeleton variant="text" width="60%" height={32} />
                </Box>
                <Skeleton variant="rectangular" height={200} />
            </Container>
        );
    }

    if (error) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("recipes.failedToLoadRecipe")}
                </Alert>
            </Container>
        );
    }

    if (!hasActiveHousehold || !householdId) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    if (!recipe) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="warning">
                    {t("recipes.failedToLoadRecipe")}
                </Alert>
            </Container>
        );
    }

    const menuActions: HeadNavigationAction[] = [
        {
            text: t("recipes.deleteRecipe"),
            icon: <Delete fontSize="small" color="error" />,
            onClick: () => setDeleteDialogOpen(true),
            color: "error",
        },
    ];

    return (
        <Box
            sx={{
                height: "calc(100dvh - 56px)",
                display: "flex",
                flexDirection: "column",
                overflow: "hidden",
            }}
        >
            <PageHeadActionBar
                title={t("recipes.editRecipe")}
                section="recipes"
                directActions={[]}
                menuActions={menuActions}
                menuButtonTestId="recipe-edit-menu-toggle"
            />

            <Box
                ref={scrollContainerRef}
                sx={{ flex: 1, overflow: "auto", minHeight: 0 }}
            >
                <Container
                    maxWidth="sm"
                    sx={{ px: featureContentPx, pt: 2, pb: 1 }}
                >
                    <EditRecipeForm householdId={householdId} recipe={recipe} />
                </Container>
                <RecipeContainer
                    householdId={householdId}
                    recipeId={recipeId}
                    editingItem={editingItem}
                    onEdit={setEditingItem}
                    isExtracting={isExtracting}
                    extractingItemId={extractingItemId}
                    scrollable={false}
                />
            </Box>

            <RecipeFooter
                editingItem={editingItem}
                existingItems={items}
                onAddItem={handleAddItem}
                onUpdateItem={handleUpdateItem}
                onCancelEdit={() => setEditingItem(null)}
                isLoading={createMutation.isPending || updateMutation.isPending}
                onScrollToLast={scrollToLastItem}
            />

            {recipe.id ? (
                <DeleteRecipeConfirmDialog
                    open={deleteDialogOpen}
                    onClose={() => setDeleteDialogOpen(false)}
                    householdId={householdId}
                    recipeId={recipe.id}
                    recipeName={recipe.name || t("recipes.untitledRecipe")}
                />
            ) : null}
        </Box>
    );
};
```

Note: `EditRecipeForm` is still keyed in the page by nothing here — the original keyed it `key={recipe.id}`. Keep that behavior by passing `key={recipe.id}` on the `EditRecipeForm` element if the once-on-mount seeding must reset across recipes. Add `key={recipe.id}` to the `<EditRecipeForm ... />`.

- [ ] **Step 4: Add `key={recipe.id}` to EditRecipeForm in the edit page**

```tsx
                    <EditRecipeForm
                        key={recipe.id}
                        householdId={householdId}
                        recipe={recipe}
                    />
```

- [ ] **Step 5: Type-check + lint + build**

Run from `ClientApp/`: `npm run tsc` && `npm run lint` && `npm run build`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeContainer.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeItemContent.tsx
git commit -m "feat(recipes): move ingredient editor onto the edit page"
```

---

### Task 5: Implicit save-on-change for the metadata form

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx`

Remove Save/Cancel + `router.history.back()`. Auto-save on change (debounced ~600 ms) + flush on blur + clear pending timer on unmount. Validation gates the save. Light saving/saved status.

- [ ] **Step 1: Replace file contents**

```tsx
import { Box, Card, CardContent, Stack, TextField, Typography } from "@mui/material";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";
import { useUpdateRecipe } from "../useUpdateRecipe";

const SAVE_DEBOUNCE_MS = 600;

interface EditRecipeFormProps {
    householdId: number;
    recipe: RecipeResponse;
}

export const EditRecipeForm = ({
    householdId,
    recipe,
}: EditRecipeFormProps) => {
    const { t } = useTranslation();
    const updateRecipeMutation = useUpdateRecipe();

    // Seeded once on mount. The parent keys this form by recipe.id, so switching to a different
    // recipe remounts and reseeds — no reset-on-prop effect (which would also clobber edits).
    const [editedName, setEditedName] = useState(recipe.name || "");
    const [editedDescription, setEditedDescription] = useState(
        recipe.description ?? "",
    );
    const [editedServings, setEditedServings] = useState(
        recipe.servings != null ? String(recipe.servings) : "",
    );

    const nameInvalid = editedName.trim().length === 0;
    const servingsNum = editedServings === "" ? null : Number(editedServings);
    const servingsInvalid =
        editedServings !== "" &&
        (!Number.isInteger(servingsNum) ||
            (servingsNum as number) < 1 ||
            (servingsNum as number) > 99);

    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    // Latest field state, read by the debounced/blur flush without re-creating the timer.
    const latest = useRef({
        name: editedName,
        description: editedDescription,
        servings: editedServings,
        nameInvalid,
        servingsInvalid,
    });
    latest.current = {
        name: editedName,
        description: editedDescription,
        servings: editedServings,
        nameInvalid,
        servingsInvalid,
    };

    const { mutate } = updateRecipeMutation;
    const recipeId = recipe.id;

    const save = useCallback(() => {
        if (!recipeId) return;
        const cur = latest.current;
        if (cur.nameInvalid || cur.servingsInvalid) return;
        mutate({
            path: { householdId, recipeId },
            body: {
                name: cur.name.trim(),
                description: cur.description.trim() || null,
                servings: cur.servings === "" ? null : Number(cur.servings),
            },
        });
    }, [householdId, recipeId, mutate]);

    const scheduleSave = useCallback(() => {
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(() => {
            timerRef.current = null;
            save();
        }, SAVE_DEBOUNCE_MS);
    }, [save]);

    const flushSave = useCallback(() => {
        if (timerRef.current) {
            clearTimeout(timerRef.current);
            timerRef.current = null;
        }
        save();
    }, [save]);

    useEffect(
        () => () => {
            if (timerRef.current) clearTimeout(timerRef.current);
        },
        [],
    );

    let status: "saving" | "saved" | "idle" = "idle";
    if (updateRecipeMutation.isPending) {
        status = "saving";
    } else if (updateRecipeMutation.isSuccess) {
        status = "saved";
    }

    return (
        <Card elevation={4}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                <Stack spacing={3}>
                    <TextField
                        label={t("recipes.recipeName")}
                        value={editedName}
                        onChange={(e) => {
                            setEditedName(e.target.value);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        fullWidth
                        required
                        error={nameInvalid}
                        helperText={
                            nameInvalid ? t("recipes.recipeNameRequired") : ""
                        }
                    />

                    <TextField
                        label={t("recipes.description")}
                        value={editedDescription}
                        onChange={(e) => {
                            setEditedDescription(e.target.value);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        fullWidth
                        multiline
                        minRows={2}
                        placeholder={t("recipes.descriptionPlaceholder")}
                        slotProps={{
                            htmlInput: {
                                maxLength: 1000,
                                "data-testid": "recipe-description-input",
                            },
                        }}
                    />

                    <TextField
                        type="number"
                        label={t("recipes.servings")}
                        value={editedServings}
                        onChange={(e) => {
                            setEditedServings(e.target.value);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        sx={{ width: 140 }}
                        error={servingsInvalid}
                        helperText={
                            servingsInvalid ? t("recipes.servingsRange") : ""
                        }
                        slotProps={{
                            htmlInput: {
                                min: 1,
                                max: 99,
                                "data-testid": "recipe-servings-input",
                            },
                        }}
                    />

                    <Box
                        data-testid="recipe-metadata-status"
                        data-status={status}
                        sx={{ minHeight: 20 }}
                    >
                        <Typography variant="caption" color="text.secondary">
                            {status === "saving"
                                ? t("common.saving")
                                : status === "saved"
                                  ? t("common.saved")
                                  : ""}
                        </Typography>
                    </Box>
                </Stack>
            </CardContent>
        </Card>
    );
};
```

- [ ] **Step 2: Type-check + lint**

Run from `ClientApp/`: `npm run tsc` && `npm run lint`
Expected: pass. (If the linter flags `react-hooks/exhaustive-deps` on `save`, the deps are correct as written — `latest.current` is intentionally read inside without being a dep. Do not add `latest` to deps.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx
git commit -m "feat(recipes): implicit save-on-change for recipe metadata"
```

---

### Task 6: Create flow lands on `/edit`

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/CreateRecipeForm.tsx`

- [ ] **Step 1: Change the post-create navigation**

Replace the `navigate({...})` call in `handleSubmit` (currently navigating to `/recipes/$recipeId/view`) with:

```tsx
                navigate({
                    to: "/recipes/$recipeId/edit",
                    params: { recipeId: response.id.toString() },
                    replace: true,
                });
```

`replace: true` keeps the create route off the history stack (mitigates the back-nav issue logged in `BUGS.md`).

- [ ] **Step 2: Type-check + lint**

Run from `ClientApp/`: `npm run tsc` && `npm run lint`
Expected: pass.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/CreateRecipeForm.tsx
git commit -m "feat(recipes): land on edit page after creating a recipe"
```

---

### Task 7: Dead-code cleanup

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/common/ItemQuantityChip.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/{en,de}/translation.json`

- [ ] **Step 1: Confirm `ItemQuantityChip.color` is now unused, then remove it**

Run from `ClientApp/`:
`grep -rn "ItemQuantityChip" src | grep -v "ItemQuantityChip.tsx"` to list callers, and
`grep -rn "color=" src/features/lists src/features/inventories src/features/recipes | grep -i quantitychip` (manually verify no caller passes `color` to `ItemQuantityChip`).
Expected: no caller passes `color` (the only one was the recipe scaling chip, removed in Task 4).

Then edit `ItemQuantityChip.tsx`: remove the `color?: ChipProps["color"];` line from `Props`, remove `color` from the destructure, remove the `color={color}` line on `<Chip>`, and remove the now-unused `type ChipProps` from the import (`import { Chip } from "@mui/material";`).

- [ ] **Step 2: Confirm `recipes.servingsFrom` is now unused, then remove it**

Run from `ClientApp/`: `grep -rn "servingsFrom" src`
Expected: no matches (the view now uses `servingsFor`/`scaledFrom`).
If no matches, remove the `"servingsFrom": ...` line from both `en/translation.json` and `de/translation.json`.

- [ ] **Step 3: Type-check + lint + build**

Run from `ClientApp/`: `npm run tsc` && `npm run lint` && `npm run build`
Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/common/ItemQuantityChip.tsx Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "chore(recipes): remove dead scaling-chip color prop + replaced i18n key"
```

---

### Task 8: Integration tests

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`

The create flow now lands on `/edit`; adding/editing ingredients happens on `/edit`; `/view` is read-only.

- [ ] **Step 1: Add seed helpers to `TestApiClient.cs`**

In the `// ---- Recipes ----` region, add a helper that creates a recipe with servings and returns its id, and a helper that sets an item's quantity via the update endpoint. Mirror the existing `CreateRecipeAsync` / `TryUpdateRecipeAsync` style (use the same JSON + response-parsing idiom already in the file):

```csharp
public async Task<int> CreateRecipeWithServingsAsync(string name, int servings)
{
    var response = await TryCreateRecipeWithServingsAsync(name, servings);
    var json = await response.JsonAsync();
    return json!.Value.GetProperty("id").GetInt32();
}

// Sets a numeric quantity on a recipe item via the item update endpoint, so scaling
// scenarios have a deterministic quantity to scale (extraction is async/non-deterministic).
public Task<IAPIResponse> TrySetRecipeItemQuantityAsync(
    int recipeId, int itemId, double value, string unit, int? householdId = null)
{
    return _request.PutAsync(
        $"/api/households/{householdId ?? _householdId}/recipes/{recipeId}/items/{itemId}",
        new APIRequestContextOptions
        {
            DataObject = new { quantity = new { value, unit }, clearQuantity = false },
        });
}
```

Note: match the actual field/route conventions already used by the other `Try*RecipeItem*` helpers in this file (request field name for the client, `_householdId`, the request object name). Inspect the neighbouring methods (e.g. `TryReorderRecipeItemAsync`, `TryCreateRecipeItemAsync`) and follow their exact shape — the snippet above is the intent, not a verbatim copy. Confirm the quantity DTO shape against `RecipeItemResponse`/`UpdateRecipeItem` (`value` + `unit`).

- [ ] **Step 2: Update navigation/assertion steps in `RecipeSteps.cs`**

(a) `WhenISubmitTheRecipeForm` — the create flow now lands on `/edit`. Change the final wait:

```csharp
        await ctx.Page.WaitForURLAsync("**/recipes/*/edit");
```

(b) Add an "open for editing" navigation step (keep the existing `I open the recipe` → `/view`):

```csharp
    [When("I open the recipe {string} for editing")]
    public async Task WhenIOpenTheRecipeForEditing(string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        await ctx.Page.GotoAsync(
            $"/recipes/{recipeId}/edit",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }
```

(c) Add an edit-page assertion + read-only assertions + a click-edit step:

```csharp
    [Then("I am on the recipe edit page for {string}")]
    public async Task ThenIAmOnTheRecipeEditPageFor(string recipeName)
    {
        await ctx.Page.WaitForURLAsync("**/recipes/*/edit");
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-items")).ToBeVisibleAsync();
        // The composer (add-ingredient) is present on the edit page.
        await Assertions.Expect(
            ctx.Page.GetByTestId("autocomplete-input-textfield")).ToBeVisibleAsync();
    }

    [Then("the recipe view is read-only")]
    public async Task ThenTheRecipeViewIsReadOnly()
    {
        // No add-ingredient composer on the read-only view.
        await Assertions.Expect(
            ctx.Page.GetByTestId("autocomplete-input-textfield")).Not.ToBeVisibleAsync();
        // But the edit affordance is present.
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-edit-button")).ToBeVisibleAsync();
    }

    [When("I tap the edit recipe button")]
    public async Task WhenITapTheEditRecipeButton()
    {
        await ctx.Page.GetByTestId("recipe-edit-button").ClickAsync();
    }
```

(d) Seed helpers used by new scenarios:

```csharp
    [Given("there is a recipe named {string} with servings {int}")]
    public async Task GivenThereIsARecipeNamedWithServings(string name, int servings)
    {
        var recipeId = await api.CreateRecipeWithServingsAsync(name, servings);
        ctx.RecipeIds[name] = recipeId;
    }

    [Given("the recipe {string} has an ingredient {string} with quantity {double} {string}")]
    public async Task GivenTheRecipeHasIngredientWithQuantity(
        string recipeName, string itemText, double value, string unit)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var itemId = await api.CreateRecipeItemAsync(recipeId, itemText);
        await api.TrySetRecipeItemQuantityAsync(recipeId, itemId, value, unit);
    }
```

(e) Auto-save steps:

```csharp
    [When("I set the recipe description to {string}")]
    public async Task WhenISetTheRecipeDescriptionTo(string text)
    {
        // Fill the description, then blur to flush the debounced auto-save, and await the
        // recipe (not item) PUT so the follow-up navigation reads post-save state.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/recipes/")
            && !r.Url.Contains("/items")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        var description = ctx.Page.GetByTestId("recipe-description-input");
        await description.FillAsync(text);
        await description.BlurAsync();
        await responseTask;
    }

    [Then("the recipe description shows {string}")]
    public async Task ThenTheRecipeDescriptionShows(string text)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-description"))
            .ToContainTextAsync(text);
    }
```

The description field already carries `data-testid="recipe-description-input"` (added in Task 5), so the step selects it directly — no fragile index-based textbox selection.

- [ ] **Step 3: Update + add scenarios in `Recipes.feature`**

Update the create scenario and the two item scenarios, and add read-only / auto-save / scaling scenarios:

```gherkin
  Scenario: User creates a recipe
    When I navigate to "/recipes/create"
    And I fill in the recipe name "Pasta Carbonara"
    And I submit the recipe form
    Then I am on the recipe edit page for "Pasta Carbonara"

  Scenario: User adds an ingredient to a recipe
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I add ingredient "Eggs" to the recipe
    Then "Eggs" appears in the recipe items

  Scenario: User adds a quantity to a recipe ingredient via edit mode
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I add ingredient "Eggs" to the recipe
    And I open the ingredient item menu for "Eggs"
    And I start editing the item
    And I open the "quantity" composer panel
    And I set the quantity to "3"
    And I save the recipe item edit
    Then the recipe item "Eggs" shows quantity "3"

  Scenario: Recipe view is read-only and links to edit
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara"
    Then the recipe view is read-only
    When I tap the edit recipe button
    Then I am on the recipe edit page for "Pasta Carbonara"

  Scenario: Editing recipe metadata auto-saves
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I set the recipe description to "Quick weeknight dinner"
    And I open the recipe "Pasta Carbonara"
    Then the recipe description shows "Quick weeknight dinner"
```

(The "start editing the item" / "open the quantity composer panel" / "set the quantity to" steps already exist from the recipe-item suite — reuse them.)

- [ ] **Step 4: Build the SPA, then run the recipe IT**

Run from `ClientApp/`: `npm run build` (IT serves `ClientApp/build`).
Then from repo root: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Recipes"`
Expected: the Recipes feature scenarios pass. If the filter reports an unexpected match count, re-check per the Reqnroll filter caveat (filter on scenario-title words; confirm the matched count).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeSteps.cs Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs
git commit -m "test(recipes): cover read-only view, create→edit, metadata auto-save"
```

---

### Task 9: Full verification gate + manual check

**Files:** none (verification only).

- [ ] **Step 1: Frontend verify**

From `ClientApp/`: `npm run lint` && `npm run tsc` && `npm run prettier` (or `npm run fix`) && `npm run build`
Expected: all clean (prettier writes; re-commit if it reformats).

- [ ] **Step 2: Full solution tests**

From repo root: `dotnet test Application/Frigorino.sln`
Expected: green (the 2 pre-existing undo-toast skips are expected). Read the IT log dump first on any red.

- [ ] **Step 3: Docker build**

From repo root: `docker build -f Application/Dockerfile -t frigorino .`
Expected: exit 0 (the 2 known `VITE_FCM_VAPID_KEY` secret-in-ARG warnings are expected). If the daemon is unreachable, ask the user to start Docker Desktop.

- [ ] **Step 4: Manual browser verification (dev-up + Playwright MCP)**

Verify the full flow:
- Create a recipe with a description + servings → lands on `/edit`.
- Add ingredients (one with a quantity, one without) on `/edit`; reorder; edit an item.
- Edit the description/servings fields → no Save button; status shows saving→saved; navigate back to `/view` → changes persisted.
- `/view` is read-only: description band present, "Zutaten" heading + servings stepper, quantity-column rows, no composer / drag handles.
- Scaling: increment servings → quantities scale (3 dp), accented value + struck original, "skaliert von N", Reset appears; Reset returns to base and hides.
- Search from the ⋮ menu filters ingredients on `/view`.
- A recipe with no servings shows the "Zutaten" heading but no stepper; an empty recipe shows the empty state.

- [ ] **Step 5: Commit any prettier reformat**

```bash
git add -A && git commit -m "style: prettier formatting" || echo "nothing to format"
```

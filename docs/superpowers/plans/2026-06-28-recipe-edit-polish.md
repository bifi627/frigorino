# Recipe Edit Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Calm the recipe edit page — collapse the optional extras (tags + sources) into one closed "Details" accordion, and compact the ingredient rows to dense single-line rows — while keeping the per-row ⋮ menu.

**Architecture:** Pure frontend / presentational. Three independent changes: (1) rewrite `RecipeItemContent` to a flex row; (2) add an opt-in `dense` prop to the shared `SortableList`/`SortableListItem`, passed only by `RecipeContainer`; (3) lift `RecipeTagSelector` out of `EditRecipeForm` and wrap it + `RecipeSourcesStrip` in a collapsed MUI `Accordion` on `RecipeEditPage`. No backend, no migration, no API regeneration.

**Tech Stack:** React 19, TypeScript, MUI v7, TanStack Router/Query, Vite. i18next (`recipes` namespace).

**Spec:** `docs/superpowers/specs/2026-06-28-recipe-edit-polish-design.md`

## Global Constraints

- **No new automated tests.** This is a presentational change; the spec deliberately adds no unit tests and no IT scenarios. Verification per task = `npm run tsc` + `npm run lint`. The regression net for the shared-component change is the **existing** IT suite (run once in the final task) plus a **manual browser verify**. (This deviates from the skill's default red-green TDD because there is no testable unit — the deliverables are visual. Verify visually, not with fabricated assertions.)
- **Preserve every existing testid.** `recipe-item-{id}`, `recipe-item-comment-{id}`, `recipe-item-quantity-{text}`, `item-menu-button-{text}`, `edit-item-button`, `delete-item-button`, `recipe-name-input`, `recipe-description-input`, `recipe-servings-*`, `drag-handle-item-*`. The only new testid is `recipe-details-accordion` (additive).
- **`dense` defaults to `false`.** Lists + Inventories must render byte-for-byte as before — they never pass `dense`.
- **Styling via the theme** (`knowledge/Frontend_Styling.md`): no hand-rolled `borderRadius: 2` / manual `boxShadow` / inline `fontSize` breakpoints. Use MUI size props.
- **i18n:** new key `recipes.details` goes in `public/locales/{en,de}/translation.json` only. `recipes` is typed `Record<string, string>` in `src/types/i18next.d.ts`, so **no** `.d.ts` change. Tests never assert on translated text.
- **Commits:** conventional style, no `Co-Authored-By` / "Generated with" trailers. Frontend tooling via `npm run` scripts (never raw `npx`).
- **Branch:** `feat/recipe-edit-polish` (already created off `stage`).

---

### Task 1: Compact ingredient row content (`RecipeItemContent`)

Recipe-only, lowest risk. Replace the `ListItemText` primary/secondary stack with a flex row: name (+ italic comment underneath) on the left, quantity chip right-aligned on the name's line.

**Files:**
- Modify (full rewrite): `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeItemContent.tsx`

**Interfaces:**
- Consumes: `RecipeItemResponse` (`item.id`, `item.text`, `item.comment`, `item.quantity`) from `../../../../lib/api`; `ItemQuantityChip` (`quantity`, `testId` props) from `../../../../components/common/ItemQuantityChip`.
- Produces: unchanged export `RecipeItemContent({ item }: { item: RecipeItemResponse })` — `RecipeContainer`'s `renderContent={(item) => <RecipeItemContent item={item} />}` wiring is untouched.

- [ ] **Step 1: Rewrite the component**

Replace the entire contents of `RecipeItemContent.tsx` with:

```tsx
import { Box, Typography } from "@mui/material";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import type { RecipeItemResponse } from "../../../../lib/api";

interface Props {
    item: RecipeItemResponse;
}

export function RecipeItemContent({ item }: Props) {
    return (
        <Box
            data-testid={`recipe-item-${item.id}`}
            sx={{
                display: "flex",
                width: "100%",
                alignItems: "flex-start",
                justifyContent: "space-between",
                gap: 1,
            }}
        >
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
                            fontSize: "0.7rem",
                            fontStyle: "italic",
                            whiteSpace: "pre-wrap",
                            wordBreak: "break-word",
                        }}
                    >
                        {item.comment}
                    </Typography>
                ) : null}
            </Box>
            {item.quantity ? (
                <ItemQuantityChip
                    quantity={item.quantity}
                    testId={`recipe-item-quantity-${item.text}`}
                />
            ) : null}
        </Box>
    );
}
```

(`minWidth: 0` on the left column lets long names wrap/ellipsis instead of pushing the chip off-row. `alignItems: flex-start` keeps the chip on the name's first line even when a comment wraps below.)

- [ ] **Step 2: Type-check and lint**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc && npm run lint`
Expected: both PASS, no errors. (`Box`, `ListItemText` import removal is clean — `ListItemText` is no longer referenced.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeItemContent.tsx
git commit -m "refactor(recipes): flex-row layout for ingredient row content"
```

---

### Task 2: Opt-in `dense` row chrome (shared `SortableList` + `RecipeContainer`)

Add a `dense?: boolean` prop (default `false`) that swaps the per-item card for a bottom hairline divider and kills both sources of inter-row gap. Only `RecipeContainer` opts in; Lists + Inventories are untouched.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/sortables/SortableListItem.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/components/sortables/SortableList.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeContainer.tsx`

**Interfaces:**
- Produces: `SortableListItemProps<T>` and `SortableListProps<T>` both gain `dense?: boolean` (optional, default `false`). `SortableList` forwards `dense` to every `SortableListItem`. No change to any consumer that omits `dense`.
- Consumes: nothing new; `SortableItem` is unchanged (`containerSx` flows straight through it).

- [ ] **Step 1: Add `dense` to `SortableListItem` and make `containerSx` conditional**

In `SortableListItem.tsx`:

1a. Add `dense` to the props interface (after `showCheckbox`):

```tsx
    showDragHandles?: boolean;
    showCheckbox?: boolean;
    /** Opt-in compact chrome: bottom hairline divider instead of a per-item card. Default false. */
    dense?: boolean;
    renderContent: (item: T) => React.ReactNode;
```

1b. Add `dense = false` to the destructured params (after `showCheckbox = false`):

```tsx
    showDragHandles = false,
    showCheckbox = false,
    dense = false,
    renderContent,
}: SortableListItemProps<T>) {
```

1c. Just before the `return (`, derive the shared accent + elevation as named values (replaces the inline ternaries that were inside `containerSx`):

```tsx
    let borderAccent: string;
    if (isEditing) {
        borderAccent = "warning.main";
    } else if (isProcessing) {
        borderAccent = "primary.main";
    } else {
        borderAccent = "divider";
    }

    let elevation: number;
    if (isDragging) {
        elevation = 3;
    } else if (isEditing) {
        elevation = 2;
    } else {
        elevation = 0;
    }
```

1d. Replace the `containerSx={{ ... }}` object passed to `<SortableItem>` (the whole `borderRadius`/`mb`/`bgcolor`/`border`/`borderColor`/`boxShadow`/`opacity`/`transition` block plus the two pulse spreads) with:

```tsx
            containerSx={{
                bgcolor: isEditing ? "warning.50" : "background.paper",
                opacity: item.status ? 0.7 : 1,
                transition: "all 0.2s ease",
                boxShadow: elevation,
                ...(dense
                    ? {
                          borderBottom: "1px solid",
                          borderBottomColor: borderAccent,
                      }
                    : {
                          borderRadius: 1,
                          mb: 0.5,
                          border: "1px solid",
                          borderColor: borderAccent,
                      }),
                // Editing wins over processing (you can't edit a row mid-extraction).
                ...(isEditing && {
                    animation: "pulse 2s ease-in-out infinite",
                    "@keyframes pulse": {
                        "0%": {
                            boxShadow: "0 0 0 0 rgba(237, 108, 2, 0.4)",
                        },
                        "70%": {
                            boxShadow: "0 0 0 10px rgba(237, 108, 2, 0)",
                        },
                        "100%": {
                            boxShadow: "0 0 0 0 rgba(237, 108, 2, 0)",
                        },
                    },
                }),
                ...(!isEditing &&
                    isProcessing && {
                        animation: "processingPulse 1.4s ease-in-out infinite",
                        "@keyframes processingPulse": {
                            "0%": {
                                boxShadow: "0 0 0 0 rgba(25, 118, 210, 0.4)",
                            },
                            "70%": {
                                boxShadow: "0 0 0 8px rgba(25, 118, 210, 0)",
                            },
                            "100%": {
                                boxShadow: "0 0 0 0 rgba(25, 118, 210, 0)",
                            },
                        },
                    }),
            }}
```

(Non-dense output is identical to today: `borderAccent` and `elevation` reproduce the exact same ternary values. Dense drops the card border/radius/`mb` and draws only a bottom hairline whose color still reflects the editing/processing state.)

- [ ] **Step 2: Add `dense` to `SortableList` and forward it + drop the list gap**

In `SortableList.tsx`:

2a. Add to `SortableListProps<T>` (in the `// UI props` group, after `showCheckbox`):

```tsx
    showDragHandles?: boolean;
    showCheckbox?: boolean;
    /** Opt-in compact chrome forwarded to each row (default false). */
    dense?: boolean;
    renderContent: (item: T) => React.ReactNode;
```

2b. Add `dense = false` to the destructured params (after `showCheckbox = false,`):

```tsx
    showDragHandles = false,
    showCheckbox = false,
    dense = false,
    renderContent,
```

2c. In the **unchecked** `<List>` (currently `data-section="unchecked-items"`), make the row gap conditional:

```tsx
                        <List
                            data-section="unchecked-items"
                            sx={{
                                py: 0,
                                "& .MuiListItem-root": { mb: dense ? 0 : 0.5 },
                            }}
                        >
```

2d. Do the same on the **checked** `<List>` (`data-section="checked-items"`):

```tsx
                    <List
                        data-section="checked-items"
                        sx={{
                            py: 0,
                            "& .MuiListItem-root": { mb: dense ? 0 : 0.5 },
                        }}
                    >
```

2e. Pass `dense={dense}` to **both** `<SortableListItem>` renders (the unchecked map and the checked map). Add the prop line alongside the existing `showDragHandles=...` / `renderContent=...` props, e.g. for the unchecked render:

```tsx
                                    showCheckbox={showCheckbox}
                                    showDragHandles={showDragHandles}
                                    dense={dense}
                                    renderContent={renderContent}
```

and for the checked render:

```tsx
                                showCheckbox={showCheckbox}
                                showDragHandles={false}
                                dense={dense}
                                renderContent={renderContent}
```

- [ ] **Step 3: Opt `RecipeContainer` into dense**

In `RecipeContainer.tsx`, add `dense` to the `<SortableList>` props (right after `items={sectionItems}`):

```tsx
                <SortableList
                    items={sectionItems}
                    dense
                    isLoading={isLoading}
                    error={error}
```

- [ ] **Step 4: Type-check and lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: both PASS. No other `SortableList` consumer changed signature (all omit `dense`).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/sortables/SortableListItem.tsx Application/Frigorino.Web/ClientApp/src/components/sortables/SortableList.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeContainer.tsx
git commit -m "feat(sortable): opt-in dense row chrome, used by recipe items"
```

---

### Task 3: Collapse extras into a "Details" accordion

Lift `RecipeTagSelector` out of `EditRecipeForm` and wrap it + `RecipeSourcesStrip` in one collapsed accordion on `RecipeEditPage`, between the metadata form and the sections.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

**Interfaces:**
- Consumes: `RecipeTagSelector({ householdId, recipe })`, `RecipeSourcesStrip({ householdId, recipeId })` — both already imported/used in the codebase; `RecipeTagSelector` moves its render site from `EditRecipeForm` to `RecipeEditPage`. `RecipeEditPage` already has `householdId`, `recipeId`, `recipe` in scope.
- Produces: no exported-signature change. `EditRecipeForm` keeps its `{ householdId, recipe }` props (`householdId` is still used by the save mutation at line ~81; `recipe` by name/servings/description).

- [ ] **Step 1: Remove the tag selector from `EditRecipeForm`**

In `EditRecipeForm.tsx`:
- Delete the import line: `import { RecipeTagSelector } from "./RecipeTagSelector";`
- Delete the render line: `<RecipeTagSelector householdId={householdId} recipe={recipe} />`

Leave everything else (name input, servings stepper, description, autosave-status box) untouched.

- [ ] **Step 2: Add the i18n key (en + de)**

In **both** `public/locales/en/translation.json` and `public/locales/de/translation.json`, add a `"details"` key as the first entry inside the `"recipes"` object (line ~353, right after the opening `"recipes": {`). EN:

```json
    "recipes": {
        "details": "Details",
        "recipes": "Recipes",
```

DE:

```json
    "recipes": {
        "details": "Details",
        "recipes": "Rezepte",
```

- [ ] **Step 3: Add the accordion to `RecipeEditPage`**

In `RecipeEditPage.tsx`:

3a. Extend the MUI imports. Change line 2 from:

```tsx
import { Alert, Box, Button, Container, Skeleton, Stack } from "@mui/material";
```

to:

```tsx
import {
    Accordion,
    AccordionDetails,
    AccordionSummary,
    Alert,
    Box,
    Button,
    Container,
    Skeleton,
    Stack,
    Typography,
} from "@mui/material";
```

3b. Extend the icon import. Change line 1 from:

```tsx
import { Add, Delete } from "@mui/icons-material";
```

to:

```tsx
import { Add, Delete, ExpandMore } from "@mui/icons-material";
```

3c. Add the `RecipeTagSelector` import (next to the other `../components/` imports, e.g. after the `RecipeSourcesStrip` import):

```tsx
import { RecipeTagSelector } from "../components/RecipeTagSelector";
```

3d. In the page `<Stack spacing={2}>`, replace the standalone `<RecipeSourcesStrip .../>` block (currently between `<EditRecipeForm>` and `<SortableSectionList>`) with the accordion wrapping both extras:

```tsx
                        <EditRecipeForm
                            key={recipe.id}
                            householdId={householdId}
                            recipe={recipe}
                        />

                        <Accordion
                            disableGutters
                            elevation={0}
                            sx={{
                                bgcolor: "transparent",
                                "&:before": { display: "none" },
                            }}
                        >
                            <AccordionSummary
                                expandIcon={<ExpandMore />}
                                data-testid="recipe-details-accordion"
                                sx={{ px: 0 }}
                            >
                                <Typography variant="subtitle2">
                                    {t("recipes.details")}
                                </Typography>
                            </AccordionSummary>
                            <AccordionDetails sx={{ px: 0 }}>
                                <Stack spacing={2}>
                                    <RecipeTagSelector
                                        householdId={householdId}
                                        recipe={recipe}
                                    />
                                    <RecipeSourcesStrip
                                        householdId={householdId}
                                        recipeId={recipeId}
                                    />
                                </Stack>
                            </AccordionDetails>
                        </Accordion>

                        <SortableSectionList
```

(`disableGutters` + `elevation={0}` + transparent bg + hidden `:before` make the accordion read as a flush section, not a competing card. MUI mounts `AccordionDetails` children even while collapsed — verified against MUI docs — so `RecipeTagSelector`'s tag-suggestion fetch still runs behind the fold.)

- [ ] **Step 4: Type-check and lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: both PASS. (`recipe`, `recipeId`, `householdId` are all in scope at the accordion render site; `recipes.details` typechecks via the `recipes: Record<string, string>` declaration.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(recipes): collapse tags + sources into a Details accordion"
```

---

### Task 4: Full verification gate

Static checks passed per task; now the regression net for the shared-component change and the visual confirmation.

**Files:** none (verification only).

- [ ] **Step 1: Build the SPA**

Run (from `ClientApp/`): `npm run build`
Expected: PASS (`tsc -b && vite build`). The new `recipe-details-accordion` testid lands in `ClientApp/build`, which the IT harness serves.

- [ ] **Step 2: Prettier check**

Run (from `ClientApp/`): `npm run prettier:check` (or `npm run prettier` to write, then re-stage)
Expected: clean. If it rewrites files, `git add` + amend the relevant commit.

- [ ] **Step 3: Full backend + integration suite (the shared-component regression net)**

Run (from repo root): `dotnet test Application/Frigorino.sln`
Expected: PASS. The recipe item, list item, and inventory item ITs exercise the preserved testids (`item-menu-button-*`, `edit-item-button`, `delete-item-button`, `recipe-item-*`) — green confirms the `dense` change and the content rewrite didn't break the shared rows. If a Reqnroll scenario fails in a full run but passes alone, re-run it in isolation before treating it as a regression (known shared-DB flakiness).

- [ ] **Step 4: Manual browser verify**

> **Execution note:** Confirm the UI-verification gate with the user first — they often quick-test the UI themselves. If agent-driven, bring up the stack via `/dev-up`, point Playwright MCP at the printed SPA URL, and navigate to a recipe's edit page (`/recipes/{id}/edit`).

Check:
- "Details" section is **collapsed by default**; expanding reveals the tag selector + sources strip; adding/changing a name still surfaces tag suggestions (fetch ran behind the fold).
- Ingredient rows are **dense single-line**: name + italic comment on the left, quantity pill right-aligned on the name's line, **hairline dividers** (no per-item card).
- The ⋮ menu still opens and **edits + deletes** a row; the editing pulse and processing pulse still show.
- Open a **List** and an **Inventory**: their item rows look **identical to before** (outlined cards, `mb` gap) — confirms `dense` stayed opt-in.

- [ ] **Step 5: Docker build (final drift check)**

Run (from repo root): `docker build -f Application/Dockerfile -t frigorino .`
Expected: PASS. (No project/Dockerfile changes here, but the standing rule is to build the image after a feature to catch SPA/Dockerfile drift before Railway does. Ask the user to start Docker Desktop if the daemon is unreachable.)

- [ ] **Step 6: Finishing the branch**

Use superpowers:finishing-a-development-branch to decide merge/PR. Then delete the IDEAS.md item ("Recipe edit polish: collapse extras + compact ingredient rows", lines ~26-38) as the finishing step, since its work has shipped.

---

## Self-Review

**Spec coverage:**
- (a) collapse extras → Task 3 ✓ (lift tag selector + accordion + i18n)
- (b) compact row content → Task 1 ✓
- (c) compact row chrome / `dense` → Task 2 ✓ (both gap sources: `containerSx mb` in `SortableListItem`, `.MuiListItem-root mb` in `SortableList`)
- (d) keep ⋮ menu → no task needed; explicitly untouched ✓
- Testids preserved → Global Constraints + Task 4 Step 3 ✓
- Manual verify + existing-IT regression → Task 4 ✓
- Out-of-scope items (menu-less tap-to-edit, hardcoded-German empty state) → not implemented, correctly absent ✓

**Placeholder scan:** none — every code step has complete code.

**Type consistency:** `dense?: boolean` named identically across `SortableListItemProps`, `SortableListProps`, and the `RecipeContainer` usage. `borderAccent: string` / `elevation: number` defined and consumed within the same Task 2 Step 1. `recipes.details` key matches `t("recipes.details")`.

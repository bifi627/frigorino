# Recipe Edit Page — "Recipe Sheet" Recompose Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recompose the recipe edit page from a flat stack of five equal-weight collapsible CRUD panels into a "recipe sheet" — an editable title/meta header, a slim combined "Sources & photos" strip, always-visible ingredient sections, and a section-aware composer that names and switches its target section.

**Architecture:** Pure frontend recompose of `features/recipes` — no backend, DTO, or API changes. The vertical-slice React components stay; their *composition* and *presentation* change. Item rows keep their existing `RecipeContainer`/`SortableList` rendering and the footer keeps the real `Composer`, so all item/composer testids are preserved. The accordion shells (`CollapsibleSection` for Details, `RecipeSectionCard` accordion for sections, `CollapsibleSection` for links/attachments) are replaced. A visual prototype already exists in-branch as the approved reference: `features/recipes/pages/RecipeEditPrototype.tsx` (throwaway, deleted in Phase 4).

**Tech Stack:** React 19, MUI v6 (dark theme in `src/theme.ts`), TanStack Query + Router, hey-api generated client. Tests: Reqnroll + Playwright + Postgres Testcontainers in `Frigorino.IntegrationTests` (there is **no** JS unit-test runner — per `CLAUDE.md`, frontend verification is `npm run tsc` + `npm run lint` + the Reqnroll IT + manual browser check).

## Global Constraints

- **No backend/API/DTO changes.** This is presentation-only. If you find yourself editing `Frigorino.Features` or `Frigorino.Domain`, stop — it's out of scope.
- **Preserve these testids exactly** (the IT depends on them; changing one without updating its step is a red-run-green-build trap — see `MEMORY` note "500/IT-failure diagnosability"):
  - Items/composer (DO NOT TOUCH the components that emit them): `recipe-items`, `item-menu-button-{text}`, `edit-item-button`, `recipe-item-{id}`, `recipe-item-quantity-{text}`, `recipe-item-comment-{id}`, `autocomplete-input-textfield`, `autocomplete-input-submit-button`, `composer-toggle-{id}`, `composer-panel-{id}`, `composer-quantity-value`, `composer-chip-{id}`.
  - Metadata: `recipe-description-input` (must stay on the description field AND keep debounced autosave + blur-flush → recipe PUT 200), `recipe-description` (view page — unaffected).
  - Attachments (Phase 3): `recipe-add-attachment`, `recipe-attachment-camera`, `recipe-attachment-photo`, `recipe-attachment-document`, `recipe-attachment-file-input`, `recipe-attachment-document-input`, `recipe-attachment-preview-sheet`, `recipe-attachment-document-preview`, `recipe-attachment-caption-input`, `recipe-attachment-send-button`, `recipe-attachment-caption-sheet`, `recipe-attachment-caption-edit-input`, `recipe-attachment-caption-save-button`, `recipe-attachment-{id}-edit`, `recipe-attachment-{id}-delete`, `recipe-attachment-row-{id}`, `recipe-attachment-{id}-caption`, `recipe-link-drag-handle-{id}`, `recipe-section-attachments-content` (re-homed onto the strip's photo container).
- **Styling rules** (`knowledge/Frontend_Styling.md`): theme owns `shape.borderRadius` (8) — no `borderRadius: 2` or manual `boxShadow`; use `sectionColors.recipes` + `tintedActionButtonSx` for identity; `featureContentPx`/`pageContainerSx` for insets. Coral (`sectionColors.recipes = "#D18A77"`) is the recipe identity accent, used sparingly.
- **i18n:** every user-facing string goes through `t(...)` (`CLAUDE.md` / styling guide). New keys go in `public/locales/en/translation.json` AND `de/translation.json`. Tests never assert translated text.
- **C# brace style:** block `{}` always, even single-line (`MEMORY` feedback). Applies to IT step edits.
- **Commits:** conventional, no Co-Authored-By trailers. Branch is `feat/recipe-edit-recompose` (off `stage`).
- **Per-phase gate:** after each phase, run the holistic review on **opus** (`MEMORY` feedback "opus for final holistic review") and pause for the human.

## File Structure

**Phase 1 — Header zone**
- Modify `Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx` — becomes the inline recipe-sheet header (editable title + servings stepper + description); keeps the single-PUT debounced save machinery. Renamed export stays `EditRecipeForm` to limit churn.
- Modify `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx` — drop the "Details" `CollapsibleSection` wrapper + its persisted-expand state; render `<EditRecipeForm>` inline at the top.

**Phase 2 — Ingredient sections + section-aware composer**
- Create `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeSectionGroup.tsx` — lightweight always-visible section (coral header with drag handle + editable name + hairline + ⋮ menu; description behind "rename"; items via existing `RecipeContainer`).
- Delete `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeSectionCard.tsx` (replaced).
- Modify `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeFooter.tsx` — add the section-target switcher (props: `sections`, `targetSectionId`, `onChangeTargetSection`).
- Modify `RecipeEditPage.tsx` — replace single-open `openSectionId` logic with a persisted `targetSectionId`; render all sections; composer always visible.

**Phase 3 — "Quellen & Fotos" strip (highest churn)**
- Create `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeSourcesStrip.tsx` — one compact strip combining links + photos; absorbs the wiring from the two section components below.
- Delete `Application/Frigorino.Web/ClientApp/src/features/recipes/links/components/RecipeLinksSection.tsx` and `Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentsSection.tsx` (logic moves into the strip; rows/sheets reused).
- Modify `RecipeEditPage.tsx` — render `<RecipeSourcesStrip>` in place of the two sections.
- Modify `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeAttachmentSteps.cs` — the "expand the attachments section" step (no accordion now).

**Phase 4 — Cleanup, IT reconciliation, docs, full verify**
- Delete `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPrototype.tsx` and `Application/Frigorino.Web/ClientApp/src/routes/recipes/prototype.tsx`.
- Modify `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature` — replace the obsolete "details section collapse persists" scenario.
- Modify `knowledge/Recipes.md` (frontend section) and, if patterns changed, `knowledge/Frontend_Styling.md`.

### Out of scope (deliberate — surface, don't build)
- **Item-row redesign.** The prototype showed "calmer" tap-to-edit rows with no per-row menu. Real items keep their `RecipeContainer`/`SortableList` rendering (drag handle + overflow menu with edit/delete) to preserve `item-menu-button-*`/`edit-item-button` and avoid touching the shared sortable. Revisit as a separate task if desired.
- **Multiple `recipe-items` containers.** With all sections visible, each section's `RecipeContainer` emits `data-testid="recipe-items"`. IT scenarios only ever have the single auto-seeded section, so `GetByTestId("recipe-items")` still resolves to one. If a future scenario adds a second section, those steps need scoping — note it, don't pre-solve.

---

## Phase 1 — Header zone

### Task 1: Inline recipe-sheet header (title + servings stepper + description)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx`
- Reference (read-only): `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPrototype.tsx` (approved layout), `Application/Frigorino.Web/ClientApp/src/theme.ts`

**Interfaces:**
- Consumes: `useUpdateRecipe()` (existing), `RecipeResponse` (existing — `{ id, name, description?, servings? }`).
- Produces: `EditRecipeForm({ householdId, recipe }: { householdId: number; recipe: RecipeResponse })` — unchanged prop shape, so `RecipeEditPage` keeps `<EditRecipeForm key={recipe.id} householdId={...} recipe={recipe} />`.

- [ ] **Step 1: Rewrite `EditRecipeForm.tsx`**

Keep the existing save machinery verbatim (the `latest` ref, `save`/`scheduleSave`/`flushSave`, `SAVE_DEBOUNCE_MS`, validation, `status`) — only the **rendered controls** change: name becomes a borderless heading field, servings becomes a stepper, description becomes a borderless multiline field. The single PUT still sends `{ name, description, servings }` together, so the stepper reuses the same debounced save.

```tsx
import { Add, Remove, Restaurant } from "@mui/icons-material";
import { Box, IconButton, Stack, TextField, Typography } from "@mui/material";
import {
    useCallback,
    useEffect,
    useLayoutEffect,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";
import { sectionColors } from "../../../theme";
import { useUpdateRecipe } from "../useUpdateRecipe";

const SAVE_DEBOUNCE_MS = 600;
const coral = sectionColors.recipes;

interface EditRecipeFormProps {
    householdId: number;
    recipe: RecipeResponse;
}

export const EditRecipeForm = ({ householdId, recipe }: EditRecipeFormProps) => {
    const { t } = useTranslation();
    const updateRecipeMutation = useUpdateRecipe();

    // Seeded once on mount; parent keys this by recipe.id so switching recipes remounts.
    const [editedName, setEditedName] = useState(recipe.name || "");
    const [editedDescription, setEditedDescription] = useState(
        recipe.description ?? "",
    );
    const [editedServings, setEditedServings] = useState<number | null>(
        recipe.servings ?? null,
    );
    const [dirty, setDirty] = useState(false);

    const nameInvalid = editedName.trim().length === 0;
    const servingsInvalid =
        editedServings !== null &&
        (!Number.isInteger(editedServings) ||
            editedServings < 1 ||
            editedServings > 99);

    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const latest = useRef({
        name: editedName,
        description: editedDescription,
        servings: editedServings,
        nameInvalid,
        servingsInvalid,
        dirty,
    });
    useLayoutEffect(() => {
        latest.current = {
            name: editedName,
            description: editedDescription,
            servings: editedServings,
            nameInvalid,
            servingsInvalid,
            dirty,
        };
    });

    const { mutate } = updateRecipeMutation;
    const recipeId = recipe.id;

    const save = useCallback(() => {
        if (!recipeId) return;
        const cur = latest.current;
        if (cur.nameInvalid || cur.servingsInvalid) return;
        if (!cur.dirty) return;
        mutate(
            {
                path: { householdId, recipeId },
                body: {
                    name: cur.name.trim(),
                    description: cur.description.trim() || null,
                    servings: cur.servings,
                },
            },
            { onSuccess: () => setDirty(false) },
        );
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

    // Stepper: clamp 1..99; null (unset) starts at 4 on first increment, hides nothing.
    const stepServings = useCallback(
        (delta: number) => {
            setEditedServings((prev) => {
                const base = prev ?? (delta > 0 ? 0 : 1);
                const next = Math.min(99, Math.max(1, base + delta));
                return next;
            });
            setDirty(true);
            scheduleSave();
        },
        [scheduleSave],
    );

    let status: "saving" | "saved" | "idle" = "idle";
    if (updateRecipeMutation.isPending) {
        status = "saving";
    } else if (!dirty && updateRecipeMutation.isSuccess) {
        status = "saved";
    }

    return (
        <Box>
            <TextField
                variant="standard"
                value={editedName}
                onChange={(e) => {
                    setEditedName(e.target.value);
                    setDirty(true);
                    scheduleSave();
                }}
                onBlur={flushSave}
                fullWidth
                required
                error={nameInvalid}
                helperText={nameInvalid ? t("recipes.recipeNameRequired") : ""}
                placeholder={t("recipes.recipeName")}
                slotProps={{
                    input: {
                        sx: {
                            fontSize: "1.7rem",
                            fontWeight: 700,
                            lineHeight: 1.2,
                        },
                    },
                    htmlInput: { "data-testid": "recipe-name-input" },
                }}
            />

            <Stack
                direction="row"
                spacing={0.5}
                sx={{
                    alignItems: "center",
                    border: 1,
                    borderColor: servingsInvalid ? "error.main" : "divider",
                    borderRadius: 999,
                    pl: 1.25,
                    pr: 0.25,
                    py: 0.25,
                    mt: 1.5,
                    width: "fit-content",
                }}
                data-testid="recipe-servings-stepper"
            >
                <Restaurant fontSize="small" sx={{ color: coral }} />
                <Typography
                    variant="body2"
                    sx={{ fontWeight: 700, minWidth: 16, textAlign: "center" }}
                    data-testid="recipe-servings-value"
                >
                    {editedServings ?? "–"}
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ mr: 0.5 }}>
                    {t("recipes.servings")}
                </Typography>
                <IconButton
                    size="small"
                    onClick={() => stepServings(-1)}
                    disabled={editedServings !== null && editedServings <= 1}
                    data-testid="recipe-servings-decrement"
                >
                    <Remove fontSize="small" />
                </IconButton>
                <IconButton
                    size="small"
                    onClick={() => stepServings(1)}
                    disabled={editedServings !== null && editedServings >= 99}
                    data-testid="recipe-servings-increment"
                >
                    <Add fontSize="small" />
                </IconButton>
            </Stack>

            <TextField
                variant="standard"
                value={editedDescription}
                onChange={(e) => {
                    setEditedDescription(e.target.value);
                    setDirty(true);
                    scheduleSave();
                }}
                onBlur={flushSave}
                fullWidth
                multiline
                minRows={2}
                placeholder={t("recipes.descriptionPlaceholder")}
                sx={{ mt: 1.5 }}
                slotProps={{
                    input: {
                        sx: {
                            fontSize: "0.875rem",
                            fontStyle: "italic",
                            color: "text.secondary",
                        },
                    },
                    htmlInput: {
                        maxLength: 1000,
                        "data-testid": "recipe-description-input",
                    },
                }}
            />

            <Box
                data-testid="recipe-metadata-status"
                data-status={status}
                sx={{ minHeight: 20, mt: 0.5 }}
            >
                <Typography variant="caption" color="text.secondary">
                    {status === "saving"
                        ? t("common.saving")
                        : status === "saved"
                          ? t("common.saved")
                          : ""}
                </Typography>
            </Box>
        </Box>
    );
};
```

- [ ] **Step 2: Verify types + lint**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc && npm run lint`
Expected: both exit 0, no errors referencing `EditRecipeForm.tsx`. (Capture `${PIPESTATUS[0]}` if piping — `MEMORY` "Exit code not tail pipe".)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/EditRecipeForm.tsx
git commit -m "refactor(recipes): inline editable header (title + servings stepper + description)"
```

---

### Task 2: Drop the Details accordion from the edit page

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx`

**Interfaces:**
- Consumes: `EditRecipeForm` (Task 1).
- Produces: no exported-symbol change.

- [ ] **Step 1: Remove the Details `CollapsibleSection` + its persisted state**

In `RecipeEditPage.tsx`:
1. Delete the `detailsExpanded` state line:
```tsx
const [detailsExpanded, setDetailsExpanded] = usePersistedExpanded(
    "recipe-edit-section:details",
    true,
);
```
2. Replace the `<CollapsibleSection title={t("recipes.detailsSection")} ...>` wrapper around `<EditRecipeForm>` with the bare form:
```tsx
<EditRecipeForm
    key={recipe.id}
    householdId={householdId}
    recipe={recipe}
/>
```
3. Remove the now-unused `CollapsibleSection` import if nothing else on the page uses it (the links/attachments sections still import their own copy, so this page-level import is removable). Remove the `usePersistedExpanded` import if no longer used on this page.

- [ ] **Step 2: Verify types + lint**

Run: `npm run tsc && npm run lint`
Expected: 0 errors. (Catches dangling imports / the deleted `detailsExpanded`.)

- [ ] **Step 3: Build the SPA so the IT harness serves the change**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run build`
Expected: exits 0, writes `ClientApp/build`. (`MEMORY` "IT serves ClientApp/build" — the harness serves the built bundle, not live source.)

- [ ] **Step 4: Manual browser check**

With the dev stack up (`scripts/dev-up.ps1`), open `/recipes/{id}/edit`: the title is the editable recipe name, the servings stepper and description sit beneath it, no "Details" box. Editing the name/description still shows "Saving…/Saved".

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx
git commit -m "refactor(recipes): drop Details accordion, header now inline"
```

> **Note for Phase 4:** the IT scenario "Recipe edit section collapse state persists across reload" collapses the `details` section — now gone. It is updated in Task 9. The IT will fail until then; that's expected and reconciled at phase end.

**PHASE 1 GATE:** Run holistic review on opus. Pause for human.

---

## Phase 2 — Ingredient sections + section-aware composer

### Task 3: Lightweight `RecipeSectionGroup` (replaces the section accordion)

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeSectionGroup.tsx`
- Reference: `RecipeSectionCard.tsx` (current — for the save machinery + props it must keep), `RecipeEditPrototype.tsx` (`SectionHeader` layout)

**Interfaces:**
- Consumes: `useUpdateRecipeSection()`, `RecipeContainer`, `RecipeItemResponse`, `RecipeSectionResponse`, `sectionColors`.
- Produces:
```ts
interface RecipeSectionGroupProps {
    householdId: number;
    recipeId: number;
    section: RecipeSectionResponse;          // { id, name?, description?, rank }
    canDelete: boolean;
    onDelete: () => void;
    editingItem: RecipeItemResponse | null;
    onEditItem: (item: RecipeItemResponse) => void;
    isExtracting?: boolean;
    extractingItemId?: number | null;
    dragHandle: ReactNode;
}
export const RecipeSectionGroup: (p: RecipeSectionGroupProps) => JSX.Element;
```
Note the removed props vs `RecipeSectionCard`: no `expanded` / `onToggle` (sections are always open now).

- [ ] **Step 1: Create `RecipeSectionGroup.tsx`**

The section header shows the name (or the default "Ingredients" heading) as a coral small-caps label with a trailing hairline, the drag handle, and a ⋮ menu with **Rename** (reveals the name/description fields) and **Delete**. Name/description fields are hidden until "Rename" is tapped or the section already has a name — this kills the "two empty fields above every section" clutter. Save machinery (debounced + blur flush) is lifted verbatim from `RecipeSectionCard`.

```tsx
import { Delete, DriveFileRenameOutline, MoreVert } from "@mui/icons-material";
import {
    Box,
    Collapse,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import type { ReactNode } from "react";
import {
    useCallback,
    useEffect,
    useLayoutEffect,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import type {
    RecipeItemResponse,
    RecipeSectionResponse,
} from "../../../../lib/api";
import { sectionColors } from "../../../../theme";
import { useUpdateRecipeSection } from "../../sections/useUpdateRecipeSection";
import { RecipeContainer } from "./RecipeContainer";

const SAVE_DEBOUNCE_MS = 600;
const coral = sectionColors.recipes;

interface RecipeSectionGroupProps {
    householdId: number;
    recipeId: number;
    section: RecipeSectionResponse;
    canDelete: boolean;
    onDelete: () => void;
    editingItem: RecipeItemResponse | null;
    onEditItem: (item: RecipeItemResponse) => void;
    isExtracting?: boolean;
    extractingItemId?: number | null;
    dragHandle: ReactNode;
}

export const RecipeSectionGroup = ({
    householdId,
    recipeId,
    section,
    canDelete,
    onDelete,
    editingItem,
    onEditItem,
    isExtracting,
    extractingItemId,
    dragHandle,
}: RecipeSectionGroupProps) => {
    const { t } = useTranslation();
    const updateSection = useUpdateRecipeSection();

    const [name, setName] = useState(section.name ?? "");
    const [description, setDescription] = useState(section.description ?? "");
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    // Fields are revealed when the section already carries a name/description, or on "Rename".
    const [renaming, setRenaming] = useState(
        Boolean(section.name?.trim() || section.description?.trim()),
    );

    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const latest = useRef({ name, description });
    useLayoutEffect(() => {
        latest.current = { name, description };
    });

    const { mutate } = updateSection;
    const save = useCallback(() => {
        mutate({
            path: { householdId, recipeId, sectionId: section.id },
            body: {
                name: latest.current.name.trim() || null,
                description: latest.current.description.trim() || null,
            },
        });
    }, [mutate, householdId, recipeId, section.id]);

    const scheduleSave = useCallback(() => {
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(save, SAVE_DEBOUNCE_MS);
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

    const displayName = section.name?.trim() || t("recipes.ingredientsHeading");

    return (
        <Box data-testid={`recipe-section-${section.id}`}>
            <Stack
                direction="row"
                spacing={1}
                sx={{ alignItems: "center", mt: 1, mb: 0.5 }}
            >
                {dragHandle}
                <Typography
                    variant="subtitle2"
                    sx={{
                        fontWeight: 700,
                        color: coral,
                        letterSpacing: 0.8,
                        textTransform: "uppercase",
                        fontSize: "0.72rem",
                    }}
                >
                    {displayName}
                </Typography>
                <Box sx={{ flex: 1, height: "1px", bgcolor: "divider" }} />
                <IconButton
                    size="small"
                    sx={{ opacity: 0.6 }}
                    onClick={(e) => setMenuAnchor(e.currentTarget)}
                    data-testid={`recipe-section-${section.id}-menu`}
                >
                    <MoreVert fontSize="small" />
                </IconButton>
            </Stack>

            <Collapse in={renaming}>
                <Stack spacing={2} sx={{ px: 0.5, pb: 1 }}>
                    <TextField
                        label={t("recipes.sectionName")}
                        value={name}
                        onChange={(e) => {
                            setName(e.target.value);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        size="small"
                        fullWidth
                        placeholder={t("recipes.sectionNamePlaceholder")}
                        slotProps={{
                            htmlInput: {
                                maxLength: 100,
                                "data-testid": `recipe-section-${section.id}-name-input`,
                            },
                        }}
                    />
                    <TextField
                        label={t("recipes.sectionDescription")}
                        value={description}
                        onChange={(e) => {
                            setDescription(e.target.value);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        size="small"
                        fullWidth
                        multiline
                        minRows={2}
                        placeholder={t("recipes.sectionDescriptionPlaceholder")}
                        slotProps={{
                            htmlInput: {
                                maxLength: 2000,
                                "data-testid": `recipe-section-${section.id}-description-input`,
                            },
                        }}
                    />
                </Stack>
            </Collapse>

            <RecipeContainer
                householdId={householdId}
                recipeId={recipeId}
                sectionId={section.id}
                editingItem={editingItem}
                onEdit={onEditItem}
                isExtracting={isExtracting}
                extractingItemId={extractingItemId}
                scrollable={false}
            />

            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={() => setMenuAnchor(null)}
            >
                <MenuItem
                    onClick={() => {
                        setMenuAnchor(null);
                        setRenaming(true);
                    }}
                    data-testid={`recipe-section-${section.id}-rename`}
                >
                    <ListItemIcon>
                        <DriveFileRenameOutline fontSize="small" />
                    </ListItemIcon>
                    <ListItemText>{t("recipes.renameSection")}</ListItemText>
                </MenuItem>
                <MenuItem
                    disabled={!canDelete}
                    onClick={() => {
                        setMenuAnchor(null);
                        onDelete();
                    }}
                    data-testid={`recipe-section-${section.id}-delete`}
                >
                    <ListItemIcon>
                        <Delete
                            fontSize="small"
                            color={canDelete ? "error" : "disabled"}
                        />
                    </ListItemIcon>
                    <ListItemText>{t("recipes.deleteSection")}</ListItemText>
                </MenuItem>
            </Menu>
        </Box>
    );
};
```

- [ ] **Step 2: Add the `renameSection` i18n key**

In `public/locales/en/translation.json` add to the `recipes` object: `"renameSection": "Rename section"`. In `public/locales/de/translation.json`: `"renameSection": "Abschnitt umbenennen"`. (All other keys used here already exist — verify with: `grep -n "ingredientsHeading\|sectionName\|sectionDescription\|deleteSection" public/locales/en/translation.json`.)

- [ ] **Step 3: Verify types + lint**

Run: `npm run tsc && npm run lint`
Expected: 0 errors. (`tsc` confirms `RecipeContainer`'s prop shape matches.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeSectionGroup.tsx Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(recipes): lightweight always-visible section group"
```

---

### Task 4: Section-aware composer footer

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeFooter.tsx`

**Interfaces:**
- Consumes: `RecipeSectionResponse[]`, the existing `Composer` (untouched — preserves `autocomplete-input-*`).
- Produces (added props):
```ts
interface RecipeFooterProps {
    // ...existing...
    sections: RecipeSectionResponse[];
    targetSectionId: number;
    onChangeTargetSection: (sectionId: number) => void;
}
```

- [ ] **Step 1: Add the target switcher above the `Composer`**

Insert a coral switcher chip + section menu inside the footer `Container`, above the `<Composer>`. The chip shows the target section's name (or the default heading); tapping opens a `Menu` of sections. When there is only one section, render it as a static label (no menu) so it doesn't imply a choice. Add these imports: `ExpandMore` from `@mui/icons-material`; `Button, Menu, MenuItem` from `@mui/material`; `useState` from `react`; `sectionColors, tintedActionButtonSx` from `../../../../theme`; `RecipeSectionResponse` from `../../../../lib/api`.

Replace the component body's `return (...)` with:

```tsx
        const targetSection = existingSectionsTarget(sections, targetSectionId);
        const targetLabel =
            targetSection?.name?.trim() || t("recipes.ingredientsHeading");
        const canSwitch = sections.length > 1;

        return (
            <Container
                maxWidth="sm"
                data-testid="recipe-composer-footer"
                sx={{
                    flexShrink: 0,
                    px: featureContentPx,
                    py: 1,
                    borderTop: 1,
                    borderColor: "divider",
                    bgcolor: "background.paper",
                }}
            >
                {!editingItem && (
                    <Box sx={{ mb: 0.5 }}>
                        <Button
                            size="small"
                            endIcon={canSwitch ? <ExpandMore /> : undefined}
                            onClick={(e) =>
                                canSwitch && setMenuAnchor(e.currentTarget)
                            }
                            disabled={!canSwitch}
                            data-testid="recipe-composer-target"
                            sx={{
                                ...tintedActionButtonSx(sectionColors.recipes),
                                borderRadius: 999,
                                textTransform: "none",
                                py: 0.25,
                                px: 1,
                                minWidth: 0,
                                // a disabled tinted button still needs to read as a label
                                "&.Mui-disabled": {
                                    color: sectionColors.recipes,
                                    opacity: 0.9,
                                },
                            }}
                        >
                            {t("recipes.addingTo", { section: targetLabel })}
                        </Button>
                        <Menu
                            anchorEl={menuAnchor}
                            open={Boolean(menuAnchor)}
                            onClose={() => setMenuAnchor(null)}
                        >
                            {sections.map((s) => (
                                <MenuItem
                                    key={s.id}
                                    selected={s.id === targetSectionId}
                                    onClick={() => {
                                        onChangeTargetSection(s.id);
                                        setMenuAnchor(null);
                                    }}
                                    data-testid={`recipe-composer-target-${s.id}`}
                                >
                                    {s.name?.trim() ||
                                        t("recipes.ingredientsHeading")}
                                </MenuItem>
                            ))}
                        </Menu>
                    </Box>
                )}

                <Composer
                    key={editingItem?.id ?? "new"}
                    features={features}
                    disabled={isLoading}
                    editing={{
                        active: Boolean(editingItem),
                        onCancel: onCancelEdit,
                    }}
                    initialDraft={initialDraft}
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
```

Add the menu-anchor state near the top of the component body (after the existing hooks): `const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);`. Add a small module-level helper above the component:
```tsx
const existingSectionsTarget = (
    sections: RecipeSectionResponse[],
    targetSectionId: number,
) => sections.find((s) => s.id === targetSectionId) ?? sections[0];
```
Extend the props destructure + interface with `sections`, `targetSectionId`, `onChangeTargetSection`. Keep `memo` — the new props are stable from the parent.

- [ ] **Step 2: Add i18n key `addingTo`**

`en`: `"addingTo": "Adding to {{section}}"`. `de`: `"addingTo": "Hinzufügen zu {{section}}"`.

- [ ] **Step 3: Verify types + lint**

Run: `npm run tsc && npm run lint`
Expected: 0 errors. (`tsc` flags any missing prop at the call site — wired in Task 5.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeFooter.tsx Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(recipes): section-aware composer target switcher"
```

---

### Task 5: Wire sections + composer into the edit page (drop single-open accordion)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx`

**Interfaces:**
- Consumes: `RecipeSectionGroup` (Task 3), updated `RecipeFooter` (Task 4).
- Produces: no exported-symbol change.

- [ ] **Step 1: Replace `openSectionId` single-open logic with a persisted `targetSectionId`**

1. Remove the `SECTIONS_UNTOUCHED`/`SECTIONS_ALL_COLLAPSED` sentinels and `openSectionId` block. Replace with:
```tsx
// The composer's target section (where new items land). Persisted; falls back to the first
// section when unset or stale (another recipe / a deleted section).
const [targetSectionRaw, setTargetSectionRaw] = usePersistedNumber(
    "recipe-edit:target-section",
    0,
);
const targetSectionId =
    sections.find((s) => s.id === targetSectionRaw)?.id ?? sections[0]?.id ?? 0;
const targetSectionItems = items.filter((i) => i.sectionId === targetSectionId);
```
2. Delete `effectiveOpenSectionId`, `composerVisible`, `openSectionItems`, `handleToggleSection`.
3. `handleAddItem` now targets `targetSectionId`:
```tsx
const handleAddItem = useCallback(
    async (text: string, comment: string | null) => {
        if (!householdId || !targetSectionId) return;
        try {
            const created = await createMutation.mutateAsync({
                path: { householdId, recipeId },
                body: { sectionId: targetSectionId, text, comment },
            });
            setPendingExtraction({
                id: created.id,
                extractionPending: created.extractionPending,
            });
        } catch {
            // createMutation.onError rolls back the optimistic item.
        }
    },
    [createMutation, householdId, recipeId, targetSectionId],
);
```
4. `handleAddSection` sets the new section as the target:
```tsx
const handleAddSection = useCallback(async () => {
    if (!householdId) return;
    const created = await createSection.mutateAsync({
        path: { householdId, recipeId },
        body: { name: null, description: null },
    });
    setTargetSectionRaw(created.id);
}, [createSection, householdId, recipeId, setTargetSectionRaw]);
```

- [ ] **Step 2: Render all sections via `RecipeSectionGroup` and an always-visible footer**

Replace the `SortableSectionList` `renderSection` to use `RecipeSectionGroup` (no `expanded`/`onToggle`):
```tsx
<SortableSectionList
    sections={sections}
    onReorder={async (sectionId, afterId) => {
        await reorderSection.mutateAsync({
            path: { householdId, recipeId, sectionId },
            body: { afterId },
        });
    }}
    renderSection={(section, dragHandle) => (
        <RecipeSectionGroup
            householdId={householdId}
            recipeId={recipeId}
            section={section}
            canDelete={sections.length > 1}
            onDelete={() =>
                deleteSection.mutate({
                    path: { householdId, recipeId, sectionId: section.id },
                })
            }
            editingItem={editingItem}
            onEditItem={setEditingItem}
            isExtracting={isExtracting}
            extractingItemId={extractingItemId}
            dragHandle={dragHandle}
        />
    )}
/>
```
Replace the conditional footer (`composerVisible ? ...`) with an always-rendered footer (a recipe always has ≥1 section):
```tsx
<RecipeFooter
    editingItem={editingItem}
    existingItems={targetSectionItems}
    sections={sections}
    targetSectionId={targetSectionId}
    onChangeTargetSection={setTargetSectionRaw}
    onAddItem={handleAddItem}
    onUpdateItem={handleUpdateItem}
    onCancelEdit={() => setEditingItem(null)}
    isLoading={createMutation.isPending || updateMutation.isPending}
    onScrollToLast={scrollToLastItem}
/>
```
Update imports: replace `RecipeSectionCard` import with `RecipeSectionGroup`; drop `CollapsibleSection` (removed in Task 2 already).

- [ ] **Step 3: Verify types + lint, then build**

Run: `npm run tsc && npm run lint && npm run build`
Expected: all exit 0. (`tsc` confirms every removed symbol is fully gone and `RecipeFooter`'s new required props are supplied.)

- [ ] **Step 4: Run the recipe item IT (regression — items still add/edit)**

Run (from repo root): `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Recipes"`
Expected: the item-add and quantity-edit scenarios pass (they exercise `autocomplete-input-*` + `item-menu-button-*` + `edit-item-button`, all preserved). The "details section collapse" scenario FAILS (reconciled in Task 9). Confirm only that one fails. (Reqnroll FQN filter matches the *title*, not the file — `MEMORY` "Reqnroll FQN filter".)

- [ ] **Step 5: Manual browser check**

Open an edit page with ≥2 sections (add one): all sections show at once; the footer reads "Adding to {section}" and the ▾ switches target; adding an item lands in the chosen section; section drag reorders.

- [ ] **Step 6: Delete the old `RecipeSectionCard.tsx` and commit**

```bash
git rm Application/Frigorino.Web/ClientApp/src/features/recipes/items/components/RecipeSectionCard.tsx
git add Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx
git commit -m "feat(recipes): all sections visible + section-aware composer wiring"
```

**PHASE 2 GATE:** Run holistic review on opus. Pause for human.

---

## Phase 3 — "Quellen & Fotos" strip

> Highest-churn phase: it folds two full-width accordion sections into one strip and touches the attachment IT. Keep every attachment testid from Global Constraints; the only IT step that must change is "expand the attachments section" (there is no accordion to expand).

### Task 6: `RecipeSourcesStrip` — combined links + photos

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeSourcesStrip.tsx`
- Reference: `links/components/RecipeLinksSection.tsx`, `attachments/components/RecipeAttachmentsSection.tsx`, `attachments/components/RecipeAttachmentRow.tsx`, `RecipeEditPrototype.tsx` (strip layout).

**Interfaces:**
- Consumes: `useRecipeLinks/useCreateRecipeLink/useDeleteRecipeLink/useReorderRecipeLink`, `useRecipeAttachments/useCreateRecipeAttachment/useUpdateRecipeAttachment/useDeleteRecipeAttachment/useReorderRecipeAttachment`, `RecipeAttachmentPreviewSheet`, `RecipeAttachmentCaptionSheet`, `SortableLinkList`, `useAttachmentImage`.
- Produces: `RecipeSourcesStrip({ householdId, recipeId }: { householdId: number; recipeId: number })`.

- [ ] **Step 1: Create `RecipeSourcesStrip.tsx`**

This component keeps the attachment add-menu (Popper + camera/photo/document), the two hidden inputs, the preview sheet, and the caption sheet **verbatim** from `RecipeAttachmentsSection` (so `recipe-add-attachment`, `recipe-attachment-camera/photo/document`, `recipe-attachment-file-input`, `recipe-attachment-document-input`, the sheets and their inputs are unchanged). It changes only the *layout*: a single "Quellen & Fotos" header row with a pinned add control, then a horizontally-scrollable row of link chips + photo tiles. The photo tiles wrap in a container with `data-testid="recipe-section-attachments-content"` (re-homing the testid the IT's "list shows / first attachment" assertions need), and each tile keeps the per-attachment testids via a small inline tile that reuses `useAttachmentImage`.

Because this file is large, build it by **copying `RecipeAttachmentsSection.tsx` to the new path**, then:
1. Drop the `CollapsibleSection` wrapper + `usePersistedExpanded` — return a plain `<Box>`.
2. Add the links wiring (lift the `isHttpUrl`, draft state, and `SortableLinkList` block from `RecipeLinksSection.tsx`), rendered as a chip row.
3. Replace `SortableLinkList` of `RecipeAttachmentRow` (vertical rows) with a horizontal tile row, but keep a `recipe-link-drag-handle-{id}` on each tile (the IT drag step targets it) and keep `recipe-attachment-row-{id}` / `recipe-attachment-{id}-edit` / `recipe-attachment-{id}-delete` / `recipe-attachment-{id}-caption` on each tile.

Concretely, the photo tile (replaces `RecipeAttachmentRow`'s row) — note `SortableLinkList` already supplies the `dragHandle` carrying `recipe-link-drag-handle-{id}`:

```tsx
const PhotoTile = ({
    householdId,
    recipeId,
    attachment,
    onEdit,
    onDelete,
    dragHandle,
}: {
    householdId: number;
    recipeId: number;
    attachment: RecipeAttachmentResponse;
    onEdit: () => void;
    onDelete: () => void;
    dragHandle: ReactNode;
}) => {
    const isDocument = attachment.type === "Document";
    const { data: url, isLoading, isError } = useAttachmentImage(
        householdId,
        recipeId,
        attachment.id,
        "thumbnail",
        !isDocument,
    );
    return (
        <Box
            data-testid={`recipe-attachment-row-${attachment.id}`}
            sx={{ position: "relative", flexShrink: 0 }}
        >
            <Box sx={{ position: "absolute", top: -6, left: -6, zIndex: 1 }}>
                {dragHandle}
            </Box>
            <Box
                role="button"
                tabIndex={0}
                onClick={onEdit}
                onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        onEdit();
                    }
                }}
                data-testid={`recipe-attachment-${attachment.id}-edit`}
                sx={{
                    width: 64,
                    height: 64,
                    borderRadius: 1.5,
                    overflow: "hidden",
                    bgcolor: "action.hover",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    cursor: "pointer",
                }}
            >
                {isDocument ? (
                    <Description color="action" />
                ) : isLoading ? (
                    <Skeleton variant="rectangular" width={64} height={64} />
                ) : isError || !url ? (
                    <BrokenImage fontSize="small" color="disabled" />
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={attachment.caption ?? ""}
                        sx={{ width: "100%", height: "100%", objectFit: "cover" }}
                    />
                )}
            </Box>
            {/* Caption testid the IT filters on; visually a tooltip-style label under the tile. */}
            <Typography
                variant="caption"
                data-testid={`recipe-attachment-${attachment.id}-caption`}
                sx={{
                    display: "block",
                    maxWidth: 64,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    color: attachment.caption ? "text.secondary" : "transparent",
                }}
            >
                {attachment.caption ||
                    (isDocument ? attachment.originalFileName : "·")}
            </Typography>
            <IconButton
                size="small"
                onClick={onDelete}
                data-testid={`recipe-attachment-${attachment.id}-delete`}
                sx={{ position: "absolute", top: -6, right: -6, zIndex: 1 }}
            >
                <Delete fontSize="small" color="error" />
            </IconButton>
        </Box>
    );
};
```

The strip body (header + chips + tiles), with the add control pinned in the header row (Q2 fix) and the photo container carrying the re-homed content testid:

```tsx
return (
    <Box>
        <Stack
            direction="row"
            sx={{ alignItems: "center", justifyContent: "space-between" }}
        >
            <Typography
                variant="overline"
                color="text.secondary"
                sx={{ fontWeight: 700, letterSpacing: 1 }}
            >
                {t("recipes.sourcesAndPhotos")}
            </Typography>
            <Stack direction="row" spacing={0.5}>
                <IconButton
                    size="small"
                    onClick={() => setDraftOpen(true)}
                    data-testid="recipe-add-link"
                    sx={tintedActionButtonSx(neutralActionColor)}
                >
                    <LinkIcon fontSize="small" />
                </IconButton>
                <IconButton
                    size="small"
                    onClick={(e) =>
                        setMenuAnchor(menuAnchor ? null : e.currentTarget)
                    }
                    disabled={createAttachment.isPending}
                    aria-haspopup="true"
                    data-testid="recipe-add-attachment"
                    sx={tintedActionButtonSx(neutralActionColor)}
                >
                    <Add fontSize="small" />
                </IconButton>
            </Stack>
        </Stack>

        {/* link draft form (lifted from RecipeLinksSection, unchanged testids) renders here when draftOpen */}

        <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: "center", overflowX: "auto", pt: 0.5, pb: 0.5 }}
        >
            <SortableLinkList
                links={links}
                onReorder={async (linkId, afterId) => {
                    await reorderLink.mutateAsync({
                        path: { householdId, recipeId, linkId },
                        body: { afterId },
                    });
                }}
                renderLink={(link, dragHandle) => (
                    <LinkChip
                        householdId={householdId}
                        recipeId={recipeId}
                        link={link}
                        onDelete={() =>
                            deleteLink.mutate({
                                path: { householdId, recipeId, linkId: link.id },
                            })
                        }
                        dragHandle={dragHandle}
                    />
                )}
            />
        </Stack>

        <Box
            data-testid="recipe-section-attachments-content"
            sx={{ display: "flex", gap: 1.5, overflowX: "auto", pt: 1 }}
        >
            <SortableLinkList
                links={attachments}
                onReorder={async (attachmentId, afterId) => {
                    await reorderAttachment.mutateAsync({
                        path: { householdId, recipeId, attachmentId },
                        body: { afterId },
                    });
                }}
                renderLink={(attachment, dragHandle) => (
                    <PhotoTile
                        householdId={householdId}
                        recipeId={recipeId}
                        attachment={attachment}
                        onEdit={() => setEditingAttachment(attachment)}
                        onDelete={() =>
                            deleteAttachment.mutate({
                                path: {
                                    householdId,
                                    recipeId,
                                    attachmentId: attachment.id,
                                },
                            })
                        }
                        dragHandle={dragHandle}
                    />
                )}
            />
        </Box>

        {/* uploadError Alert, Popper add-menu, hidden inputs, PreviewSheet, CaptionSheet:
            keep verbatim from RecipeAttachmentsSection — all testids unchanged. */}
    </Box>
);
```

`LinkChip` is a small chip variant of `RecipeLinkRow` showing `link.label || link.url` as an outlined `Chip` with an `onDelete`; keep `recipe-link-{id}-*` editing in the existing draft/edit flow (a tap opens the same inline edit you lift from `RecipeLinkRow` — or, to minimize churn, keep links read-as-chip + delete here and leave label/URL editing to the draft re-add; links have no UI IT, so either is safe — pick the chip+delete minimum unless you want inline link editing).

- [ ] **Step 2: Add i18n key `sourcesAndPhotos`**

`en`: `"sourcesAndPhotos": "Sources & photos"`. `de`: `"sourcesAndPhotos": "Quellen & Fotos"`.

- [ ] **Step 3: Verify types + lint**

Run: `npm run tsc && npm run lint`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/components/RecipeSourcesStrip.tsx Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(recipes): combined sources & photos strip"
```

---

### Task 7: Swap the strip into the edit page; delete the two section components

**Files:**
- Modify: `RecipeEditPage.tsx`
- Delete: `links/components/RecipeLinksSection.tsx`, `attachments/components/RecipeAttachmentsSection.tsx`

- [ ] **Step 1: Replace the two sections with the strip**

In `RecipeEditPage.tsx`, replace:
```tsx
<RecipeLinksSection householdId={householdId} recipeId={recipeId} />
<RecipeAttachmentsSection householdId={householdId} recipeId={recipeId} />
```
with:
```tsx
<RecipeSourcesStrip householdId={householdId} recipeId={recipeId} />
```
Update imports accordingly.

- [ ] **Step 2: Confirm no other importers, then delete the old sections**

Run: `grep -rn "RecipeLinksSection\|RecipeAttachmentsSection" Application/Frigorino.Web/ClientApp/src` — expect zero hits after the import swap.
```bash
git rm Application/Frigorino.Web/ClientApp/src/features/recipes/links/components/RecipeLinksSection.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentsSection.tsx
```
(`RecipeAttachmentRow.tsx` and `RecipeLinkRow.tsx` may now be unreferenced — `grep` for them; `git rm` any with zero hits, per `MEMORY` "Remove dead code when found".)

- [ ] **Step 3: Verify + build**

Run: `npm run tsc && npm run lint && npm run build`
Expected: all exit 0.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor(recipes): replace links/attachments accordions with strip"
```

---

### Task 8: Reconcile the attachment IT "expand" step

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeAttachmentSteps.cs`

- [ ] **Step 1: Replace the accordion-expand step with a strip-ready wait**

The strip has no `recipe-section-attachments-summary` accordion. Change `WhenIExpandTheAttachmentsSection` to wait for the strip's add affordance instead (block braces per house style):

```csharp
[When("I expand the attachments section")]
public async Task WhenIExpandTheAttachmentsSection()
{
    // The attachments now live in an always-visible "Sources & photos" strip — there is no
    // accordion to expand. Just wait for the add-attachment control to be ready.
    await Assertions.Expect(ctx.Page.GetByTestId("recipe-add-attachment")).ToBeVisibleAsync();
}
```

- [ ] **Step 2: Run the attachment IT**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~RecipeAttachments"`
Expected: all 5 attachment scenarios pass (upload image/doc, edit caption, reorder via drag, delete). If reorder fails with a 30s timeout, re-check the `recipe-link-drag-handle-{id}` testid is present on each `PhotoTile`'s `dragHandle` and the tiles are in-viewport (`MEMORY` "dnd-kit drag in Playwright IT").

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeAttachmentSteps.cs
git commit -m "test(recipes): adapt attachment IT to sources strip"
```

**PHASE 3 GATE:** Run holistic review on opus. Pause for human.

---

## Phase 4 — Cleanup, IT reconciliation, docs, full verify

### Task 9: Replace the obsolete "details collapse" IT scenario

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature`

- [ ] **Step 1: Remove the obsolete scenario**

Delete the scenario (the `details` accordion no longer exists):
```gherkin
  Scenario: Recipe edit section collapse state persists across reload
    ...
    Then the "details" recipe section is collapsed
```
The section-related step bindings (`WhenICollapseTheRecipeSection` / `ThenTheRecipeSectionIsCollapsed`) become unused — delete them from `RecipeSteps.cs` too (`grep -rn "recipe section is collapsed\|collapse the" Application/Frigorino.IntegrationTests` to confirm no other feature uses them first).

- [ ] **Step 2: (Optional) add a section-target scenario**

Add one scenario covering the new signature so it's regression-protected:
```gherkin
  Scenario: Adding an ingredient targets the chosen section
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I add ingredient "Eggs" to the recipe
    Then "Eggs" appears in the recipe items
```
(This already passes via existing steps — it documents that add-to-default-section still works. Cross-section targeting needs a new step that clicks `recipe-composer-target` then `recipe-composer-target-{id}`; add only if you want the deeper coverage.)

- [ ] **Step 3: Run the full recipe IT**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Recipe"`
Expected: all recipe scenarios pass (confirm `gesamt` count looks right — `MEMORY` "Reqnroll FQN filter silent skip").

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.feature Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeSteps.cs
git commit -m "test(recipes): drop obsolete details-collapse scenario"
```

---

### Task 10: Delete the throwaway prototype

**Files:**
- Delete: `Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPrototype.tsx`, `Application/Frigorino.Web/ClientApp/src/routes/recipes/prototype.tsx`

- [ ] **Step 1: Remove the files (router regenerates the tree)**

```bash
git rm Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPrototype.tsx Application/Frigorino.Web/ClientApp/src/routes/recipes/prototype.tsx
```

- [ ] **Step 2: Regenerate the route tree + verify**

Run: `npm run tsc && npm run lint` (the `@tanstack/router-plugin` regenerates `routeTree.gen.ts` on the next dev/build; if `routeTree.gen.ts` still references `prototype`, run `npm run build` once to regenerate, then commit the regenerated tree).
Expected: 0 errors; no `prototype` reference remains (`grep -n prototype Application/Frigorino.Web/ClientApp/src/routeTree.gen.ts` → empty).

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore(recipes): remove edit-page design prototype"
```

---

### Task 11: Update knowledge docs

**Files:**
- Modify: `knowledge/Recipes.md` (Frontend section), and `knowledge/Frontend_Styling.md` only if a reusable pattern emerged (e.g. the section-aware composer or the sources strip).

- [ ] **Step 1: Update the Recipes frontend notes**

In `knowledge/Recipes.md` under "Frontend", update the edit-page description: the edit page is now a "recipe sheet" — inline editable header (`EditRecipeForm`), a combined `RecipeSourcesStrip` (links + photos, replacing the two CollapsibleSection panels), always-visible `RecipeSectionGroup` sections (replacing `RecipeSectionCard`), and a section-aware `RecipeFooter` composer (target switcher persisted under `recipe-edit:target-section`). Note that `RecipeSectionCard`, `RecipeLinksSection`, `RecipeAttachmentsSection` were removed.

- [ ] **Step 2: Commit**

```bash
git add knowledge/Recipes.md knowledge/Frontend_Styling.md
git commit -m "docs(recipes): document recipe-sheet edit recompose"
```

---

### Task 12: Full verification gate

- [ ] **Step 1: Frontend gate**

Run (from `ClientApp/`): `npm run tsc && npm run lint && npm run prettier && npm run build`
Expected: all exit 0 (`MEMORY` "Prettier in verification"; `prettier` writes — re-commit if it reformats).

- [ ] **Step 2: Full solution tests**

Run (from repo root): `dotnet test Application/Frigorino.sln`
Expected: green. Don't run a second `dotnet test` or `npm run build` in parallel (shared Testcontainers port / `build/` dir — `MEMORY` "Run sln tests for full coverage"). If Docker isn't running, ask the user to start Docker Desktop (`MEMORY` "Ask user to start Docker daemon").

- [ ] **Step 3: Docker build (catches Dockerfile/SPA/pipeline drift)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: image builds (`MEMORY` "Verify with full tests + Docker build").

- [ ] **Step 4: Final holistic review on opus, then finish the branch**

Use superpowers:finishing-a-development-branch to choose merge/PR. Target promotion: `feat/recipe-edit-recompose` → `stage` (client UAT) per `MEMORY` "Branch workflow: stage is client UAT".

---

## Self-Review

**Spec coverage** (against the approved prototype + the four feedback points):
- Title-as-heading + inline description + servings stepper (replaces Details accordion) → Tasks 1–2. ✓
- Editable description (the Q1 fix) → Task 1 (`recipe-description-input`, multiline minRows 2, autosave). ✓
- "Quellen & Fotos" strip with **pinned** add control (Q2 fix) → Task 6 header row. ✓
- Section reorder preserved (Q3) → Task 3 drag handle + `SortableSectionList` in Task 5. ✓
- Section-aware composer target chip (the signature, Q4) → Tasks 4–5. ✓
- All ingredient sections visible at once → Tasks 3, 5. ✓
- Edit page need not mirror view (header stays "Rezept bearbeiten") → kept in `RecipeEditPage` header (unchanged `PageHeadActionBar`). ✓

**Placeholder scan:** No "TBD"/"handle edge cases"/"write tests for the above". The one intentionally open micro-decision (link chip: delete-only vs inline-edit) is explicitly bounded in Task 6 Step 1 with the safe default named ("pick the chip+delete minimum"). Item-row redesign and multi-`recipe-items` scoping are explicitly **out of scope** with rationale.

**Type/name consistency:** `targetSectionId`/`setTargetSectionRaw` (Task 5) match `RecipeFooter`'s `targetSectionId`/`onChangeTargetSection` props (Task 4). `RecipeSectionGroup` prop set (Task 3) matches its call site (Task 5) — `expanded`/`onToggle` removed in both. `EditRecipeForm` prop shape unchanged (Task 1) so its call site (Task 2) needs no edit. Preserved-testid list (Global Constraints) cross-checked against `RecipeSteps.cs`, `ComposerSteps.cs`, `RecipeAttachmentSteps.cs`.

**Known IT timing:** Phase 1–2 intentionally leave the "details collapse" scenario red until Task 9; each phase gate notes it. This is the only deliberately-red window.

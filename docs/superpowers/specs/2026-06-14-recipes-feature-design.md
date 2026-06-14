# Recipes feature (MVP) — Design

**Date:** 2026-06-14
**Status:** Approved — revised after codebase-verification review (extraction/classification seam + maintenance cascade corrected)
**Scope:** A new top-level household resource — **Recipes** — alongside Lists and Inventories. MVP is an ingredient-list holder. Sections, tags, attachments/links, blueprint sorting, promote-to-shopping-list, and classification-driven behavior are all explicitly deferred (see Out of scope).

## Problem

The client wants recipes in the household app. The first cut is **not** a cooking app: a recipe is "a structured list of ingredients" — the same item experience as a list/inventory (natural-language entry → clean name + structured quantity), grouped under a named recipe. No cooking instructions, no scaling, no media in the MVP. The value is capturing reusable ingredient lists with the same pleasant composer the rest of the app already has, so a later **promote-to-shopping-list** feature can copy a recipe's ingredients into a real shopping List.

Frigorino already has two parallel household-scoped item collections — `List` (checkable items, two sections, **AI quantity extraction** on entry) and `Inventory` (single ordered list, expiry dates, **manual** quantity entry — no extraction). A **Recipe** is a third sibling that deliberately takes its shape from one and its item-entry behavior from the other:

- **Aggregate / CRUD / ordering / soft-delete ← Inventory.** Single ordered list, no check-off, no expiry — structurally an `Inventory` minus the expiry field.
- **Item create / update / extraction / composer ← Lists.** Recipe ingredients are entered natural-language and quantity-extracted (`"250g flour"` → "Flour" + `Quantity{250, Gram}`), which is a **List** behavior. **Inventory has no extraction at all**, so the item-entry slices, the `ExtractionPending` response field, the composer feature set, and the poll hook must be copied from `Lists/`, *not* `Inventories/`.

This split is the single most important thing to get right and is called out per-section below. "Mirror Inventory" alone would silently lose extraction; "mirror Lists" alone would drag in check-off/sections. Recipe is the Inventory *shape* with the List *item-entry pipeline*.

## Decisions (locked during brainstorming)

1. **Parallel slice, split provenance.** A standalone `Recipe` aggregate + `RecipeItem`, built as its own vertical-slice folder. **Aggregate/CRUD/ordering mirror `Inventories/`; item create/update mirror `Lists/Items/`** (see Problem). No shared base abstraction with List/Inventory (they are already deliberately parallel, not shared); no typed-variant overload of an existing aggregate. Matches the flat-schema / clean-domain-separation / no-abstraction-for-sibling-symmetry preferences.
2. **Recipe item shape = Inventory item minus expiry, plus comment.** Single fractional-index-ordered list, **no `Status`/check-off** (a recipe is a template you read; ticking happens later on the promoted shopping list), **no `ExpiryDate`**. Carries `Text`, an optional `Comment` (per-ingredient note — "room temperature", "finely chopped"), and the structured `Quantity` VO.
3. **Quantity extraction yes, classification no.** Recipe-item entry copies the **List** quantity-extraction path (`"250g flour"` → "Flour" + `Quantity{250, Gram}`, async with the `ExtractionPending` poll). The `IItemClassifier` is **not** triggered — nothing in the recipe MVP consumes aisle/expiry classification, and the future promote-to-list flow will classify items when they are copied into a List (which runs the normal list-item pipeline). **This is not free reuse:** the existing `ExtractQuantityJob` unconditionally chains classification (`OnProductReferenced`) after extraction, so a **recipe-specific extraction job/trigger that omits the classification chain is required** — reusing the list job as-is would accrue `Product` rows and contradict this decision. See "Quantity extraction wiring".
4. **Revision-gated collaborative sync**, soft-delete + restore, auto timestamps, fractional-index reordering — all inherited free from the established pattern.
5. **Comment semantics follow the existing `ListItem.Comment` design** (commit on list-item-comments): `null = preserve`, empty/whitespace = clear-to-null, otherwise trim + length-validate, no `clearComment` flag. The comment never participates in quantity extraction.
6. **Frontend mirrors `inventories/`**: overview (card grid + create), recipe view (ingredient list + composer footer + revision polling), edit (name/description), create. Recipes become a third top-level entry in household navigation.
7. **MVP cuts** (moved to phase 2 during brainstorming): **sections**, **tags**, **source links + file/image/document attachments**. Schema is shaped so each can be added later without a rewrite.

## Backend

### Entity — `Frigorino.Domain/Entities/Recipe.cs` (mirrors `Inventory`)

Fields: `Id`, `Name` (max 255), `Description?` (max 1000), `HouseholdId`, `CreatedByUserId`, `CreatedAt`, `UpdatedAt`, `IsActive`. Navigation: `Household`, `CreatedByUser`, `ICollection<RecipeItem> Items`.

- Factory `static Result<Recipe> Create(string name, string? description, int householdId, string createdByUserId)`.
- `Update(...)` / `SoftDelete(...)` — permission = `CreatedByUserId == callerUserId || callerRole >= HouseholdRole.Admin` (a `CanBeManagedBy(callerUserId, callerRole)` helper, same as `Inventory`).
- Item coordination methods (collaborative — no role gate on items, matching Inventory):
  - `AddItem(string text, Quantity? quantity, string? comment)`
  - `UpdateItem(int itemId, string? text, Quantity? quantity, bool clearQuantity, string? comment)` — **no `status` param** (no check-off).
  - `RemoveItem(int itemId)` / `RestoreItem(int itemId)` (with the rank-collision re-mint on restore, same as Inventory).
  - `ReorderItem(int itemId, string? afterRank, string? beforeRank)`.
  - `ApplyExtractedQuantity(int itemId, string cleanName, Quantity? quantity)` — async-extraction callback, same three-arg signature as `List.ApplyExtractedQuantity` (the job unpacks `extraction.CleanName` / `extraction.Quantity` at the call site; no `QuantityExtraction` object crosses the aggregate boundary).
- Shared validators: `ValidateName`, `ValidateDescription`, `ValidateItemText`, `ValidateComment` (mirror the existing aggregates; reuse the same const widths to avoid an unnecessary divergence).

### Entity — `Frigorino.Domain/Entities/RecipeItem.cs` (Inventory-item-shaped + comment)

```csharp
public const int TextMaxLength = 500;       // match ListItem (entry is List-style: "250g whole wheat flour…")
public const int CommentMaxLength = 500;     // match ListItem.Comment

public int Id { get; set; }
public int RecipeId { get; set; }
public string Text { get; set; } = string.Empty;
public string? Comment { get; set; }
public decimal? QuantityValue { get; set; }  // Quantity VO, two flat nullable cols
public QuantityUnit? QuantityUnit { get; set; }
public string Rank { get; set; } = string.Empty;  // fractional index, single section
public DateTime CreatedAt { get; set; }
public DateTime UpdatedAt { get; set; }
public bool IsActive { get; set; } = true;
// Navigation: Recipe
```

No `Status`, no `ExpiryDate`, no media/promotion columns. Quantity is the same two-nullable-column VO pattern (`QuantityValue` + `QuantityUnit`, both set or both null) used by `ListItem`/`InventoryItem` — reuse the `Quantity` VO and `QuantityDto` verbatim.

### EF — `RecipeConfiguration` / `RecipeItemConfiguration`

Mirror `InventoryConfiguration` / `InventoryItemConfiguration`: `HasMaxLength` from the entity consts, the `(RecipeId, Rank)` partial unique index on active items (the same fractional-index ordering index Inventory uses, scoped per recipe with a single section). `IsActive` filtered per-slice (no global query filter), timestamps auto-stamped in `ApplicationDbContext.SaveChangesAsync`. New `DbSet<Recipe>` + `DbSet<RecipeItem>`. **One migration** (`AddRecipes`): two tables, no backfill, reversible.

**FK delete behavior must match `InventoryConfiguration` exactly.** `Household.SoftDelete` only flips `IsActive` on the household; child rows stay `IsActive=true` and are hidden because the parent is filtered out per-slice. The *actual* reaping is the `DeleteInactiveItems` maintenance task, which `ExecuteDeleteAsync`-hard-deletes the household and relies on **DB-level cascade** to drop children. So `Recipe`→`Household` and `RecipeItem`→`Recipe` FKs must use the same `OnDelete` (cascade) as Inventory's, or recipe rows orphan/block the purge.

### Maintenance purge — `DeleteInactiveItems` (REQUIRED edit, not optional)

`DeleteInactiveItems.Run` (`Frigorino.Infrastructure/.../DeleteInactiveItems.cs`) explicitly hard-deletes inactive `Households`, `Lists`, `ListItems`, `Inventories`, `InventoryItems`. **It does not know about recipes** — without an edit, soft-deleted recipes and recipe items leak forever (there is no EF soft-delete cascade; this task is the cascade). Add `Recipes` + `RecipeItems` to the purge: hard-delete recipes whose household is gone or that are themselves `IsActive=false`, and inactive recipe items (mirror the exact predicates used for `Inventories`/`InventoryItems`). This must ship in the same change as the entities.

### Slices — `Frigorino.Features/Recipes/`

**Provenance per slice (do not copy the wrong sibling):**
- Recipe CRUD (`CreateRecipe`/`UpdateRecipe`/`Delete`/`Get`/`GetRecipes`) + `GetRecipeRevision` ← copy `Inventories/` equivalents.
- **Item create/update (`CreateRecipeItem`/`UpdateRecipeItem`) ← copy `Lists/Items/CreateItem.cs` + `UpdateItem.cs`**, because those carry the `ItemTextRouter` routing, the extraction trigger, and the `ExtractionPending` response field that Inventory's item slices lack.
- Item delete/restore/get/reorder ← either sibling (identical); use `Inventories/Items/` since there's no status section to worry about.

Recipe CRUD + sync:
- `CreateRecipe` — `POST /api/household/{householdId}/recipes`
- `UpdateRecipe` — `PUT  …/recipes/{recipeId}`
- `DeleteRecipe` — `DELETE …/recipes/{recipeId}` (soft-delete)
- `GetRecipe` — `GET …/recipes/{recipeId}`
- `GetRecipes` — `GET …/recipes`
- `GetRecipeRevision` — `GET …/recipes/{recipeId}/revision` → `RevisionResponse.Compute(recipe.UpdatedAt, maxItemUpdatedAt, activeItemCount)` (the exact revision-gated-sync pattern from commit `df07a67`).

Items — `Recipes/Items/`:
- `CreateRecipeItem` — `POST …/recipes/{recipeId}/items`
- `UpdateRecipeItem` — `PUT …/recipes/{recipeId}/items/{itemId}`
- `DeleteRecipeItem` — `DELETE …/recipes/{recipeId}/items/{itemId}`
- `RestoreRecipeItem` — `POST …/recipes/{recipeId}/items/{itemId}/restore`
- `GetRecipeItems` — `GET …/recipes/{recipeId}/items`
- `ReorderRecipeItem` — `POST …/recipes/{recipeId}/items/{itemId}/reorder`
- **No `ToggleItemStatus`** (no check-off). **No media / blueprint / promote slices** (deferred).

DTOs — sealed records with static `.From()` + EF `.ToProjection`, mirroring `InventoryResponse` / `InventoryItemResponse`:
- `RecipeResponse(int Id, string Name, string? Description, int HouseholdId, DateTime CreatedAt, DateTime UpdatedAt, RecipeCreatorResponse CreatedByUser, int ItemCount)`.
- `RecipeItemResponse(int Id, int RecipeId, string Text, string? Comment, QuantityDto? Quantity, string Rank, DateTime CreatedAt, DateTime UpdatedAt, bool ExtractionPending)`.
- `CreateRecipeRequest(string Name, string? Description)`, `UpdateRecipeRequest(string Name, string? Description)`.
- `CreateRecipeItemRequest(string Text, string? Comment, QuantityDto? Quantity = null)`, `UpdateRecipeItemRequest(string? Text, QuantityDto? Quantity, bool ClearQuantity, string? Comment)`.

### Quantity extraction wiring — the two real (non-mechanical) decisions

This is where Recipe genuinely diverges from "copy a sibling," because the desired combination — **List extraction + no classification** — does not exist anywhere today. `CreateRecipeItem` (copied from `Lists/Items/CreateItem.cs`) routes `request.Text` through `ItemTextRouter.Analyze` and, on the `NeedsExtraction` route, sets `ExtractionPending = true` on the create response and enqueues a quantity-extraction job. The job calls `IQuantityExtractor.ExtractAsync` and applies the result via `Recipe.ApplyExtractedQuantity(itemId, cleanName, quantity)`. The frontend polls `ExtractionPending` to refetch, identical to lists.

**Decision 1 — a recipe-specific, no-classify extraction job/trigger.** The existing `ExtractQuantityJob` unconditionally calls `_classificationTrigger.OnProductReferenced(householdId, cleanName)` after extraction, and the enabled `QueueingQuantityExtractionTrigger.OnItemRouted` enqueues exactly that job. Reusing either as-is **would classify and accrue `Product` rows** — violating "classification no". So the plan must introduce a recipe-scoped extraction path that performs quantity extraction **without** the `OnProductReferenced` chain (a parallel `RecipeQuantityExtractionTrigger` + a job variant, or a flag on the job that suppresses classification). This is not a config toggle on the existing wiring and not free reuse.

**Decision 2 — does `UpdateRecipeItem` re-route on text change?** `Lists/Items/UpdateItem.cs` re-routes edited text through `ItemTextRouter` and re-triggers extraction when text changes without an explicit quantity; `Inventories/Items/UpdateInventoryItem.cs` does neither. Since recipe entry is List-style, the consistent choice is **yes, re-extract on text change** — which means `UpdateRecipeItem` also injects the recipe-scoped (no-classify) trigger, and `UpdateRecipeItemRequest` behaves like the list update (text change without quantity intent → re-extract). The plan must state this explicitly rather than inherit Inventory's silent no-op.

Both decisions trace to the same root: Recipe wants List item-entry behavior but neither sibling offers "extraction minus classification." Budget for a small new trigger/job, not pure copy.

### Wiring — `Frigorino.Web/Program.cs`

`app.MapGroup("/api/household/{householdId:int}/recipes").RequireAuthorization().WithTags("Recipes")` with per-slice extension methods (`recipes.MapCreateRecipe()`, …), same as the inventories group.

### API client regeneration

`npm run api` from `ClientApp/` after backend changes (build-time MSBuild target emits `openapi.json`, regenerates the TS client + tanstack hooks). No hand-edits under `src/lib/api/`. Enums serialize as string names on the wire as usual.

## Frontend — `src/features/recipes/` (mirrors `inventories/`)

### Hooks (one-per-file, spread generated options — never hand-write `queryFn`/`mutationFn`/`queryKey`)

- Collection / aggregate: `useHouseholdRecipes.ts`, `useRecipe.ts`, `useCreateRecipe.ts`, `useUpdateRecipe.ts`, `useDeleteRecipe.ts`.
- Items (`items/`): `useRecipeItems.ts`, `useCreateRecipeItem.ts`, `useUpdateRecipeItem.ts`, `useDeleteRecipeItem.ts`, `useRestoreRecipeItem.ts`, `useReorderRecipeItem.ts`, `useRecipeRevision.ts` (polls the revision token via `useRevisionInvalidation`), `useExtractionPoll`-equivalent for `ExtractionPending`.
- Optimistic update hooks (create/update/reorder) keep their `onMutate`/`onError`/`onSettled` callbacks, reading keys via `getRecipeItemsQueryKey({ path: {...} })`.

### Components (`components/`, `items/components/`)

Mirror `inventories/`: `CreateRecipeForm`, `EditRecipeForm`, `RecipeActionsMenu`, `RecipeSummaryCard`, `DeleteRecipeConfirmDialog`; `RecipeContainer` (item list), `RecipeFooter` (composer), `RecipeItemContent` (renderer with quantity chip + comment preview — reuse the `ListItemContent` comment-preview affordance). Drag-to-reorder reuses the inventory reorder UX.

### Composer

Reuse the existing composer framework. The composer + extraction-poll behavior come from **Lists**, not Inventory (Inventory's composer is manual-quantity). Add-mode and edit-mode features: `quantityComposerFeature` (edit) + `commentComposerFeature` (add + edit). Add-mode stays quantity-free (extraction owns quantity at add time), same as lists.

### Pages + routes

- `pages/`: `RecipesPage` (overview card grid + create), `RecipeViewPage` (items + composer + revision polling), `RecipeEditPage` (name/description), `CreateRecipePage`.
- `src/routes/recipes/`: `index.tsx` → `RecipesPage`, `create.tsx` → `CreateRecipePage`, `$recipeId/view.tsx` → `RecipeViewPage`, `$recipeId/edit.tsx` → `RecipeEditPage`. Thin shells: `createFileRoute` + `requireAuth` + import page component. `routeTree.gen.ts` regenerates via the router vite plugin.
- Navigation: add Recipes as a third top-level household resource (wherever Lists/Inventories entries live).

## i18n

Add a `recipes.*` namespace to `public/locales/{en,de}/translation.json` mirroring the `inventories.*` keys (title, create, edit, delete-confirm, composer placeholders, comment label/placeholder, empty-state). Tests never assert on translated text.

## Testing

- **Domain unit tests** (`Frigorino.Test`, xUnit + FakeItEasy, InMemory):
  - `Recipe.Create` validation; `Update`/`SoftDelete` permission (creator vs Admin vs Member).
  - `Recipe.AddItem` / `UpdateItem` — text + comment trim/clear/over-length (`Property = Comment` / `Text`), quantity set/clear, no-op guard (a comment-only update is valid).
  - `ReorderItem` fractional-index behavior + restore rank-collision re-mint.
  - `ApplyExtractedQuantity(itemId, cleanName, quantity)` rewrites name + quantity only.
  - **No-classify guarantee:** adding a recipe item that triggers extraction must **not** create/touch a `Product` row (assert the recipe extraction path never calls `OnProductReferenced`). This is the regression test guarding Decision 1.
  - ArchUnit layer rules pass (new types respect Domain/Features/Web boundaries).
- **Integration** (`Frigorino.IntegrationTests`, Reqnroll + Playwright + Postgres Testcontainers):
  - Create a recipe, add an ingredient via the composer (assert structured quantity after extraction settles), edit/reorder/delete an ingredient, soft-delete + restore a recipe. Assert via `data-testid` only.
  - Requires `npm run build` first so testids land in `ClientApp/build` (IT serves the built SPA).

**Verification gate:** `dotnet test Application/Frigorino.sln` (full SLN — Test + IntegrationTests) + frontend `npm run lint` / `npm run tsc` / prettier + `docker build` as the final drift check (and update `Application/Dockerfile` if the project layout changes — it should not, since Recipes is new files in existing projects).

## Impact / cost

Medium, additive, mostly reversible. New files in existing projects (Domain entities + EF config, Features slice folder, ClientApp feature folder + routes), one EF migration (two tables), regenerated API client, one i18n namespace, plus **one required edit to existing code** (`DeleteInactiveItems` purge lines). No new dependencies, no changes to List/Inventory *behavior*.

The non-mechanical work (budget for these, they are not copy-paste):
1. A recipe-scoped extraction trigger + job variant that omits classification (Decision 1 above).
2. Settling edit-time re-extraction (Decision 2 above).
3. The `DeleteInactiveItems` purge + matching FK `OnDelete` cascade (or recipes leak).

Everything else — aggregate, CRUD slices, DTOs, frontend, i18n, revision sync — is genuine copy-from-sibling.

## Out of scope (phase 2+)

- **Sections** (crust/filling). Will add a nullable `SectionId` FK on `RecipeItem` + a lightweight ordered `RecipeSection` entity. Schema is shaped to absorb this without rewriting item ordering (rank becomes per-section).
- **Tags** (Entry/Main/Side/Salad/Dessert…). Will add a flat `RecipeTag` join row (`RecipeId`, `Tag` enum) + overview filter chips. No tag concept in MVP.
- **Attachments**: source **links** (URL) and **file/image/document** uploads. Links come first in phase 2 (cheapest; feeds the future "AI reads the source" path); file uploads reuse the existing blob-storage + thumbnail infra (a flat `RecipeAttachment` table with a `Type` enum + nullable `StorageKey`/`Url`).
- **Promote-to-shopping-list**: copy a recipe's items into a List (mirrors the list→inventory promotion). Classification fires then, via the normal list-item pipeline. This is the headline phase-2 feature and the reason recipe items mirror the list item shape.
- **Blueprint sorting** of recipe ingredients (reuse `BlueprintSorter`) — depends on classification, which MVP does not run for recipes.
- **Classification-driven anything** in the recipe UI.
- Servings/yield, scaling, cooking instructions/steps, in-app instruction generation from sources.

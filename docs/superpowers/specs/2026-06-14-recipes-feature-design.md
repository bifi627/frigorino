# Recipes feature (MVP) — Design

**Date:** 2026-06-14
**Status:** Approved (pending spec review)
**Scope:** A new top-level household resource — **Recipes** — alongside Lists and Inventories. MVP is an ingredient-list holder. Sections, tags, attachments/links, blueprint sorting, promote-to-shopping-list, and classification-driven behavior are all explicitly deferred (see Out of scope).

## Problem

The client wants recipes in the household app. The first cut is **not** a cooking app: a recipe is "a structured list of ingredients" — the same item experience as a list/inventory (natural-language entry → clean name + structured quantity), grouped under a named recipe. No cooking instructions, no scaling, no media in the MVP. The value is capturing reusable ingredient lists with the same pleasant composer the rest of the app already has, so a later **promote-to-shopping-list** feature can copy a recipe's ingredients into a real shopping List.

Frigorino already has two parallel household-scoped item collections — `List` (checkable items, two sections) and `Inventory` (single ordered list, expiry dates). A **Recipe** is a third sibling: a single ordered list of ingredient items with no check-off and no expiry. This design mirrors the **Inventory** feature shape almost exactly, swapping expiry for an ingredient comment and keeping the list composer's quantity-extraction path.

## Decisions (locked during brainstorming)

1. **Parallel slice, mirror by copy.** A standalone `Recipe` aggregate + `RecipeItem`, built as its own vertical-slice folder mirroring `Inventories/`. No shared base abstraction with List/Inventory (they are already deliberately parallel, not shared); no typed-variant overload of an existing aggregate. Matches the flat-schema / clean-domain-separation / no-abstraction-for-sibling-symmetry preferences.
2. **Recipe item shape = Inventory item minus expiry, plus comment.** Single fractional-index-ordered list, **no `Status`/check-off** (a recipe is a template you read; ticking happens later on the promoted shopping list), **no `ExpiryDate`**. Carries `Text`, an optional `Comment` (per-ingredient note — "room temperature", "finely chopped"), and the structured `Quantity` VO.
3. **Quantity extraction yes, classification no.** Recipe-item entry reuses the list composer's quantity-extraction path (`"250g flour"` → "Flour" + `Quantity{250, Gram}`, async with the `ExtractionPending` poll). The `IItemClassifier` is **not** triggered — nothing in the recipe MVP consumes aisle/expiry classification, and the future promote-to-list flow will classify items when they are copied into a List (which runs the normal list-item pipeline). No `Product` rows accrue from recipe entry.
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
  - `ApplyExtractedQuantity(int itemId, QuantityExtraction extraction)` — async-extraction callback, same contract as `List`/`Inventory`.
- Shared validators: `ValidateName`, `ValidateDescription`, `ValidateItemText`, `ValidateComment` (mirror the existing aggregates; reuse the same const widths to avoid an unnecessary divergence).

### Entity — `Frigorino.Domain/Entities/RecipeItem.cs` (Inventory-item-shaped + comment)

```csharp
public const int TextMaxLength = 255;       // match InventoryItem
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

Mirror `InventoryConfiguration` / `InventoryItemConfiguration`: `HasMaxLength` from the entity consts, the `(RecipeId, Rank)` partial unique index on active items (the same fractional-index ordering index Inventory uses, scoped per recipe with a single section), FK + cascade. `IsActive` filtered per-slice (no global query filter), timestamps auto-stamped in `ApplicationDbContext.SaveChangesAsync`. New `DbSet<Recipe>` + `DbSet<RecipeItem>`. **One migration** (`AddRecipes`): two tables, no backfill, reversible.

### Slices — `Frigorino.Features/Recipes/` (mirrors `Inventories/`)

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

### Quantity extraction wiring

`CreateRecipeItem` routes `request.Text` through the existing `ItemTextRouter.Analyze` and, on the `NeedsExtraction` route, sets `ExtractionPending = true` on the create response and triggers the **quantity** extraction job for `RecipeItem` (via the existing async-extraction trigger generalized to recipe items, or a parallel recipe-scoped trigger if generalization is invasive — implementation plan decides). The job calls `IQuantityExtractor.ExtractAsync` and applies the result via `Recipe.ApplyExtractedQuantity`. **The classifier (`IItemClassifier`) is not invoked and no `Product` row is touched.** The frontend polls `ExtractionPending` to refetch, identical to lists.

> Implementation note: the current extraction trigger is List-shaped (`OnItemRouted(householdId, listId, itemId, …)`). The plan must decide whether to widen it to an item-kind-agnostic shape or add a recipe-scoped sibling. This is the one place the "mirror by copy" is not free — flagged for the plan, not silently assumed.

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

Reuse the existing composer framework. Add-mode and edit-mode features: `quantityComposerFeature` (edit) + `commentComposerFeature` (add + edit). Add-mode stays quantity-free (extraction owns quantity at add time), same as lists.

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
  - `ApplyExtractedQuantity` rewrites name + quantity only.
  - ArchUnit layer rules pass (new types respect Domain/Features/Web boundaries).
- **Integration** (`Frigorino.IntegrationTests`, Reqnroll + Playwright + Postgres Testcontainers):
  - Create a recipe, add an ingredient via the composer (assert structured quantity after extraction settles), edit/reorder/delete an ingredient, soft-delete + restore a recipe. Assert via `data-testid` only.
  - Requires `npm run build` first so testids land in `ClientApp/build` (IT serves the built SPA).

**Verification gate:** `dotnet test Application/Frigorino.sln` (full SLN — Test + IntegrationTests) + frontend `npm run lint` / `npm run tsc` / prettier + `docker build` as the final drift check (and update `Application/Dockerfile` if the project layout changes — it should not, since Recipes is new files in existing projects).

## Impact / cost

Medium, additive, reversible. New files in existing projects (Domain entities + EF config, Features slice folder, ClientApp feature folder + routes), one EF migration (two tables), regenerated API client, one i18n namespace. No new dependencies, no changes to List/Inventory behavior. The only non-mechanical decision is generalizing vs. duplicating the quantity-extraction trigger (flagged above).

## Out of scope (phase 2+)

- **Sections** (crust/filling). Will add a nullable `SectionId` FK on `RecipeItem` + a lightweight ordered `RecipeSection` entity. Schema is shaped to absorb this without rewriting item ordering (rank becomes per-section).
- **Tags** (Entry/Main/Side/Salad/Dessert…). Will add a flat `RecipeTag` join row (`RecipeId`, `Tag` enum) + overview filter chips. No tag concept in MVP.
- **Attachments**: source **links** (URL) and **file/image/document** uploads. Links come first in phase 2 (cheapest; feeds the future "AI reads the source" path); file uploads reuse the existing blob-storage + thumbnail infra (a flat `RecipeAttachment` table with a `Type` enum + nullable `StorageKey`/`Url`).
- **Promote-to-shopping-list**: copy a recipe's items into a List (mirrors the list→inventory promotion). Classification fires then, via the normal list-item pipeline. This is the headline phase-2 feature and the reason recipe items mirror the list item shape.
- **Blueprint sorting** of recipe ingredients (reuse `BlueprintSorter`) — depends on classification, which MVP does not run for recipes.
- **Classification-driven anything** in the recipe UI.
- Servings/yield, scaling, cooking instructions/steps, in-app instruction generation from sources.

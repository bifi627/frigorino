# Recipes Feature (MVP) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a household-scoped **Recipes** resource — a single ordered list of ingredient items (text + optional comment + structured quantity via LLM extraction, no check-off, no expiry), with full CRUD, revision-gated collaborative sync, soft-delete, and a frontend mirroring Inventories.

**Architecture:** Split-provenance vertical slices. The `Recipe` aggregate, CRUD slices, ordering, and frontend structure mirror **Inventories**; item *creation/update* mirrors **Lists** (text routing + async quantity extraction + `ExtractionPending` poll), because Inventory has no extraction. Extraction reuses the list router/extractor/queue but runs through a **recipe-specific job + trigger that omit the classification chain** (no `Product` rows accrue). One EF migration (two tables), one required edit to the `DeleteInactiveItems` maintenance purge.

**Tech Stack:** .NET 10 minimal-API vertical slices, EF Core (Postgres), FluentResults, xUnit + FakeItEasy; React 19 + TanStack Query/Router + MUI + Zustand; hey-api generated client. Reqnroll + Playwright + Testcontainers for E2E (DB-touching tests are Testcontainers-only — no SQLite/InMemory).

**Spec:** `docs/superpowers/specs/2026-06-14-recipes-feature-design.md` (read it first).

---

## Implementation status — ✅ COMPLETE (backend + frontend + gate, updated 2026-06-14)

**ALL tasks (1–19) DONE** on branch `feat/recipes-impl`, subagent-driven, each task spec- + quality-reviewed. Final gate green: `dotnet test Application/Frigorino.sln` = **477 unit + 148 integration passed** (2 skipped); frontend lint/tsc/prettier/build all clean; `docker build` exit 0.

**Frontend (Tasks 9–19) commits:** T9 `1e27bff` (regen client), T10 `783ab04` (collection hooks), T11 `2ffce6e` (item hooks), T12 `600d88d` (composer/container/content + typed `recipes` i18n namespace), T13 `4c9114c` (pages/forms/cards), T14 `d31264b` (routes + tightened typed nav), T15 `55a94c9` (dashboard), T16 `db8128c` (i18n en/de), T18 `c0061d0` (e2e test) + `13224d0` (prettier normalization). T17/T19 = build/gate (no commit).

**Bug found by the T18 e2e test + fixed:** the optimistic create row used `id: Date.now()`; editing/reordering before the debounced refetch targeted that temp id, which overflows the `{itemId:int}` route → fell through to the SPA fallback → opaque `500`. Fixed by reconciling temp→real id in `useCreateRecipeItem` `onSuccess` (`cd5ec3d`), mirroring `useCreateListItem`. The **same latent bug in `useCreateInventoryItem`** was also fixed (`57e0ebc`). Diagnosability gaps that made it expensive to find (empty 500 body, hidden server logs, SPA fallback masking unmatched `/api`) logged in `TECH_DEBT.md` (`e5e80ce`).

<details><summary>Backend (Tasks 1–8) — DONE, holistic opus review passed</summary>

| Task | Commit | Notes |
|------|--------|-------|
| T1-2 RecipeItem + Recipe aggregate | `ef962b3`, `3b33559` | nav collection kept as `Recipe.Items` (used throughout) |
| T3 EF config + `AddRecipes` migration | `9b0a5a8` | explicit `Recipe→Household` Cascade FK (Household has no Recipes nav) |
| T4 No-classify extraction (job/trigger/DI) | `15c8616` | regression test asserts `Products` stays empty |
| T5 Recipe CRUD + revision slices | `e0e9f5b` | fixed 3 idiom drifts vs Inventory siblings |
| T6 Recipe item slices (split provenance) | `cc5bba9` | Delete/Restore/Reorder ← Inventory; Create/Update ← Lists |
| T7 MapGroups wiring + `openapi.json` | `5a77ee9` | 13 endpoints |
| T8 Maintenance purge | `05a1026` | **scope change:** SQLite unit test dropped (see below); purge code kept |
| docs: testing convention | `ed10f06` | DB tests = Testcontainers, not InMemory/SQLite |

- **T8 reality vs. plan text:** the plan's T8 below still describes an EF-InMemory unit test with `TestApplicationDbContext.Create()`. That was NOT used — `ExecuteDeleteAsync` is relational-only and this project does not do SQLite/InMemory DB tests. The 2-line purge was added to `DeleteInactiveItems.cs` with NO unit test; a tech-debt item in `TECH_DEBT.md` tracks adding Testcontainer coverage in `Frigorino.IntegrationTests`. Treat T8 as fully done; ignore its InMemory test steps.

</details>

---

## File Structure

**Backend — create:**
- `Application/Frigorino.Domain/Entities/Recipe.cs` — aggregate (factory, `CanBeManagedBy`, `Update`, `SoftDelete`, item-coordination methods, `ApplyExtractedQuantity`). Mirrors `Inventory.cs`, no status, comment instead of expiry.
- `Application/Frigorino.Domain/Entities/RecipeItem.cs` — item entity (Text, Comment, Quantity cols, Rank). Mirrors `InventoryItem.cs`, no expiry, plus Comment.
- `Application/Frigorino.Domain/Interfaces/IRecipeQuantityExtractionTrigger.cs` — recipe-scoped trigger interface.
- `Application/Frigorino.Domain/Interfaces/IExtractRecipeQuantityJob.cs` — recipe-scoped job interface.
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeConfiguration.cs`
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeItemConfiguration.cs`
- `Application/Frigorino.Infrastructure/Services/RecipeQuantityExtractionTrigger.cs` — enabled (queueing) + null impls, NO classification chain.
- `Application/Frigorino.Infrastructure/Services/ExtractRecipeQuantityJob.cs` — extracts + applies; NO `OnProductReferenced`.
- `Application/Frigorino.Infrastructure/Services/RecipeQuantityExtractionDependencyInjection.cs` — `AddRecipeQuantityExtraction`.
- `Application/Frigorino.Features/Recipes/CreateRecipe.cs`, `UpdateRecipe.cs`, `DeleteRecipe.cs`, `GetRecipe.cs`, `GetRecipes.cs`, `GetRecipeRevision.cs`, `RecipeResponse.cs`.
- `Application/Frigorino.Features/Recipes/Items/CreateRecipeItem.cs`, `UpdateRecipeItem.cs`, `DeleteRecipeItem.cs`, `RestoreRecipeItem.cs`, `GetRecipeItems.cs`, `ReorderRecipeItem.cs`, `RecipeItemResponse.cs`.
- `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs`, `Application/Frigorino.Test/Recipes/ExtractRecipeQuantityJobTests.cs`.

**Backend — modify:**
- `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs` — add `DbSet<Recipe>`/`DbSet<RecipeItem>` + timestamp stamping in `SaveChangesAsync`.
- `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs` — add Recipes/RecipeItems purge.
- `Application/Frigorino.Web/Program.cs` — register `AddRecipeQuantityExtraction`, wire the `recipes` + `recipeItems` MapGroups.
- EF migration `AddRecipes` (generated).

**Frontend — create (under `Application/Frigorino.Web/ClientApp/src/`):**
- `features/recipes/useHouseholdRecipes.ts`, `useRecipe.ts`, `useCreateRecipe.ts`, `useUpdateRecipe.ts`, `useDeleteRecipe.ts`.
- `features/recipes/items/useRecipeItems.ts`, `useCreateRecipeItem.ts`, `useUpdateRecipeItem.ts`, `useDeleteRecipeItem.ts`, `useRestoreRecipeItem.ts`, `useReorderRecipeItem.ts`, `useRecipeRevision.ts`, `useRecipeExtractionPoll.ts`.
- `features/recipes/items/components/RecipeContainer.tsx`, `RecipeFooter.tsx`, `RecipeItemContent.tsx`.
- `features/recipes/components/RecipeSummaryCard.tsx`, `RecipeActionsMenu.tsx`, `CreateRecipeForm.tsx`, `EditRecipeForm.tsx`, `DeleteRecipeConfirmDialog.tsx`.
- `features/recipes/pages/RecipesPage.tsx`, `RecipeViewPage.tsx`, `RecipeEditPage.tsx`, `CreateRecipePage.tsx`.
- `routes/recipes/index.tsx`, `routes/recipes/create.tsx`, `routes/recipes/$recipeId/view.tsx`, `routes/recipes/$recipeId/edit.tsx`.

**Frontend — modify:**
- `components/dashboard/WelcomePage.tsx` — replace `rezepte` placeholder with a real collection; add `"rezepte"` branches.
- `public/locales/en/translation.json` + `public/locales/de/translation.json` — `recipes` namespace + `dashboard`/`navigation` recipe keys.
- Generated client under `src/lib/api/` — via `npm run api` (do not hand-edit).

---

## Conventions every task follows

- **Backend errors:** `result.ToValidationProblem()` (from `Frigorino.Features.Results`) for validation; `result.Errors[0] is EntityNotFoundError` → `TypedResults.NotFound()`. `"Property"` metadata drives the `ValidationProblem` field.
- **Membership:** write slices that need the creator use `db.FindActiveMembershipWithUserAsync(householdId, currentUser.UserId, ct)` then `.User`; everything else uses `db.FindActiveMembershipAsync(...)`.
- **Rank concurrency:** create/reorder/restore item slices wrap the save in `RankRetry.SaveWithRetryAsync(async () => { db.ChangeTracker.Clear(); ... })` (from `Frigorino.Features.Items`).
- **Frontend hooks:** spread generated `getXOptions` / `xMutation` / `getXQueryKey`; never hand-write `queryFn`/`mutationFn`/`queryKey`. Query hooks guard `enabled` on ids `> 0` and set `staleTime`.
- **Commits:** one per task (or per logical step where noted). No `Co-Authored-By` trailer.
- **No frontend unit-test runner exists** — frontend tasks verify via `npm run tsc` + `npm run lint`, and behavior is covered by the integration test in Task 18.

---

## Task 1: `RecipeItem` entity — ✅ DONE (`ef962b3`)

**Files:**
- Create: `Application/Frigorino.Domain/Entities/RecipeItem.cs`

- [ ] **Step 1: Write the entity** (mirrors `InventoryItem.cs`, drops `ExpiryDate`, adds `Comment`; `TextMaxLength = 500` to match List-style entry, `CommentMaxLength = 500` to match `ListItem.Comment`):

```csharp
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Entities
{
    public class RecipeItem
    {
        // Entry is List-style (free-text "250g whole wheat flour…"), so Text matches ListItem's
        // 500 cap, not InventoryItem's 255. Comment matches ListItem.Comment. Behaviour lives on
        // the parent Recipe aggregate.
        public const int TextMaxLength = 500;
        public const int CommentMaxLength = 500;

        public int Id { get; set; }
        public int RecipeId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Comment { get; set; }

        // Structured quantity, two flat nullable columns (mirrors ListItem/InventoryItem). Both set
        // together or both null; the Recipe aggregate enforces it.
        public decimal? QuantityValue { get; set; }
        public QuantityUnit? QuantityUnit { get; set; }

        // Lexicographic ordering key (fractional index). Single section — no status split.
        public string Rank { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Recipe Recipe { get; set; } = null!;
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Domain`
Expected: PASS (no Recipe.cs yet — RecipeItem references `Recipe` as a nav type; this will fail until Task 2's `Recipe` exists). **If it fails only on the missing `Recipe` type, that's expected — proceed to Task 2 and build them together.** Otherwise fix.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Domain/Entities/RecipeItem.cs
git commit -m "feat(recipes): add RecipeItem entity"
```

---

## Task 2: `Recipe` aggregate (TDD) — ✅ DONE (`3b33559`)

**Files:**
- Create: `Application/Frigorino.Domain/Entities/Recipe.cs`
- Test: `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs`

- [ ] **Step 1: Write the failing tests** (mirrors the existing Inventory aggregate tests; covers factory validation, permission, item add/update with comment, no-op guard, reorder, extraction apply):

```csharp
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Quantities;
using Xunit;

namespace Frigorino.Test.Recipes
{
    public class RecipeAggregateTests
    {
        private static Recipe NewRecipe()
        {
            var r = Recipe.Create("Apple Pie", null, householdId: 1, createdByUserId: "u1");
            Assert.True(r.IsSuccess);
            var recipe = r.Value;
            recipe.Id = 10;
            return recipe;
        }

        [Fact]
        public void Create_BlankName_Fails()
        {
            var result = Recipe.Create("  ", null, 1, "u1");
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(Recipe.Name));
        }

        [Fact]
        public void Update_NonOwnerNonAdmin_Denied()
        {
            var recipe = NewRecipe();
            var result = recipe.Update("someone-else", HouseholdRole.Member, "New", null);
            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void AddItem_TrimsAndStoresCommentAndQuantity()
        {
            var recipe = NewRecipe();
            var q = Quantity.Create(250, QuantityUnit.Gram).Value;
            var result = recipe.AddItem("Flour", q, "  sifted  ");
            Assert.True(result.IsSuccess);
            Assert.Equal("Flour", result.Value.Text);
            Assert.Equal("sifted", result.Value.Comment);
            Assert.Equal(250m, result.Value.QuantityValue);
            Assert.Single(recipe.Items);
        }

        [Fact]
        public void AddItem_EmptyComment_StoresNull()
        {
            var recipe = NewRecipe();
            var result = recipe.AddItem("Flour", null, "   ");
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddItem_OverlongText_FailsWithTextProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.AddItem(new string('x', RecipeItem.TextMaxLength + 1), null, null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeItem.Text));
        }

        [Fact]
        public void UpdateItem_AllFieldsNull_RejectedAsNoOp()
        {
            var recipe = NewRecipe();
            var added = recipe.AddItem("Flour", null, null).Value;
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: null);
            Assert.True(result.IsFailed);
        }

        [Fact]
        public void UpdateItem_CommentOnly_Succeeds()
        {
            var recipe = NewRecipe();
            var added = recipe.AddItem("Flour", null, null).Value;
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: "room temp");
            Assert.True(result.IsSuccess);
            Assert.Equal("room temp", result.Value.Comment);
        }

        [Fact]
        public void UpdateItem_EmptyComment_Clears()
        {
            var recipe = NewRecipe();
            var added = recipe.AddItem("Flour", null, "note").Value;
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: "");
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void ApplyExtractedQuantity_RewritesNameAndQuantity()
        {
            var recipe = NewRecipe();
            var added = recipe.AddItem("250g flour", null, null).Value;
            var q = Quantity.Create(250, QuantityUnit.Gram).Value;
            var result = recipe.ApplyExtractedQuantity(added.Id, "Flour", q);
            Assert.True(result.IsSuccess);
            Assert.Equal("Flour", added.Text);
            Assert.Equal(250m, added.QuantityValue);
        }

        [Fact]
        public void ReorderItem_ToTop_PlacesBeforeFirst()
        {
            var recipe = NewRecipe();
            var a = recipe.AddItem("A", null, null).Value;
            var b = recipe.AddItem("B", null, null).Value;
            var result = recipe.ReorderItem(b.Id, afterItemId: 0);
            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(b.Rank, a.Rank) < 0);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAggregateTests"`
Expected: FAIL — `Recipe` does not exist / does not compile.

- [ ] **Step 3: Write the `Recipe` aggregate** (mirror `Inventory.cs`; rename Inventory→Recipe, `InventoryItems`→`Items`, drop `expiryDate`, thread `comment` through `AddItem`/`UpdateItem`, add `ApplyExtractedQuantity` from `List.cs`, add comment validators from `List.cs`):

```csharp
using FluentResults;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Entities
{
    public class Recipe
    {
        public const int NameMaxLength = 255;
        public const int DescriptionMaxLength = 1000;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int HouseholdId { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public Household Household { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public ICollection<RecipeItem> Items { get; set; } = new List<RecipeItem>();

        public static Result<Recipe> Create(string name, string? description, int householdId, string createdByUserId)
        {
            var errors = ValidateMetadata(name, description);
            if (householdId <= 0)
            {
                errors.Add(new Error("Household id is required.").WithMetadata("Property", nameof(HouseholdId)));
            }
            if (string.IsNullOrWhiteSpace(createdByUserId))
            {
                errors.Add(new Error("Creator user id is required.").WithMetadata("Property", nameof(CreatedByUserId)));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<Recipe>(errors);
            }

            var now = DateTime.UtcNow;
            return Result.Ok(new Recipe
            {
                Name = name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                HouseholdId = householdId,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            });
        }

        public bool CanBeManagedBy(string callerUserId, HouseholdRole callerRole)
        {
            return CreatedByUserId == callerUserId || callerRole >= HouseholdRole.Admin;
        }

        public Result Update(string callerUserId, HouseholdRole callerRole, string name, string? description)
        {
            if (!CanBeManagedBy(callerUserId, callerRole))
            {
                return Result.Fail(new AccessDeniedError("Only the recipe creator or an admin can edit this recipe."));
            }

            var errors = ValidateMetadata(name, description);
            if (errors.Count > 0)
            {
                return Result.Fail(errors);
            }

            Name = name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        public Result SoftDelete(string callerUserId, HouseholdRole callerRole)
        {
            if (!CanBeManagedBy(callerUserId, callerRole))
            {
                return Result.Fail(new AccessDeniedError("Only the recipe creator or an admin can delete this recipe."));
            }

            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // ---- RecipeItem coordination (collaborative — any member; no role gate) ----

        public Result<RecipeItem> AddItem(string text, Quantity? quantity, string? comment)
        {
            var errors = ValidateItemText(text, requireText: true);
            errors.AddRange(ValidateComment(comment));
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new RecipeItem
            {
                RecipeId = Id,
                Text = text.Trim(),
                Comment = NormalizeComment(comment),
                QuantityValue = quantity?.Value,
                QuantityUnit = quantity?.Unit,
                Rank = ComputeAppendRank(),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            Items.Add(item);
            return Result.Ok(item);
        }

        public Result<RecipeItem> UpdateItem(int itemId, string? text, Quantity? quantity, bool clearQuantity, string? comment)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }

            if (text is null && quantity is null && !clearQuantity && comment is null)
            {
                return Result.Fail<RecipeItem>(
                    new Error("Update request must set at least one field.").WithMetadata("Property", string.Empty));
            }

            var errors = ValidateItemText(text, requireText: text is not null);
            if (comment is not null)
            {
                errors.AddRange(ValidateComment(comment));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeItem>(errors);
            }

            if (text is not null)
            {
                item.Text = text.Trim();
            }
            if (clearQuantity)
            {
                item.QuantityValue = null;
                item.QuantityUnit = null;
            }
            else if (quantity is not null)
            {
                item.QuantityValue = quantity.Value.Value;
                item.QuantityUnit = quantity.Value.Unit;
            }
            if (comment is not null)
            {
                item.Comment = NormalizeComment(comment);
            }

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result RemoveItem(int itemId)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        public Result<RecipeItem> RestoreItem(int itemId)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && !i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }
            item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result<RecipeItem> ReplaceRestoredItemRank(int itemId)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }
            item.Rank = ComputeAppendRank();
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        public Result<RecipeItem> ReorderItem(int itemId, int afterItemId)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }
            if (afterItemId == itemId)
            {
                return Result.Ok(item);
            }

            var section = Items
                .Where(i => i.IsActive && i.Id != item.Id)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();

            var afterItem = afterItemId == 0 ? null : section.FirstOrDefault(i => i.Id == afterItemId);
            var beforeItem = afterItem is not null
                ? section.FirstOrDefault(i => string.CompareOrdinal(i.Rank, afterItem.Rank) > 0)
                : null;

            string newRank;
            if (afterItem is null)
            {
                newRank = section.Count == 0
                    ? FractionalIndex.GenerateKeyBetween(null, null)
                    : FractionalIndex.GenerateKeyBetween(null, section[0].Rank);
            }
            else if (beforeItem is null)
            {
                newRank = FractionalIndex.GenerateKeyBetween(afterItem.Rank, null);
            }
            else
            {
                newRank = FractionalIndex.GenerateKeyBetween(afterItem.Rank, beforeItem.Rank);
            }

            item.Rank = newRank;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        // Applied by the recipe quantity-extraction job: rewrite text to the clean name + set/clear
        // quantity. Skips the write (and the UpdatedAt bump that would move the revision token) when
        // nothing changed. Mirrors List.ApplyExtractedQuantity.
        public Result<RecipeItem> ApplyExtractedQuantity(int itemId, string cleanName, Quantity? quantity)
        {
            var item = Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<RecipeItem>(new EntityNotFoundError($"Recipe item {itemId} not found."));
            }

            var errors = ValidateItemText(cleanName, requireText: true);
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeItem>(errors);
            }

            var trimmed = cleanName.Trim();
            var unchanged = item.Text == trimmed
                && item.QuantityValue == quantity?.Value
                && item.QuantityUnit == quantity?.Unit;
            if (unchanged)
            {
                return Result.Ok(item);
            }

            item.Text = trimmed;
            item.QuantityValue = quantity?.Value;
            item.QuantityUnit = quantity?.Unit;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        private static List<IError> ValidateMetadata(string name, string? description)
        {
            var errors = new List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Recipe name is required.").WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"Recipe name must be {NameMaxLength} characters or fewer.").WithMetadata("Property", nameof(Name)));
            }
            if (description is not null && description.Length > DescriptionMaxLength)
            {
                errors.Add(new Error($"Recipe description must be {DescriptionMaxLength} characters or fewer.").WithMetadata("Property", nameof(Description)));
            }
            return errors;
        }

        private static List<IError> ValidateItemText(string? text, bool requireText)
        {
            var errors = new List<IError>();
            if (requireText)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    errors.Add(new Error("Item text is required.").WithMetadata("Property", nameof(RecipeItem.Text)));
                }
                else if (text!.Trim().Length > RecipeItem.TextMaxLength)
                {
                    errors.Add(new Error($"Item text must be {RecipeItem.TextMaxLength} characters or fewer.").WithMetadata("Property", nameof(RecipeItem.Text)));
                }
            }
            return errors;
        }

        private static List<IError> ValidateComment(string? comment)
        {
            var errors = new List<IError>();
            var trimmed = NormalizeComment(comment);
            if (trimmed is not null && trimmed.Length > RecipeItem.CommentMaxLength)
            {
                errors.Add(new Error($"Item comment must be {RecipeItem.CommentMaxLength} characters or fewer.").WithMetadata("Property", nameof(RecipeItem.Comment)));
            }
            return errors;
        }

        private static string? NormalizeComment(string? comment)
            => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        private string ComputeAppendRank()
        {
            var section = Items
                .Where(i => i.IsActive)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .ToList();
            return section.Count == 0
                ? FractionalIndex.GenerateKeyBetween(null, null)
                : FractionalIndex.GenerateKeyBetween(section[^1].Rank, null);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAggregateTests"`
Expected: PASS (all). Confirm the matched count is the number of `[Fact]`s above (per the Reqnroll/xUnit filter caveat — verify the count, don't trust green alone).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Recipe.cs Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs
git commit -m "feat(recipes): add Recipe aggregate with item coordination + extraction apply"
```

---

## Task 3: EF configs, DbSets, timestamp stamping, migration — ✅ DONE (`9b0a5a8`)

**Files:**
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeConfiguration.cs`, `RecipeItemConfiguration.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`

- [ ] **Step 1: Write `RecipeConfiguration.cs`** (mirror `InventoryConfiguration`):

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
    {
        public void Configure(EntityTypeBuilder<Recipe> builder)
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedOnAdd();
            builder.Property(r => r.Name).HasMaxLength(Recipe.NameMaxLength).IsRequired();
            builder.Property(r => r.Description).HasMaxLength(Recipe.DescriptionMaxLength);
            builder.Property(r => r.HouseholdId).IsRequired();
            builder.Property(r => r.CreatedByUserId).HasMaxLength(128).IsRequired();
            builder.Property(r => r.CreatedAt).IsRequired();
            builder.Property(r => r.UpdatedAt).IsRequired();
            builder.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .HasPrincipalKey(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Restrict);

            // FK to Household so the DeleteInactiveItems hard-delete of a household cascades to its
            // recipes (matches how Inventory items get reaped). Household has no Recipes nav, so
            // WithMany() with no navigation.
            builder.HasOne(r => r.Household)
                .WithMany()
                .HasForeignKey(r => r.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => r.HouseholdId);
            builder.HasIndex(r => r.CreatedByUserId);
            builder.HasIndex(r => r.IsActive);
            builder.HasIndex(r => r.CreatedAt);
            builder.HasIndex(r => new { r.HouseholdId, r.IsActive });
        }
    }
}
```

> Note: `InventoryConfiguration` does NOT declare the `Household` FK explicitly (it's inferred). We declare it here with `OnDelete(DeleteBehavior.Cascade)` to guarantee the maintenance hard-delete of a soft-deleted household cascades to its recipes. Verify against `InventoryConfiguration`/`Inventory.Household` mapping during review — if Inventory relies on convention cascade, match whichever behavior actually drops inventory rows on household hard-delete.

- [ ] **Step 2: Write `RecipeItemConfiguration.cs`** (mirror `InventoryItemConfiguration`, drop `ExpiryDate` indexes, add `Comment`):

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
    {
        public void Configure(EntityTypeBuilder<RecipeItem> builder)
        {
            builder.HasKey(ri => ri.Id);
            builder.Property(ri => ri.Id).ValueGeneratedOnAdd();
            builder.Property(ri => ri.Text).HasMaxLength(RecipeItem.TextMaxLength).IsRequired();
            builder.Property(ri => ri.Comment).HasMaxLength(RecipeItem.CommentMaxLength);
            builder.Property(ri => ri.QuantityValue).HasColumnType("numeric(12,3)");
            builder.Property(ri => ri.QuantityUnit);
            builder.Property(ri => ri.Rank).HasColumnType("text").UseCollation("C").IsRequired();
            builder.Property(ri => ri.RecipeId).IsRequired();
            builder.Property(ri => ri.CreatedAt).IsRequired();
            builder.Property(ri => ri.UpdatedAt).IsRequired();
            builder.Property(ri => ri.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(ri => ri.Recipe)
                .WithMany(r => r.Items)
                .HasForeignKey(ri => ri.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(ri => ri.RecipeId);
            builder.HasIndex(ri => ri.IsActive);
            builder.HasIndex(ri => ri.CreatedAt);
            builder.HasIndex(ri => new { ri.RecipeId, ri.IsActive });
            builder.HasIndex(ri => new { ri.RecipeId, ri.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_RecipeItems_RecipeId_Rank_Active");
        }
    }
}
```

- [ ] **Step 3: Add DbSets + timestamp stamping in `ApplicationDbContext.cs`**

Add after the `DbSet<InventoryItem> InventoryItems` line:
```csharp
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeItem> RecipeItems { get; set; }
```

In `SaveChangesAsync`, add to the **Added** block (alongside the `Inventory`/`InventoryItem` cases):
```csharp
                    if (entry.Entity is Recipe recipe && recipe.CreatedAt == default)
                    {
                        recipe.CreatedAt = now;
                        recipe.UpdatedAt = now;
                    }

                    if (entry.Entity is RecipeItem recipeItem && recipeItem.CreatedAt == default)
                    {
                        recipeItem.CreatedAt = now;
                        recipeItem.UpdatedAt = now;
                    }
```

And to the **Modified** block:
```csharp
                    if (entry.Entity is Recipe recipe)
                    {
                        recipe.UpdatedAt = now;
                    }

                    if (entry.Entity is RecipeItem recipeItem)
                    {
                        recipeItem.UpdatedAt = now;
                    }
```

(`OnModelCreating` needs no edit — configs are auto-discovered via `ApplyConfigurationsFromAssembly`.)

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AddRecipes --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: a new migration under `Application/Frigorino.Infrastructure/.../Migrations/` creating `Recipes` + `RecipeItems` tables with the partial unique index `UX_RecipeItems_RecipeId_Rank_Active`. Open it and confirm: two `CreateTable`s, the FK cascades, the filtered unique index. No changes to other tables.

- [ ] **Step 5: Build to verify**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure Application/Frigorino.Web
git commit -m "feat(recipes): EF config, DbSets, timestamp stamping, AddRecipes migration"
```

---

## Task 4: Recipe-specific extraction (no-classify) — trigger, job, DI (TDD) — ✅ DONE (`15c8616`)

This is the genuinely-new backend piece. It reuses `ItemTextRouter`, `IQuantityExtractor`, `IBackgroundTaskQueue` but **omits the `IProductClassificationTrigger.OnProductReferenced` chain** so no `Product` rows accrue.

**Files:**
- Create: `Application/Frigorino.Domain/Interfaces/IRecipeQuantityExtractionTrigger.cs`, `IExtractRecipeQuantityJob.cs`
- Create: `Application/Frigorino.Infrastructure/Services/RecipeQuantityExtractionTrigger.cs`, `ExtractRecipeQuantityJob.cs`, `RecipeQuantityExtractionDependencyInjection.cs`
- Test: `Application/Frigorino.Test/Recipes/ExtractRecipeQuantityJobTests.cs`

- [ ] **Step 1: Write the interfaces**

`IRecipeQuantityExtractionTrigger.cs`:
```csharp
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Interfaces
{
    // Recipe analog of IQuantityExtractionTrigger. CRUCIAL DIFFERENCE: it never chains
    // classification — recipe items must NOT create Product rows (MVP decision). NeedsExtraction
    // enqueues the recipe extract job; SkipAi does nothing on either impl.
    public interface IRecipeQuantityExtractionTrigger
    {
        void OnItemRouted(int householdId, int recipeId, int itemId, ItemTextAnalysis analysis);
    }
}
```

`IExtractRecipeQuantityJob.cs`:
```csharp
namespace Frigorino.Domain.Interfaces
{
    public interface IExtractRecipeQuantityJob
    {
        Task Run(int householdId, int recipeId, int itemId, string rawText, CancellationToken ct);
    }
}
```

- [ ] **Step 2: Write the job test** (verifies extraction applies AND that classification is never invoked — the regression guard for the MVP "no classify" decision):

```csharp
using FakeItEasy;
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.Services;
using Frigorino.Test.Infrastructure; // TestApplicationDbContext factory
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Frigorino.Test.Recipes
{
    public class ExtractRecipeQuantityJobTests
    {
        [Fact]
        public async Task Run_AppliesQuantity_AndNeverClassifies()
        {
            using var db = TestApplicationDbContext.Create(); // mirror existing test ctx helper
            var recipe = Recipe.Create("Pie", null, 1, "u1").Value;
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            var item = recipe.AddItem("250g flour", null, null).Value;
            await db.SaveChangesAsync();

            var extractor = A.Fake<IQuantityExtractor>();
            A.CallTo(() => extractor.ExtractAsync("250g flour", A<CancellationToken>._))
                .Returns(Result.Ok(new QuantityExtraction("Flour", Quantity.Create(250, QuantityUnit.Gram).Value)));

            var classificationTrigger = A.Fake<IProductClassificationTrigger>();

            var job = new ExtractRecipeQuantityJob(db, extractor, NullLogger<ExtractRecipeQuantityJob>.Instance);
            await job.Run(1, recipe.Id, item.Id, "250g flour", CancellationToken.None);

            await db.Entry(item).ReloadAsync();
            Assert.Equal("Flour", item.Text);
            Assert.Equal(250m, item.QuantityValue);
            // The job has NO IProductClassificationTrigger dependency at all — assert by construction:
            A.CallTo(classificationTrigger).MustNotHaveHappened();
        }
    }
}
```

> If `TestApplicationDbContext.Create()` doesn't exist with that exact shape, use the existing test DbContext factory pattern in `Frigorino.Test` (EF InMemory) — match how `Inventory`/`List` job/aggregate tests build their context.

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExtractRecipeQuantityJobTests"`
Expected: FAIL — `ExtractRecipeQuantityJob` does not exist.

- [ ] **Step 4: Write the job** (copy `ExtractQuantityJob`, swap List→Recipe, **delete the `_classificationTrigger` field/ctor-param and the final `OnProductReferenced` call**):

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Recipe quantity extraction. Identical to ExtractQuantityJob EXCEPT it never chains
    // classification (no IProductClassificationTrigger) — recipe items must not create Product rows.
    public class ExtractRecipeQuantityJob : IExtractRecipeQuantityJob
    {
        private readonly ApplicationDbContext _db;
        private readonly IQuantityExtractor _extractor;
        private readonly ILogger<ExtractRecipeQuantityJob> _logger;

        public ExtractRecipeQuantityJob(
            ApplicationDbContext db,
            IQuantityExtractor extractor,
            ILogger<ExtractRecipeQuantityJob> logger)
        {
            _db = db;
            _extractor = extractor;
            _logger = logger;
        }

        public async Task Run(int householdId, int recipeId, int itemId, string rawText, CancellationToken ct)
        {
            var recipe = await _db.Recipes
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            var item = recipe?.Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (recipe is null || item is null)
            {
                return;
            }

            var expectedText = rawText.Trim();
            if (!string.Equals(item.Text, expectedText, StringComparison.Ordinal))
            {
                return;
            }

            var result = await _extractor.ExtractAsync(rawText, ct);
            if (result.IsFailed)
            {
                _logger.LogWarning(
                    "Recipe quantity extraction failed for item {ItemId} (household {HouseholdId}); dropping.",
                    itemId, householdId);
                return;
            }

            var currentText = await _db.RecipeItems
                .Where(i => i.Id == itemId && i.IsActive)
                .Select(i => i.Text)
                .FirstOrDefaultAsync(ct);
            if (currentText is null || !string.Equals(currentText, expectedText, StringComparison.Ordinal))
            {
                return;
            }

            var extraction = result.Value;
            var applied = recipe.ApplyExtractedQuantity(itemId, extraction.CleanName, extraction.Quantity);
            if (applied.IsFailed)
            {
                return;
            }

            await _db.SaveChangesAsync(ct);
            // NO classification chain here — deliberate (recipe MVP). See spec Decision 1.
        }
    }
}
```

- [ ] **Step 5: Write the triggers** (copy `QuantityExtractionTriggers`; the **null impl is a pure no-op** — it must NOT fall through to classification like the list null trigger does):

`RecipeQuantityExtractionTrigger.cs`:
```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    // Enabled path: NeedsExtraction enqueues the recipe extract job. No classification ever.
    public class QueueingRecipeQuantityExtractionTrigger : IRecipeQuantityExtractionTrigger
    {
        private readonly IBackgroundTaskQueue _queue;

        public QueueingRecipeQuantityExtractionTrigger(IBackgroundTaskQueue queue)
        {
            _queue = queue;
        }

        public void OnItemRouted(int householdId, int recipeId, int itemId, ItemTextAnalysis analysis)
        {
            if (analysis.Route == ItemTextRoute.NeedsExtraction)
            {
                _queue.TryEnqueue((sp, ct) =>
                    sp.GetRequiredService<IExtractRecipeQuantityJob>()
                      .Run(householdId, recipeId, itemId, analysis.CleanName, ct));
            }
        }
    }

    // Disabled path: extraction off. PURE no-op — unlike the list NullQuantityExtractionTrigger,
    // recipes never classify, so there is nothing to fall through to.
    public class NullRecipeQuantityExtractionTrigger : IRecipeQuantityExtractionTrigger
    {
        public void OnItemRouted(int householdId, int recipeId, int itemId, ItemTextAnalysis analysis)
        {
        }
    }
}
```

- [ ] **Step 6: Write the DI extension** (`RecipeQuantityExtractionDependencyInjection.cs`) — reuses the SAME `IQuantityExtractor` registered by `AddQuantityExtraction`, so this must run AFTER it:

```csharp
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class RecipeQuantityExtractionDependencyInjection
    {
        // Call AFTER AddQuantityExtraction — reuses the IQuantityExtractor it registers on the
        // enabled path. Gated on the same Ai:QuantityExtractor:Enabled + Ai:ApiKey flags.
        public static IServiceCollection AddRecipeQuantityExtraction(
            this IServiceCollection services, IConfiguration configuration)
        {
            var enabled = configuration.GetValue<bool>("Ai:QuantityExtractor:Enabled");
            var apiKey = configuration["Ai:ApiKey"];

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddScoped<IExtractRecipeQuantityJob, ExtractRecipeQuantityJob>();
                services.AddScoped<IRecipeQuantityExtractionTrigger, QueueingRecipeQuantityExtractionTrigger>();
            }
            else
            {
                services.AddScoped<IRecipeQuantityExtractionTrigger, NullRecipeQuantityExtractionTrigger>();
            }

            return services;
        }
    }
}
```

- [ ] **Step 7: Register in `Program.cs`** — add immediately after the `AddQuantityExtraction` line:
```csharp
builder.Services.AddRecipeQuantityExtraction(builder.Configuration);
```

- [ ] **Step 8: Run the job test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExtractRecipeQuantityJobTests"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Domain/Interfaces Application/Frigorino.Infrastructure/Services Application/Frigorino.Web/Program.cs Application/Frigorino.Test/Recipes/ExtractRecipeQuantityJobTests.cs
git commit -m "feat(recipes): no-classify quantity extraction trigger + job + DI"
```

---

## Task 5: Recipe CRUD slices + RecipeResponse — ✅ DONE (`e0e9f5b`)

**Files:**
- Create: `Application/Frigorino.Features/Recipes/RecipeResponse.cs`, `CreateRecipe.cs`, `GetRecipe.cs`, `GetRecipes.cs`, `UpdateRecipe.cs`, `DeleteRecipe.cs`, `GetRecipeRevision.cs`

- [ ] **Step 1: `RecipeResponse.cs`** (mirror `InventoryResponse`, drop expiry fields, add `ItemCount`):

```csharp
using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes
{
    public sealed record RecipeResponse(
        int Id,
        string Name,
        string? Description,
        int HouseholdId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        RecipeCreatorResponse CreatedByUser,
        int ItemCount)
    {
        public static RecipeResponse From(Recipe recipe, User creator, int itemCount)
            => new(recipe.Id, recipe.Name, recipe.Description, recipe.HouseholdId,
                   recipe.CreatedAt, recipe.UpdatedAt,
                   new RecipeCreatorResponse(creator.ExternalId, creator.Name, creator.Email), itemCount);

        public static readonly Expression<Func<Recipe, RecipeResponse>> ToProjection = r => new RecipeResponse(
            r.Id, r.Name, r.Description, r.HouseholdId, r.CreatedAt, r.UpdatedAt,
            new RecipeCreatorResponse(r.CreatedByUser.ExternalId, r.CreatedByUser.Name, r.CreatedByUser.Email),
            r.Items.Count(x => x.IsActive));
    }

    public sealed record RecipeCreatorResponse(string ExternalId, string Name, string? Email);
}
```

- [ ] **Step 2: `CreateRecipe.cs`** (mirror `CreateInventory`):

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Recipes
{
    public sealed record CreateRecipeRequest(string Name, string? Description);

    public static class CreateRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipe(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            CreateRecipeRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipWithUserAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var creator = membership.User;
            var creation = Recipe.Create(request.Name, request.Description, householdId, currentUser.UserId);
            if (creation.IsFailed)
            {
                return creation.ToValidationProblem();
            }

            var recipe = creation.Value;
            recipe.CreatedByUser = creator;
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(ct);

            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipe.Id}",
                RecipeResponse.From(recipe, creator, itemCount: 0));
        }
    }
}
```

- [ ] **Step 3: `GetRecipe.cs` + `GetRecipes.cs`** (mirror `GetInventory`/`GetInventories`):

`GetRecipe.cs`:
```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes
{
    public static class GetRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipe(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{recipeId:int}", Handle)
               .WithName("GetRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeResponse>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var response = await db.Recipes
                .Where(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive)
                .Select(RecipeResponse.ToProjection)
                .FirstOrDefaultAsync(ct);

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
```

`GetRecipes.cs`:
```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes
{
    public static class GetRecipesEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipes(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetRecipes")
               .Produces<RecipeResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeResponse[]>, NotFound>> Handle(
            int householdId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var response = await db.Recipes
                .Where(r => r.HouseholdId == householdId && r.IsActive)
                .OrderByDescending(r => r.CreatedAt)
                .Select(RecipeResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
```

- [ ] **Step 4: `UpdateRecipe.cs` + `DeleteRecipe.cs`** (use `FindActiveMembershipAsync` then resolve role for the aggregate's `CanBeManagedBy`; mirror `UpdateInventory`/`DeleteInventory` — open them to copy the exact role-resolution + creator-reload shape). `UpdateRecipe` must return the refreshed `RecipeResponse.From(recipe, creator, itemCount)`; `DeleteRecipe` calls `recipe.SoftDelete(...)`. Dispatch errors: `AccessDeniedError` → `TypedResults.Forbid()` (match how `UpdateInventory` maps it), `EntityNotFoundError` → 404, else `ToValidationProblem()`.

```csharp
// UpdateRecipe.cs
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes
{
    public sealed record UpdateRecipeRequest(string Name, string? Description);

    public static class UpdateRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateRecipe(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{recipeId:int}", Handle)
               .WithName("UpdateRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status403Forbidden)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeResponse>, ForbidHttpResult, NotFound, ValidationProblem>> Handle(
            int householdId, int recipeId, UpdateRecipeRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipWithUserAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Include(r => r.CreatedByUser)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.Update(currentUser.UserId, membership.Role, request.Name, request.Description);
            if (result.IsFailed)
            {
                if (result.Errors[0] is AccessDeniedError) return TypedResults.Forbid();
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            var itemCount = await db.RecipeItems.CountAsync(i => i.RecipeId == recipeId && i.IsActive, ct);
            return TypedResults.Ok(RecipeResponse.From(recipe, recipe.CreatedByUser, itemCount));
        }
    }
}
```

```csharp
// DeleteRecipe.cs
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes
{
    public static class DeleteRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteRecipe(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{recipeId:int}", Handle)
               .WithName("DeleteRecipe")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status403Forbidden)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, ForbidHttpResult, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.SoftDelete(currentUser.UserId, membership.Role);
            if (result.IsFailed)
            {
                if (result.Errors[0] is AccessDeniedError) return TypedResults.Forbid();
                return TypedResults.NotFound();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
```

> Verify `membership.Role` is the correct property name and that `FindActiveMembershipAsync` returns a membership carrying the role — match `UpdateInventory.cs` exactly (it does the same role-gated update). Adjust if the helper differs.

- [ ] **Step 5: `GetRecipeRevision.cs`** (mirror `GetInventoryRevision`):

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Sync;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes
{
    public static class GetRecipeRevisionEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeRevision(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{recipeId:int}/revision", Handle)
               .WithName("GetRecipeRevision")
               .Produces<RevisionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RevisionResponse>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Where(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive)
                .Select(r => new { r.UpdatedAt })
                .FirstOrDefaultAsync(ct);
            if (recipe is null) return TypedResults.NotFound();

            var items = db.RecipeItems.Where(i => i.RecipeId == recipeId && i.IsActive);
            var maxUpdatedAt = await items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
            var count = await items.CountAsync(ct);

            return TypedResults.Ok(RevisionResponse.Compute(recipe.UpdatedAt, maxUpdatedAt, count));
        }
    }
}
```

- [ ] **Step 6: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Features/Recipes
git commit -m "feat(recipes): recipe CRUD slices + revision endpoint"
```

---

## Task 6: Recipe item slices + RecipeItemResponse — ✅ DONE (`cc5bba9`)

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Items/RecipeItemResponse.cs`, `CreateRecipeItem.cs`, `UpdateRecipeItem.cs`, `DeleteRecipeItem.cs`, `RestoreRecipeItem.cs`, `GetRecipeItems.cs`, `ReorderRecipeItem.cs`

- [ ] **Step 1: `RecipeItemResponse.cs`** (mirror `InventoryItemResponse`, drop expiry, add `Comment` + `ExtractionPending`; `ExtractionPending` is settable via `with` like `ListItemResponse`):

```csharp
using System.Linq.Expressions;
using Frigorino.Domain.Entities;
using Frigorino.Features.Quantities;

namespace Frigorino.Features.Recipes.Items
{
    public sealed record RecipeItemResponse(
        int Id,
        int RecipeId,
        string Text,
        string? Comment,
        QuantityDto? Quantity,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool ExtractionPending)
    {
        public static RecipeItemResponse From(RecipeItem item)
            => new(item.Id, item.RecipeId, item.Text, item.Comment,
                   item.QuantityValue == null ? null : new QuantityDto(item.QuantityValue.Value, item.QuantityUnit!.Value),
                   item.Rank, item.CreatedAt, item.UpdatedAt, ExtractionPending: false);

        public static readonly Expression<Func<RecipeItem, RecipeItemResponse>> ToProjection = i => new RecipeItemResponse(
            i.Id, i.RecipeId, i.Text, i.Comment,
            i.QuantityValue == null ? null : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.Rank, i.CreatedAt, i.UpdatedAt, false);
    }
}
```

- [ ] **Step 2: `GetRecipeItems.cs`** (mirror `GetInventoryItems` — open it to match the exact shape; orders by `Rank` ordinal):

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Items
{
    public static class GetRecipeItemsEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeItems(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetRecipeItems")
               .Produces<RecipeItemResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse[]>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipeExists = await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (!recipeExists) return TypedResults.NotFound();

            var items = await db.RecipeItems
                .Where(i => i.RecipeId == recipeId && i.IsActive)
                .OrderBy(i => i.Rank)
                .Select(RecipeItemResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(items);
        }
    }
}
```

> Confirm `GetInventoryItems` orders with the DB collation (the `Rank` column is `UseCollation("C")` so `OrderBy(i => i.Rank)` is ordinal in SQL). Match its exact ordering/membership pattern.

- [ ] **Step 3: `GetRecipeItem.cs`** — the extraction poll needs a single-item GET (the list side has `GetItem`). Add it:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Items
{
    public static class GetRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{itemId:int}", Handle)
               .WithName("GetRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse>, NotFound>> Handle(
            int householdId, int recipeId, int itemId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var item = await db.RecipeItems
                .Where(i => i.Id == itemId && i.RecipeId == recipeId && i.IsActive
                            && i.Recipe.HouseholdId == householdId && i.Recipe.IsActive)
                .Select(RecipeItemResponse.ToProjection)
                .FirstOrDefaultAsync(ct);

            return item is null ? TypedResults.NotFound() : TypedResults.Ok(item);
        }
    }
}
```

- [ ] **Step 4: `CreateRecipeItem.cs`** — copy `Lists/Items/CreateItem.cs` (the extraction path), swap List→Recipe and `IQuantityExtractionTrigger`→`IRecipeQuantityExtractionTrigger`. Recipe has no "re-order direct quantity" path, so drop the `request.Quantity is not null` branch; recipe-item create is always free-text → route + extract. Carries `Comment`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Items
{
    public sealed record CreateRecipeItemRequest(string Text, string? Comment);

    public static class CreateRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int recipeId,
            CreateRecipeItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IRecipeQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var analysis = ItemTextRouter.Analyze(request.Text);

            var outcome = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null)
                {
                    return new CreateOutcome(null, NotFound: true, Problem: null);
                }

                var result = recipe.AddItem(analysis.CleanName, quantity: null, request.Comment);
                if (result.IsFailed)
                {
                    return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                }

                await db.SaveChangesAsync(ct);

                var resp = RecipeItemResponse.From(result.Value) with
                {
                    ExtractionPending = analysis.Route == ItemTextRoute.NeedsExtraction,
                };
                return new CreateOutcome(resp, NotFound: false, Problem: null);
            });

            if (outcome.NotFound) return TypedResults.NotFound();
            if (outcome.Problem is not null) return outcome.Problem;

            var response = outcome.Response!;
            quantityTrigger.OnItemRouted(householdId, recipeId, response.Id, analysis);

            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipeId}/items/{response.Id}",
                response);
        }

        private sealed record CreateOutcome(RecipeItemResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
```

- [ ] **Step 5: `UpdateRecipeItem.cs`** — copy `Lists/Items/UpdateItem.cs`, swap List→Recipe, drop `Status`, keep the **re-route-on-text-change** behavior (spec Decision 2) with `IRecipeQuantityExtractionTrigger`:

```csharp
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Items
{
    public sealed record UpdateRecipeItemRequest(string? Text, QuantityDto? Quantity, bool? ClearQuantity, string? Comment);

    public static class UpdateRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{itemId:int}", Handle)
               .WithName("UpdateRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int recipeId,
            int itemId,
            UpdateRecipeItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IRecipeQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            Quantity? quantity = null;
            if (request.Quantity is not null)
            {
                var parsed = Quantity.Create(request.Quantity.Value, request.Quantity.Unit);
                if (parsed.IsFailed) return parsed.ToValidationProblem();
                quantity = parsed.Value;
            }

            var textChangedWithoutQuantityIntent =
                request.Text is not null && request.Quantity is null && request.ClearQuantity != true;
            ItemTextAnalysis? analysis = textChangedWithoutQuantityIntent ? ItemTextRouter.Analyze(request.Text) : null;

            var recipe = await db.Recipes
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.UpdateItem(itemId, request.Text, quantity, request.ClearQuantity ?? false, request.Comment);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            if (analysis is ItemTextAnalysis routed)
            {
                quantityTrigger.OnItemRouted(householdId, recipeId, itemId, routed);
            }

            return TypedResults.Ok(RecipeItemResponse.From(result.Value));
        }
    }
}
```

> Note: unlike list `UpdateItem`, recipe update has no status-flip rank re-mint, so no `RankRetry` wrap is needed (matches `UpdateInventoryItem`, which also doesn't wrap). The extraction enqueue fires only after the save commits.

- [ ] **Step 6: `DeleteRecipeItem.cs`, `RestoreRecipeItem.cs`, `ReorderRecipeItem.cs`** — copy `Inventories/Items/` equivalents verbatim, swapping Inventory→Recipe, `inventory.InventoryItems`→`recipe.Items`, and the route param `inventoryId`→`recipeId`. `Reorder`/`Restore` keep the `RankRetry` + `ReplaceRestoredItemRank` pattern. They use the shared `Frigorino.Features.Items.ReorderItemRequest`. Example — `ReorderRecipeItem.cs`:

```csharp
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Items
{
    public static class ReorderRecipeItemEndpoint
    {
        public static IEndpointRouteBuilder MapReorderRecipeItem(this IEndpointRouteBuilder app)
        {
            app.MapPatch("/{itemId:int}/reorder", Handle)
               .WithName("ReorderRecipeItem")
               .Produces<RecipeItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeItemResponse>, NotFound>> Handle(
            int householdId, int recipeId, int itemId, ReorderItemRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var recipe = await db.Recipes
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeItemResponse?)null;

                var result = recipe.ReorderItem(itemId, request.AfterId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException($"ReorderRecipeItem cannot map error of type {result.Errors[0].GetType().Name}.");
                }
                await db.SaveChangesAsync(ct);
                return RecipeItemResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
```

`DeleteRecipeItem.cs` (mirror `DeleteInventoryItem` — confirm whether it soft-deletes via `recipe.RemoveItem` with `Include(r => r.Items)` + `SaveChanges`, returning 204; copy that exact shape) and `RestoreRecipeItem.cs` (mirror `RestoreInventoryItem` verbatim with the `attemptedReplace` retry).

- [ ] **Step 7: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Items
git commit -m "feat(recipes): recipe item slices (create w/ extraction, update, delete, restore, get, reorder)"
```

---

## Task 7: Wire the MapGroups in Program.cs — ✅ DONE (`5a77ee9`)

**Files:**
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Add the recipe groups** after the inventory groups block:

```csharp
var recipes = app.MapGroup("/api/household/{householdId:int}/recipes")
    .RequireAuthorization()
    .WithTags("Recipes");
recipes.MapCreateRecipe();
recipes.MapGetRecipes();
recipes.MapGetRecipe();
recipes.MapGetRecipeRevision();
recipes.MapUpdateRecipe();
recipes.MapDeleteRecipe();

var recipeItems = app.MapGroup("/api/household/{householdId:int}/recipes/{recipeId:int}/items")
    .RequireAuthorization()
    .WithTags("RecipeItems");
recipeItems.MapGetRecipeItems();
recipeItems.MapGetRecipeItem();
recipeItems.MapCreateRecipeItem();
recipeItems.MapUpdateRecipeItem();
recipeItems.MapDeleteRecipeItem();
recipeItems.MapRestoreRecipeItem();
recipeItems.MapReorderRecipeItem();
```

Add the matching `using Frigorino.Features.Recipes;` and `using Frigorino.Features.Recipes.Items;` at the top if the slice extension methods aren't found.

- [ ] **Step 2: Build + run the app to confirm the OpenAPI document generates**

Run: `dotnet build Application/Frigorino.Web`
Expected: PASS, and `ClientApp/src/lib/openapi.json` is regenerated by the build target with `Recipes`/`RecipeItems` paths.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/Program.cs Application/Frigorino.Web/ClientApp/src/lib/openapi.json
git commit -m "feat(recipes): wire recipe + recipe-item endpoint groups"
```

---

## Task 8: Add recipes to the maintenance purge (TDD) — ✅ DONE (`05a1026`; SQLite test dropped — see status header)

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`
- Test: extend the existing `DeleteInactiveItems` test (find it under `Frigorino.Test`; if none exists, add `Application/Frigorino.Test/Maintenance/DeleteInactiveItemsTests.cs`)

- [ ] **Step 1: Write/extend the failing test** — assert a soft-deleted recipe and soft-deleted recipe item are purged:

```csharp
[Fact]
public async Task Run_PurgesInactiveRecipesAndItems()
{
    using var db = TestApplicationDbContext.Create();
    var recipe = Recipe.Create("Old", null, 1, "u1").Value;
    recipe.IsActive = false;
    db.Recipes.Add(recipe);
    await db.SaveChangesAsync();

    await new DeleteInactiveItems(db).Run(CancellationToken.None);

    Assert.False(await db.Recipes.AnyAsync(r => r.Id == recipe.Id));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~DeleteInactiveItems"`
Expected: FAIL (recipe not purged).

- [ ] **Step 3: Add the purge lines** in `DeleteInactiveItems.Run`, alongside the `InventoryItems` purge:

```csharp
            await _dbContext.Recipes.Where(r => !r.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.RecipeItems.Where(ri => !ri.IsActive).ExecuteDeleteAsync(cancellationToken);
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~DeleteInactiveItems"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs Application/Frigorino.Test
git commit -m "feat(recipes): purge soft-deleted recipes in maintenance task"
```

---

## Task 9: Regenerate the API client — ✅ DONE (`1e27bff`)

**Files:**
- Generated under `Application/Frigorino.Web/ClientApp/src/lib/api/` (committed, do not hand-edit)

- [ ] **Step 1: Regenerate**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run api
```
Expected: rebuilds backend, emits `openapi.json`, regenerates the TS client. New symbols appear: `createRecipeMutation`, `getRecipesOptions`, `getRecipeOptions`, `getRecipeRevisionOptions`, `getRecipeItemsOptions`, `createRecipeItemMutation`, `reorderRecipeItemMutation`, `getRecipeItemOptions`, plus `RecipeResponse` / `RecipeItemResponse` / request types in `types.gen.ts`.

- [ ] **Step 2: Type-check**

Run: `npm run tsc`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib
git commit -m "feat(recipes): regenerate API client"
```

---

## Task 10: Recipe collection + aggregate hooks

**Files:**
- Create: `features/recipes/useHouseholdRecipes.ts`, `useRecipe.ts`, `useCreateRecipe.ts`, `useUpdateRecipe.ts`, `useDeleteRecipe.ts`

- [ ] **Step 1: Write the hooks** (mirror the inventory hooks exactly, renaming inventory→recipe):

`useHouseholdRecipes.ts`:
```ts
import { useQuery } from "@tanstack/react-query";
import { getRecipesOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useHouseholdRecipes = (householdId: number, enabled = true) =>
    useQuery({
        ...getRecipesOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
        refetchOnMount: "always",
    });
```

`useRecipe.ts`:
```ts
import { useQuery } from "@tanstack/react-query";
import { getRecipeOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useRecipe = (householdId: number, recipeId: number, enabled = true) =>
    useQuery({
        ...getRecipeOptions({ path: { householdId, recipeId } }),
        enabled: enabled && recipeId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
```

`useCreateRecipe.ts`:
```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createRecipeMutation, getRecipesQueryKey } from "../../lib/api/@tanstack/react-query.gen";

export const useCreateRecipe = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...createRecipeMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({ path: { householdId: variables.path.householdId } }),
            });
        },
    });
};
```

`useUpdateRecipe.ts` (mirror `useUpdateInventory` — invalidate both the single + collection keys):
```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeQueryKey,
    getRecipesQueryKey,
    updateRecipeMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateRecipe = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...updateRecipeMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeQueryKey({
                    path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({ path: { householdId: variables.path.householdId } }),
            });
        },
    });
};
```

`useDeleteRecipe.ts` (mirror `useDeleteInventory`):
```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    deleteRecipeMutation,
    getRecipeQueryKey,
    getRecipesQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useDeleteRecipe = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...deleteRecipeMutation(),
        onSuccess: (_data, variables) => {
            queryClient.removeQueries({
                queryKey: getRecipeQueryKey({
                    path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({ path: { householdId: variables.path.householdId } }),
            });
        },
    });
};
```

- [ ] **Step 2: Type-check**

Run (from `ClientApp/`): `npm run tsc`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/use*.ts
git commit -m "feat(recipes): recipe collection + aggregate query/mutation hooks"
```

---

## Task 11: Recipe item hooks (items, optimistic CRUD, revision, extraction poll)

**Files:**
- Create: `features/recipes/items/useRecipeItems.ts`, `useCreateRecipeItem.ts`, `useUpdateRecipeItem.ts`, `useDeleteRecipeItem.ts`, `useRestoreRecipeItem.ts`, `useReorderRecipeItem.ts`, `useRecipeRevision.ts`, `useRecipeExtractionPoll.ts`

- [ ] **Step 1: `useRecipeItems.ts`** (mirror `useInventoryItems`):
```ts
import { useQuery } from "@tanstack/react-query";
import { getRecipeItemsOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useRecipeItems = (householdId: number, recipeId: number, enabled = true) =>
    useQuery({
        ...getRecipeItemsOptions({ path: { householdId, recipeId } }),
        enabled: enabled && householdId > 0 && recipeId > 0,
        staleTime: 1000 * 30,
    });
```

- [ ] **Step 2: `useCreateRecipeItem.ts`** (mirror `useCreateInventoryItem`'s optimistic shape; the optimistic `RecipeItemResponse` omits expiry/isExpiring, adds `comment`/`extractionPending`):
```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    createRecipeItemMutation,
    getRecipeItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeItemResponse } from "../../../lib/api/types.gen";

export const useCreateRecipeItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...createRecipeItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeItemsQueryKey({
                path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousItems = queryClient.getQueryData<RecipeItemResponse[]>(queryKey);

            const optimisticItem: RecipeItemResponse = {
                id: Date.now(),
                recipeId: variables.path.recipeId,
                text: variables.body.text,
                comment: variables.body.comment ?? null,
                quantity: null,
                rank: "",
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
                extractionPending: false,
            };
            queryClient.setQueryData<RecipeItemResponse[]>(queryKey, (old) =>
                old ? [...old, optimisticItem] : [optimisticItem],
            );
            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getRecipeItemsQueryKey({
                        path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
                    }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_data, variables) => {
            debouncedInvalidate(
                getRecipeItemsQueryKey({
                    path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
                }),
            );
        },
    });
};
```

- [ ] **Step 3: `useUpdateRecipeItem.ts`** — mirror `useUpdateInventoryItem` (open it for the exact optimistic onMutate; apply `text`/`quantity`/`comment` with domain semantics: `comment == null ? old : (comment.trim() || null)`). Keep `onError` rollback + `onSettled` debounced invalidate on `getRecipeItemsQueryKey`.

- [ ] **Step 4: `useDeleteRecipeItem.ts` + `useRestoreRecipeItem.ts`** — mirror the inventory equivalents (optimistic remove / re-add, rollback, settle-invalidate).

- [ ] **Step 5: `useReorderRecipeItem.ts`** (mirror `useReorderInventoryItem` verbatim, swap inventory→recipe):
```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getRecipeItemsQueryKey,
    reorderRecipeItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeItemResponse } from "../../../lib/api/types.gen";

export const useReorderRecipeItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderRecipeItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeItemsQueryKey({
                path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousItems = queryClient.getQueryData<RecipeItemResponse[]>(queryKey);

            queryClient.setQueryData<RecipeItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const moved = old.find((i) => i.id === variables.path.itemId);
                if (!moved) return old;
                const others = old.filter((i) => i.id !== moved.id);
                const afterId = variables.body.afterId;
                if (!afterId) {
                    others.unshift(moved);
                    return others;
                }
                const anchorIdx = others.findIndex((i) => i.id === afterId);
                others.splice(anchorIdx === -1 ? others.length : anchorIdx + 1, 0, moved);
                return others;
            });
            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getRecipeItemsQueryKey({
                        path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
                    }),
                    context.previousItems,
                );
            }
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeItemsQueryKey({
                    path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
                }),
            );
        },
    });
};
```

- [ ] **Step 6: `useRecipeRevision.ts`** (mirror `useInventoryRevision`):
```ts
import { useQuery } from "@tanstack/react-query";
import {
    getRecipeItemsQueryKey,
    getRecipeRevisionOptions,
} from "../../../lib/api/@tanstack/react-query.gen";
import {
    REVISION_QUERY_OPTIONS,
    useRevisionInvalidation,
} from "../../../hooks/useRevisionInvalidation";

export const useRecipeRevision = (householdId: number, recipeId: number) => {
    const enabled = householdId > 0 && recipeId > 0;
    const { data } = useQuery({
        ...getRecipeRevisionOptions({ path: { householdId, recipeId } }),
        ...REVISION_QUERY_OPTIONS,
        enabled,
    });
    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getRecipeItemsQueryKey({ path: { householdId, recipeId } }),
        isLocalMutation: (variables) =>
            (variables as { path?: { recipeId?: number } } | undefined)?.path?.recipeId === recipeId,
    });
};
```

- [ ] **Step 7: `useRecipeExtractionPoll.ts`** (copy `features/lists/items/useExtractionPoll.ts`, swap list→recipe and `ListItemResponse`→`RecipeItemResponse`, `getItemOptions`→`getRecipeItemOptions`, `getItemsQueryKey`→`getRecipeItemsQueryKey`):
```ts
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import {
    getRecipeItemOptions,
    getRecipeItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeItemResponse } from "../../../lib/api/types.gen";

const MAX_POLL_MS = 4000;
const INTERVAL_MS = 600;

export const useRecipeExtractionPoll = (
    householdId: number,
    recipeId: number,
    itemId: number | null,
    enabled: boolean,
) => {
    const queryClient = useQueryClient();
    const [active, setActive] = useState(false);
    const pollable = enabled && (itemId ?? 0) > 0;

    useEffect(() => {
        if (!pollable) {
            // eslint-disable-next-line react-hooks/set-state-in-effect
            setActive(false);
            return;
        }
        setActive(true);
        const timer = setTimeout(() => setActive(false), MAX_POLL_MS);
        return () => clearTimeout(timer);
    }, [itemId, pollable]);

    const query = useQuery({
        ...getRecipeItemOptions({ path: { householdId, recipeId, itemId: itemId ?? 0 } }),
        enabled: active,
        refetchInterval: (q) => {
            const data = q.state.data as RecipeItemResponse | undefined;
            return data?.quantity ? false : INTERVAL_MS;
        },
        staleTime: 0,
        gcTime: 0,
    });

    useEffect(() => {
        const item = query.data;
        if (!item?.quantity) return;
        // eslint-disable-next-line react-hooks/set-state-in-effect
        setActive(false);
        queryClient.setQueryData<RecipeItemResponse[]>(
            getRecipeItemsQueryKey({ path: { householdId, recipeId } }),
            (old) =>
                old?.map((i) =>
                    i.id === item.id ? { ...i, text: item.text, quantity: item.quantity } : i,
                ) ?? old,
        );
    }, [query.data, householdId, recipeId, queryClient]);

    const isExtracting = active && !query.data?.quantity;
    return { isExtracting, extractingItemId: itemId };
};
```

- [ ] **Step 8: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/items/use*.ts
git commit -m "feat(recipes): recipe item hooks (items, optimistic CRUD, revision, extraction poll)"
```

---

## Task 12: Composer + item rendering components

**Files:**
- Create: `features/recipes/items/components/RecipeFooter.tsx`, `RecipeContainer.tsx`, `RecipeItemContent.tsx`

- [ ] **Step 1: `RecipeFooter.tsx`** — model on `ListFooter` (ADD vs EDIT split), but no `attach`. ADD = `[commentComposerFeature]`, EDIT = `[quantityComposerFeature, commentComposerFeature]`:

```tsx
import { Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    commentComposerFeature,
    draftToQuantity,
    formatQuantity,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
import { useItemComposer } from "../../../../hooks/useItemComposer";
import type { QuantityDto, RecipeItemResponse } from "../../../../lib/api";
import { featureContentPx } from "../../../../theme";

const EDIT_FEATURES = [quantityComposerFeature, commentComposerFeature] as const;
const ADD_FEATURES = [commentComposerFeature] as const;

interface RecipeFooterProps {
    editingItem: RecipeItemResponse | null;
    existingItems: RecipeItemResponse[];
    onAddItem: (text: string, comment: string | null) => void;
    onUpdateItem: (text: string, quantity: QuantityDto | null, comment: string | null) => void;
    onCancelEdit: () => void;
    isLoading: boolean;
    onScrollToLast: () => void;
}

export const RecipeFooter = memo(
    ({
        editingItem,
        existingItems,
        onAddItem,
        onUpdateItem,
        onCancelEdit,
        isLoading,
        onScrollToLast,
    }: RecipeFooterProps) => {
        const { t } = useTranslation();

        const onDuplicate = useCallback(
            (): DuplicateResult => ({ message: t("recipes.alreadyInRecipe"), tone: "warning" }),
            [t],
        );

        const getSecondaryLabel = useCallback(
            (item: RecipeItemResponse) => (item.quantity ? formatQuantity(t, item.quantity) : undefined),
            [t],
        );

        const { suggestions, duplicate } = useItemComposer({
            editingItem,
            existingItems,
            getBadge: () => undefined,
            getSecondaryLabel,
            onDuplicate,
        });

        const features = editingItem ? EDIT_FEATURES : ADD_FEATURES;

        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: {
                              quantity: quantityToDraft(editingItem.quantity),
                              comment: editingItem.comment ?? "",
                          },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: Completion<typeof ADD_FEATURES> | Completion<typeof EDIT_FEATURES>) => {
                const text = r as Completion<typeof EDIT_FEATURES>;
                if (text.mode === "edit") {
                    onUpdateItem(text.text, draftToQuantity(text.quantity), text.comment.trim());
                } else {
                    onAddItem(text.text, text.comment.trim() || null);
                    onScrollToLast();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLast],
        );

        return (
            <Container
                maxWidth="sm"
                sx={{
                    flexShrink: 0,
                    px: featureContentPx,
                    py: 1,
                    borderTop: 1,
                    borderColor: "divider",
                    bgcolor: "background.paper",
                }}
            >
                <Composer
                    key={editingItem?.id ?? "new"}
                    features={features}
                    disabled={isLoading}
                    editing={{ active: Boolean(editingItem), onCancel: onCancelEdit }}
                    initialDraft={initialDraft}
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

RecipeFooter.displayName = "RecipeFooter";
```

> `useItemComposer` requires a `getBadge`; recipes have no badge so it returns `undefined`. Confirm the `getBadge` prop accepts a no-badge function by checking the `useItemComposer` signature; if it's required-non-optional, pass `() => undefined` as shown.

- [ ] **Step 2: `RecipeContainer.tsx`** — model on `InventoryContainer` (open it). It renders the ordered item list with drag-to-reorder via `useReorderRecipeItem`, a comment preview (reuse `ListItemContent`'s comment-preview affordance from `RecipeItemContent`), tap-to-edit calling `onEdit(item)`, and an `isExtracting`/`extractingItemId` indicator passed down. Wire `useRecipeItems` + `useDeleteRecipeItem` + `useReorderRecipeItem`. Provide `data-testid="recipe-items"` on the list and `data-testid={`recipe-item-${item.id}`}` per row.

- [ ] **Step 3: `RecipeItemContent.tsx`** — model on `InventoryItemContent` + the list comment-preview: render `text`, a quantity chip (`formatQuantity`), and a muted comment preview line when `item.comment` is present (`data-testid={`recipe-item-comment-${item.id}`}`).

- [ ] **Step 4: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/items/components
git commit -m "feat(recipes): recipe composer footer, container, item content"
```

---

## Task 13: Pages

**Files:**
- Create: `features/recipes/pages/RecipesPage.tsx`, `RecipeViewPage.tsx`, `RecipeEditPage.tsx`, `CreateRecipePage.tsx`
- Create: `features/recipes/components/RecipeSummaryCard.tsx`, `RecipeActionsMenu.tsx`, `CreateRecipeForm.tsx`, `EditRecipeForm.tsx`, `DeleteRecipeConfirmDialog.tsx`

- [ ] **Step 1: `RecipesPage.tsx`** — mirror `InventoriesPage` (open it). Drop the calendar action. Uses `useHouseholdRecipes` + `useDeleteRecipe` + `RecipeSummaryCard` + `RecipeActionsMenu`. Navigate: create → `/recipes/create`, click → `/recipes/$recipeId/view`. Strings from `t("recipes.*")`.

- [ ] **Step 2: `RecipeViewPage.tsx`** — mirror `InventoryViewPage` but: no sort modes (single manual order), wire the extraction poll like `ListViewPage`:
  - `const [pendingExtraction, setPendingExtraction] = useState<{id:number; extractionPending:boolean}|null>(null);`
  - `const { isExtracting, extractingItemId } = useRecipeExtractionPoll(householdId, recipeId, pendingExtraction?.id ?? null, pendingExtraction?.extractionPending ?? false);`
  - In the add handler, after `createMutation.mutateAsync(...)` resolves to `created`, call `setPendingExtraction({ id: created.id, extractionPending: created.extractionPending })`.
  - Pass `isExtracting`/`extractingItemId` into `RecipeContainer`.
  - `useRecipeRevision(householdId, recipeId)` for sync.
  - `RecipeFooter` `onAddItem` → `createMutation` with `body: { text, comment }`; `onUpdateItem` → `updateMutation` with `body: { text, quantity, clearQuantity: quantity === null, comment }`.
  - Keep the search affordance if cheap; otherwise omit (not required by spec).

- [ ] **Step 3: `CreateRecipePage.tsx` + `CreateRecipeForm.tsx`** — mirror `CreateInventoryPage`/`CreateInventoryForm`: name + description fields, `useCreateRecipe`, navigate to `/recipes/$recipeId/view` on success.

- [ ] **Step 4: `RecipeEditPage.tsx` + `EditRecipeForm.tsx`** — mirror `InventoryEditPage`/`EditInventoryForm`: edit name/description via `useUpdateRecipe`, plus a delete affordance via `DeleteRecipeConfirmDialog` + `useDeleteRecipe`.

- [ ] **Step 5: `RecipeSummaryCard.tsx`, `RecipeActionsMenu.tsx`, `DeleteRecipeConfirmDialog.tsx`** — mirror the inventory components (card shows name, description, `itemCount` items; actions menu = edit/delete; confirm dialog = type-name-to-confirm or simple confirm, matching `DeleteInventoryConfirmDialog`).

- [ ] **Step 6: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/pages Application/Frigorino.Web/ClientApp/src/features/recipes/components
git commit -m "feat(recipes): recipe pages + summary card / forms / menus"
```

---

## Task 14: Routes

**Files:**
- Create: `routes/recipes/index.tsx`, `routes/recipes/create.tsx`, `routes/recipes/$recipeId/view.tsx`, `routes/recipes/$recipeId/edit.tsx`

- [ ] **Step 1: Write the route shells** (mirror the inventory routes — index + create wrap in `<RequireHousehold>`, view + edit do not):

`routes/recipes/index.tsx`:
```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { RecipesPage } from "../../features/recipes/pages/RecipesPage";

export const Route = createFileRoute("/recipes/")({
    beforeLoad: requireAuth,
    component: () => (
        <RequireHousehold>
            <RecipesPage />
        </RequireHousehold>
    ),
});
```

`routes/recipes/create.tsx`:
```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { CreateRecipePage } from "../../features/recipes/pages/CreateRecipePage";

export const Route = createFileRoute("/recipes/create")({
    beforeLoad: requireAuth,
    component: () => (
        <RequireHousehold>
            <CreateRecipePage />
        </RequireHousehold>
    ),
});
```

`routes/recipes/$recipeId/view.tsx`:
```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { RecipeViewPage } from "../../../features/recipes/pages/RecipeViewPage";

export const Route = createFileRoute("/recipes/$recipeId/view")({
    beforeLoad: requireAuth,
    component: RecipeViewPage,
});
```

`routes/recipes/$recipeId/edit.tsx`:
```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { RecipeEditPage } from "../../../features/recipes/pages/RecipeEditPage";

export const Route = createFileRoute("/recipes/$recipeId/edit")({
    beforeLoad: requireAuth,
    component: RecipeEditPage,
});
```

- [ ] **Step 2: Regenerate the route tree + type-check** (the router vite plugin regenerates `routeTree.gen.ts` on dev/build):

Run (from `ClientApp/`): `npm run tsc`
Expected: PASS (if `routeTree.gen.ts` is stale, run `npm run dev` briefly or `npm run build` to regenerate, then re-run tsc). The `RecipeViewPage`'s `useParams({ from: "/recipes/$recipeId/view" })` must type-check against the generated tree.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/routes/recipes Application/Frigorino.Web/ClientApp/src/routeTree.gen.ts
git commit -m "feat(recipes): recipe routes"
```

---

## Task 15: Dashboard wiring — replace the `rezepte` placeholder

**Files:**
- Modify: `components/dashboard/WelcomePage.tsx`

A `rezepte` placeholder collection already exists (non-expandable "coming soon"). Replace it with a real, navigable collection mirroring `inventar`.

- [ ] **Step 1: Add the data hook** near the inventories hook:
```tsx
import { useHouseholdRecipes } from "../../features/recipes/useHouseholdRecipes";
...
const { data: recipes = [], isLoading: recipesLoading } = useHouseholdRecipes(
    currentHousehold?.householdId || 0,
    !!currentHousehold?.householdId,
);
```

- [ ] **Step 2: Replace the `rezepte` collection object** (the "coming soon" placeholder) with a real one mirroring `inventar`:
```tsx
{
    id: "rezepte",
    label: t("dashboard.recipes"),
    icon: <RecipesIcon />,
    color: sectionColors.recipes,
    items: recipesLoading
        ? [{ name: t("common.loading"), count: "", status: "Loading", id: 0 }]
        : recipes.length > 0
          ? recipes.map((recipe) => ({
                name: recipe.name || t("recipes.untitledRecipe"),
                count: `${recipe.itemCount || 0} ${t("dashboard.items")}`,
                status: t("common.current"),
                id: recipe.id,
            }))
          : [{ name: t("recipes.noRecipesYet"), count: "", status: t("recipes.createFirstRecipe"), id: 0 }],
},
```

- [ ] **Step 3: Add `"rezepte"` branches** to the three special-cased switches/conditionals:
  - `handleAddItem` — replace the TODO stub with `navigate({ to: "/recipes/create" });`.
  - `isExpandable` — add `|| collection.id === "rezepte"`.
  - The "View all" navigate handler — add a `rezepte` → `/recipes` branch (match the inventar branch's navigate shape).
  - The per-item navigate handler — add a `rezepte` → `/recipes/$recipeId/view` branch (params `{ recipeId: id.toString() }`).

  (Open `WelcomePage.tsx` and find each `=== "einkaufslisten" || === "inventar"` site; add the recipes branch consistently.)

- [ ] **Step 4: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/dashboard/WelcomePage.tsx
git commit -m "feat(recipes): wire recipes collection into dashboard"
```

---

## Task 16: i18n

**Files:**
- Modify: `public/locales/en/translation.json`, `public/locales/de/translation.json`

- [ ] **Step 1: Add a `recipes` namespace to `en/translation.json`** (mirror the `inventory` block, recipe-flavored):
```json
"recipes": {
    "recipes": "Recipes",
    "createRecipe": "Create recipe",
    "createNewRecipe": "Create new recipe",
    "recipeName": "Recipe name",
    "recipeNameRequired": "Please enter a recipe name",
    "searchPlaceholder": "Search ingredients",
    "noRecipes": "No recipes yet. Create your first recipe!",
    "noRecipesYet": "No recipes yet",
    "createFirstRecipe": "Create your first recipe to get started",
    "createYourFirstRecipe": "Create your first recipe",
    "selectHouseholdToViewRecipes": "Select a household to see its recipes.",
    "failedToLoadRecipes": "We couldn't load your recipes. Please try again.",
    "loadingRecipe": "Loading recipe...",
    "failedToLoadRecipe": "We couldn't load this recipe. Please try again.",
    "untitledRecipe": "Untitled recipe",
    "editRecipe": "Edit recipe",
    "deleteRecipe": "Delete recipe",
    "alreadyInRecipe": "Already in this recipe",
    "confirmTypeRecipeName": "To confirm, type the recipe name:",
    "typeRecipeNameToConfirm": "Type \"{{recipeName}}\" to confirm"
}
```

- [ ] **Step 2: Confirm/add `dashboard.recipes`** — it already exists (placeholder used `t("dashboard.recipes")`). Leave it. (The `dashboard.comingSoon` / `dashboard.recipeManagementLater` keys are now unused by the recipe card — leave them; other code may reference them. Grep to confirm before removing; out of scope here.)

- [ ] **Step 3: Mirror the `recipes` block into `de/translation.json`** with German translations (e.g. `"recipes": "Rezepte"`, `"createRecipe": "Rezept erstellen"`, `"recipeName": "Rezeptname"`, `"untitledRecipe": "Unbenanntes Rezept"`, etc.). Keep keys identical to `en`.

- [ ] **Step 4: Verify JSON validity + type-check**

Run (from `ClientApp/`): `npm run tsc && npm run prettier`
Expected: PASS, JSON well-formed.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales
git commit -m "feat(recipes): i18n recipes namespace (en/de)"
```

---

## Task 17: Build the SPA (so integration testids exist)

- [ ] **Step 1: Build the SPA**

Run (from `ClientApp/`): `npm run build`
Expected: PASS — outputs to `ClientApp/build` (the IT harness serves this, not live source).

- [ ] **Step 2: Commit** (build output is gitignored; nothing to commit — this step just guarantees the integration test in Task 18 sees the new testids. If `npm run build` produced no tracked changes, skip the commit.)

---

## Task 18: End-to-end integration test

**Files:**
- Create: a Reqnroll feature + steps under `Application/Frigorino.IntegrationTests/` (mirror an existing inventory feature file)

- [ ] **Step 1: Write the scenario** — mirror an existing inventory `.feature` + step structure. Cover: create a recipe, open it, add an ingredient via the composer, assert the row appears (by `data-testid`), edit it, reorder (if a reorder step helper exists), delete the recipe. Assert via `data-testid` only (never translated text). Use the testids defined in Tasks 12–13 (`recipe-items`, `recipe-item-<id>`, `composer-comment`, `composer-quantity-value`, recipe edit/delete buttons).

- [ ] **Step 2: Run the integration tests**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Recipe"`
Expected: PASS. Verify the matched scenario count is what you wrote (Reqnroll filter caveat — confirm `gesamt: N`, don't trust green alone). Requires Docker running (Testcontainers Postgres) — if the daemon is unreachable, ask the user to start Docker Desktop.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.IntegrationTests
git commit -m "test(recipes): end-to-end recipe create/add/edit/delete scenario"
```

---

## Task 19: Final verification gate

- [ ] **Step 1: Full solution test run**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS (Frigorino.Test + Frigorino.IntegrationTests). Don't trust a piped exit code — read the pass/fail summary lines (capture `${PIPESTATUS[0]}` if piping). Requires Docker for the IT.

- [ ] **Step 2: Frontend gate**

Run (from `ClientApp/`): `npm run lint && npm run tsc && npm run prettier && npm run build`
Expected: all PASS.

- [ ] **Step 3: Docker build (drift check)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: PASS. (No Dockerfile edit expected — Recipes is new files in existing projects. If it fails on a missing project reference, update `Application/Dockerfile` accordingly.)

- [ ] **Step 4: Manual smoke (recommended)** — bring up the dev stack (`/dev-up`), create a recipe, type `"250g flour"`, confirm the quantity chip appears after extraction settles, confirm no `Product` row is created for it (the no-classify guarantee), reorder/edit/delete. This catches plan-baked DOM/runtime bugs that static checks miss.

- [ ] **Step 5: Final commit (if any verification fixes were needed)**

```bash
git add -A
git commit -m "chore(recipes): verification fixes"
```

---

## Self-Review (completed by plan author)

**Spec coverage:** Recipe aggregate (T2) ✓ · RecipeItem no-status/no-expiry/comment (T1) ✓ · EF + migration + timestamps + FK cascade (T3) ✓ · no-classify extraction job/trigger/DI (T4) ✓ · CRUD + revision slices (T5) ✓ · item slices incl. List-style create + re-route update (T6) ✓ · MapGroup wiring (T7) ✓ · DeleteInactiveItems purge (T8) ✓ · API regen (T9) ✓ · hooks (T10–11) ✓ · composer/container/content (T12) ✓ · pages (T13) ✓ · routes (T14) ✓ · dashboard replace-placeholder (T15) ✓ · i18n en/de (T16) ✓ · integration test (T18) ✓ · full gate (T19) ✓.

**Type consistency:** `Recipe.Items` (not `RecipeItems`) used throughout aggregate + slices + EF nav. `RecipeItem.TextMaxLength = 500`. `ApplyExtractedQuantity(int, string, Quantity?)` three-arg everywhere. `IRecipeQuantityExtractionTrigger.OnItemRouted(householdId, recipeId, itemId, analysis)` consistent between interface, impls, and both item slices. `RecipeItemResponse.ExtractionPending` set via `with` in create only. Frontend: `getRecipeItemsQueryKey` / `getRecipeRevisionOptions` / `getRecipeItemOptions` names match the generated client produced in T9.

**Open verification flags for the implementer** (called out inline, not placeholders): (a) the `Recipe`→`Household` FK `OnDelete` must match whatever actually cascades inventory rows on household hard-delete — confirm against `InventoryConfiguration`; (b) `membership.Role` property name + `FindActiveMembershipWithUserAsync` shape — confirm against `UpdateInventory.cs`; (c) `useItemComposer`'s `getBadge` optionality; (d) `DeleteRecipeItem`/`RestoreRecipeItem` exact bodies — copy from the inventory siblings.

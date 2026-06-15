# Recipe Attachments (Images) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let any household member attach images (dish photos, scanned cards) to a recipe — upload, caption, drag-reorder, soft-delete/undo — shown as a thumbnail grid on the view page and a sortable list on the edit page.

**Architecture:** A new flat `RecipeAttachment` entity (sibling of the shipped `RecipeLink`), its own aggregate methods on `Recipe`, eight vertical slices under a new `recipeAttachments` route group, and a new blob "area" (`recipe-attachment`) that plugs into the already-landed keyed-DI blob infrastructure + per-area orphan sweep. The frontend mirrors the source-links feature plus the list-item image/lightbox pattern.

**Tech Stack:** .NET 10 vertical slices (FluentResults + minimal APIs), EF Core / Postgres, keyed-DI `IFileStorage` + `MagickImageProcessor`, React 19 + TanStack Query/Router + MUI + dnd-kit, hey-api generated client. Spec: `docs/superpowers/specs/2026-06-15-recipe-attachments-design.md`.

**Prerequisite status (verified 2026-06-15):** The blob-area refactor and `feat(recipes): source links` are both merged to `stage`. This branch (`feat/recipe-attachments`) is cut from `stage`. Infra confirmed present: `BlobAreas` (only `ListItem` today), `IBlobReferenceSource` + `ListItemBlobReferences`, keyed `IFileStorage`/`IFileStorageMaintenance` per area, `ReclaimOrphanBlobs` iterating `IEnumerable<IBlobReferenceSource>`, `FileStorage:Environment` config.

**Two deviations from the spec wording (intentional, follow this plan):**
1. **Reorder verb:** the spec text says `PUT …/reorder`, but the sibling `ReorderRecipeLink` uses `MapPatch` + reuses `ReorderItemRequest`. We follow the sibling (PATCH) for codebase consistency — the generated `reorderRecipeAttachmentMutation` is identical either way.
2. **Lightbox:** the spec says "reuse `ImageLightbox`", but that component is hard-coded to `useItemImage` (list path). We add a sibling `RecipeAttachmentLightbox` mirroring it but using `useAttachmentImage` (clean separation, no risky refactor of a shipped component).

**Response DTO:** `RecipeAttachmentResponse` includes `createdAt`/`updatedAt` in addition to the spec's listed fields, matching `RecipeLinkResponse` for sibling consistency. Storage keys are never exposed.

---

## Execution phases

The 14 tasks run in three phases. Task numbers are stable (tasks cross-reference each other by number) — the phases group them, they don't renumber them. Each phase ends at a natural checkpoint where the work is independently reviewable.

- **Phase A — Backend (Tasks 1–8):** entity → aggregate methods (TDD) → EF config + migration → blob area + reference source → read slices → mutation slices → route wiring + revision + purge → regenerate the TS client. End state: the full API exists, is wired, and the generated client reflects it. Task 8 (regenerate client) is the seam — backend-driven, but it produces the typed surface the frontend consumes.
- **Phase B — Frontend (Tasks 9–12):** TanStack Query hooks → components + page wiring → i18n keys → SPA build + in-browser smoke verify. End state: the feature is usable in the UI and manually verified.
- **Phase C — Finals (Tasks 13–14):** integration tests (Reqnroll + Playwright + Testcontainers) → full verification gate (`dotnet test` on the solution + `docker build` + frontend lint/tsc/prettier). End state: shippable.

---

## File Structure

**Backend — create:**
- `Application/Frigorino.Domain/Entities/RecipeAttachment.cs` — flat entity + constants.
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeAttachmentConfiguration.cs` — EF mapping (mirror `RecipeLinkConfiguration`).
- `Application/Frigorino.Infrastructure/Tasks/RecipeAttachmentBlobReferences.cs` — `IBlobReferenceSource` for the new area.
- `Application/Frigorino.Features/Recipes/Attachments/RecipeAttachmentResponse.cs`
- `Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachments.cs`
- `Application/Frigorino.Features/Recipes/Attachments/CreateRecipeAttachment.cs`
- `Application/Frigorino.Features/Recipes/Attachments/UpdateRecipeAttachment.cs`
- `Application/Frigorino.Features/Recipes/Attachments/DeleteRecipeAttachment.cs`
- `Application/Frigorino.Features/Recipes/Attachments/RestoreRecipeAttachment.cs`
- `Application/Frigorino.Features/Recipes/Attachments/ReorderRecipeAttachment.cs`
- `Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachmentFile.cs`
- `Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachmentThumbnail.cs`
- EF migration `AddRecipeAttachments` (generated).

**Backend — modify:**
- `Application/Frigorino.Domain/Entities/Recipe.cs` — `Attachments` nav + six aggregate methods + helpers.
- `Application/Frigorino.Infrastructure/Services/BlobAreas.cs` — add `RecipeAttachment` constant.
- `Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs` — add area to `Areas[]` + register the reference source.
- `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs` — `DbSet<RecipeAttachment>`.
- `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs` — purge soft-deleted attachments.
- `Application/Frigorino.Features/Recipes/GetRecipeRevision.cs` — fold attachments into the token.
- `Application/Frigorino.Web/Program.cs` — register the `recipeAttachments` route group.

**Frontend — create (`ClientApp/src/`):**
- `features/recipes/attachments/useRecipeAttachments.ts`
- `features/recipes/attachments/useCreateRecipeAttachment.ts`
- `features/recipes/attachments/useUpdateRecipeAttachment.ts`
- `features/recipes/attachments/useDeleteRecipeAttachment.ts`
- `features/recipes/attachments/useRestoreRecipeAttachment.ts`
- `features/recipes/attachments/useReorderRecipeAttachment.ts`
- `features/recipes/attachments/useAttachmentImage.ts`
- `features/recipes/attachments/components/RecipeAttachmentRow.tsx`
- `features/recipes/attachments/components/RecipeAttachmentsSection.tsx`
- `features/recipes/attachments/components/RecipeAttachmentLightbox.tsx`
- `features/recipes/attachments/components/RecipeViewAttachments.tsx`

**Frontend — modify:**
- `features/recipes/items/useRecipeRevision.ts` — add the attachments-key invalidation.
- `features/recipes/pages/RecipeEditPage.tsx` — render `RecipeAttachmentsSection`.
- `features/recipes/pages/RecipeViewPage.tsx` — render `RecipeViewAttachments`.
- `public/locales/en/translation.json` + `public/locales/de/translation.json` — new `recipes.*` keys.
- `src/lib/api/**` — regenerated by `npm run api`.

**Tests:**
- `Application/Frigorino.Test/Recipes/RecipeAttachmentAggregateTests.cs` (create)
- `Application/Frigorino.Test/Infrastructure/RecipeAttachmentBlobReferencesTests.cs` (create — pure, no DB; mirrors the reference-source contract)
- `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeAttachmentApiSteps.cs` (create)
- `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature` (modify — add scenarios)
- `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` (modify — add attachment helpers)

---

# Phase A — Backend (Tasks 1–8)

Builds the entire API: entity, aggregate rules, persistence, blob plumbing, the eight slices, route wiring, and the regenerated client. Reviewable on its own — at the end the API is exercisable via Scalar/curl and the generated TS client compiles.

## Task 1: `RecipeAttachment` entity + constants

**Files:**
- Create: `Application/Frigorino.Domain/Entities/RecipeAttachment.cs`

- [ ] **Step 1: Write the entity**

Mirror `RecipeLink.cs` shape. Constants are this entity's own source of truth (the EF config and aggregate read them), mirroring `ListItem`'s media values.

```csharp
namespace Frigorino.Domain.Entities
{
    // An image attached to a recipe as source material (dish photo, scanned card). One kind of
    // attachment today (image), so no Type discriminator — every row has a generated thumbnail.
    // Ordering, validation, and lifecycle (add/update-caption/delete/restore/reorder) live on the
    // parent Recipe aggregate; this is a plain data holder. Sibling of RecipeLink.
    public class RecipeAttachment
    {
        // Media limits — own source of truth (mirrors ListItem's media values; no cross-aggregate coupling).
        public const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB
        public const int StorageKeyMaxLength = 200;
        public const int ContentTypeMaxLength = 255;
        public const int OriginalFileNameMaxLength = 255;
        public const int CaptionMaxLength = 255;

        // Accepted *input* content types (the slice pre-filter). Stored output is always image/webp.
        public static readonly string[] ImageContentTypes =
            ["image/jpeg", "image/png", "image/webp"];

        public int Id { get; set; }
        public int RecipeId { get; set; }

        public string StorageKey { get; set; } = string.Empty;       // full-res WebP blob key (required)
        public string? ThumbnailStorageKey { get; set; }             // nullable column, always set for images
        public string ContentType { get; set; } = string.Empty;      // stored output, always image/webp
        public string? OriginalFileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string? Caption { get; set; }

        // Lexicographic ordering key (fractional index), unique per RECIPE among active rows.
        public string Rank { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Recipe Recipe { get; set; } = null!;
    }
}
```

- [ ] **Step 2: Build the Domain project**

Run: `dotnet build Application/Frigorino.Domain`
Expected: PASS (no references to `Attachments` yet — that comes in Task 3).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Domain/Entities/RecipeAttachment.cs
git commit -m "feat(recipes): RecipeAttachment entity + constants"
```

---

## Task 2: Recipe aggregate — attachment methods (TDD)

**Files:**
- Test: `Application/Frigorino.Test/Recipes/RecipeAttachmentAggregateTests.cs`
- Modify: `Application/Frigorino.Domain/Entities/Recipe.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Recipes/RecipeAttachmentAggregateTests.cs`. The `NewRecipe()` helper mirrors `RecipeAggregateTests.cs` (Recipe.Create + an id). `ValidFile()` mirrors the `StoredFile` shape used by `ListAggregateMediaItemTests`.

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Files;
using Xunit;

namespace Frigorino.Test.Recipes
{
    public class RecipeAttachmentAggregateTests
    {
        private static Recipe NewRecipe()
        {
            var r = Recipe.Create("Apple Pie", null, householdId: 1, createdByUserId: "u1");
            Assert.True(r.IsSuccess);
            var recipe = r.Value;
            recipe.Id = 10;
            return recipe;
        }

        // Valid stored image: webp output + thumbnail present (what the processor produces).
        private static StoredFile ValidFile() =>
            new("full-key", "thumb-key", "image/webp", "photo.png", 2048);

        private static bool HasProperty(FluentResults.IResultBase result, string property) =>
            result.Errors.Exists(e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == property);

        [Fact]
        public void AddAttachment_ValidImage_SetsColumnsAndAppends()
        {
            var recipe = NewRecipe();

            var result = recipe.AddAttachment("front of dish", ValidFile());

            Assert.True(result.IsSuccess);
            var a = result.Value;
            Assert.Equal("full-key", a.StorageKey);
            Assert.Equal("thumb-key", a.ThumbnailStorageKey);
            Assert.Equal("image/webp", a.ContentType);
            Assert.Equal("photo.png", a.OriginalFileName);
            Assert.Equal(2048, a.FileSizeBytes);
            Assert.Equal("front of dish", a.Caption);
            Assert.True(a.IsActive);
            Assert.NotEmpty(a.Rank);
            Assert.Single(recipe.Attachments);
        }

        [Fact]
        public void AddAttachment_TrimsCaption_EmptyToNull()
        {
            var recipe = NewRecipe();
            Assert.Equal("hi", recipe.AddAttachment("  hi  ", ValidFile()).Value.Caption);
            Assert.Null(recipe.AddAttachment("   ", ValidFile()).Value.Caption);
        }

        [Fact]
        public void AddAttachment_MissingThumbnail_FailsKeyedOnThumbnail()
        {
            var recipe = NewRecipe();
            var noThumb = new StoredFile("full-key", null, "image/webp", "photo.png", 2048);

            var result = recipe.AddAttachment(null, noThumb);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.ThumbnailStorageKey)));
        }

        [Fact]
        public void AddAttachment_WrongStoredContentType_FailsKeyedOnContentType()
        {
            var recipe = NewRecipe();
            var jpeg = new StoredFile("full-key", "thumb-key", "image/jpeg", "photo.jpg", 2048);

            var result = recipe.AddAttachment(null, jpeg);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.ContentType)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(RecipeAttachment.MaxFileSizeBytes + 1)]
        public void AddAttachment_BadSize_FailsKeyedOnFileSize(long size)
        {
            var recipe = NewRecipe();
            var bad = new StoredFile("full-key", "thumb-key", "image/webp", "photo.png", size);

            var result = recipe.AddAttachment(null, bad);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.FileSizeBytes)));
        }

        [Fact]
        public void AddAttachment_MissingStorageKey_FailsKeyedOnStorageKey()
        {
            var recipe = NewRecipe();
            var noKey = new StoredFile("   ", "thumb-key", "image/webp", "photo.png", 2048);

            var result = recipe.AddAttachment(null, noKey);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.StorageKey)));
        }

        [Fact]
        public void AddAttachment_CaptionTooLong_FailsKeyedOnCaption()
        {
            var recipe = NewRecipe();
            var caption = new string('x', RecipeAttachment.CaptionMaxLength + 1);

            var result = recipe.AddAttachment(caption, ValidFile());

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.Caption)));
        }

        [Fact]
        public void UpdateAttachmentCaption_ChangesCaptionOnly()
        {
            var recipe = NewRecipe();
            var a = recipe.AddAttachment("old", ValidFile()).Value;
            a.Id = 1;

            var result = recipe.UpdateAttachmentCaption(1, "  new  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("new", recipe.Attachments.Single().Caption);
        }

        [Fact]
        public void UpdateAttachmentCaption_NotFound_Fails()
        {
            var recipe = NewRecipe();
            var result = recipe.UpdateAttachmentCaption(999, "x");
            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveThenRestoreAttachment_RoundTrips()
        {
            var recipe = NewRecipe();
            var a = recipe.AddAttachment(null, ValidFile()).Value;
            a.Id = 1;

            Assert.True(recipe.RemoveAttachment(1).IsSuccess);
            Assert.False(recipe.Attachments.Single().IsActive);

            Assert.True(recipe.RestoreAttachment(1).IsSuccess);
            Assert.True(recipe.Attachments.Single().IsActive);
        }

        [Fact]
        public void RestoreAttachment_RankCollision_DeCollides()
        {
            var recipe = NewRecipe();
            var first = recipe.AddAttachment(null, ValidFile()).Value;
            first.Id = 1;
            recipe.RemoveAttachment(1);
            // A new attachment takes the freed-up rank slot while #1 is deleted.
            var second = recipe.AddAttachment(null, ValidFile()).Value;
            second.Id = 2;
            second.Rank = first.Rank; // force the collision

            var result = recipe.RestoreAttachment(1);

            Assert.True(result.IsSuccess);
            var ranks = recipe.Attachments.Where(x => x.IsActive).Select(x => x.Rank).ToList();
            Assert.Equal(ranks.Count, ranks.Distinct().Count()); // no two active rows share a rank
        }

        [Fact]
        public void ReorderAttachment_ToTop_PlacesFirst()
        {
            var recipe = NewRecipe();
            var a = recipe.AddAttachment(null, ValidFile()).Value; a.Id = 1;
            var b = recipe.AddAttachment(null, ValidFile()).Value; b.Id = 2;

            var result = recipe.ReorderAttachment(2, afterAttachmentId: 0);

            Assert.True(result.IsSuccess);
            var ordered = recipe.Attachments.Where(x => x.IsActive)
                .OrderBy(x => x.Rank, StringComparer.Ordinal).Select(x => x.Id).ToList();
            Assert.Equal(2, ordered[0]);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAttachmentAggregateTests"`
Expected: FAIL — `Recipe` has no `Attachments` / `AddAttachment` etc. (compile error).

- [ ] **Step 3: Add the `Attachments` navigation to `Recipe`**

In `Application/Frigorino.Domain/Entities/Recipe.cs`, after the `Links` collection (line 27):

```csharp
        public ICollection<RecipeAttachment> Attachments { get; set; } = new List<RecipeAttachment>();
```

- [ ] **Step 4: Add the aggregate methods**

In `Recipe.cs`, after the `ReorderLink` method (line 582, before the `private static List<IError> ValidateSectionMetadata` block), add the `// ---- RecipeAttachment coordination ----` region. These mirror the link methods exactly, with image validation folded into `AddAttachment` and the rank-de-collision guard from `RestoreSection` adapted (no child rows, so just re-mint this row's rank on collision):

```csharp
        // ---- RecipeAttachment coordination (collaborative — any member; no role gate) ----

        public Result<RecipeAttachment> AddAttachment(string? caption, StoredFile file)
        {
            var errors = ValidateAttachmentImage(file);
            errors.AddRange(ValidateCaption(caption));
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeAttachment>(errors);
            }

            var now = DateTime.UtcNow;
            var attachment = new RecipeAttachment
            {
                RecipeId = Id,
                StorageKey = file.StorageKey,
                ThumbnailStorageKey = file.ThumbnailKey,
                ContentType = file.ContentType,
                OriginalFileName = file.OriginalFileName,
                FileSizeBytes = file.SizeBytes,
                Caption = NormalizeCaption(caption),
                Rank = ComputeAppendAttachmentRank(),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            Attachments.Add(attachment);
            return Result.Ok(attachment);
        }

        // Caption is the only mutable field — image bytes are immutable (replace = delete + re-add).
        public Result<RecipeAttachment> UpdateAttachmentCaption(int attachmentId, string? caption)
        {
            var attachment = Attachments.FirstOrDefault(a => a.Id == attachmentId && a.IsActive);
            if (attachment is null)
            {
                return Result.Fail<RecipeAttachment>(new EntityNotFoundError($"Recipe attachment {attachmentId} not found."));
            }

            var errors = ValidateCaption(caption);
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeAttachment>(errors);
            }

            attachment.Caption = NormalizeCaption(caption);
            attachment.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(attachment);
        }

        public Result RemoveAttachment(int attachmentId)
        {
            var attachment = Attachments.FirstOrDefault(a => a.Id == attachmentId && a.IsActive);
            if (attachment is null)
            {
                return Result.Fail(new EntityNotFoundError($"Recipe attachment {attachmentId} not found."));
            }
            attachment.IsActive = false;
            attachment.UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // Undo of a delete: reactivate with the ORIGINAL rank to preserve position. If a now-active
        // sibling took that rank while it was deleted, de-collide by re-minting this row's rank (the
        // partial unique index would otherwise reject it; mirrors the RestoreSection guard).
        public Result<RecipeAttachment> RestoreAttachment(int attachmentId)
        {
            var attachment = Attachments.FirstOrDefault(a => a.Id == attachmentId && !a.IsActive);
            if (attachment is null)
            {
                return Result.Fail<RecipeAttachment>(new EntityNotFoundError($"Recipe attachment {attachmentId} not found."));
            }

            attachment.IsActive = true;
            attachment.UpdatedAt = DateTime.UtcNow;

            var rankTaken = Attachments.Any(o => o.IsActive && o.Id != attachment.Id
                && string.CompareOrdinal(o.Rank, attachment.Rank) == 0);
            if (rankTaken)
            {
                attachment.Rank = ComputeAppendAttachmentRank();
            }
            return Result.Ok(attachment);
        }

        public Result<RecipeAttachment> ReplaceRestoredAttachmentRank(int attachmentId)
        {
            var attachment = Attachments.FirstOrDefault(a => a.Id == attachmentId && a.IsActive);
            if (attachment is null)
            {
                return Result.Fail<RecipeAttachment>(new EntityNotFoundError($"Recipe attachment {attachmentId} not found."));
            }
            attachment.Rank = ComputeAppendAttachmentRank();
            attachment.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(attachment);
        }

        public Result<RecipeAttachment> ReorderAttachment(int attachmentId, int afterAttachmentId)
        {
            var attachment = Attachments.FirstOrDefault(a => a.Id == attachmentId && a.IsActive);
            if (attachment is null)
            {
                return Result.Fail<RecipeAttachment>(new EntityNotFoundError($"Recipe attachment {attachmentId} not found."));
            }
            if (afterAttachmentId == attachmentId)
            {
                return Result.Ok(attachment);
            }

            var others = Attachments
                .Where(a => a.IsActive && a.Id != attachment.Id)
                .OrderBy(a => a.Rank, StringComparer.Ordinal)
                .ToList();

            var after = afterAttachmentId == 0 ? null : others.FirstOrDefault(a => a.Id == afterAttachmentId);
            var before = after is not null
                ? others.FirstOrDefault(a => string.CompareOrdinal(a.Rank, after.Rank) > 0)
                : null;

            string newRank;
            if (after is null)
            {
                newRank = others.Count == 0
                    ? FractionalIndex.GenerateKeyBetween(null, null)
                    : FractionalIndex.GenerateKeyBetween(null, others[0].Rank);
            }
            else if (before is null)
            {
                newRank = FractionalIndex.GenerateKeyBetween(after.Rank, null);
            }
            else
            {
                newRank = FractionalIndex.GenerateKeyBetween(after.Rank, before.Rank);
            }

            attachment.Rank = newRank;
            attachment.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(attachment);
        }

        private static List<IError> ValidateAttachmentImage(StoredFile file)
        {
            var errors = new List<IError>();

            // Stored output is always image/webp (the processor's rendition). Reject anything else.
            if (file.ContentType != "image/webp")
            {
                errors.Add(new Error($"Stored content type '{file.ContentType}' must be image/webp.")
                    .WithMetadata("Property", nameof(RecipeAttachment.ContentType)));
            }
            if (string.IsNullOrWhiteSpace(file.StorageKey) || file.StorageKey.Length > RecipeAttachment.StorageKeyMaxLength)
            {
                errors.Add(new Error($"Storage key is required and must be {RecipeAttachment.StorageKeyMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(RecipeAttachment.StorageKey)));
            }
            if (string.IsNullOrWhiteSpace(file.ThumbnailKey))
            {
                errors.Add(new Error("Image attachments require a thumbnail key.")
                    .WithMetadata("Property", nameof(RecipeAttachment.ThumbnailStorageKey)));
            }
            if (file.OriginalFileName is not null && file.OriginalFileName.Length > RecipeAttachment.OriginalFileNameMaxLength)
            {
                errors.Add(new Error($"File name must be {RecipeAttachment.OriginalFileNameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(RecipeAttachment.OriginalFileName)));
            }
            if (file.SizeBytes <= 0 || file.SizeBytes > RecipeAttachment.MaxFileSizeBytes)
            {
                errors.Add(new Error($"File size must be between 1 and {RecipeAttachment.MaxFileSizeBytes} bytes.")
                    .WithMetadata("Property", nameof(RecipeAttachment.FileSizeBytes)));
            }
            return errors;
        }

        private static List<IError> ValidateCaption(string? caption)
        {
            var errors = new List<IError>();
            var trimmed = NormalizeCaption(caption);
            if (trimmed is not null && trimmed.Length > RecipeAttachment.CaptionMaxLength)
            {
                errors.Add(new Error($"Caption must be {RecipeAttachment.CaptionMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(RecipeAttachment.Caption)));
            }
            return errors;
        }

        private static string? NormalizeCaption(string? caption)
            => string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();

        private string ComputeAppendAttachmentRank()
        {
            var ordered = Attachments
                .Where(a => a.IsActive)
                .OrderBy(a => a.Rank, StringComparer.Ordinal)
                .ToList();
            return ordered.Count == 0
                ? FractionalIndex.GenerateKeyBetween(null, null)
                : FractionalIndex.GenerateKeyBetween(ordered[^1].Rank, null);
        }
```

Add `using Frigorino.Domain.Files;` to the top of `Recipe.cs` if not already present (it isn't — current usings are `FluentResults`, `Frigorino.Domain.Errors`, `Frigorino.Domain.Quantities`).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAttachmentAggregateTests"`
Expected: PASS (all facts/theories green).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Recipe.cs Application/Frigorino.Test/Recipes/RecipeAttachmentAggregateTests.cs
git commit -m "feat(recipes): Recipe attachment aggregate methods + unit tests"
```

---

## Task 3: EF configuration + DbSet + migration

**Files:**
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeAttachmentConfiguration.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`

- [ ] **Step 1: Write the EF configuration**

Mirror `RecipeLinkConfiguration`. Partial unique index on `(RecipeId, Rank)` filtered `WHERE "IsActive"`, named `UX_RecipeAttachments_RecipeId_Rank_Active`.

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeAttachmentConfiguration : IEntityTypeConfiguration<RecipeAttachment>
    {
        public void Configure(EntityTypeBuilder<RecipeAttachment> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedOnAdd();
            builder.Property(a => a.StorageKey).HasMaxLength(RecipeAttachment.StorageKeyMaxLength).IsRequired();
            builder.Property(a => a.ThumbnailStorageKey).HasMaxLength(RecipeAttachment.StorageKeyMaxLength);
            builder.Property(a => a.ContentType).HasMaxLength(RecipeAttachment.ContentTypeMaxLength).IsRequired();
            builder.Property(a => a.OriginalFileName).HasMaxLength(RecipeAttachment.OriginalFileNameMaxLength);
            builder.Property(a => a.FileSizeBytes).IsRequired();
            builder.Property(a => a.Caption).HasMaxLength(RecipeAttachment.CaptionMaxLength);
            builder.Property(a => a.Rank).HasColumnType("text").UseCollation("C").IsRequired();
            builder.Property(a => a.RecipeId).IsRequired();
            builder.Property(a => a.CreatedAt).IsRequired();
            builder.Property(a => a.UpdatedAt).IsRequired();
            builder.Property(a => a.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(a => a.Recipe)
                .WithMany(r => r.Attachments)
                .HasForeignKey(a => a.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => a.RecipeId);
            builder.HasIndex(a => a.IsActive);
            builder.HasIndex(a => new { a.RecipeId, a.IsActive });
            builder.HasIndex(a => new { a.RecipeId, a.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_RecipeAttachments_RecipeId_Rank_Active");
        }
    }
}
```

(The context calls `ApplyConfigurationsFromAssembly`, so no manual registration is needed.)

- [ ] **Step 2: Add the DbSet**

In `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`, after `DbSet<RecipeLink> RecipeLinks` (line 24):

```csharp
        public DbSet<RecipeAttachment> RecipeAttachments { get; set; }
```

- [ ] **Step 3: Build before generating the migration**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: PASS.

- [ ] **Step 4: Generate the migration**

Run: `dotnet ef migrations add AddRecipeAttachments --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web`
Expected: a new migration under `Frigorino.Infrastructure/Migrations/` that creates the `RecipeAttachments` table with the four indexes (incl. the filtered unique). Open it and confirm: no unintended changes to other tables, the unique index has `filter: "\"IsActive\""`, `Rank` column is `text` with `C` collation.

- [ ] **Step 5: Apply + verify the model compiles**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS (migration applies automatically at startup via `MigrateAsync`; no need to run it manually here).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeAttachmentConfiguration.cs Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs Application/Frigorino.Infrastructure/Migrations/
git commit -m "feat(recipes): RecipeAttachment EF config + migration"
```

---

## Task 4: Blob area + reference source (TDD for the source)

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Services/BlobAreas.cs`
- Create: `Application/Frigorino.Infrastructure/Tasks/RecipeAttachmentBlobReferences.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs`
- Test: `Application/Frigorino.Test/Infrastructure/RecipeAttachmentBlobReferencesTests.cs`

- [ ] **Step 1: Add the area constant**

In `BlobAreas.cs`, add inside the class:

```csharp
        public const string RecipeAttachment = "recipe-attachment";
```

- [ ] **Step 2: Write the reference source**

Create `Application/Frigorino.Infrastructure/Tasks/RecipeAttachmentBlobReferences.cs`, mirroring `ListItemBlobReferences`. Returns every full-res + thumbnail key across **all** rows (active AND soft-deleted).

```csharp
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Infrastructure.Tasks
{
    // Referenced-key source for the recipe-attachment blob area: every full-res + thumbnail key
    // across ALL RecipeAttachment rows (active AND soft-deleted — soft-deleted rows keep their blob
    // for undo until they are purged). Scoped (depends on the request/scope DbContext).
    public sealed class RecipeAttachmentBlobReferences : IBlobReferenceSource
    {
        private readonly ApplicationDbContext _dbContext;

        public RecipeAttachmentBlobReferences(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public string AreaName => BlobAreas.RecipeAttachment;

        public async Task<ISet<string>> GetReferencedKeysAsync(CancellationToken ct)
        {
            var keyPairs = await _dbContext.RecipeAttachments
                .Select(a => new { a.StorageKey, a.ThumbnailStorageKey })
                .ToListAsync(ct);

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in keyPairs)
            {
                if (!string.IsNullOrEmpty(pair.StorageKey))
                {
                    referenced.Add(pair.StorageKey);
                }
                if (pair.ThumbnailStorageKey is not null)
                {
                    referenced.Add(pair.ThumbnailStorageKey);
                }
            }

            return referenced;
        }
    }
}
```

- [ ] **Step 3: Register the area + reference source**

In `FileStorageDependencyInjection.cs`:
- Add the area to the `Areas` array (line 18):
  ```csharp
  private static readonly string[] Areas = [BlobAreas.ListItem, BlobAreas.RecipeAttachment];
  ```
- Register the second reference source next to the existing one (after line 42):
  ```csharp
  services.AddScoped<IBlobReferenceSource, RecipeAttachmentBlobReferences>();
  ```

- [ ] **Step 4: Write the reference-source unit test**

Create `Application/Frigorino.Test/Infrastructure/RecipeAttachmentBlobReferencesTests.cs`. This is a *pure-contract* test that does NOT touch a DB — it verifies the `AreaName` matches the constant (DB behavior is covered by an IT in Task 13). Keeps `Frigorino.Test` DB-free per the testing rules.

```csharp
using Frigorino.Infrastructure.Services;
using Frigorino.Infrastructure.Tasks;
using Xunit;

namespace Frigorino.Test.Infrastructure
{
    public class RecipeAttachmentBlobReferencesTests
    {
        [Fact]
        public void AreaName_MatchesConstant()
        {
            var source = new RecipeAttachmentBlobReferences(null!);
            Assert.Equal(BlobAreas.RecipeAttachment, source.AreaName);
            Assert.Equal("recipe-attachment", source.AreaName);
        }
    }
}
```

- [ ] **Step 5: Build + run the test**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAttachmentBlobReferencesTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/BlobAreas.cs Application/Frigorino.Infrastructure/Tasks/RecipeAttachmentBlobReferences.cs Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs Application/Frigorino.Test/Infrastructure/RecipeAttachmentBlobReferencesTests.cs
git commit -m "feat(recipes): recipe-attachment blob area + reference source"
```

---

## Task 5: Response DTO + read slices (Get list, GetFile, GetThumbnail)

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Attachments/RecipeAttachmentResponse.cs`
- Create: `Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachments.cs`
- Create: `Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachmentFile.cs`
- Create: `Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachmentThumbnail.cs`

- [ ] **Step 1: Response DTO**

```csharp
using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes.Attachments
{
    // Storage keys are deliberately NOT exposed — the client fetches /file and /thumbnail.
    public sealed record RecipeAttachmentResponse(
        int Id,
        int RecipeId,
        string ContentType,
        string? OriginalFileName,
        long FileSizeBytes,
        string? Caption,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static RecipeAttachmentResponse From(RecipeAttachment a)
            => new(a.Id, a.RecipeId, a.ContentType, a.OriginalFileName, a.FileSizeBytes, a.Caption, a.Rank, a.CreatedAt, a.UpdatedAt);

        public static readonly Expression<Func<RecipeAttachment, RecipeAttachmentResponse>> ToProjection = a =>
            new RecipeAttachmentResponse(a.Id, a.RecipeId, a.ContentType, a.OriginalFileName, a.FileSizeBytes, a.Caption, a.Rank, a.CreatedAt, a.UpdatedAt);
    }
}
```

- [ ] **Step 2: `GetRecipeAttachments`** (mirror `GetRecipeLinks`)

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Attachments
{
    public static class GetRecipeAttachmentsEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeAttachments(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetRecipeAttachments")
               .Produces<RecipeAttachmentResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeAttachmentResponse[]>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipeExists = await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (!recipeExists) return TypedResults.NotFound();

            var attachments = await db.RecipeAttachments
                .Where(a => a.RecipeId == recipeId && a.IsActive)
                .OrderBy(a => a.Rank)
                .Select(RecipeAttachmentResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(attachments);
        }
    }
}
```

- [ ] **Step 3: `GetRecipeAttachmentFile`** (mirror `GetItemFile`, recipe path, keyed `RecipeAttachment` storage)

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Features.Recipes.Attachments
{
    public static class GetRecipeAttachmentFileEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeAttachmentFile(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{attachmentId:int}/file", Handle)
               .WithName("GetRecipeAttachmentFile")
               .Produces(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        public static async Task<Results<FileStreamHttpResult, NotFound>> Handle(
            int householdId, int recipeId, int attachmentId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            [FromKeyedServices(BlobAreas.RecipeAttachment)] IFileStorage storage,
            HttpContext http,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var attachment = await db.RecipeAttachments
                .Where(a => a.Id == attachmentId && a.RecipeId == recipeId && a.IsActive
                    && a.Recipe.HouseholdId == householdId && a.Recipe.IsActive)
                .Select(a => new { a.StorageKey, a.ContentType, a.OriginalFileName })
                .FirstOrDefaultAsync(ct);
            if (attachment is null || string.IsNullOrEmpty(attachment.StorageKey)) return TypedResults.NotFound();

            var stream = await storage.OpenAsync(attachment.StorageKey, ct);
            if (stream is null) return TypedResults.NotFound();

            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return TypedResults.Stream(
                stream,
                attachment.ContentType ?? "application/octet-stream",
                fileDownloadName: SanitizeFileName(attachment.OriginalFileName));
        }

        private static string? SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '/' && c != '\\' && c != '"').ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }
    }
}
```

- [ ] **Step 4: `GetRecipeAttachmentThumbnail`** (mirror `GetItemThumbnail`)

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Features.Recipes.Attachments
{
    public static class GetRecipeAttachmentThumbnailEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeAttachmentThumbnail(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{attachmentId:int}/thumbnail", Handle)
               .WithName("GetRecipeAttachmentThumbnail")
               .Produces(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        public static async Task<Results<FileStreamHttpResult, NotFound>> Handle(
            int householdId, int recipeId, int attachmentId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            [FromKeyedServices(BlobAreas.RecipeAttachment)] IFileStorage storage,
            HttpContext http,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var attachment = await db.RecipeAttachments
                .Where(a => a.Id == attachmentId && a.RecipeId == recipeId && a.IsActive
                    && a.Recipe.HouseholdId == householdId && a.Recipe.IsActive)
                .Select(a => new { a.ThumbnailStorageKey, a.ContentType })
                .FirstOrDefaultAsync(ct);
            if (attachment is null || string.IsNullOrEmpty(attachment.ThumbnailStorageKey)) return TypedResults.NotFound();

            var stream = await storage.OpenAsync(attachment.ThumbnailStorageKey, ct);
            if (stream is null) return TypedResults.NotFound();

            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            return TypedResults.Stream(stream, attachment.ContentType ?? "application/octet-stream");
        }
    }
}
```

- [ ] **Step 5: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Attachments/RecipeAttachmentResponse.cs Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachments.cs Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachmentFile.cs Application/Frigorino.Features/Recipes/Attachments/GetRecipeAttachmentThumbnail.cs
git commit -m "feat(recipes): attachment read slices (list + file + thumbnail)"
```

---

## Task 6: Mutation slices (Create/Update/Delete/Restore/Reorder)

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Attachments/CreateRecipeAttachment.cs`
- Create: `Application/Frigorino.Features/Recipes/Attachments/UpdateRecipeAttachment.cs`
- Create: `Application/Frigorino.Features/Recipes/Attachments/DeleteRecipeAttachment.cs`
- Create: `Application/Frigorino.Features/Recipes/Attachments/RestoreRecipeAttachment.cs`
- Create: `Application/Frigorino.Features/Recipes/Attachments/ReorderRecipeAttachment.cs`

- [ ] **Step 1: `CreateRecipeAttachment`** (multipart; mirrors `CreateMediaItem` flow exactly — input allowlist pre-filter, process, upload-before-persist + `RankRetry`, compensate on failure)

```csharp
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Files;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Frigorino.Features.Recipes.Attachments
{
    public static class CreateRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipeAttachment")
               .DisableAntiforgery() // API endpoint: no antiforgery token on multipart form posts.
               .Produces<RecipeAttachmentResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status413PayloadTooLarge)
               .ProducesValidationProblem();
            return app;
        }

        public static async Task<Results<Created<RecipeAttachmentResponse>, NotFound, ValidationProblem, StatusCodeHttpResult>> Handle(
            int householdId,
            int recipeId,
            IFormFile file,
            [FromForm] string? caption,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            [FromKeyedServices(BlobAreas.RecipeAttachment)] IFileStorage storage,
            IImageProcessor imageProcessor,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger(typeof(CreateRecipeAttachmentEndpoint));

            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipeExists = await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (!recipeExists) return TypedResults.NotFound();

            if (file is null || file.Length <= 0)
            {
                return new Error("A file is required.").WithProperty("file").ToValidationProblemResult();
            }

            // App-level size gate — the real limit; framework defaults are only the outer backstop.
            if (file.Length > RecipeAttachment.MaxFileSizeBytes)
            {
                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            // Input content-type allowlist pre-filter (the processor is the real gate, but this gives a
            // clear 400 before we decode).
            if (string.IsNullOrWhiteSpace(file.ContentType) || !RecipeAttachment.ImageContentTypes.Contains(file.ContentType))
            {
                return new Error($"Content type '{file.ContentType}' is not an allowed image type.")
                    .WithProperty("file").ToValidationProblemResult();
            }

            Result<ProcessedImage> processed;
            await using (var upload = file.OpenReadStream())
            {
                processed = await imageProcessor.ProcessAsync(upload, ct);
            }
            if (processed.IsFailed) return processed.ToValidationProblem();

            // Orphan-safe ordering: save blobs BEFORE the row exists; compensate on any later failure.
            string? storageKey = null;
            string? thumbnailKey = null;
            try
            {
                storageKey = await storage.SaveAsync(new MemoryStream(processed.Value.FullRes), ct);
                thumbnailKey = await storage.SaveAsync(new MemoryStream(processed.Value.Thumbnail), ct);

                var stored = new StoredFile(
                    storageKey, thumbnailKey, processed.Value.ContentType,
                    file.FileName, processed.Value.FullResSizeBytes);

                var outcome = await RankRetry.SaveWithRetryAsync(async () =>
                {
                    db.ChangeTracker.Clear();

                    var recipe = await db.Recipes
                        .Include(r => r.Attachments)
                        .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                    if (recipe is null)
                    {
                        return new CreateOutcome(null, NotFound: true, Problem: null);
                    }

                    var result = recipe.AddAttachment(caption, stored);
                    if (result.IsFailed)
                    {
                        return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                    }

                    await db.SaveChangesAsync(ct);
                    return new CreateOutcome(RecipeAttachmentResponse.From(result.Value), NotFound: false, Problem: null);
                });

                if (outcome.NotFound)
                {
                    await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                    return TypedResults.NotFound();
                }
                if (outcome.Problem is not null)
                {
                    await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                    return outcome.Problem;
                }

                var response = outcome.Response!;
                return TypedResults.Created(
                    $"/api/household/{householdId}/recipes/{recipeId}/attachments/{response.Id}", response);
            }
            catch
            {
                await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                throw;
            }
        }

        private sealed record CreateOutcome(RecipeAttachmentResponse? Response, bool NotFound, ValidationProblem? Problem);

        private static async Task CompensateAsync(IFileStorage storage, string? storageKey, string? thumbnailKey, ILogger logger)
        {
            await DeleteQuietlyAsync(storage, storageKey, logger);
            await DeleteQuietlyAsync(storage, thumbnailKey, logger);
        }

        private static async Task DeleteQuietlyAsync(IFileStorage storage, string? key, ILogger logger)
        {
            if (string.IsNullOrEmpty(key)) return;
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete orphaned blob {Key} during attachment-upload compensation.", key);
            }
        }
    }
}
```

- [ ] **Step 2: `UpdateRecipeAttachment`** (caption-only; mirror `UpdateRecipeLink`)

```csharp
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

namespace Frigorino.Features.Recipes.Attachments
{
    public sealed record UpdateRecipeAttachmentRequest(string? Caption);

    public static class UpdateRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{attachmentId:int}", Handle)
               .WithName("UpdateRecipeAttachment")
               .Produces<RecipeAttachmentResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeAttachmentResponse>, NotFound, ValidationProblem>> Handle(
            int householdId, int recipeId, int attachmentId, UpdateRecipeAttachmentRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.UpdateAttachmentCaption(attachmentId, request.Caption);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(RecipeAttachmentResponse.From(result.Value));
        }
    }
}
```

- [ ] **Step 3: `DeleteRecipeAttachment`** (mirror `DeleteRecipeLink`)

```csharp
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Attachments
{
    public static class DeleteRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{attachmentId:int}", Handle)
               .WithName("DeleteRecipeAttachment")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, NotFound>> Handle(
            int householdId, int recipeId, int attachmentId,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.RemoveAttachment(attachmentId);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                throw new InvalidOperationException(
                    $"DeleteRecipeAttachment cannot map error of type {result.Errors[0].GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
```

- [ ] **Step 4: `RestoreRecipeAttachment`** (mirror `RestoreRecipeLink`, using `ReplaceRestoredAttachmentRank`)

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

namespace Frigorino.Features.Recipes.Attachments
{
    public static class RestoreRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{attachmentId:int}/restore", Handle)
               .WithName("RestoreRecipeAttachment")
               .Produces<RecipeAttachmentResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeAttachmentResponse>, NotFound>> Handle(
            int householdId, int recipeId, int attachmentId,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var attemptedReplace = false;
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Attachments)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeAttachmentResponse?)null;

                var result = recipe.RestoreAttachment(attachmentId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException(
                        $"RestoreRecipeAttachment cannot map error of type {result.Errors[0].GetType().Name}.");
                }

                if (attemptedReplace)
                {
                    recipe.ReplaceRestoredAttachmentRank(attachmentId);
                }
                attemptedReplace = true;

                await db.SaveChangesAsync(ct);
                return RecipeAttachmentResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
```

- [ ] **Step 5: `ReorderRecipeAttachment`** (mirror `ReorderRecipeLink` — `MapPatch`, reuse `ReorderItemRequest`)

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

namespace Frigorino.Features.Recipes.Attachments
{
    public static class ReorderRecipeAttachmentEndpoint
    {
        public static IEndpointRouteBuilder MapReorderRecipeAttachment(this IEndpointRouteBuilder app)
        {
            app.MapPatch("/{attachmentId:int}/reorder", Handle)
               .WithName("ReorderRecipeAttachment")
               .Produces<RecipeAttachmentResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeAttachmentResponse>, NotFound>> Handle(
            int householdId, int recipeId, int attachmentId, ReorderItemRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var recipe = await db.Recipes
                    .Include(r => r.Attachments)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeAttachmentResponse?)null;

                var result = recipe.ReorderAttachment(attachmentId, request.AfterId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException($"ReorderRecipeAttachment cannot map error of type {result.Errors[0].GetType().Name}.");
                }
                await db.SaveChangesAsync(ct);
                return RecipeAttachmentResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
```

- [ ] **Step 6: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Attachments/CreateRecipeAttachment.cs Application/Frigorino.Features/Recipes/Attachments/UpdateRecipeAttachment.cs Application/Frigorino.Features/Recipes/Attachments/DeleteRecipeAttachment.cs Application/Frigorino.Features/Recipes/Attachments/RestoreRecipeAttachment.cs Application/Frigorino.Features/Recipes/Attachments/ReorderRecipeAttachment.cs
git commit -m "feat(recipes): attachment mutation slices (create/update/delete/restore/reorder)"
```

---

## Task 7: Wire the route group + extend revision + extend purge

**Files:**
- Modify: `Application/Frigorino.Web/Program.cs`
- Modify: `Application/Frigorino.Features/Recipes/GetRecipeRevision.cs`
- Modify: `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`

- [ ] **Step 1: Register the `recipeAttachments` route group**

In `Program.cs`, add `using Frigorino.Features.Recipes.Attachments;` near the other recipe usings (after line 18), and add the group after the `recipeLinks` block (after line 460):

```csharp
var recipeAttachments = app.MapGroup("/api/household/{householdId:int}/recipes/{recipeId:int}/attachments")
    .RequireAuthorization()
    .WithTags("RecipeAttachments");
recipeAttachments.MapGetRecipeAttachments();
recipeAttachments.MapCreateRecipeAttachment();
recipeAttachments.MapUpdateRecipeAttachment();
recipeAttachments.MapDeleteRecipeAttachment();
recipeAttachments.MapRestoreRecipeAttachment();
recipeAttachments.MapReorderRecipeAttachment();
recipeAttachments.MapGetRecipeAttachmentFile();
recipeAttachments.MapGetRecipeAttachmentThumbnail();
```

> Confirm the exact route prefix matches the existing recipe groups (`/api/household/{householdId:int}/recipes/...`). Note the established groups use `/api/household/` (singular) — match that, not `/api/households/`.

- [ ] **Step 2: Fold attachments into the revision token**

In `GetRecipeRevision.cs`, after the `links` block (line 52) add:

```csharp
            var attachments = db.RecipeAttachments.Where(a => a.RecipeId == recipeId && a.IsActive);
            var attachmentMaxUpdatedAt = await attachments.MaxAsync(a => (DateTime?)a.UpdatedAt, ct);
            var attachmentCount = await attachments.CountAsync(ct);
```

Then extend the max-fold and the count (replace lines 59-63):

```csharp
            if (linkMaxUpdatedAt is not null && (maxUpdatedAt is null || linkMaxUpdatedAt > maxUpdatedAt))
            {
                maxUpdatedAt = linkMaxUpdatedAt;
            }
            if (attachmentMaxUpdatedAt is not null && (maxUpdatedAt is null || attachmentMaxUpdatedAt > maxUpdatedAt))
            {
                maxUpdatedAt = attachmentMaxUpdatedAt;
            }
            var count = itemCount + sectionCount + linkCount + attachmentCount;
```

- [ ] **Step 3: Purge soft-deleted attachments**

In `DeleteInactiveItems.cs`, add this line in the recipe block (after line 48, the `RecipeLinks` purge):

```csharp
            await _dbContext.RecipeAttachments.Where(a => !a.IsActive).ExecuteDeleteAsync(cancellationToken);
```

> Note ordering: this purges soft-deleted attachments. Attachments of a purged recipe are removed by the FK cascade when `Recipes.Where(r => !r.IsActive).ExecuteDeleteAsync` runs (line 50). Their blobs become reclaimable by the per-area orphan sweep on a later run — no blob deletion here.

- [ ] **Step 4: Build the solution**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/Program.cs Application/Frigorino.Features/Recipes/GetRecipeRevision.cs Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs
git commit -m "feat(recipes): wire attachment route group + revision + purge"
```

---

## Task 8: Regenerate the TS client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/api/**`, `src/lib/openapi.json`

- [ ] **Step 1: Regenerate**

From `Application/Frigorino.Web/ClientApp/`:
Run: `npm run api`
Expected: succeeds; emits the new `getRecipeAttachments*`, `createRecipeAttachmentMutation`, `updateRecipeAttachmentMutation`, `deleteRecipeAttachmentMutation`, `restoreRecipeAttachmentMutation`, `reorderRecipeAttachmentMutation` helpers + `RecipeAttachmentResponse` type in `src/lib/api/`.

- [ ] **Step 2: Verify the generated symbols exist**

Run: `grep -l "getRecipeAttachmentsOptions\|createRecipeAttachmentMutation\|RecipeAttachmentResponse" src/lib/api/@tanstack/react-query.gen.ts src/lib/api/types.gen.ts`
Expected: both files match.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/api Application/Frigorino.Web/ClientApp/src/lib/openapi.json
git commit -m "chore(api): regenerate client for recipe attachments"
```

---

# Phase B — Frontend (Tasks 9–12)

Builds the UI on top of the regenerated client from Phase A: query/mutation hooks, the sortable edit list + view grid + lightbox, page wiring, translations, then a real SPA build and an in-browser smoke test. Reviewable on its own — at the end a user can upload, caption, reorder, delete, and undo attachments in the running app.

## Task 9: Frontend hooks

**Files:**
- Create: `features/recipes/attachments/useRecipeAttachments.ts`
- Create: `features/recipes/attachments/useCreateRecipeAttachment.ts`
- Create: `features/recipes/attachments/useUpdateRecipeAttachment.ts`
- Create: `features/recipes/attachments/useDeleteRecipeAttachment.ts`
- Create: `features/recipes/attachments/useRestoreRecipeAttachment.ts`
- Create: `features/recipes/attachments/useReorderRecipeAttachment.ts`
- Create: `features/recipes/attachments/useAttachmentImage.ts`

All paths below are relative to `Application/Frigorino.Web/ClientApp/`. Each mirrors its source-links sibling; only names/types change.

- [ ] **Step 1: `useRecipeAttachments.ts`** (mirror `useRecipeLinks.ts`)

```typescript
import { useQuery } from "@tanstack/react-query";
import { getRecipeAttachmentsOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useRecipeAttachments = (
    householdId: number,
    recipeId: number,
    enabled = true,
) =>
    useQuery({
        ...getRecipeAttachmentsOptions({ path: { householdId, recipeId } }),
        enabled: enabled && householdId > 0 && recipeId > 0,
        staleTime: 1000 * 30,
    });
```

- [ ] **Step 2: `useCreateRecipeAttachment.ts`** (mirror `useCreateMediaItem.ts` — multipart, invalidate on success)

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createRecipeAttachmentMutation,
    getRecipeAttachmentsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

// Arg-less, per the hook convention. Caller passes
//   { path: { householdId, recipeId }, body: { file, caption } }.
// hey-api serializes the body via formDataBodySerializer (FormData); do NOT set Content-Type — the
// browser sets the multipart boundary. No optimistic insert (upload shows a busy state);
// invalidate the attachments query on success.
export const useCreateRecipeAttachment = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createRecipeAttachmentMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeAttachmentsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
        },
    });
};
```

> No temp-id reconciliation is needed here (unlike the optimistic list-item create): this hook does **not** optimistically insert a row — it invalidates on success and the server id arrives with the refetch. The optimistic-temp-id rule applies only to hooks that insert a `Date.now()` row.

- [ ] **Step 3: `useUpdateRecipeAttachment.ts`** (mirror `useUpdateRecipeLink.ts`)

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeAttachmentsQueryKey,
    updateRecipeAttachmentMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useUpdateRecipeAttachment = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateRecipeAttachmentMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeAttachmentsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
        },
    });
};
```

- [ ] **Step 4: `useRestoreRecipeAttachment.ts`** (mirror `useRestoreRecipeLink.ts`)

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeAttachmentsQueryKey,
    restoreRecipeAttachmentMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreRecipeAttachment = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreRecipeAttachmentMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeAttachmentsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
        },
    });
};
```

- [ ] **Step 5: `useDeleteRecipeAttachment.ts`** (mirror `useDeleteRecipeLink.ts` — optimistic remove + undo toast)

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteRecipeAttachmentMutation,
    getRecipeAttachmentsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeAttachmentResponse } from "../../../lib/api/types.gen";
import { useRestoreRecipeAttachment } from "./useRestoreRecipeAttachment";

export const useDeleteRecipeAttachment = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreAttachment = useRestoreRecipeAttachment();

    return useMutation({
        ...deleteRecipeAttachmentMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeAttachmentsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousAttachments =
                queryClient.getQueryData<RecipeAttachmentResponse[]>(queryKey);

            queryClient.setQueryData<RecipeAttachmentResponse[]>(
                queryKey,
                (old) =>
                    old?.filter(
                        (a) => a.id !== variables.path.attachmentId,
                    ),
            );

            return { previousAttachments };
        },
        onError: (_data, variables, context) => {
            if (context?.previousAttachments) {
                queryClient.setQueryData(
                    getRecipeAttachmentsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousAttachments,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("recipes.attachmentDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreAttachment.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeAttachmentsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};
```

- [ ] **Step 6: `useReorderRecipeAttachment.ts`** (mirror `useReorderRecipeLink.ts`)

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getRecipeAttachmentsQueryKey,
    reorderRecipeAttachmentMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeAttachmentResponse } from "../../../lib/api/types.gen";

export const useReorderRecipeAttachment = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderRecipeAttachmentMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeAttachmentsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousAttachments =
                queryClient.getQueryData<RecipeAttachmentResponse[]>(queryKey);

            queryClient.setQueryData<RecipeAttachmentResponse[]>(
                queryKey,
                (old) => {
                    if (!old) return old;
                    const moved = old.find(
                        (a) => a.id === variables.path.attachmentId,
                    );
                    if (!moved) return old;
                    const others = old.filter((a) => a.id !== moved.id);
                    const afterId = variables.body.afterId;
                    if (!afterId) {
                        others.unshift(moved);
                        return others;
                    }
                    const anchorIdx = others.findIndex(
                        (a) => a.id === afterId,
                    );
                    others.splice(
                        anchorIdx === -1 ? others.length : anchorIdx + 1,
                        0,
                        moved,
                    );
                    return others;
                },
            );

            return { previousAttachments };
        },
        onError: (_data, variables, context) => {
            if (context?.previousAttachments) {
                queryClient.setQueryData(
                    getRecipeAttachmentsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousAttachments,
                );
            }
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeAttachmentsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};
```

> Confirm the reorder mutation's `body` shape after codegen — the PATCH `ReorderItemRequest` serializes as `{ afterId }`. If the generated mutation names the path key differently, mirror the generated `variables.path` keys (`householdId`, `recipeId`, `attachmentId`).

- [ ] **Step 7: `useAttachmentImage.ts`** (mirror `useItemImage.ts`, recipe path)

```typescript
import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { client } from "../../../lib/api/client.gen";

type Variant = "thumbnail" | "file";

// Fetches an attachment's image bytes (auth'd, via the configured client). Caches the decoded Blob
// (keyed by attachment id + variant) — NOT an object URL. Each consumer derives its own short-lived
// object URL via a paired useEffect (StrictMode-safe). Direct mirror of useItemImage; see that file
// for the why-Blob-not-URL rationale.
export const useAttachmentImage = (
    householdId: number,
    recipeId: number,
    attachmentId: number,
    variant: Variant,
    enabled = true,
) => {
    const query = useQuery({
        queryKey: [
            "attachment-image",
            householdId,
            recipeId,
            attachmentId,
            variant,
        ],
        enabled:
            enabled && householdId > 0 && recipeId > 0 && attachmentId > 0,
        staleTime: Infinity,
        gcTime: 5 * 60 * 1000,
        queryFn: async () => {
            const { data, error } = await client.get({
                url: `/api/household/${householdId}/recipes/${recipeId}/attachments/${attachmentId}/${variant}`,
                parseAs: "blob",
            });
            if (error || !data) {
                throw new Error("Failed to load image");
            }
            return data as Blob;
        },
    });

    const blob = query.data;
    const [url, setUrl] = useState<string>();
    useEffect(() => {
        if (!blob) {
            // eslint-disable-next-line react-hooks/set-state-in-effect
            setUrl(undefined);
            return;
        }
        const objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
        return () => {
            URL.revokeObjectURL(objectUrl);
        };
    }, [blob]);

    return {
        ...query,
        data: url,
        isLoading: query.isLoading || (!!blob && !url),
    };
};
```

> Confirm the `/api/household/.../recipes/.../attachments/.../{variant}` URL matches the route prefix registered in Task 7. It must (singular `household`).

- [ ] **Step 8: Type-check**

From `ClientApp/`: Run: `npm run tsc`
Expected: PASS (components in the next task aren't created yet, but hooks alone must type-check; if `tsc -b` reports unrelated project errors, ensure only these new files are involved).

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/
git commit -m "feat(recipes): attachment query/mutation/image hooks"
```

---

## Task 10: Frontend components + page wiring

**Files:**
- Create: `features/recipes/attachments/components/RecipeAttachmentLightbox.tsx`
- Create: `features/recipes/attachments/components/RecipeAttachmentRow.tsx`
- Create: `features/recipes/attachments/components/RecipeAttachmentsSection.tsx`
- Create: `features/recipes/attachments/components/RecipeViewAttachments.tsx`
- Modify: `features/recipes/items/useRecipeRevision.ts`
- Modify: `features/recipes/pages/RecipeEditPage.tsx`
- Modify: `features/recipes/pages/RecipeViewPage.tsx`

- [ ] **Step 1: `RecipeAttachmentLightbox.tsx`** (mirror `ImageLightbox.tsx`, using `useAttachmentImage`)

```tsx
import { Close } from "@mui/icons-material";
import {
    Box,
    CircularProgress,
    Dialog,
    IconButton,
    Typography,
} from "@mui/material";
import { useAttachmentImage } from "../useAttachmentImage";

interface Props {
    householdId: number;
    recipeId: number;
    attachmentId: number;
    caption?: string | null;
    open: boolean;
    onClose: () => void;
}

export function RecipeAttachmentLightbox({
    householdId,
    recipeId,
    attachmentId,
    caption,
    open,
    onClose,
}: Props) {
    const { data: url, isLoading } = useAttachmentImage(
        householdId,
        recipeId,
        attachmentId,
        "file",
        open,
    );

    return (
        <Dialog
            open={open}
            onClose={onClose}
            maxWidth="lg"
            data-testid="recipe-attachment-lightbox"
        >
            <Box sx={{ position: "relative", bgcolor: "common.black" }}>
                <IconButton
                    onClick={onClose}
                    aria-label="close"
                    sx={{
                        position: "absolute",
                        top: 8,
                        right: 8,
                        color: "common.white",
                        zIndex: 1,
                    }}
                >
                    <Close />
                </IconButton>
                {isLoading || !url ? (
                    <Box
                        sx={{
                            display: "flex",
                            justifyContent: "center",
                            alignItems: "center",
                            minHeight: 240,
                            minWidth: 240,
                        }}
                    >
                        <CircularProgress sx={{ color: "common.white" }} />
                    </Box>
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={caption ?? ""}
                        sx={{
                            display: "block",
                            maxWidth: "90vw",
                            maxHeight: "85vh",
                            width: "auto",
                            height: "auto",
                        }}
                    />
                )}
            </Box>
            {caption ? (
                <Typography
                    variant="body2"
                    sx={{ p: 1.5, color: "text.secondary" }}
                >
                    {caption}
                </Typography>
            ) : null}
        </Dialog>
    );
}
```

- [ ] **Step 2: `RecipeAttachmentRow.tsx`** (edit-list row: drag handle + thumbnail + debounced caption + delete; mirrors `RecipeLinkRow.tsx` caption-save pattern + `ImageItemRenderer` thumbnail)

```tsx
import { BrokenImage, Delete } from "@mui/icons-material";
import { Box, IconButton, Skeleton, Stack, TextField } from "@mui/material";
import type { ReactNode } from "react";
import {
    useCallback,
    useEffect,
    useLayoutEffect,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import type { RecipeAttachmentResponse } from "../../../../lib/api";
import { useAttachmentImage } from "../useAttachmentImage";
import { useUpdateRecipeAttachment } from "../useUpdateRecipeAttachment";

const SAVE_DEBOUNCE_MS = 600;
const THUMB_SIZE = 56;

interface RecipeAttachmentRowProps {
    householdId: number;
    recipeId: number;
    attachment: RecipeAttachmentResponse;
    onDelete: () => void;
    dragHandle: ReactNode;
}

export const RecipeAttachmentRow = ({
    householdId,
    recipeId,
    attachment,
    onDelete,
    dragHandle,
}: RecipeAttachmentRowProps) => {
    const { t } = useTranslation();
    const updateAttachment = useUpdateRecipeAttachment();

    const [caption, setCaption] = useState(attachment.caption ?? "");
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const latest = useRef({ caption });
    useLayoutEffect(() => {
        latest.current = { caption };
    });

    const { mutate } = updateAttachment;

    const save = useCallback(() => {
        mutate({
            path: { householdId, recipeId, attachmentId: attachment.id },
            body: { caption: latest.current.caption.trim() || null },
        });
    }, [mutate, householdId, recipeId, attachment.id]);

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

    const {
        data: url,
        isLoading,
        isError,
    } = useAttachmentImage(householdId, recipeId, attachment.id, "thumbnail");

    return (
        <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: "center" }}
            data-testid={`recipe-attachment-row-${attachment.id}`}
        >
            {dragHandle}
            <Box
                sx={{
                    width: THUMB_SIZE,
                    height: THUMB_SIZE,
                    flexShrink: 0,
                    borderRadius: 1,
                    overflow: "hidden",
                    bgcolor: "action.hover",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                }}
            >
                {isLoading ? (
                    <Skeleton
                        variant="rectangular"
                        width={THUMB_SIZE}
                        height={THUMB_SIZE}
                    />
                ) : isError || !url ? (
                    <BrokenImage fontSize="small" color="disabled" />
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={attachment.caption ?? ""}
                        sx={{
                            width: "100%",
                            height: "100%",
                            objectFit: "cover",
                        }}
                    />
                )}
            </Box>
            <TextField
                label={t("recipes.attachmentCaption")}
                value={caption}
                onChange={(e) => {
                    setCaption(e.target.value);
                    scheduleSave();
                }}
                onBlur={flushSave}
                size="small"
                fullWidth
                placeholder={t("recipes.attachmentCaptionPlaceholder")}
                slotProps={{
                    htmlInput: {
                        maxLength: 255,
                        "data-testid": `recipe-attachment-${attachment.id}-caption-input`,
                    },
                }}
            />
            <IconButton
                size="small"
                onClick={onDelete}
                data-testid={`recipe-attachment-${attachment.id}-delete`}
            >
                <Delete fontSize="small" color="error" />
            </IconButton>
        </Stack>
    );
};
```

- [ ] **Step 3: `RecipeAttachmentsSection.tsx`** (edit page; upload button + file picker + sortable list; mirrors `RecipeLinksSection.tsx` structure with a file input instead of a draft composer)

```tsx
import { Add } from "@mui/icons-material";
import { Alert, Box, Button, Stack } from "@mui/material";
import { useCallback, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { CollapsibleSection } from "../../../../components/shared/CollapsibleSection";
import { SortableLinkList } from "../../../../components/sortables/SortableLinkList";
import { usePersistedExpanded } from "../../../../hooks/usePersistedExpanded";
import { useCreateRecipeAttachment } from "../useCreateRecipeAttachment";
import { useDeleteRecipeAttachment } from "../useDeleteRecipeAttachment";
import { useRecipeAttachments } from "../useRecipeAttachments";
import { useReorderRecipeAttachment } from "../useReorderRecipeAttachment";
import { RecipeAttachmentRow } from "./RecipeAttachmentRow";

const ACCEPT = "image/jpeg,image/png,image/webp";

interface RecipeAttachmentsSectionProps {
    householdId: number;
    recipeId: number;
}

export const RecipeAttachmentsSection = ({
    householdId,
    recipeId,
}: RecipeAttachmentsSectionProps) => {
    const { t } = useTranslation();
    const [expanded, setExpanded] = usePersistedExpanded(
        "recipe-edit-section:attachments",
        false,
    );

    const { data: attachments = [] } = useRecipeAttachments(
        householdId,
        recipeId,
    );
    const createAttachment = useCreateRecipeAttachment();
    const deleteAttachment = useDeleteRecipeAttachment();
    const reorderAttachment = useReorderRecipeAttachment();

    const fileInputRef = useRef<HTMLInputElement>(null);
    const [uploadError, setUploadError] = useState<string | null>(null);

    const handlePick = useCallback(
        async (e: React.ChangeEvent<HTMLInputElement>) => {
            const file = e.target.files?.[0];
            // Reset the input so picking the same file again re-fires change.
            e.target.value = "";
            if (!file) return;
            setUploadError(null);
            try {
                await createAttachment.mutateAsync({
                    path: { householdId, recipeId },
                    body: { file, caption: null },
                });
            } catch {
                setUploadError(t("recipes.uploadFailed"));
            }
        },
        [createAttachment, householdId, recipeId, t],
    );

    return (
        <CollapsibleSection
            title={t("recipes.attachments")}
            expanded={expanded}
            onChange={setExpanded}
            testId="recipe-section-attachments"
        >
            <Stack spacing={1}>
                <SortableLinkList
                    links={attachments}
                    onReorder={async (attachmentId, afterId) => {
                        await reorderAttachment.mutateAsync({
                            path: { householdId, recipeId, attachmentId },
                            body: { afterId },
                        });
                    }}
                    renderLink={(attachment, dragHandle) => (
                        <RecipeAttachmentRow
                            householdId={householdId}
                            recipeId={recipeId}
                            attachment={attachment}
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

                {uploadError ? (
                    <Alert
                        severity="error"
                        onClose={() => setUploadError(null)}
                        data-testid="recipe-attachment-upload-error"
                    >
                        {uploadError}
                    </Alert>
                ) : null}

                <Box>
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept={ACCEPT}
                        hidden
                        onChange={handlePick}
                        data-testid="recipe-attachment-file-input"
                    />
                    <Button
                        startIcon={<Add />}
                        onClick={() => fileInputRef.current?.click()}
                        disabled={createAttachment.isPending}
                        data-testid="recipe-add-attachment"
                        sx={{ alignSelf: "flex-start" }}
                    >
                        {t("recipes.addAttachment")}
                    </Button>
                </Box>
            </Stack>
        </CollapsibleSection>
    );
};
```

> `SortableLinkList<T extends { id: number }>` is generic — passing `attachments` (each has `id`) is type-safe. Its drag-handle testid is hard-coded `recipe-link-drag-handle-{id}`; that's acceptable cosmetically, but if a distinct testid is wanted for attachments, generalize `SortableLinkList`'s `data-testid` prefix via an optional prop in a follow-up. For this plan, reuse as-is (the IT drives reorder by drag, not by that testid).

- [ ] **Step 4: `RecipeViewAttachments.tsx`** (view page; thumbnail grid + lightbox; hidden when empty)

```tsx
import { BrokenImage } from "@mui/icons-material";
import { Box, Container, Skeleton, Typography } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeAttachmentResponse } from "../../../../lib/api";
import { useAttachmentImage } from "../useAttachmentImage";
import { useRecipeAttachments } from "../useRecipeAttachments";
import { RecipeAttachmentLightbox } from "./RecipeAttachmentLightbox";

interface RecipeViewAttachmentsProps {
    householdId: number;
    recipeId: number;
}

const Tile = ({
    householdId,
    recipeId,
    attachment,
    onOpen,
}: {
    householdId: number;
    recipeId: number;
    attachment: RecipeAttachmentResponse;
    onOpen: () => void;
}) => {
    const {
        data: url,
        isLoading,
        isError,
    } = useAttachmentImage(householdId, recipeId, attachment.id, "thumbnail");

    return (
        <Box>
            <Box
                role="button"
                tabIndex={0}
                aria-label="open image"
                data-testid={`recipe-attachment-${attachment.id}`}
                onClick={onOpen}
                onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        onOpen();
                    }
                }}
                sx={{
                    aspectRatio: "1 / 1",
                    width: "100%",
                    borderRadius: 1,
                    overflow: "hidden",
                    cursor: "pointer",
                    bgcolor: "action.hover",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                }}
            >
                {isLoading ? (
                    <Skeleton
                        variant="rectangular"
                        width="100%"
                        height="100%"
                    />
                ) : isError || !url ? (
                    <BrokenImage fontSize="small" color="disabled" />
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={attachment.caption ?? ""}
                        sx={{
                            width: "100%",
                            height: "100%",
                            objectFit: "cover",
                        }}
                    />
                )}
            </Box>
            {attachment.caption ? (
                <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{
                        display: "block",
                        mt: 0.5,
                        wordBreak: "break-word",
                    }}
                >
                    {attachment.caption}
                </Typography>
            ) : null}
        </Box>
    );
};

export const RecipeViewAttachments = ({
    householdId,
    recipeId,
}: RecipeViewAttachmentsProps) => {
    const { t } = useTranslation();
    const { data: attachments = [] } = useRecipeAttachments(
        householdId,
        recipeId,
    );
    const [openId, setOpenId] = useState<number | null>(null);

    if (attachments.length === 0) return null;

    const openAttachment = attachments.find((a) => a.id === openId) ?? null;

    return (
        <Container
            maxWidth="sm"
            data-testid="recipe-view-attachments"
            sx={{ px: 2, pb: 1 }}
        >
            <Typography
                variant="overline"
                color="text.secondary"
                sx={{ fontWeight: 700, letterSpacing: 1 }}
            >
                {t("recipes.attachments")}
            </Typography>
            <Box
                sx={{
                    mt: 0.5,
                    display: "grid",
                    gridTemplateColumns: {
                        xs: "repeat(2, 1fr)",
                        sm: "repeat(3, 1fr)",
                    },
                    gap: 1,
                }}
            >
                {attachments.map((attachment) => (
                    <Tile
                        key={attachment.id}
                        householdId={householdId}
                        recipeId={recipeId}
                        attachment={attachment}
                        onOpen={() => setOpenId(attachment.id)}
                    />
                ))}
            </Box>
            {openAttachment ? (
                <RecipeAttachmentLightbox
                    householdId={householdId}
                    recipeId={recipeId}
                    attachmentId={openAttachment.id}
                    caption={openAttachment.caption}
                    open={openId !== null}
                    onClose={() => setOpenId(null)}
                />
            ) : null}
        </Container>
    );
};
```

- [ ] **Step 5: Extend `useRecipeRevision.ts`**

In `features/recipes/items/useRecipeRevision.ts`, add `getRecipeAttachmentsQueryKey` to the import from `react-query.gen`, and add a fourth `useRevisionInvalidation` block mirroring the links one:

```typescript
    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getRecipeAttachmentsQueryKey({
            path: { householdId, recipeId },
        }),
        isLocalMutation: (variables) =>
            (variables as { path?: { recipeId?: number } } | undefined)?.path
                ?.recipeId === recipeId,
    });
```

- [ ] **Step 6: Wire `RecipeAttachmentsSection` into the edit page**

In `RecipeEditPage.tsx`, add the import:

```typescript
import { RecipeAttachmentsSection } from "../attachments/components/RecipeAttachmentsSection";
```

and render it directly after `<RecipeLinksSection .../>` (after line 300):

```tsx
                        <RecipeAttachmentsSection
                            householdId={householdId}
                            recipeId={recipeId}
                        />
```

- [ ] **Step 7: Wire `RecipeViewAttachments` into the view page**

In `RecipeViewPage.tsx`, add the import:

```typescript
import { RecipeViewAttachments } from "../attachments/components/RecipeViewAttachments";
```

and render it directly after `<RecipeViewLinks .../>` (after line 173):

```tsx
                <RecipeViewAttachments
                    householdId={householdId}
                    recipeId={recipeId}
                />
```

- [ ] **Step 8: Type-check + lint + format**

From `ClientApp/`:
Run: `npm run tsc && npm run lint && npm run prettier`
Expected: all PASS (prettier writes any formatting; re-run `npm run prettier:check` is implicit in CI — `npm run prettier` here applies it locally).

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes
git commit -m "feat(recipes): attachment edit section, view grid, lightbox + revision wiring"
```

---

## Task 11: i18n keys

**Files:**
- Modify: `public/locales/en/translation.json`
- Modify: `public/locales/de/translation.json`

- [ ] **Step 1: Add keys under `recipes`**

Add these keys to the `recipes` object in **both** files (English values shown; provide German translations in the `de` file):

en:
```json
"attachments": "Attachments",
"addAttachment": "Add attachment",
"attachmentCaption": "Caption",
"attachmentCaptionPlaceholder": "Optional caption",
"deleteAttachment": "Delete attachment",
"attachmentDeleted": "Attachment deleted",
"uploadFailed": "Upload failed",
"fileTooLarge": "File is too large",
"unsupportedFileType": "Unsupported file type"
```

de (suggested):
```json
"attachments": "Anhänge",
"addAttachment": "Anhang hinzufügen",
"attachmentCaption": "Beschriftung",
"attachmentCaptionPlaceholder": "Optionale Beschriftung",
"deleteAttachment": "Anhang löschen",
"attachmentDeleted": "Anhang gelöscht",
"uploadFailed": "Upload fehlgeschlagen",
"fileTooLarge": "Datei ist zu groß",
"unsupportedFileType": "Nicht unterstützter Dateityp"
```

> `fileTooLarge` / `unsupportedFileType` are added for completeness per the spec; the section currently surfaces only `uploadFailed` generically. Wire the more specific messages if you parse the 413 / 400 body in `RecipeAttachmentsSection.handlePick` (optional refinement — not required for the gate).

- [ ] **Step 2: Verify JSON validity + type-check**

From `ClientApp/`: Run: `npm run tsc`
Expected: PASS (i18n is runtime; this just confirms nothing else broke).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales
git commit -m "feat(recipes): attachment i18n (en + de)"
```

---

## Task 12: Build the SPA + smoke-verify in the browser

**Files:** none (verification only).

- [ ] **Step 1: Build the SPA** (the IT harness serves `ClientApp/build`)

From `ClientApp/`: Run: `npm run build`
Expected: PASS.

- [ ] **Step 2: Manual browser verify** (per the verbatim-plan-needs-runtime-verify rule)

Bring the dev stack up (`/dev-up`), open a recipe edit page, expand "Attachments", upload a JPEG/PNG, confirm: a thumbnail row appears; editing the caption persists (reload); drag-reorder sticks; delete shows an undo toast and undo restores it. On the view page, confirm the thumbnail grid renders, a tile opens the lightbox with the full image, and the block is hidden for a recipe with no attachments. Tear down (`/dev-down`) when done.

> This catches the runtime/DOM bugs that tsc + lint can't (object-URL StrictMode pairing, multipart body shape, route-prefix typos). If the thumbnail is broken, check the `/api/household/.../attachments/.../thumbnail` URL prefix matches Task 7's group exactly.

- [ ] **Step 3: Commit (if the build produced committed artifacts)**

`ClientApp/build` is gitignored — nothing to commit here unless a source fix was needed during verify (commit those fixes with a descriptive message).

---

# Phase C — Finals (Tasks 13–14)

End-to-end coverage and the shippable gate: BDD integration tests against real Postgres + a real blob area override, then the full verification gate (solution tests + `docker build` + frontend lint/tsc/prettier). At the end the branch is ready to promote.

## Task 13: Integration tests (Reqnroll + Playwright + Testcontainers)

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeAttachmentApiSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature`

First read these mirrors: `Slices/Recipes/RecipeLinkApiSteps.cs`, `Slices/Lists/MediaItemSteps.cs` (for the `TinyPng` byte buffer + multipart upload), `Slices/Lists/MediaItems.Api.feature`, `Infrastructure/TestApiClient.cs` (existing `TryCreateRecipeLinkAsync` / `TryGetRecipeLinksAsync` / `TryDeleteRecipeLinkAsync` / `TryRestoreRecipeLinkAsync` helpers), and `Infrastructure/ScenarioContextHolder.cs` (the `RecipeIds` / `RecipeLinkIds` dictionaries). Confirm whether attachment-blob storage in IT is the keyed `LocalFileStorage` registered under `BlobAreas.RecipeAttachment` — `TestWebApplicationFactory` currently overrides only the `BlobAreas.ListItem` keyed storage, so **add a parallel override for `BlobAreas.RecipeAttachment`** (mirror the existing `RemoveAllKeyed`/`AddKeyedSingleton` block for the new area, pointing at a temp dir).

- [ ] **Step 1: Extend `TestWebApplicationFactory` for the new area**

In `Infrastructure/TestWebApplicationFactory.cs`, alongside the existing `BlobAreas.ListItem` keyed override, add the same for `BlobAreas.RecipeAttachment` (its own `LocalFileStorage` temp dir, registered keyed under both `IFileStorage` and `IFileStorageMaintenance`). This keeps attachment blobs on local disk in tests and lets the orphan-sweep IT exercise the area folder. Read the existing block and mirror it exactly.

- [ ] **Step 2: Add `TestApiClient` helpers**

Mirror the link helpers. Add (signatures; implement against the existing `TryRequest` infrastructure in the file):

```csharp
Task<ApiResponse> TryGetRecipeAttachmentsAsync(int recipeId);
Task<ApiResponse> TryCreateRecipeAttachmentAsync(int recipeId, string fileName, string mimeType, byte[] bytes, string? caption);
Task<ApiResponse> TryUpdateRecipeAttachmentAsync(int recipeId, int attachmentId, string? caption);
Task<ApiResponse> TryDeleteRecipeAttachmentAsync(int recipeId, int attachmentId);
Task<ApiResponse> TryRestoreRecipeAttachmentAsync(int recipeId, int attachmentId);
Task<ApiResponse> TryGetRecipeAttachmentThumbnailAsync(int recipeId, int attachmentId);
```

The create helper posts `multipart/form-data` with `file` (the bytes) + optional `caption` — mirror how `MediaItemSteps`/`TestApiClient` builds the list-item media upload. Reuse the `TinyPng` byte buffer from `MediaItemSteps` (or move it to a shared helper) for a valid decodable image.

- [ ] **Step 3: Write the step bindings**

Create `Slices/Recipes/RecipeAttachmentApiSteps.cs`, `[Binding]`, mirroring `RecipeLinkApiSteps`. Double-decorate `[Given]`+`[When]` on any step reused as both setup and action (this project's Reqnroll is keyword-sensitive). Track created attachment ids in a `ScenarioContextHolder` dictionary keyed by `(recipeName, caption)` (add the dictionary if absent, mirroring `RecipeLinkIds`). Steps needed (assert on counts / status / headers — never translated text):

```csharp
namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeAttachmentApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("I upload an attachment captioned {string} to recipe {string} via the API")]
    [When("I upload an attachment captioned {string} to recipe {string} via the API")]
    public async Task WhenIUploadAttachment(string caption, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var response = await api.TryCreateRecipeAttachmentAsync(
            recipeId, "photo.png", "image/png", MediaItemSteps.TinyPng, caption);
        ctx.LastApiResponse = response;
        if (response.Status == 201)
        {
            var json = (await response.JsonAsync())!.Value;
            ctx.RecipeAttachmentIds[(recipeName, caption)] = json.GetProperty("id").GetInt32();
        }
    }

    [When("I GET the attachments of recipe {string} via the API")]
    public async Task WhenIGetAttachments(string recipeName)
    {
        ctx.LastApiResponse = await api.TryGetRecipeAttachmentsAsync(ctx.RecipeIds[recipeName]);
    }

    [Then("the API attachments of recipe {string} number {int}")]
    public async Task ThenAttachmentsNumber(string recipeName, int expected)
    {
        var response = await api.TryGetRecipeAttachmentsAsync(ctx.RecipeIds[recipeName]);
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        Assert.Equal(expected, json.GetArrayLength());
    }

    [When("I DELETE the attachment {string} of recipe {string} via the API")]
    public async Task WhenIDeleteAttachment(string caption, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryDeleteRecipeAttachmentAsync(
            recipeId, ctx.RecipeAttachmentIds[(recipeName, caption)]);
    }

    [When("I POST restore for the attachment {string} of recipe {string} via the API")]
    public async Task WhenIRestoreAttachment(string caption, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryRestoreRecipeAttachmentAsync(
            recipeId, ctx.RecipeAttachmentIds[(recipeName, caption)]);
    }

    [Then("the attachment {string} of recipe {string} serves a thumbnail")]
    public async Task ThenServesThumbnail(string caption, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var resp = await api.TryGetRecipeAttachmentThumbnailAsync(
            recipeId, ctx.RecipeAttachmentIds[(recipeName, caption)]);
        Assert.Equal(200, resp.Status);
        Assert.Contains("image/webp", resp.Headers["content-type"]);
    }
}
```

> Make `MediaItemSteps.TinyPng` accessible (e.g. `internal static readonly byte[] TinyPng`) or duplicate the literal. Add `public Dictionary<(string, string), int> RecipeAttachmentIds { get; } = new();` to `ScenarioContextHolder` mirroring `RecipeLinkIds`.

- [ ] **Step 4: Add the scenarios to `Recipes.Api.feature`**

Append after the source-link scenarios. Reuse the existing recipe-setup background/`Given` steps from `RecipeApiSteps`/`RecipeSteps` (a recipe named "Pizza" already exists in this feature's earlier scenarios — follow how those are seeded).

```gherkin
  Scenario: A new recipe has no attachments
    When I GET the attachments of recipe "Pizza" via the API
    Then the last API call status is 200
    And the API attachments of recipe "Pizza" number 0

  Scenario: Uploading an image attachment succeeds, is listed, and serves a thumbnail
    When I upload an attachment captioned "Dish photo" to recipe "Pizza" via the API
    Then the last API call status is 201
    And the API attachments of recipe "Pizza" number 1
    And the attachment "Dish photo" of recipe "Pizza" serves a thumbnail

  Scenario: Deleting an attachment removes it, and restore brings it back
    Given I upload an attachment captioned "Dish photo" to recipe "Pizza" via the API
    When I DELETE the attachment "Dish photo" of recipe "Pizza" via the API
    Then the last API call status is 204
    And the API attachments of recipe "Pizza" number 0
    When I POST restore for the attachment "Dish photo" of recipe "Pizza" via the API
    Then the last API call status is 200
    And the API attachments of recipe "Pizza" number 1

  Scenario: An attachment change moves the recipe revision token
    Given I note the revision token of recipe "Pizza"
    When I upload an attachment captioned "Dish photo" to recipe "Pizza" via the API
    Then the revision token of recipe "Pizza" has changed
```

> Reuse the existing revision-token steps from the link scenario ("A source-link change moves the recipe revision token", `Recipes.Api.feature:151`) — match their exact step text so the bindings are shared. Confirm "the last API call status is {int}" exists as a shared step; if the link scenarios assert status differently, mirror that phrasing.

- [ ] **Step 5: (Optional but recommended) orphan-sweep + reference-source DB coverage**

Add one scenario (or an xUnit IT against the harness, following any existing `ReclaimOrphanBlobs`-style test) asserting: after uploading an attachment then directly deleting its row, the per-area sweep reclaims the unreferenced blob in the `recipe-attachment` folder while keeping a referenced one — **and does not touch the list-item area**. Plus a DB-backed assertion that `RecipeAttachmentBlobReferences.GetReferencedKeysAsync` returns keys for active **and** soft-deleted rows. If no existing sweep IT exists to mirror, scope this to a focused Testcontainers-backed xUnit test in `Frigorino.IntegrationTests` that builds the real `ApplicationDbContext`, seeds rows, and calls the reference source directly. Do not add EF-InMemory tests.

- [ ] **Step 6: Build the SPA, then run the recipe IT**

From `ClientApp/`: `npm run build` (only if step 12 wasn't just run).
Then: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Recipe"`
Expected: PASS (watch for the keyword-sensitive step pitfall — confirm the matched scenario count; per the Reqnroll-filter memory, filtering can silently skip).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.IntegrationTests
git commit -m "test(recipes): attachment API integration scenarios + orphan sweep coverage"
```

---

## Task 14: Full verification gate

**Files:** none (verification only). Per the verify-with-full-tests-and-docker rule, this is the final gate.

- [ ] **Step 1: Frontend gate**

From `ClientApp/`: Run: `npm run tsc && npm run lint && npm run prettier:check && npm run build`
Expected: all PASS.

- [ ] **Step 2: Full backend + IT suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: all PASS (unit + integration). Capture the pass/fail line; do not trust a piped tail — read the summary (`${PIPESTATUS[0]}` if piping).

- [ ] **Step 3: Docker build** (catches Dockerfile/pipeline/SPA drift)

Ensure Docker Desktop is running (ask the user to start it if the daemon is unreachable).
Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: PASS. (No new project was added, so no Dockerfile change is expected — the build confirms that.)

- [ ] **Step 4: Final commit (only if a fix was needed during the gate)**

Commit any gate fixes with a descriptive message. Otherwise nothing to do.

---

## Post-implementation cleanup

- [ ] Remove the "File / image / document attachments" bullet's *now-shipped* status from `IDEAS_Recipes.md` only if the whole attachments idea is fully delivered. **Phase 1 (images) ships here; PDF/documents remain a future phase** — so update the bullet to note images shipped and keep the documents phase, rather than deleting it (per the remove-tracking-items-when-done rule: delete only completed work).
- [ ] Update the spec's status line (`docs/superpowers/specs/2026-06-15-recipe-attachments-design.md`) to "implemented" once merged, or leave as historical record per user preference.
- [ ] No `TECH_DEBT.md` entry to remove (the blob-area prerequisite was already removed when it landed).

---

## Self-review notes (spec coverage)

- Data model (entity, constants, migration, indexes) → Tasks 1, 3.
- Aggregate methods (add/update-caption/remove/restore/replace-rank/reorder + image invariants) → Task 2.
- Eight slices + route group → Tasks 5, 6, 7.
- Revision-token fold + `DeleteInactiveItems` purge → Task 7.
- Blob area + reference source (sweep unchanged) → Task 4.
- Frontend hooks, edit section, view grid, lightbox, revision wiring → Tasks 9, 10.
- i18n (en + de) → Task 11.
- Unit + integration + orphan-sweep coverage → Tasks 2, 4, 13.
- Verification gate (tsc/lint/prettier/build/sln test/docker) → Task 14.

**Open items to confirm during execution (flagged inline, not blockers):**
- Exact generated mutation `variables.path`/`body` key names after `npm run api` (Task 9 step 6).
- Whether the IT harness needs the new keyed-area storage override (Task 13 step 1) — almost certainly yes; confirm against `TestWebApplicationFactory`.
- Route prefix `/api/household/` (singular) consistency across slice URLs and the `useAttachmentImage` fetch URL.

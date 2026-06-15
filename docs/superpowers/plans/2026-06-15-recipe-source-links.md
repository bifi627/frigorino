# Recipe Source Links Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an ordered list of source links (required `http(s)` URL + optional display label) to a recipe, editable on the edit page and shown as clickable links on the view page.

**Architecture:** A new flat `RecipeLink` entity (sibling of `RecipeSection`), managed entirely through `Recipe` aggregate methods returning `FluentResults.Result<T>`. Vertical-slice endpoints under a new `recipeLinks` route group mirror the Sections slices one-for-one (soft-delete + undo, fractional drag-reorder, revision-token participation). The SPA gets one-hook-per-file TanStack Query hooks, a static collapsible "Source links" block on the edit page, and a "Sources" block on the view page. No AI, no blob storage.

**Tech Stack:** .NET 10 / EF Core 10 / Postgres; React 19 + TanStack Query (hey-api generated client) + dnd-kit + MUI; xUnit + FakeItEasy; Reqnroll + Playwright + Testcontainers.

**Branch:** `feat/recipe-source-links` (already created off `stage`, which contains the merged sections work).

**Spec:** `docs/superpowers/specs/2026-06-15-recipe-source-links-design.md`

**Conventions to honor throughout:**
- C# always block braces `{}`, even single-line. Prefer `if/else` over multi-condition ternary. Name compound boolean conditions before branching.
- Frontend: never hand-write `queryFn`/`mutationFn`/`queryKey`; spread the generated `getXOptions`/`xMutation`/`getXQueryKey()`. One hook per file.
- Tests assert on **testids / data-attributes only**, never translated text.
- IT (`Frigorino.IntegrationTests`) Reqnroll matches step bindings **keyword-sensitively** — a step reused under both `When` and `Given`/`And` must be **double-decorated** `[Given(...)]` + `[When(...)]`.
- Run npm commands from `Application/Frigorino.Web/ClientApp/`. The IT harness serves `ClientApp/build`, so run `npm run build` before the IT after any React edit.
- No `Co-Authored-By` trailers in commits.

---

## File Structure

**Backend — create:**
- `Application/Frigorino.Domain/Entities/RecipeLink.cs` — entity (data holder).
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeLinkConfiguration.cs` — EF config.
- `Application/Frigorino.Features/Recipes/Links/RecipeLinkResponse.cs` — response DTO.
- `Application/Frigorino.Features/Recipes/Links/GetRecipeLinks.cs`
- `Application/Frigorino.Features/Recipes/Links/CreateRecipeLink.cs`
- `Application/Frigorino.Features/Recipes/Links/UpdateRecipeLink.cs`
- `Application/Frigorino.Features/Recipes/Links/DeleteRecipeLink.cs`
- `Application/Frigorino.Features/Recipes/Links/RestoreRecipeLink.cs`
- `Application/Frigorino.Features/Recipes/Links/ReorderRecipeLink.cs`
- EF migration `Application/Frigorino.Infrastructure/Migrations/<timestamp>_AddRecipeLinks.cs` (generated).

**Backend — modify:**
- `Application/Frigorino.Domain/Entities/Recipe.cs` — `Links` nav + aggregate methods.
- `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs` — `DbSet` + timestamp stamping.
- `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs` — purge soft-deleted links.
- `Application/Frigorino.Web/Program.cs` — route group + `using`.
- `Application/Frigorino.Features/Recipes/GetRecipeRevision.cs` — fold links into the token.

**Frontend — create (`ClientApp/src/`):**
- `components/sortables/SortableLinkList.tsx` — vertical sortable (link testids).
- `features/recipes/links/useRecipeLinks.ts`
- `features/recipes/links/useCreateRecipeLink.ts`
- `features/recipes/links/useUpdateRecipeLink.ts`
- `features/recipes/links/useDeleteRecipeLink.ts`
- `features/recipes/links/useRestoreRecipeLink.ts`
- `features/recipes/links/useReorderRecipeLink.ts`
- `features/recipes/links/components/RecipeLinkRow.tsx` — one editable row.
- `features/recipes/links/components/RecipeLinksSection.tsx` — edit-page collapsible block.
- `features/recipes/links/components/RecipeViewLinks.tsx` — view-page sources block.

**Frontend — modify:**
- `features/recipes/items/useRecipeRevision.ts` — invalidate links key too.
- `features/recipes/pages/RecipeEditPage.tsx` — render `RecipeLinksSection`.
- `features/recipes/pages/RecipeViewPage.tsx` — render `RecipeViewLinks`.
- `public/locales/en/translation.json`, `public/locales/de/translation.json` — strings.

**Tests — modify:**
- `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs` — link aggregate tests.
- `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature` — API scenarios.
- `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeLinkApiSteps.cs` — **create** new bindings file.
- `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` — link helpers.
- `Application/Frigorino.IntegrationTests/Infrastructure/ScenarioContextHolder.cs` — link id map.
- `Application/Frigorino.IntegrationTests/Tasks/DeleteInactiveItemsTests.cs` — seed + assert a link.

---

## Phase A — Domain & persistence

### Task 1: `RecipeLink` entity

**Files:**
- Create: `Application/Frigorino.Domain/Entities/RecipeLink.cs`

- [ ] **Step 1: Create the entity**

```csharp
namespace Frigorino.Domain.Entities
{
    // An external source link for a recipe (blog post, video, …). A required URL plus an
    // optional display label. Ordering, validation, and lifecycle (add/update/delete/restore/
    // reorder) live on the parent Recipe aggregate; this is a plain data holder.
    public class RecipeLink
    {
        public const int UrlMaxLength = 2048;
        public const int LabelMaxLength = 255;

        public int Id { get; set; }
        public int RecipeId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Label { get; set; }

        // Lexicographic ordering key (fractional index), unique per RECIPE.
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
Expected: Build succeeded (the type compiles; not yet referenced).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Domain/Entities/RecipeLink.cs
git commit -m "feat(recipes): RecipeLink entity"
```

---

### Task 2: `Recipe` aggregate link methods (TDD)

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/Recipe.cs`
- Test: `Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs`

- [ ] **Step 1: Write the failing tests**

Append these to `RecipeAggregateTests.cs` inside the `RecipeAggregateTests` class (before the final closing brace). They mirror the existing section tests' style.

```csharp
        // ---- Source links ----

        [Fact]
        public void AddLink_ValidHttpsUrl_TrimsAndStores()
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink("  https://example.com/recipe  ", "  My Blog  ");
            Assert.True(result.IsSuccess);
            Assert.Equal("https://example.com/recipe", result.Value.Url);
            Assert.Equal("My Blog", result.Value.Label);
            Assert.Single(recipe.Links);
        }

        [Fact]
        public void AddLink_EmptyLabel_StoresNull()
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink("https://example.com", "   ");
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Label);
        }

        [Fact]
        public void AddLink_BlankUrl_FailsWithUrlProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink("   ", null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeLink.Url));
        }

        [Theory]
        [InlineData("ftp://example.com/file")]
        [InlineData("javascript:alert(1)")]
        [InlineData("not a url")]
        [InlineData("example.com")]
        public void AddLink_NonHttpUrl_FailsWithUrlProperty(string url)
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink(url, null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeLink.Url));
        }

        [Fact]
        public void AddLink_OverlongLabel_FailsWithLabelProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink("https://example.com", new string('x', RecipeLink.LabelMaxLength + 1));
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeLink.Label));
        }

        [Fact]
        public void AddLink_AppendsWithRankAfterPrevious()
        {
            var recipe = NewRecipe();
            var first = recipe.AddLink("https://a.example.com", null).Value;
            var second = recipe.AddLink("https://b.example.com", null).Value;
            Assert.True(string.CompareOrdinal(first.Rank, second.Rank) < 0);
        }

        [Fact]
        public void UpdateLink_ChangesUrlAndLabel()
        {
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://old.example.com", "old").Value;
            link.Id = 500;
            var result = recipe.UpdateLink(500, "https://new.example.com", "new");
            Assert.True(result.IsSuccess);
            Assert.Equal("https://new.example.com", result.Value.Url);
            Assert.Equal("new", result.Value.Label);
        }

        [Fact]
        public void UpdateLink_InvalidUrl_FailsWithUrlProperty()
        {
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://old.example.com", null).Value;
            link.Id = 501;
            var result = recipe.UpdateLink(501, "ftp://nope", null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeLink.Url));
        }

        [Fact]
        public void UpdateLink_UnknownId_NotFound()
        {
            var recipe = NewRecipe();
            var result = recipe.UpdateLink(999, "https://example.com", null);
            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveLink_DeactivatesIt()
        {
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://example.com", null).Value;
            link.Id = 510;
            var result = recipe.RemoveLink(510);
            Assert.True(result.IsSuccess);
            Assert.False(link.IsActive);
        }

        [Fact]
        public void RemoveLink_LastLink_IsAllowed()
        {
            // Unlike sections, zero links is a valid state.
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://example.com", null).Value;
            link.Id = 511;
            var result = recipe.RemoveLink(511);
            Assert.True(result.IsSuccess);
            Assert.Empty(recipe.Links.Where(l => l.IsActive));
        }

        [Fact]
        public void RestoreLink_ReactivatesIt()
        {
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://example.com", null).Value;
            link.Id = 520;
            recipe.RemoveLink(520);
            var result = recipe.RestoreLink(520);
            Assert.True(result.IsSuccess);
            Assert.True(link.IsActive);
        }

        [Fact]
        public void ReplaceRestoredLinkRank_MovesToEnd()
        {
            var recipe = NewRecipe();
            var first = recipe.AddLink("https://a.example.com", null).Value;
            first.Id = 530;
            var second = recipe.AddLink("https://b.example.com", null).Value;
            second.Id = 531;
            var result = recipe.ReplaceRestoredLinkRank(530);
            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(second.Rank, first.Rank) < 0);
        }

        [Fact]
        public void ReorderLink_ToTop_PlacesFirst()
        {
            var recipe = NewRecipe();
            var first = recipe.AddLink("https://a.example.com", null).Value;
            first.Id = 540;
            var second = recipe.AddLink("https://b.example.com", null).Value;
            second.Id = 541;
            var result = recipe.ReorderLink(541, 0);
            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(second.Rank, first.Rank) < 0);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAggregateTests"`
Expected: FAIL to compile (`AddLink`/`UpdateLink`/`RemoveLink`/`RestoreLink`/`ReplaceRestoredLinkRank`/`ReorderLink` do not exist).

- [ ] **Step 3: Add the `Links` navigation to `Recipe`**

In `Application/Frigorino.Domain/Entities/Recipe.cs`, after the `Sections` collection (line ~26):

```csharp
        public ICollection<RecipeLink> Links { get; set; } = new List<RecipeLink>();
```

- [ ] **Step 4: Add the aggregate methods**

In `Recipe.cs`, add a new region after the RecipeItem coordination methods (i.e. after `ApplyExtractedQuantity`, before the `private static List<IError> ValidateSectionMetadata` helpers):

```csharp
        // ---- RecipeLink coordination (collaborative — any member; no role gate) ----

        public Result<RecipeLink> AddLink(string url, string? label)
        {
            var errors = ValidateLinkMetadata(url, label);
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeLink>(errors);
            }

            var now = DateTime.UtcNow;
            var link = new RecipeLink
            {
                RecipeId = Id,
                Url = url.Trim(),
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                Rank = ComputeAppendLinkRank(),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            Links.Add(link);
            return Result.Ok(link);
        }

        public Result<RecipeLink> UpdateLink(int linkId, string url, string? label)
        {
            var link = Links.FirstOrDefault(l => l.Id == linkId && l.IsActive);
            if (link is null)
            {
                return Result.Fail<RecipeLink>(new EntityNotFoundError($"Recipe link {linkId} not found."));
            }

            var errors = ValidateLinkMetadata(url, label);
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeLink>(errors);
            }

            link.Url = url.Trim();
            link.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            link.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(link);
        }

        public Result RemoveLink(int linkId)
        {
            var link = Links.FirstOrDefault(l => l.Id == linkId && l.IsActive);
            if (link is null)
            {
                return Result.Fail(new EntityNotFoundError($"Recipe link {linkId} not found."));
            }
            link.IsActive = false;
            link.UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // Undo of a delete: reactivates the link with its ORIGINAL rank to preserve position. If a
        // live link took that rank while it was deleted, the partial unique index rejects it; the
        // restore slice re-mints via ReplaceRestoredLinkRank on that 23505 retry. (Links have no
        // child rows, so unlike RestoreSection there is nothing else to de-collide here.)
        public Result<RecipeLink> RestoreLink(int linkId)
        {
            var link = Links.FirstOrDefault(l => l.Id == linkId && !l.IsActive);
            if (link is null)
            {
                return Result.Fail<RecipeLink>(new EntityNotFoundError($"Recipe link {linkId} not found."));
            }
            link.IsActive = true;
            link.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(link);
        }

        public Result<RecipeLink> ReplaceRestoredLinkRank(int linkId)
        {
            var link = Links.FirstOrDefault(l => l.Id == linkId && l.IsActive);
            if (link is null)
            {
                return Result.Fail<RecipeLink>(new EntityNotFoundError($"Recipe link {linkId} not found."));
            }
            link.Rank = ComputeAppendLinkRank();
            link.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(link);
        }

        public Result<RecipeLink> ReorderLink(int linkId, int afterLinkId)
        {
            var link = Links.FirstOrDefault(l => l.Id == linkId && l.IsActive);
            if (link is null)
            {
                return Result.Fail<RecipeLink>(new EntityNotFoundError($"Recipe link {linkId} not found."));
            }
            if (afterLinkId == linkId)
            {
                return Result.Ok(link);
            }

            var others = Links
                .Where(l => l.IsActive && l.Id != link.Id)
                .OrderBy(l => l.Rank, StringComparer.Ordinal)
                .ToList();

            var after = afterLinkId == 0 ? null : others.FirstOrDefault(l => l.Id == afterLinkId);
            var before = after is not null
                ? others.FirstOrDefault(l => string.CompareOrdinal(l.Rank, after.Rank) > 0)
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

            link.Rank = newRank;
            link.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(link);
        }
```

- [ ] **Step 5: Add the private helpers**

In `Recipe.cs`, add near the other private helpers (e.g. after `ComputeAppendSectionRank`):

```csharp
        private static List<IError> ValidateLinkMetadata(string? url, string? label)
        {
            var errors = new List<IError>();
            var trimmedUrl = url?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedUrl))
            {
                errors.Add(new Error("Source link URL is required.").WithMetadata("Property", nameof(RecipeLink.Url)));
            }
            else
            {
                if (trimmedUrl.Length > RecipeLink.UrlMaxLength)
                {
                    errors.Add(new Error($"Source link URL must be {RecipeLink.UrlMaxLength} characters or fewer.")
                        .WithMetadata("Property", nameof(RecipeLink.Url)));
                }
                var isHttpUrl = Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
                if (!isHttpUrl)
                {
                    errors.Add(new Error("Source link must be a valid http(s) URL.")
                        .WithMetadata("Property", nameof(RecipeLink.Url)));
                }
            }
            if (label is not null && label.Trim().Length > RecipeLink.LabelMaxLength)
            {
                errors.Add(new Error($"Source link label must be {RecipeLink.LabelMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(RecipeLink.Label)));
            }
            return errors;
        }

        private string ComputeAppendLinkRank()
        {
            var ordered = Links
                .Where(l => l.IsActive)
                .OrderBy(l => l.Rank, StringComparer.Ordinal)
                .ToList();
            return ordered.Count == 0
                ? FractionalIndex.GenerateKeyBetween(null, null)
                : FractionalIndex.GenerateKeyBetween(ordered[^1].Rank, null);
        }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAggregateTests"`
Expected: PASS (all existing + new link tests).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Recipe.cs Application/Frigorino.Test/Recipes/RecipeAggregateTests.cs
git commit -m "feat(recipes): Recipe aggregate source-link methods + tests"
```

---

### Task 3: EF config, DbSet, timestamps, migration

**Files:**
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeLinkConfiguration.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`
- Create: migration via EF tooling.

- [ ] **Step 1: Create the EF configuration**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class RecipeLinkConfiguration : IEntityTypeConfiguration<RecipeLink>
    {
        public void Configure(EntityTypeBuilder<RecipeLink> builder)
        {
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedOnAdd();
            builder.Property(l => l.Url).HasMaxLength(RecipeLink.UrlMaxLength).IsRequired();
            builder.Property(l => l.Label).HasMaxLength(RecipeLink.LabelMaxLength);
            builder.Property(l => l.Rank).HasColumnType("text").UseCollation("C").IsRequired();
            builder.Property(l => l.RecipeId).IsRequired();
            builder.Property(l => l.CreatedAt).IsRequired();
            builder.Property(l => l.UpdatedAt).IsRequired();
            builder.Property(l => l.IsActive).IsRequired().HasDefaultValue(true);

            builder.HasOne(l => l.Recipe)
                .WithMany(r => r.Links)
                .HasForeignKey(l => l.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(l => l.RecipeId);
            builder.HasIndex(l => l.IsActive);
            builder.HasIndex(l => new { l.RecipeId, l.IsActive });
            builder.HasIndex(l => new { l.RecipeId, l.Rank })
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("UX_RecipeLinks_RecipeId_Rank_Active");
        }
    }
}
```

(`IEntityTypeConfiguration`s are auto-applied via `ApplyConfigurationsFromAssembly` in `OnModelCreating` — no manual registration needed; verify by grepping `ApplyConfigurationsFromAssembly` in `ApplicationDbContext.cs` if unsure.)

- [ ] **Step 2: Add the `DbSet`**

In `ApplicationDbContext.cs`, after `public DbSet<RecipeSection> RecipeSections { get; set; }` (line ~23):

```csharp
        public DbSet<RecipeLink> RecipeLinks { get; set; }
```

- [ ] **Step 3: Add timestamp stamping (Added + Modified)**

In `ApplicationDbContext.cs` `SaveChangesAsync`, in the **Added** branch after the `RecipeSection` block (line ~117):

```csharp
                    if (entry.Entity is RecipeLink recipeLink && recipeLink.CreatedAt == default)
                    {
                        recipeLink.CreatedAt = now;
                        recipeLink.UpdatedAt = now;
                    }
```

And in the **Modified** branch after the `RecipeSection` block (line ~200):

```csharp
                    if (entry.Entity is RecipeLink recipeLink)
                    {
                        recipeLink.UpdatedAt = now;
                    }
```

- [ ] **Step 4: Build to verify the model compiles**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: Build succeeded.

- [ ] **Step 5: Generate the migration**

Run:
```bash
dotnet ef migrations add AddRecipeLinks --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: creates `Application/Frigorino.Infrastructure/Migrations/<timestamp>_AddRecipeLinks.cs` (+ `.Designer.cs` + snapshot update).

- [ ] **Step 6: Verify the generated migration**

Open the generated `_AddRecipeLinks.cs`. Confirm `Up` creates the `RecipeLinks` table with: `Id` identity PK; `RecipeId` FK to `Recipes` with `onDelete: Cascade`; `Url` `character varying(2048)` NOT NULL; `Label` `character varying(255)` nullable; `Rank` `text` collation `C` NOT NULL; `CreatedAt`/`UpdatedAt` `timestamp with time zone` NOT NULL; `IsActive` boolean NOT NULL default true; plus the four indexes incl. unique `UX_RecipeLinks_RecipeId_Rank_Active` with `filter: "\"IsActive\""`. Confirm `Down` does `migrationBuilder.DropTable(name: "RecipeLinks")`. **There must be no `RecipeItems`/`RecipeSections` changes** (this migration only adds one table — no backfill). If the diff shows anything else, the model drifted; fix and re-scaffold.

- [ ] **Step 7: Build the solution**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeLinkConfiguration.cs Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs Application/Frigorino.Infrastructure/Migrations/
git commit -m "feat(recipes): EF config + DbSet + AddRecipeLinks migration"
```

---

### Task 4: Purge soft-deleted links

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`
- Test: `Application/Frigorino.IntegrationTests/Tasks/DeleteInactiveItemsTests.cs`

- [ ] **Step 1: Add the purge line**

In `DeleteInactiveItems.cs`, in the recipe purge block, add the links purge **before** the `RecipeSections` line (links must go before sections/recipes, though order is not strictly required since each is an independent soft-delete sweep and recipe-delete cascades the rest):

```csharp
            await _dbContext.RecipeItems.Where(ri => !ri.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.RecipeLinks.Where(l => !l.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.RecipeSections.Where(s => !s.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.Recipes.Where(r => !r.IsActive).ExecuteDeleteAsync(cancellationToken);
```

- [ ] **Step 2: Extend the purge IT to seed + assert links**

In `DeleteInactiveItemsTests.cs`, after the `RecipeSections.AddRange(keepSection, dropSection);` line (~134), add:

```csharp
            // A link feeds the direct RecipeLinks.Where(!IsActive) purge: the active one survives,
            // the soft-deleted one is removed.
            var keepLink = new RecipeLink { RecipeId = keepRecipe.Id, Url = "https://keep.example.com", Rank = "a0", IsActive = true, CreatedAt = now, UpdatedAt = now };
            var dropLink = new RecipeLink { RecipeId = keepRecipe.Id, Url = "https://drop.example.com", Rank = "a1", IsActive = false, CreatedAt = now, UpdatedAt = now };
            db.RecipeLinks.AddRange(keepLink, dropLink);
```

Then after the `recipeSections` assertion (~178):

```csharp
            // The soft-deleted link under the surviving recipe is gone (direct purge); the active
            // one survives.
            var recipeLinks = await db.RecipeLinks.CountAsync();
            Assert.Equal(1, recipeLinks);
```

Ensure `db.SaveChangesAsync(...)` is called after the new `AddRange` (it should already be saved together with the existing seed block — verify the new `AddRange` precedes the existing `await db.SaveChangesAsync`).

- [ ] **Step 3: Build (defer running until the full IT pass in Task 16)**

Run: `dotnet build Application/Frigorino.IntegrationTests`
Expected: Build succeeded. (Testcontainers IT runs in the Task 16 gate.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs Application/Frigorino.IntegrationTests/Tasks/DeleteInactiveItemsTests.cs
git commit -m "feat(recipes): purge soft-deleted source links + IT coverage"
```

---

## Phase B — API slices

### Task 5: `RecipeLinkResponse` + `GetRecipeLinks`

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Links/RecipeLinkResponse.cs`
- Create: `Application/Frigorino.Features/Recipes/Links/GetRecipeLinks.cs`

- [ ] **Step 1: Create the response DTO**

```csharp
using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes.Links
{
    public sealed record RecipeLinkResponse(
        int Id,
        int RecipeId,
        string Url,
        string? Label,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static RecipeLinkResponse From(RecipeLink l)
            => new(l.Id, l.RecipeId, l.Url, l.Label, l.Rank, l.CreatedAt, l.UpdatedAt);

        public static readonly Expression<Func<RecipeLink, RecipeLinkResponse>> ToProjection = l =>
            new RecipeLinkResponse(l.Id, l.RecipeId, l.Url, l.Label, l.Rank, l.CreatedAt, l.UpdatedAt);
    }
}
```

- [ ] **Step 2: Create the GET slice**

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Links
{
    public static class GetRecipeLinksEndpoint
    {
        public static IEndpointRouteBuilder MapGetRecipeLinks(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetRecipeLinks")
               .Produces<RecipeLinkResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeLinkResponse[]>, NotFound>> Handle(
            int householdId, int recipeId, ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipeExists = await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (!recipeExists) return TypedResults.NotFound();

            var links = await db.RecipeLinks
                .Where(l => l.RecipeId == recipeId && l.IsActive)
                .OrderBy(l => l.Rank)
                .Select(RecipeLinkResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(links);
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Links/RecipeLinkResponse.cs Application/Frigorino.Features/Recipes/Links/GetRecipeLinks.cs
git commit -m "feat(recipes): RecipeLinkResponse + GetRecipeLinks slice"
```

---

### Task 6: `CreateRecipeLink` + `UpdateRecipeLink`

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Links/CreateRecipeLink.cs`
- Create: `Application/Frigorino.Features/Recipes/Links/UpdateRecipeLink.cs`

- [ ] **Step 1: Create the POST slice**

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Recipes.Links
{
    public sealed record CreateRecipeLinkRequest(string Url, string? Label);

    public static class CreateRecipeLinkEndpoint
    {
        public static IEndpointRouteBuilder MapCreateRecipeLink(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateRecipeLink")
               .Produces<RecipeLinkResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeLinkResponse>, NotFound, ValidationProblem>> Handle(
            int householdId, int recipeId, CreateRecipeLinkRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var outcome = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Links)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null)
                {
                    return new CreateOutcome(null, NotFound: true, Problem: null);
                }

                var result = recipe.AddLink(request.Url, request.Label);
                if (result.IsFailed)
                {
                    return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                }

                await db.SaveChangesAsync(ct);
                return new CreateOutcome(RecipeLinkResponse.From(result.Value), NotFound: false, Problem: null);
            });

            if (outcome.NotFound) return TypedResults.NotFound();
            if (outcome.Problem is not null) return outcome.Problem;

            var response = outcome.Response!;
            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipeId}/links/{response.Id}", response);
        }

        private sealed record CreateOutcome(RecipeLinkResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
```

- [ ] **Step 2: Create the PUT slice**

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

namespace Frigorino.Features.Recipes.Links
{
    public sealed record UpdateRecipeLinkRequest(string Url, string? Label);

    public static class UpdateRecipeLinkEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateRecipeLink(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{linkId:int}", Handle)
               .WithName("UpdateRecipeLink")
               .Produces<RecipeLinkResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<RecipeLinkResponse>, NotFound, ValidationProblem>> Handle(
            int householdId, int recipeId, int linkId, UpdateRecipeLinkRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Include(r => r.Links)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.UpdateLink(linkId, request.Url, request.Label);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(RecipeLinkResponse.From(result.Value));
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Links/CreateRecipeLink.cs Application/Frigorino.Features/Recipes/Links/UpdateRecipeLink.cs
git commit -m "feat(recipes): create + update source-link slices"
```

---

### Task 7: `DeleteRecipeLink` + `RestoreRecipeLink`

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Links/DeleteRecipeLink.cs`
- Create: `Application/Frigorino.Features/Recipes/Links/RestoreRecipeLink.cs`

- [ ] **Step 1: Create the DELETE slice**

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

namespace Frigorino.Features.Recipes.Links
{
    public static class DeleteRecipeLinkEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteRecipeLink(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{linkId:int}", Handle)
               .WithName("DeleteRecipeLink")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, NotFound>> Handle(
            int householdId, int recipeId, int linkId,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var recipe = await db.Recipes
                .Include(r => r.Links)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
            if (recipe is null) return TypedResults.NotFound();

            var result = recipe.RemoveLink(linkId);
            if (result.IsFailed)
            {
                if (result.Errors[0] is EntityNotFoundError) return TypedResults.NotFound();
                throw new InvalidOperationException(
                    $"DeleteRecipeLink cannot map error of type {result.Errors[0].GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
```

- [ ] **Step 2: Create the restore slice**

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

namespace Frigorino.Features.Recipes.Links
{
    public static class RestoreRecipeLinkEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreRecipeLink(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{linkId:int}/restore", Handle)
               .WithName("RestoreRecipeLink")
               .Produces<RecipeLinkResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeLinkResponse>, NotFound>> Handle(
            int householdId, int recipeId, int linkId,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            // Restore reactivates the link with its ORIGINAL rank to preserve position. If a live
            // link took that rank while it was deleted, the partial unique index rejects it; on that
            // retry we re-mint to the end.
            var attemptedReplace = false;
            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var recipe = await db.Recipes
                    .Include(r => r.Links)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeLinkResponse?)null;

                var result = recipe.RestoreLink(linkId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException(
                        $"RestoreRecipeLink cannot map error of type {result.Errors[0].GetType().Name}.");
                }

                if (attemptedReplace)
                {
                    recipe.ReplaceRestoredLinkRank(linkId);
                }
                attemptedReplace = true;

                await db.SaveChangesAsync(ct);
                return RecipeLinkResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Links/DeleteRecipeLink.cs Application/Frigorino.Features/Recipes/Links/RestoreRecipeLink.cs
git commit -m "feat(recipes): delete + restore source-link slices"
```

---

### Task 8: `ReorderRecipeLink`

**Files:**
- Create: `Application/Frigorino.Features/Recipes/Links/ReorderRecipeLink.cs`

> Note: reorder uses **PATCH** with the shared `ReorderItemRequest(int AfterId)` record, mirroring `ReorderRecipeSection` (the established precedent). The spec mentioned PUT; PATCH matches the codebase and the generated `reorderRecipeLinkMutation` the frontend hook will spread.

- [ ] **Step 1: Create the reorder slice**

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

namespace Frigorino.Features.Recipes.Links
{
    public static class ReorderRecipeLinkEndpoint
    {
        public static IEndpointRouteBuilder MapReorderRecipeLink(this IEndpointRouteBuilder app)
        {
            app.MapPatch("/{linkId:int}/reorder", Handle)
               .WithName("ReorderRecipeLink")
               .Produces<RecipeLinkResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RecipeLinkResponse>, NotFound>> Handle(
            int householdId, int recipeId, int linkId, ReorderItemRequest request,
            ICurrentUserService currentUser, ApplicationDbContext db, CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null) return TypedResults.NotFound();

            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var recipe = await db.Recipes
                    .Include(r => r.Links)
                    .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                if (recipe is null) return (RecipeLinkResponse?)null;

                var result = recipe.ReorderLink(linkId, request.AfterId);
                if (result.IsFailed)
                {
                    if (result.Errors[0] is EntityNotFoundError) return null;
                    throw new InvalidOperationException($"ReorderRecipeLink cannot map error of type {result.Errors[0].GetType().Name}.");
                }
                await db.SaveChangesAsync(ct);
                return RecipeLinkResponse.From(result.Value);
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Links/ReorderRecipeLink.cs
git commit -m "feat(recipes): reorder source-link slice"
```

---

### Task 9: Route group wiring + revision token

**Files:**
- Modify: `Application/Frigorino.Web/Program.cs`
- Modify: `Application/Frigorino.Features/Recipes/GetRecipeRevision.cs`

- [ ] **Step 1: Add the `using`**

In `Program.cs`, after `using Frigorino.Features.Recipes.Sections;` (line ~17):

```csharp
using Frigorino.Features.Recipes.Links;
```

- [ ] **Step 2: Register the route group**

In `Program.cs`, after the `recipeSections` group block (after `recipeSections.MapReorderRecipeSection();`, line ~449):

```csharp

var recipeLinks = app.MapGroup("/api/household/{householdId:int}/recipes/{recipeId:int}/links")
    .RequireAuthorization()
    .WithTags("RecipeLinks");
recipeLinks.MapGetRecipeLinks();
recipeLinks.MapCreateRecipeLink();
recipeLinks.MapUpdateRecipeLink();
recipeLinks.MapDeleteRecipeLink();
recipeLinks.MapRestoreRecipeLink();
recipeLinks.MapReorderRecipeLink();
```

- [ ] **Step 3: Fold links into the revision token**

In `GetRecipeRevision.cs`, after the `sections`/`sectionMaxUpdatedAt`/`sectionCount` block, add:

```csharp
            var links = db.RecipeLinks.Where(l => l.RecipeId == recipeId && l.IsActive);
            var linkMaxUpdatedAt = await links.MaxAsync(l => (DateTime?)l.UpdatedAt, ct);
            var linkCount = await links.CountAsync(ct);
```

Then update the `maxUpdatedAt` reconciliation and `count` to include links:

```csharp
            DateTime? maxUpdatedAt = itemMaxUpdatedAt;
            if (sectionMaxUpdatedAt is not null && (maxUpdatedAt is null || sectionMaxUpdatedAt > maxUpdatedAt))
            {
                maxUpdatedAt = sectionMaxUpdatedAt;
            }
            if (linkMaxUpdatedAt is not null && (maxUpdatedAt is null || linkMaxUpdatedAt > maxUpdatedAt))
            {
                maxUpdatedAt = linkMaxUpdatedAt;
            }
            var count = itemCount + sectionCount + linkCount;
```

- [ ] **Step 4: Build the solution**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/Program.cs Application/Frigorino.Features/Recipes/GetRecipeRevision.cs
git commit -m "feat(recipes): wire source-link route group + fold links into revision token"
```

---

### Task 10: Regenerate the TS client + API integration scenarios

**Files:**
- Generated: `ClientApp/src/lib/openapi.json`, `ClientApp/src/lib/api/**`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/ScenarioContextHolder.cs`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeLinkApiSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature`

- [ ] **Step 1: Regenerate the client**

Run (from `Application/Frigorino.Web/ClientApp`):
```bash
npm run api
```
Expected: rebuilds the backend, emits `openapi.json`, regenerates `src/lib/api`. Confirm the generated `@tanstack/react-query.gen.ts` now contains `getRecipeLinksOptions`, `getRecipeLinksQueryKey`, `createRecipeLinkMutation`, `updateRecipeLinkMutation`, `deleteRecipeLinkMutation`, `restoreRecipeLinkMutation`, `reorderRecipeLinkMutation`, and `types.gen.ts` has `RecipeLinkResponse`.

```bash
grep -c "RecipeLink" src/lib/api/@tanstack/react-query.gen.ts
```
Expected: a non-zero count.

- [ ] **Step 2: Add the link id map to the context holder**

In `ScenarioContextHolder.cs`, after the `RecipeSectionIds` dictionary (line ~15):

```csharp
    public Dictionary<(string Recipe, string Label), int> RecipeLinkIds { get; } = new();
```

- [ ] **Step 3: Add link helpers to `TestApiClient`**

In `TestApiClient.cs`, after `TryRestoreRecipeSectionAsync` (~line 524), add:

```csharp
    public Task<IAPIResponse> TryGetRecipeLinksAsync(int recipeId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/links",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryCreateRecipeLinkAsync(int recipeId, string? url, string? label = null, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/links",
            new APIRequestContextOptions
            {
                DataObject = new { url, label },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryDeleteRecipeLinkAsync(int recipeId, int linkId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/links/{linkId}",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryRestoreRecipeLinkAsync(int recipeId, int linkId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/recipes/{recipeId}/links/{linkId}/restore",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }
```

- [ ] **Step 4: Create the step bindings**

Create `Application/Frigorino.IntegrationTests/Slices/Recipes/RecipeLinkApiSteps.cs`:

```csharp
namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeLinkApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // Reused as both When (action under test) and Given/And (setup). This project's Reqnroll is
    // keyword-sensitive, so both attributes are required.
    [Given("I POST a source link {string} labelled {string} to recipe {string} via the API")]
    [When("I POST a source link {string} labelled {string} to recipe {string} via the API")]
    public async Task WhenIPostLink(string url, string label, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        var response = await api.TryCreateRecipeLinkAsync(recipeId, url, label);
        ctx.LastApiResponse = response;
        if (response.Status == 201)
        {
            var json = (await response.JsonAsync())!.Value;
            ctx.RecipeLinkIds[(recipeName, label)] = json.GetProperty("id").GetInt32();
        }
    }

    [When("I POST a source link {string} with no scheme to recipe {string} via the API")]
    public async Task WhenIPostInvalidLink(string url, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryCreateRecipeLinkAsync(recipeId, url, null);
    }

    [When("I GET the source links of recipe {string} via the API")]
    public async Task WhenIGetLinks(string recipeName)
    {
        ctx.LastApiResponse = await api.TryGetRecipeLinksAsync(ctx.RecipeIds[recipeName]);
    }

    [Then("the API source links of recipe {string} number {int}")]
    public async Task ThenLinksNumber(string recipeName, int expected)
    {
        var response = await api.TryGetRecipeLinksAsync(ctx.RecipeIds[recipeName]);
        Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        Assert.Equal(expected, json.GetArrayLength());
    }

    [When("I DELETE the source link {string} of recipe {string} via the API")]
    public async Task WhenIDeleteLink(string label, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryDeleteRecipeLinkAsync(recipeId, ctx.RecipeLinkIds[(recipeName, label)]);
    }

    [When("I POST restore for the source link {string} of recipe {string} via the API")]
    public async Task WhenIRestoreLink(string label, string recipeName)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.LastApiResponse = await api.TryRestoreRecipeLinkAsync(recipeId, ctx.RecipeLinkIds[(recipeName, label)]);
    }
}
```

- [ ] **Step 5: Add the API scenarios**

Append to `Application/Frigorino.IntegrationTests/Slices/Recipes/Recipes.Api.feature`:

```gherkin

  Scenario: A new recipe has no source links
    Given there is a recipe named "Pizza"
    When I GET the source links of recipe "Pizza" via the API
    Then the API response status is 200
    And the API source links of recipe "Pizza" number 0

  Scenario: Adding a valid source link succeeds and is listed
    Given there is a recipe named "Pizza"
    When I POST a source link "https://example.com/pizza" labelled "Best Pizza" to recipe "Pizza" via the API
    Then the API response status is 201
    And the API source links of recipe "Pizza" number 1

  Scenario: Adding a non-http source link returns a validation error
    Given there is a recipe named "Pizza"
    When I POST a source link "ftp://example.com/file" with no scheme to recipe "Pizza" via the API
    Then the API response status is 400
    And the API response has a validation error for "Url"

  Scenario: Deleting a source link removes it, and restore brings it back
    Given there is a recipe named "Pizza"
    And I POST a source link "https://example.com/pizza" labelled "Best Pizza" to recipe "Pizza" via the API
    When I DELETE the source link "Best Pizza" of recipe "Pizza" via the API
    Then the API response status is 204
    And the API source links of recipe "Pizza" number 0
    When I POST restore for the source link "Best Pizza" of recipe "Pizza" via the API
    Then the API response status is 200
    And the API source links of recipe "Pizza" number 1

  Scenario: A source-link change moves the recipe revision token
    Given there is a recipe named "Pizza"
    When I capture the revision of recipe "Pizza" via the API
    And I POST a source link "https://example.com/pizza" labelled "Best Pizza" to recipe "Pizza" via the API
    And I capture the revision of recipe "Pizza" via the API
    Then the two captured recipe revisions differ
```

(The `the API response has a validation error for "..."`, `I capture the revision ...`, and `the two captured recipe revisions differ` steps already exist — reused as-is.)

- [ ] **Step 6: Build the IT project (full IT run happens in Task 16)**

Run: `dotnet build Application/Frigorino.IntegrationTests`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib Application/Frigorino.IntegrationTests/
git commit -m "feat(recipes): regenerate client + source-link API integration scenarios"
```

---

## Phase C — Frontend hooks

### Task 11: Source-link hooks + revision invalidation

**Files:**
- Create: `ClientApp/src/features/recipes/links/useRecipeLinks.ts`
- Create: `ClientApp/src/features/recipes/links/useCreateRecipeLink.ts`
- Create: `ClientApp/src/features/recipes/links/useUpdateRecipeLink.ts`
- Create: `ClientApp/src/features/recipes/links/useRestoreRecipeLink.ts`
- Create: `ClientApp/src/features/recipes/links/useDeleteRecipeLink.ts`
- Create: `ClientApp/src/features/recipes/links/useReorderRecipeLink.ts`
- Modify: `ClientApp/src/features/recipes/items/useRecipeRevision.ts`

> Link create mirrors `useCreateRecipeSection` (invalidate-only, **no** optimistic temp row), so the temp-id reconcile concern does not apply.

- [ ] **Step 1: Query hook** — `useRecipeLinks.ts`

```ts
import { useQuery } from "@tanstack/react-query";
import { getRecipeLinksOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useRecipeLinks = (
    householdId: number,
    recipeId: number,
    enabled = true,
) =>
    useQuery({
        ...getRecipeLinksOptions({ path: { householdId, recipeId } }),
        enabled: enabled && householdId > 0 && recipeId > 0,
        staleTime: 1000 * 30,
    });
```

- [ ] **Step 2: Create hook** — `useCreateRecipeLink.ts`

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createRecipeLinkMutation,
    getRecipeLinksQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useCreateRecipeLink = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createRecipeLinkMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeLinksQueryKey({
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

- [ ] **Step 3: Update hook** — `useUpdateRecipeLink.ts`

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeLinksQueryKey,
    updateRecipeLinkMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useUpdateRecipeLink = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateRecipeLinkMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeLinksQueryKey({
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

- [ ] **Step 4: Restore hook** — `useRestoreRecipeLink.ts`

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeLinksQueryKey,
    restoreRecipeLinkMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreRecipeLink = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreRecipeLinkMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeLinksQueryKey({
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

- [ ] **Step 5: Delete hook (optimistic + undo toast)** — `useDeleteRecipeLink.ts`

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteRecipeLinkMutation,
    getRecipeLinksQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeLinkResponse } from "../../../lib/api/types.gen";
import { useRestoreRecipeLink } from "./useRestoreRecipeLink";

export const useDeleteRecipeLink = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreLink = useRestoreRecipeLink();

    return useMutation({
        ...deleteRecipeLinkMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeLinksQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousLinks =
                queryClient.getQueryData<RecipeLinkResponse[]>(queryKey);

            queryClient.setQueryData<RecipeLinkResponse[]>(queryKey, (old) =>
                old?.filter((l) => l.id !== variables.path.linkId),
            );

            return { previousLinks };
        },
        onError: (_data, variables, context) => {
            if (context?.previousLinks) {
                queryClient.setQueryData(
                    getRecipeLinksQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousLinks,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("recipes.linkDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreLink.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeLinksQueryKey({
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

- [ ] **Step 6: Reorder hook (optimistic)** — `useReorderRecipeLink.ts`

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getRecipeLinksQueryKey,
    reorderRecipeLinkMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeLinkResponse } from "../../../lib/api/types.gen";

export const useReorderRecipeLink = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderRecipeLinkMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeLinksQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousLinks =
                queryClient.getQueryData<RecipeLinkResponse[]>(queryKey);

            queryClient.setQueryData<RecipeLinkResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const moved = old.find((l) => l.id === variables.path.linkId);
                if (!moved) return old;
                const others = old.filter((l) => l.id !== moved.id);
                const afterId = variables.body.afterId;
                if (!afterId) {
                    others.unshift(moved);
                    return others;
                }
                const anchorIdx = others.findIndex((l) => l.id === afterId);
                others.splice(
                    anchorIdx === -1 ? others.length : anchorIdx + 1,
                    0,
                    moved,
                );
                return others;
            });

            return { previousLinks };
        },
        onError: (_data, variables, context) => {
            if (context?.previousLinks) {
                queryClient.setQueryData(
                    getRecipeLinksQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousLinks,
                );
            }
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeLinksQueryKey({
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

- [ ] **Step 7: Add links to the revision invalidation**

In `items/useRecipeRevision.ts`, add `getRecipeLinksQueryKey` to the import from `react-query.gen`, then add a third `useRevisionInvalidation` call after the sections one:

```ts
    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getRecipeLinksQueryKey({
            path: { householdId, recipeId },
        }),
        isLocalMutation: (variables) =>
            (variables as { path?: { recipeId?: number } } | undefined)?.path
                ?.recipeId === recipeId,
    });
```

- [ ] **Step 8: Type-check**

Run (from `Application/Frigorino.Web/ClientApp`): `npm run tsc`
Expected: no errors.

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/links Application/Frigorino.Web/ClientApp/src/features/recipes/items/useRecipeRevision.ts
git commit -m "feat(recipes): source-link query/mutation hooks + revision invalidation"
```

---

## Phase D — Frontend UI

### Task 12: `SortableLinkList` + `RecipeLinkRow`

**Files:**
- Create: `ClientApp/src/components/sortables/SortableLinkList.tsx`
- Create: `ClientApp/src/features/recipes/links/components/RecipeLinkRow.tsx`

- [ ] **Step 1: Create `SortableLinkList`** (mirror `SortableSectionList`, link testids)

```tsx
import {
    closestCenter,
    DndContext,
    PointerSensor,
    TouchSensor,
    useSensor,
    useSensors,
    type DragEndEvent,
} from "@dnd-kit/core";
import {
    SortableContext,
    useSortable,
    verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { DragIndicator } from "@mui/icons-material";
import { Box } from "@mui/material";
import type { ReactNode } from "react";

interface SortableLinkItem {
    id: number;
}

interface SortableLinkListProps<T extends SortableLinkItem> {
    links: T[];
    onReorder: (linkId: number, afterId: number) => Promise<void>;
    // The drag handle is rendered here (where useSortable lives) and handed to the row to place —
    // spreading the dnd-kit listeners/attributes in the hook's own component satisfies the React
    // Compiler ref rule.
    renderLink: (link: T, dragHandle: ReactNode) => ReactNode;
}

function SortableLink<T extends SortableLinkItem>({
    link,
    renderLink,
}: {
    link: T;
    renderLink: (link: T, dragHandle: ReactNode) => ReactNode;
}) {
    const {
        attributes,
        listeners,
        setNodeRef,
        setActivatorNodeRef,
        transform,
        transition,
        isDragging,
    } = useSortable({ id: link.id });

    const dragHandle = (
        <Box
            ref={setActivatorNodeRef}
            {...attributes}
            {...listeners}
            sx={{
                display: "flex",
                alignItems: "center",
                cursor: "grab",
                mr: 1,
                touchAction: "none",
            }}
            data-testid={`recipe-link-drag-handle-${link.id}`}
        >
            <DragIndicator fontSize="small" color="action" />
        </Box>
    );

    return (
        <Box
            ref={setNodeRef}
            sx={{
                transform: CSS.Transform.toString(transform),
                transition,
                opacity: isDragging ? 0.5 : 1,
                mb: 1,
            }}
        >
            {renderLink(link, dragHandle)}
        </Box>
    );
}

export function SortableLinkList<T extends SortableLinkItem>({
    links,
    onReorder,
    renderLink,
}: SortableLinkListProps<T>) {
    const sensors = useSensors(
        useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
        useSensor(TouchSensor, {
            activationConstraint: { delay: 200, tolerance: 5 },
        }),
    );

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        if (!over || active.id === over.id) return;
        const ids = links.map((l) => l.id);
        const from = ids.indexOf(Number(active.id));
        const to = ids.indexOf(Number(over.id));
        if (from === -1 || to === -1) return;
        // afterId = the link that will sit directly above the dropped one (0 = top).
        const afterId = to > from ? ids[to] : to > 0 ? ids[to - 1] : 0;
        void onReorder(Number(active.id), afterId);
    };

    return (
        <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={handleDragEnd}
        >
            <SortableContext
                items={links.map((l) => l.id)}
                strategy={verticalListSortingStrategy}
            >
                {links.map((link) => (
                    <SortableLink
                        key={link.id}
                        link={link}
                        renderLink={renderLink}
                    />
                ))}
            </SortableContext>
        </DndContext>
    );
}
```

- [ ] **Step 2: Create `RecipeLinkRow`** (single editable row, debounced save)

```tsx
import { Delete } from "@mui/icons-material";
import { IconButton, Stack, TextField } from "@mui/material";
import type { ReactNode } from "react";
import {
    useCallback,
    useEffect,
    useLayoutEffect,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import type { RecipeLinkResponse } from "../../../../lib/api";
import { useUpdateRecipeLink } from "../useUpdateRecipeLink";

const SAVE_DEBOUNCE_MS = 600;

// A valid http(s) URL — mirrors the server-side aggregate check so we can show an inline hint.
const isHttpUrl = (value: string): boolean => {
    const trimmed = value.trim();
    if (!trimmed) return false;
    try {
        const parsed = new URL(trimmed);
        return parsed.protocol === "http:" || parsed.protocol === "https:";
    } catch {
        return false;
    }
};

interface RecipeLinkRowProps {
    householdId: number;
    recipeId: number;
    link: RecipeLinkResponse;
    onDelete: () => void;
    dragHandle: ReactNode;
}

export const RecipeLinkRow = ({
    householdId,
    recipeId,
    link,
    onDelete,
    dragHandle,
}: RecipeLinkRowProps) => {
    const { t } = useTranslation();
    const updateLink = useUpdateRecipeLink();

    const [label, setLabel] = useState(link.label ?? "");
    const [url, setUrl] = useState(link.url);
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    // Latest field state, read by the debounced/blur flush without re-creating the timer.
    const latest = useRef({ label, url });
    useLayoutEffect(() => {
        latest.current = { label, url };
    });

    const { mutate } = updateLink;

    const save = useCallback(() => {
        // Skip the save when the URL is invalid — the server would 400; the inline error guides the
        // user. A blur/flush with a still-invalid URL simply leaves the last good value persisted.
        if (!isHttpUrl(latest.current.url)) return;
        mutate({
            path: { householdId, recipeId, linkId: link.id },
            body: {
                url: latest.current.url.trim(),
                label: latest.current.label.trim() || null,
            },
        });
    }, [mutate, householdId, recipeId, link.id]);

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

    const urlInvalid = url.trim().length > 0 && !isHttpUrl(url);

    return (
        <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: "flex-start" }}
            data-testid={`recipe-link-row-${link.id}`}
        >
            {dragHandle}
            <Stack spacing={1} sx={{ flex: 1 }}>
                <TextField
                    label={t("recipes.linkLabel")}
                    value={label}
                    onChange={(e) => {
                        setLabel(e.target.value);
                        scheduleSave();
                    }}
                    onBlur={flushSave}
                    size="small"
                    fullWidth
                    placeholder={t("recipes.linkLabelPlaceholder")}
                    slotProps={{
                        htmlInput: {
                            maxLength: 255,
                            "data-testid": `recipe-link-${link.id}-label-input`,
                        },
                    }}
                />
                <TextField
                    label={t("recipes.linkUrl")}
                    value={url}
                    onChange={(e) => {
                        setUrl(e.target.value);
                        scheduleSave();
                    }}
                    onBlur={flushSave}
                    size="small"
                    fullWidth
                    error={urlInvalid}
                    helperText={urlInvalid ? t("recipes.invalidUrl") : undefined}
                    placeholder={t("recipes.linkUrlPlaceholder")}
                    slotProps={{
                        htmlInput: {
                            maxLength: 2048,
                            "data-testid": `recipe-link-${link.id}-url-input`,
                        },
                    }}
                />
            </Stack>
            <IconButton
                size="small"
                onClick={onDelete}
                data-testid={`recipe-link-${link.id}-delete`}
            >
                <Delete fontSize="small" color="error" />
            </IconButton>
        </Stack>
    );
};
```

- [ ] **Step 3: Type-check**

Run (from `ClientApp`): `npm run tsc`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/sortables/SortableLinkList.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/links/components/RecipeLinkRow.tsx
git commit -m "feat(recipes): SortableLinkList + editable RecipeLinkRow"
```

---

### Task 13: `RecipeLinksSection` + wire into the edit page

**Files:**
- Create: `ClientApp/src/features/recipes/links/components/RecipeLinksSection.tsx`
- Modify: `ClientApp/src/features/recipes/pages/RecipeEditPage.tsx`

- [ ] **Step 1: Create `RecipeLinksSection`** (collapsible block: list + draft composer)

```tsx
import { Add } from "@mui/icons-material";
import { Box, Button, Stack, TextField } from "@mui/material";
import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { CollapsibleSection } from "../../../../components/shared/CollapsibleSection";
import { SortableLinkList } from "../../../../components/sortables/SortableLinkList";
import { usePersistedExpanded } from "../../../../hooks/usePersistedExpanded";
import { useCreateRecipeLink } from "../useCreateRecipeLink";
import { useDeleteRecipeLink } from "../useDeleteRecipeLink";
import { useRecipeLinks } from "../useRecipeLinks";
import { useReorderRecipeLink } from "../useReorderRecipeLink";
import { RecipeLinkRow } from "./RecipeLinkRow";

// A valid http(s) URL — mirrors the server-side aggregate check.
const isHttpUrl = (value: string): boolean => {
    const trimmed = value.trim();
    if (!trimmed) return false;
    try {
        const parsed = new URL(trimmed);
        return parsed.protocol === "http:" || parsed.protocol === "https:";
    } catch {
        return false;
    }
};

interface RecipeLinksSectionProps {
    householdId: number;
    recipeId: number;
}

export const RecipeLinksSection = ({
    householdId,
    recipeId,
}: RecipeLinksSectionProps) => {
    const { t } = useTranslation();
    const [expanded, setExpanded] = usePersistedExpanded(
        "recipe-edit-section:links",
        false,
    );

    const { data: links = [] } = useRecipeLinks(householdId, recipeId);
    const createLink = useCreateRecipeLink();
    const deleteLink = useDeleteRecipeLink();
    const reorderLink = useReorderRecipeLink();

    // Local draft composer — a link can't be created empty (URL is required), so it POSTs only on
    // submit once a valid URL is entered.
    const [draftOpen, setDraftOpen] = useState(false);
    const [draftLabel, setDraftLabel] = useState("");
    const [draftUrl, setDraftUrl] = useState("");

    const resetDraft = useCallback(() => {
        setDraftOpen(false);
        setDraftLabel("");
        setDraftUrl("");
    }, []);

    const draftUrlInvalid = draftUrl.trim().length > 0 && !isHttpUrl(draftUrl);
    const canSubmitDraft = isHttpUrl(draftUrl);

    const handleSubmitDraft = useCallback(async () => {
        if (!canSubmitDraft) return;
        await createLink.mutateAsync({
            path: { householdId, recipeId },
            body: {
                url: draftUrl.trim(),
                label: draftLabel.trim() || null,
            },
        });
        resetDraft();
    }, [
        canSubmitDraft,
        createLink,
        householdId,
        recipeId,
        draftUrl,
        draftLabel,
        resetDraft,
    ]);

    return (
        <CollapsibleSection
            title={t("recipes.sourceLinks")}
            expanded={expanded}
            onChange={setExpanded}
            testId="recipe-section-links"
        >
            <Stack spacing={1}>
                <SortableLinkList
                    links={links}
                    onReorder={async (linkId, afterId) => {
                        await reorderLink.mutateAsync({
                            path: { householdId, recipeId, linkId },
                            body: { afterId },
                        });
                    }}
                    renderLink={(link, dragHandle) => (
                        <RecipeLinkRow
                            householdId={householdId}
                            recipeId={recipeId}
                            link={link}
                            onDelete={() =>
                                deleteLink.mutate({
                                    path: {
                                        householdId,
                                        recipeId,
                                        linkId: link.id,
                                    },
                                })
                            }
                            dragHandle={dragHandle}
                        />
                    )}
                />

                {draftOpen ? (
                    <Stack
                        spacing={1}
                        data-testid="recipe-link-draft"
                        sx={{ pt: 1 }}
                    >
                        <TextField
                            label={t("recipes.linkLabel")}
                            value={draftLabel}
                            onChange={(e) => setDraftLabel(e.target.value)}
                            size="small"
                            fullWidth
                            placeholder={t("recipes.linkLabelPlaceholder")}
                            slotProps={{
                                htmlInput: {
                                    maxLength: 255,
                                    "data-testid": "recipe-link-draft-label-input",
                                },
                            }}
                        />
                        <TextField
                            label={t("recipes.linkUrl")}
                            value={draftUrl}
                            onChange={(e) => setDraftUrl(e.target.value)}
                            size="small"
                            fullWidth
                            autoFocus
                            error={draftUrlInvalid}
                            helperText={
                                draftUrlInvalid
                                    ? t("recipes.invalidUrl")
                                    : undefined
                            }
                            placeholder={t("recipes.linkUrlPlaceholder")}
                            slotProps={{
                                htmlInput: {
                                    maxLength: 2048,
                                    "data-testid": "recipe-link-draft-url-input",
                                },
                            }}
                        />
                        <Stack direction="row" spacing={1}>
                            <Button
                                size="small"
                                variant="contained"
                                disabled={!canSubmitDraft || createLink.isPending}
                                onClick={handleSubmitDraft}
                                data-testid="recipe-link-draft-submit"
                            >
                                {t("common.add")}
                            </Button>
                            <Button
                                size="small"
                                onClick={resetDraft}
                                data-testid="recipe-link-draft-cancel"
                            >
                                {t("common.cancel")}
                            </Button>
                        </Stack>
                    </Stack>
                ) : (
                    <Box>
                        <Button
                            startIcon={<Add />}
                            onClick={() => setDraftOpen(true)}
                            data-testid="recipe-add-link"
                            sx={{ alignSelf: "flex-start" }}
                        >
                            {t("recipes.addLink")}
                        </Button>
                    </Box>
                )}
            </Stack>
        </CollapsibleSection>
    );
};
```

- [ ] **Step 2: Verify the `common.add` / `common.cancel` keys exist**

Run (from `ClientApp`):
```bash
grep -E '"add"|"cancel"|"undo"' public/locales/en/translation.json
```
Expected: `add`, `cancel`, and `undo` are present under `common`. If `add`/`cancel` are missing, add them in Task 15 alongside the recipe strings (the `recipes.*` keys are added there).

- [ ] **Step 3: Wire into `RecipeEditPage`**

In `RecipeEditPage.tsx`, add the import near the other recipe imports:

```tsx
import { RecipeLinksSection } from "../links/components/RecipeLinksSection";
```

Then, in the JSX `<Stack spacing={2}>`, insert the block **between** the Details `</CollapsibleSection>` and the `<SortableSectionList ... />`:

```tsx
                        <RecipeLinksSection
                            householdId={householdId}
                            recipeId={recipeId}
                        />
```

- [ ] **Step 4: Type-check + lint**

Run (from `ClientApp`): `npm run tsc && npm run lint`
Expected: no errors. (Watch for `react-hooks/refs` — the `latest` ref is synced in `useLayoutEffect`, not during render, mirroring `RecipeSectionCard`.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/links/components/RecipeLinksSection.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeEditPage.tsx
git commit -m "feat(recipes): edit-page source-links section between details and ingredients"
```

---

### Task 14: `RecipeViewLinks` + wire into the view page

**Files:**
- Create: `ClientApp/src/features/recipes/links/components/RecipeViewLinks.tsx`
- Modify: `ClientApp/src/features/recipes/pages/RecipeViewPage.tsx`

- [ ] **Step 1: Create `RecipeViewLinks`** (sources block; hidden when empty)

```tsx
import { Container, Link, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useRecipeLinks } from "../useRecipeLinks";

interface RecipeViewLinksProps {
    householdId: number;
    recipeId: number;
}

export const RecipeViewLinks = ({
    householdId,
    recipeId,
}: RecipeViewLinksProps) => {
    const { t } = useTranslation();
    const { data: links = [] } = useRecipeLinks(householdId, recipeId);

    if (links.length === 0) return null;

    return (
        <Container
            maxWidth="sm"
            data-testid="recipe-view-links"
            sx={{ px: 2, pb: 1, flexShrink: 0 }}
        >
            <Typography
                variant="overline"
                color="text.secondary"
                sx={{ fontWeight: 700, letterSpacing: 1 }}
            >
                {t("recipes.sourceLinks")}
            </Typography>
            <Stack spacing={0.5} sx={{ mt: 0.5 }}>
                {links.map((link) => (
                    <Link
                        key={link.id}
                        href={link.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        variant="body2"
                        data-testid={`recipe-link-${link.id}`}
                        sx={{ wordBreak: "break-word" }}
                    >
                        {link.label?.trim() || link.url}
                    </Link>
                ))}
            </Stack>
        </Container>
    );
};
```

- [ ] **Step 2: Wire into `RecipeViewPage`**

In `RecipeViewPage.tsx`, add the import:

```tsx
import { RecipeViewLinks } from "../links/components/RecipeViewLinks";
```

Then render it **after the description `Container` block and before `<SearchInputRow ... />`**:

```tsx
            <RecipeViewLinks householdId={householdId} recipeId={recipeId} />
```

- [ ] **Step 3: Type-check + lint**

Run (from `ClientApp`): `npm run tsc && npm run lint`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/links/components/RecipeViewLinks.tsx Application/Frigorino.Web/ClientApp/src/features/recipes/pages/RecipeViewPage.tsx
git commit -m "feat(recipes): view-page sources block (clickable external links)"
```

---

## Phase E — i18n + verification

### Task 15: i18n strings

**Files:**
- Modify: `ClientApp/public/locales/en/translation.json`
- Modify: `ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add English strings**

In `en/translation.json`, under the `"recipes"` object (next to the section keys), add:

```json
    "sourceLinks": "Source links",
    "addLink": "Add link",
    "linkUrl": "URL",
    "linkLabel": "Display text",
    "linkUrlPlaceholder": "https://example.com/recipe",
    "linkLabelPlaceholder": "e.g. The original blog post",
    "deleteLink": "Delete link",
    "linkDeleted": "Link deleted",
    "invalidUrl": "Enter a valid http(s) URL"
```

If `common.add` / `common.cancel` were found missing in Task 13 Step 2, also add under `"common"`:

```json
    "add": "Add",
    "cancel": "Cancel"
```

- [ ] **Step 2: Add German strings**

In `de/translation.json`, under `"recipes"`:

```json
    "sourceLinks": "Quellenlinks",
    "addLink": "Link hinzufügen",
    "linkUrl": "URL",
    "linkLabel": "Anzeigetext",
    "linkUrlPlaceholder": "https://example.com/rezept",
    "linkLabelPlaceholder": "z. B. Der ursprüngliche Blogbeitrag",
    "deleteLink": "Link löschen",
    "linkDeleted": "Link gelöscht",
    "invalidUrl": "Gib eine gültige http(s)-URL ein"
```

If needed, under `"common"`: `"add": "Hinzufügen"`, `"cancel": "Abbrechen"`.

- [ ] **Step 3: Validate JSON + format**

Run (from `ClientApp`):
```bash
node -e "JSON.parse(require('fs').readFileSync('public/locales/en/translation.json','utf8'));JSON.parse(require('fs').readFileSync('public/locales/de/translation.json','utf8'));console.log('ok')"
npm run fix
```
Expected: `ok`, then eslint/prettier complete without errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales
git commit -m "i18n(recipes): source-link strings (en, de)"
```

---

### Task 16: Full verification gate

**Files:** none (verification only).

- [ ] **Step 1: Frontend static checks**

Run (from `Application/Frigorino.Web/ClientApp`): `npm run tsc && npm run lint`
Expected: both clean.

- [ ] **Step 2: Prettier check**

Run (from `ClientApp`): `npm run prettier:check` (or `npm run fix` then confirm no diff)
Expected: no formatting issues.

- [ ] **Step 3: Build the SPA** (required before IT — the harness serves `ClientApp/build`)

Run (from `ClientApp`): `npm run build`
Expected: build succeeds, `ClientApp/build` updated.

- [ ] **Step 4: Full solution tests** (unit + integration; needs Docker for Testcontainers)

Run (from repo root): `dotnet test Application/Frigorino.sln`
Expected: all green (existing + new recipe-link aggregate tests, API scenarios, and the purge IT). If Docker is unreachable, ask the user to start Docker Desktop rather than skipping.
Read the pass/fail summary lines directly — do not trust a piped `tail` exit code. If any IT fails, read the dumped server Warning+ logs in the test output first.

- [ ] **Step 5: Docker image build** (catches Dockerfile/SPA/pipeline drift)

Run (from repo root): `docker build -f Application/Dockerfile -t frigorino .`
Expected: exit 0. (The two `VITE_FCM_VAPID_KEY` ARG/ENV secret warnings are pre-existing and expected.)

- [ ] **Step 6: Final commit (only if any verification step required a fix)**

```bash
git add -A
git commit -m "chore(recipes): verification fixes for source links"
```

(If nothing changed, skip.)

---

## Manual UI verification (optional, recommended before integration)

Not a code task — offered for completeness (per the team's manual-verify net for plan-baked runtime bugs). Bring up the dev stack (`/dev-up`), then with Playwright MCP on the printed SPA URL:
1. Open a recipe → edit. Expand "Source links", click **Add link**, enter a label + a valid `https://` URL, submit → row appears.
2. Enter an invalid URL in a row → inline error shows, no save fires.
3. Add a second link, drag to reorder → order persists across reload.
4. Delete a link → undo toast → click undo → link returns.
5. Open the view page → "Sources" block shows the links as clickable external links (new tab); with zero links the block is absent.

---

## Self-Review (completed by plan author)

**Spec coverage:**
- Data model (entity, EF config, partial unique index, DbSet, migration, no backfill) → Tasks 1, 3. ✓
- Aggregate methods + http(s) validation → Task 2. ✓
- Slices (get/create/update/delete/restore/reorder) + response DTO → Tasks 5–8. ✓
- Route group + revision token fold-in → Task 9. ✓
- Purge wiring + IT → Task 4. ✓
- Client regen + API IT scenarios → Task 10. ✓
- Hooks (query/create/update/delete-undo/restore/reorder) + revision invalidation → Task 11. ✓
- Edit-page static collapsible block (between Details and sections), drag rows, draft composer → Tasks 12, 13. ✓
- View-page sources block (clickable, hidden when empty) → Task 14. ✓
- i18n en + de → Task 15. ✓
- Tests (aggregate, API IT, purge IT) → Tasks 2, 4, 10. ✓
- Verification gate → Task 16. ✓

**Type/name consistency:** Entity `RecipeLink` (Url/Label/Rank/IsActive); aggregate methods `AddLink`/`UpdateLink`/`RemoveLink`/`RestoreLink`/`ReplaceRestoredLinkRank`/`ReorderLink`; DTO `RecipeLinkResponse`; request records `CreateRecipeLinkRequest`/`UpdateRecipeLinkRequest` + shared `ReorderItemRequest`; generated hooks `getRecipeLinks*`/`createRecipeLinkMutation`/etc.; testids `recipe-section-links`, `recipe-add-link`, `recipe-link-row-{id}`, `recipe-link-{id}-url-input`/`-label-input`, `recipe-link-{id}-delete`, `recipe-link-draft*`, `recipe-view-links`, `recipe-link-{id}`. Consistent across tasks.

**Deviations from spec (intentional, noted inline):**
- Reorder uses **PATCH** (not PUT) to match `ReorderRecipeSection` and the generated mutation name.
- `RestoreLink` has no in-method rank de-collision (links have no child rows); rank collisions are handled by the slice's `RankRetry` + `ReplaceRestoredLinkRank`, exactly as sections handle their own rank.
- Create hook is invalidate-only (no optimistic temp row), so the temp-id reconcile rule does not apply.

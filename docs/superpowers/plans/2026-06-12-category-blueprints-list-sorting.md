# Category Blueprints — List Sorting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a household curate named "category blueprints" (ordered subsets of supermarket aisles) and apply one to a list to deterministically reorder that list's unchecked items into the blueprint's walk-order, reading each item's already-classified `ProductCategory`.

**Architecture:** A new household-scoped `SortBlueprint` aggregate (+ `SortBlueprintCategory` child rows) with Owner/Admin-gated CRUD slices. A pure `BlueprintSorter` computes item order from the blueprint + per-item category (resolved via the `Product` catalog); a new `List.ApplyOrder` re-mints the unchecked section's fractional-index ranks. Frontend: a two-block dnd-kit blueprint editor in the household-settings area, and a "Sort by category" popup on the list page. No LLM calls at sort time — classification already runs in the background.

**Tech Stack:** .NET 10 vertical slices + EF Core (Postgres), FluentResults; React 19 + TanStack Router/Query + MUI + @dnd-kit; xUnit + Reqnroll/Playwright.

**Spec:** `docs/superpowers/specs/2026-06-12-category-blueprints-list-sorting-design.md`

**Branch:** `feat/category-blueprints` (already created off `stage`; the spec commit `b74e964` is on it).

---

## Conventions to honor (from the codebase + user preferences)

- **C# brace style:** always block `{ }`, even single-line `if`. Namespaces use block style.
- **Control flow:** `if/else` over ternary for business logic; extract multi-clause boolean checks into a named variable.
- **No EF migration churn for widths:** `SortBlueprint.Name` reuses width `255` (matches `List.NameMaxLength`).
- **Enums on the wire:** `ProductCategory` serializes as its **string name** (registered `JsonStringEnumConverter`); the TS client gets a string union. DB stores int (EF default) — no value mapping needed.
- **Slices:** one file = one endpoint; `private static Handle`; `TypedResults`; dispatch FluentResults errors (`AccessDeniedError` → `Forbid`, validation `Error` with `Property` metadata → `ToValidationProblem()`).
- **Frontend hooks:** one-hook-per-file; spread generated `*Options` / `*Mutation` / `get*QueryKey`; never hand-write `queryFn`/`mutationFn`/`queryKey`.
- **Tests never assert on translated text** — testids / `data-*` only.
- **Tooling via npm scripts:** `npm run lint` / `tsc` / `fix` / `api` (never raw `npx`).
- **Commits:** no `Co-Authored-By` / "Generated with" trailers.
- **Verification scoping:** cheap per-task checks (filtered tests / `tsc`+lint); full-sln `dotnet test` + `docker build` only at the final gate (Task 13).

---

## File Structure

**Create (backend):**
- `Application/Frigorino.Domain/Entities/SortBlueprint.cs` — aggregate (factory, `Update`, `SoftDelete`, `CreateDefault`, ordered-category helpers).
- `Application/Frigorino.Domain/Entities/SortBlueprintCategory.cs` — child entity (`BlueprintId`, `Category`, `OrderIndex`).
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/SortBlueprintConfiguration.cs`
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/SortBlueprintCategoryConfiguration.cs`
- `Application/Frigorino.Features/Households/Blueprints/SortBlueprintResponse.cs`
- `Application/Frigorino.Features/Households/Blueprints/GetBlueprints.cs`
- `Application/Frigorino.Features/Households/Blueprints/CreateBlueprint.cs`
- `Application/Frigorino.Features/Households/Blueprints/UpdateBlueprint.cs`
- `Application/Frigorino.Features/Households/Blueprints/DeleteBlueprint.cs`
- `Application/Frigorino.Features/Lists/Blueprints/BlueprintSorter.cs` — pure ordering.
- `Application/Frigorino.Features/Lists/Blueprints/ApplyBlueprint.cs` — apply slice.

**Modify (backend):**
- `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs` — DbSets + timestamp stamping.
- `Application/Frigorino.Domain/Entities/List.cs` — add `ApplyOrder`.
- `Application/Frigorino.Web/Program.cs` — wire the `blueprints` group + `lists.MapApplyBlueprint()`.

**Create (tests):**
- `Application/Frigorino.Test/Domain/SortBlueprintTests.cs`
- `Application/Frigorino.Test/Domain/ListApplyOrderTests.cs`
- `Application/Frigorino.Test/Features/BlueprintSorterTests.cs`
- `Application/Frigorino.IntegrationTests/Slices/Lists/Blueprints.Api.feature` (+ minimal step bindings)

**Create (frontend):**
- `src/features/blueprints/aisles.ts`
- `src/features/blueprints/useSortBlueprints.ts`, `useCreateSortBlueprint.ts`, `useUpdateSortBlueprint.ts`, `useDeleteSortBlueprint.ts`, `useApplyBlueprint.ts`
- `src/features/blueprints/components/BlueprintEditor.tsx`
- `src/features/blueprints/components/BlueprintCard.tsx`
- `src/features/blueprints/components/ApplyBlueprintDialog.tsx`
- `src/features/blueprints/pages/BlueprintsPage.tsx`
- `src/routes/household/blueprints.tsx`

**Modify (frontend):**
- `public/locales/en/translation.json`, `public/locales/de/translation.json` — aisle + blueprint keys.
- `src/features/households/pages/ManageHouseholdPage.tsx` — link to the blueprints page.
- `src/features/lists/pages/ListViewPage.tsx` — "Sort by category" menu action + dialog.
- Regenerated `src/lib/api/**` (via `npm run api`).

---

## Task 1: `SortBlueprint` + `SortBlueprintCategory` domain aggregate

**Files:**
- Create: `Application/Frigorino.Domain/Entities/SortBlueprintCategory.cs`
- Create: `Application/Frigorino.Domain/Entities/SortBlueprint.cs`
- Test: `Application/Frigorino.Test/Domain/SortBlueprintTests.cs`

- [ ] **Step 1: Write the child entity**

`SortBlueprintCategory.cs`:
```csharp
using Frigorino.Domain.Products;

namespace Frigorino.Domain.Entities
{
    // One ordered aisle within a blueprint. Rows are replaced wholesale on edit; the
    // composite key (BlueprintId, Category) enforces "an aisle appears at most once".
    public class SortBlueprintCategory
    {
        public int BlueprintId { get; set; }
        public ProductCategory Category { get; set; }
        public int OrderIndex { get; set; }

        public SortBlueprint Blueprint { get; set; } = null!;
    }
}
```

- [ ] **Step 2: Write the failing tests**

`SortBlueprintTests.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class SortBlueprintTests
    {
        private const int HouseholdId = 42;

        private static readonly ProductCategory[] ValidOrder =
        {
            ProductCategory.Produce, ProductCategory.DairyAndEggs, ProductCategory.Pantry,
        };

        [Fact]
        public void Create_Valid_BuildsOrderedCategories()
        {
            var result = SortBlueprint.Create(HouseholdId, " My Store ", ValidOrder, HouseholdRole.Admin);

            Assert.True(result.IsSuccess);
            var blueprint = result.Value;
            Assert.Equal(HouseholdId, blueprint.HouseholdId);
            Assert.Equal("My Store", blueprint.Name);
            Assert.True(blueprint.IsActive);
            Assert.Equal(
                new[] { ProductCategory.Produce, ProductCategory.DairyAndEggs, ProductCategory.Pantry },
                blueprint.OrderedCategories());
            Assert.Equal(new[] { 0, 1, 2 }, blueprint.Categories.OrderBy(c => c.OrderIndex).Select(c => c.OrderIndex));
        }

        [Fact]
        public void Create_Member_FailsWithAccessDenied()
        {
            var result = SortBlueprint.Create(HouseholdId, "Store", ValidOrder, HouseholdRole.Member);

            Assert.True(result.IsFailed);
            Assert.True(result.HasError<AccessDeniedError>());
        }

        [Fact]
        public void Create_BlankName_Fails()
        {
            var result = SortBlueprint.Create(HouseholdId, "   ", ValidOrder, HouseholdRole.Admin);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_EmptyCategories_Fails()
        {
            var result = SortBlueprint.Create(HouseholdId, "Store", Array.Empty<ProductCategory>(), HouseholdRole.Admin);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_DuplicateCategory_Fails()
        {
            var dupes = new[] { ProductCategory.Produce, ProductCategory.Produce };

            var result = SortBlueprint.Create(HouseholdId, "Store", dupes, HouseholdRole.Admin);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_SentinelCategory_Fails()
        {
            var withSentinel = new[] { ProductCategory.Produce, ProductCategory.Other };

            var result = SortBlueprint.Create(HouseholdId, "Store", withSentinel, HouseholdRole.Admin);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Update_Valid_ReplacesNameAndCategories()
        {
            var blueprint = SortBlueprint.Create(HouseholdId, "Store", ValidOrder, HouseholdRole.Owner).Value;

            var result = blueprint.Update("Renamed", new[] { ProductCategory.Bakery, ProductCategory.Produce }, HouseholdRole.Owner);

            Assert.True(result.IsSuccess);
            Assert.Equal("Renamed", blueprint.Name);
            Assert.Equal(new[] { ProductCategory.Bakery, ProductCategory.Produce }, blueprint.OrderedCategories());
        }

        [Fact]
        public void Update_Member_FailsWithAccessDenied()
        {
            var blueprint = SortBlueprint.Create(HouseholdId, "Store", ValidOrder, HouseholdRole.Owner).Value;

            var result = blueprint.Update("Renamed", ValidOrder, HouseholdRole.Member);

            Assert.True(result.IsFailed);
            Assert.True(result.HasError<AccessDeniedError>());
        }

        [Fact]
        public void SoftDelete_Admin_DeactivatesAndIsGated()
        {
            var blueprint = SortBlueprint.Create(HouseholdId, "Store", ValidOrder, HouseholdRole.Owner).Value;

            Assert.True(blueprint.SoftDelete(HouseholdRole.Member).IsFailed);
            Assert.True(blueprint.IsActive);

            Assert.True(blueprint.SoftDelete(HouseholdRole.Admin).IsSuccess);
            Assert.False(blueprint.IsActive);
        }

        [Fact]
        public void CreateDefault_CoversAll23AislesInOrder_NoSentinels()
        {
            var blueprint = SortBlueprint.CreateDefault(HouseholdId);

            var categories = blueprint.OrderedCategories();
            Assert.Equal(23, categories.Count);
            Assert.Equal(categories.Count, categories.Distinct().Count());
            Assert.DoesNotContain(ProductCategory.Unknown, categories);
            Assert.DoesNotContain(ProductCategory.Other, categories);
            Assert.Equal(ProductCategory.Produce, categories[0]);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail to compile**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~SortBlueprintTests"`
Expected: FAIL — `SortBlueprint` does not exist.

- [ ] **Step 4: Write the aggregate**

`SortBlueprint.cs`:
```csharp
using FluentResults;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Products;

namespace Frigorino.Domain.Entities
{
    // Household-scoped, named ordered subset of supermarket aisles ("walk-order"). Applying a
    // blueprint to a list reorders the list's unchecked items by these category ranks. Curated
    // and reordered by Owner/Admin; any member may apply one. Sentinels (Unknown/Other) can never
    // be ranked — items in those categories (or unclassified) sink to the bottom on apply.
    public class SortBlueprint
    {
        // Shares List.NameMaxLength's width so no new column-width constant / migration churn.
        public const int NameMaxLength = 255;
        public const string DefaultName = "Supermarket";

        // Canonical full walk-order over all 23 real aisles, used to seed the starter blueprint.
        private static readonly ProductCategory[] DefaultOrder =
        {
            ProductCategory.Produce, ProductCategory.Bakery, ProductCategory.DeliAndColdCuts,
            ProductCategory.Meat, ProductCategory.Fish, ProductCategory.DairyAndEggs,
            ProductCategory.Cheese, ProductCategory.Frozen, ProductCategory.Cereal,
            ProductCategory.Pantry, ProductCategory.CannedGoods, ProductCategory.Sauces,
            ProductCategory.OilsAndVinegar, ProductCategory.Spices, ProductCategory.Spreads,
            ProductCategory.Snacks, ProductCategory.Sweets, ProductCategory.Beverages,
            ProductCategory.Alcohol, ProductCategory.HouseholdAndCleaning,
            ProductCategory.HealthAndBeauty, ProductCategory.Baby, ProductCategory.Pet,
        };

        public int Id { get; set; }
        public int HouseholdId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Household Household { get; set; } = null!;
        public ICollection<SortBlueprintCategory> Categories { get; set; } = new List<SortBlueprintCategory>();

        public static Result<SortBlueprint> Create(
            int householdId, string name, IReadOnlyList<ProductCategory> orderedCategories, HouseholdRole callerRole)
        {
            if (!callerRole.CanManageSettings())
            {
                return Result.Fail<SortBlueprint>(
                    new AccessDeniedError("Only an owner or admin can manage sort blueprints."));
            }

            var errors = Validate(householdId, name, orderedCategories);
            if (errors.Count > 0)
            {
                return Result.Fail<SortBlueprint>(errors);
            }

            var blueprint = new SortBlueprint
            {
                HouseholdId = householdId,
                Name = name.Trim(),
                IsActive = true,
            };
            blueprint.ReplaceCategories(orderedCategories);
            return Result.Ok(blueprint);
        }

        public Result Update(string name, IReadOnlyList<ProductCategory> orderedCategories, HouseholdRole callerRole)
        {
            if (!callerRole.CanManageSettings())
            {
                return Result.Fail(new AccessDeniedError("Only an owner or admin can manage sort blueprints."));
            }

            var errors = Validate(HouseholdId, name, orderedCategories);
            if (errors.Count > 0)
            {
                return Result.Fail(errors);
            }

            Name = name.Trim();
            ReplaceCategories(orderedCategories);
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        public Result SoftDelete(HouseholdRole callerRole)
        {
            if (!callerRole.CanManageSettings())
            {
                return Result.Fail(new AccessDeniedError("Only an owner or admin can manage sort blueprints."));
            }

            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            return Result.Ok();
        }

        // System seed (no role gate): the starter "Supermarket" blueprint covering every aisle.
        public static SortBlueprint CreateDefault(int householdId)
        {
            var blueprint = new SortBlueprint
            {
                HouseholdId = householdId,
                Name = DefaultName,
                IsActive = true,
            };
            blueprint.ReplaceCategories(DefaultOrder);
            return blueprint;
        }

        public IReadOnlyList<ProductCategory> OrderedCategories()
        {
            return Categories.OrderBy(c => c.OrderIndex).Select(c => c.Category).ToList();
        }

        private void ReplaceCategories(IReadOnlyList<ProductCategory> orderedCategories)
        {
            Categories.Clear();
            for (var i = 0; i < orderedCategories.Count; i++)
            {
                Categories.Add(new SortBlueprintCategory
                {
                    Category = orderedCategories[i],
                    OrderIndex = i,
                });
            }
        }

        private static List<IError> Validate(int householdId, string name, IReadOnlyList<ProductCategory> orderedCategories)
        {
            var errors = new List<IError>();
            if (householdId <= 0)
            {
                errors.Add(new Error("Household id is required.")
                    .WithMetadata("Property", nameof(HouseholdId)));
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Blueprint name is required.")
                    .WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"Blueprint name must be {NameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Name)));
            }
            if (orderedCategories.Count == 0)
            {
                errors.Add(new Error("A blueprint must include at least one aisle.")
                    .WithMetadata("Property", nameof(Categories)));
            }
            if (orderedCategories.Distinct().Count() != orderedCategories.Count)
            {
                errors.Add(new Error("A blueprint cannot list the same aisle twice.")
                    .WithMetadata("Property", nameof(Categories)));
            }
            var hasSentinel = orderedCategories.Any(c => c == ProductCategory.Unknown || c == ProductCategory.Other);
            if (hasSentinel)
            {
                errors.Add(new Error("A blueprint can only contain real aisles (not Unknown or Other).")
                    .WithMetadata("Property", nameof(Categories)));
            }
            return errors;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~SortBlueprintTests"`
Expected: PASS (10 tests).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Entities/SortBlueprint.cs Application/Frigorino.Domain/Entities/SortBlueprintCategory.cs Application/Frigorino.Test/Domain/SortBlueprintTests.cs
git commit -m "feat(domain): SortBlueprint aggregate with curated ordered aisles"
```

---

## Task 2: `List.ApplyOrder` — bulk re-rank of the unchecked section

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/List.cs` (add method after `ReorderItem`, ~line 508)
- Test: `Application/Frigorino.Test/Domain/ListApplyOrderTests.cs`

- [ ] **Step 1: Write the failing tests**

`ListApplyOrderTests.cs`:
```csharp
using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class ListApplyOrderTests
    {
        // Builds a list with the given unchecked + one checked item, all active, ranks minted by AddItem.
        private static List NewListWith(out ListItem a, out ListItem b, out ListItem c, out ListItem checkedItem)
        {
            var list = List.Create("Groceries", null, 1, "user").Value;
            list.Id = 1;
            a = AddUnchecked(list, 101, "apples");
            b = AddUnchecked(list, 102, "bread");
            c = AddUnchecked(list, 103, "milk");
            checkedItem = AddUnchecked(list, 200, "done");
            checkedItem.Status = true; // move to checked section (rank irrelevant for this test)
            return list;
        }

        private static ListItem AddUnchecked(List list, int id, string text)
        {
            var item = list.AddItem(text).Value;
            item.Id = id;
            return item;
        }

        [Fact]
        public void ApplyOrder_ReordersUncheckedToGivenSequence()
        {
            var list = NewListWith(out var a, out var b, out var c, out _);

            var result = list.ApplyOrder(new[] { c.Id, a.Id, b.Id });

            Assert.True(result.IsSuccess);
            var ordered = list.ListItems
                .Where(i => i.IsActive && !i.Status)
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .Select(i => i.Id)
                .ToArray();
            Assert.Equal(new[] { c.Id, a.Id, b.Id }, ordered);
        }

        [Fact]
        public void ApplyOrder_DoesNotTouchCheckedItems()
        {
            var list = NewListWith(out var a, out var b, out var c, out var checkedItem);
            var checkedRankBefore = checkedItem.Rank;

            list.ApplyOrder(new[] { c.Id, b.Id, a.Id });

            Assert.Equal(checkedRankBefore, checkedItem.Rank);
        }

        [Fact]
        public void ApplyOrder_IdSetMismatch_Fails()
        {
            var list = NewListWith(out var a, out var b, out _, out _);

            // Missing one unchecked id.
            var result = list.ApplyOrder(new[] { a.Id, b.Id });

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void ApplyOrder_ForeignId_Fails()
        {
            var list = NewListWith(out var a, out var b, out var c, out _);

            var result = list.ApplyOrder(new[] { a.Id, b.Id, c.Id, 9999 });

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void ApplyOrder_EmptyUncheckedSection_Succeeds()
        {
            var list = List.Create("Empty", null, 1, "user").Value;

            var result = list.ApplyOrder(Array.Empty<int>());

            Assert.True(result.IsSuccess);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListApplyOrderTests"`
Expected: FAIL — `ApplyOrder` not defined.

- [ ] **Step 3: Implement `ApplyOrder`**

In `List.cs`, add this method immediately after `ReorderItem` (after line 508, before the `NormalizeComment` helper):
```csharp
        // Bulk re-rank of the unchecked section: re-mints Rank so items land in exactly
        // orderedUncheckedItemIds order. The id set MUST equal the current active unchecked ids
        // (the apply handler builds it from the same loaded aggregate). The checked section is
        // untouched. New ranks are generated strictly ABOVE the current max unchecked rank so no
        // intermediate row update collides with a still-present old rank on the partial unique
        // index (ListId, Status, Rank) during the multi-row save.
        public Result ApplyOrder(IReadOnlyList<int> orderedUncheckedItemIds)
        {
            var uncheckedItems = ListItems
                .Where(i => i.IsActive && !i.Status)
                .ToList();

            var currentIds = uncheckedItems.Select(i => i.Id).ToHashSet();
            var givenIds = orderedUncheckedItemIds.ToHashSet();
            var idsMatch = givenIds.Count == orderedUncheckedItemIds.Count && currentIds.SetEquals(givenIds);
            if (!idsMatch)
            {
                return Result.Fail(
                    new Error("Ordered ids must match the list's active unchecked items exactly.")
                        .WithMetadata("Property", string.Empty));
            }

            if (uncheckedItems.Count == 0)
            {
                return Result.Ok();
            }

            var maxRank = uncheckedItems
                .OrderBy(i => i.Rank, StringComparer.Ordinal)
                .Last()
                .Rank;
            var ranks = FractionalIndex.GenerateKeysBetween(maxRank, null, uncheckedItems.Count);
            var byId = uncheckedItems.ToDictionary(i => i.Id);
            var now = DateTime.UtcNow;
            for (var i = 0; i < orderedUncheckedItemIds.Count; i++)
            {
                var item = byId[orderedUncheckedItemIds[i]];
                item.Rank = ranks[i];
                item.UpdatedAt = now;
            }
            return Result.Ok();
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListApplyOrderTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/List.cs Application/Frigorino.Test/Domain/ListApplyOrderTests.cs
git commit -m "feat(domain): List.ApplyOrder bulk re-rank of unchecked section"
```

---

## Task 3: EF configuration, DbSets, timestamp stamping, migration

**Files:**
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/SortBlueprintConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/SortBlueprintCategoryConfiguration.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`

- [ ] **Step 1: Write `SortBlueprintConfiguration`**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class SortBlueprintConfiguration : IEntityTypeConfiguration<SortBlueprint>
    {
        public void Configure(EntityTypeBuilder<SortBlueprint> builder)
        {
            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id)
                .ValueGeneratedOnAdd();

            builder.Property(b => b.HouseholdId)
                .IsRequired();

            builder.Property(b => b.Name)
                .HasMaxLength(SortBlueprint.NameMaxLength)
                .IsRequired();

            builder.Property(b => b.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(b => b.CreatedAt).IsRequired();
            builder.Property(b => b.UpdatedAt).IsRequired();

            // FK-only relationship to Household — cascade with the household.
            builder.HasOne(b => b.Household)
                .WithMany()
                .HasForeignKey(b => b.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // Children deleted with the blueprint (and orphan-deleted on wholesale replace).
            builder.HasMany(b => b.Categories)
                .WithOne(c => c.Blueprint)
                .HasForeignKey(c => c.BlueprintId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(b => new { b.HouseholdId, b.IsActive });
        }
    }
}
```

- [ ] **Step 2: Write `SortBlueprintCategoryConfiguration`**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class SortBlueprintCategoryConfiguration : IEntityTypeConfiguration<SortBlueprintCategory>
    {
        public void Configure(EntityTypeBuilder<SortBlueprintCategory> builder)
        {
            // Composite key — an aisle appears at most once per blueprint. Category stored as int.
            builder.HasKey(c => new { c.BlueprintId, c.Category });

            builder.Property(c => c.Category).IsRequired();
            builder.Property(c => c.OrderIndex).IsRequired();
        }
    }
}
```

- [ ] **Step 3: Register DbSets + timestamp stamping in `ApplicationDbContext.cs`**

Add the DbSets after line 24 (`public DbSet<HouseholdSettings> HouseholdSettings ...`):
```csharp
        public DbSet<SortBlueprint> SortBlueprints { get; set; }
        public DbSet<SortBlueprintCategory> SortBlueprintCategories { get; set; }
```

In `SaveChangesAsync`, inside the `EntityState.Added` block (after the `HouseholdSettings` added-stamp at line 111):
```csharp
                    if (entry.Entity is SortBlueprint sortBlueprintAdded && sortBlueprintAdded.CreatedAt == default)
                    {
                        sortBlueprintAdded.CreatedAt = now;
                        sortBlueprintAdded.UpdatedAt = now;
                    }
```

In the `EntityState.Modified` block (after the `HouseholdSettings` modified-stamp at line 171):
```csharp
                    if (entry.Entity is SortBlueprint sortBlueprintModified)
                    {
                        sortBlueprintModified.UpdatedAt = now;
                    }
```

- [ ] **Step 4: Add the migration**

Ensure no `Frigorino.Web` instance is running (it locks `bin/Debug` DLLs). Then:
```bash
dotnet ef migrations add AddSortBlueprints --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: creates `Application/Frigorino.Infrastructure/Migrations/<timestamp>_AddSortBlueprints.cs` with `SortBlueprints` + `SortBlueprintCategories` tables, the composite PK, FKs, and the `(HouseholdId, IsActive)` index.

- [ ] **Step 5: Build to verify**

Run: `dotnet build Application/Frigorino.sln`
Expected: 0 errors, 0 warnings. (The `dotnet build Frigorino.Web` step also re-emits `ClientApp/src/lib/openapi.json`; expect NO change yet — no new endpoints. If it changed, revert that file.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework Application/Frigorino.Infrastructure/Migrations
git commit -m "feat(infra): EF config + migration for sort blueprints"
```

---

## Task 4: Blueprint CRUD slices + wiring + lazy seed

**Files:**
- Create: `Application/Frigorino.Features/Households/Blueprints/SortBlueprintResponse.cs`
- Create: `GetBlueprints.cs`, `CreateBlueprint.cs`, `UpdateBlueprint.cs`, `DeleteBlueprint.cs` (same folder)
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Write the response DTO**

`SortBlueprintResponse.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Features.Households.Blueprints
{
    // Categories is the walk-order (already sorted by OrderIndex). ProductCategory serializes as
    // its string name → the TS client gets a string union.
    public sealed record SortBlueprintResponse(int Id, string Name, IReadOnlyList<ProductCategory> Categories)
    {
        public static SortBlueprintResponse From(SortBlueprint blueprint)
        {
            return new SortBlueprintResponse(blueprint.Id, blueprint.Name, blueprint.OrderedCategories());
        }
    }
}
```

- [ ] **Step 2: Write `GetBlueprints` (with lazy default seed)**

`GetBlueprints.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Blueprints
{
    public static class GetBlueprintsEndpoint
    {
        public static IEndpointRouteBuilder MapGetBlueprints(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetBlueprints")
               .Produces<SortBlueprintResponse[]>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<SortBlueprintResponse[]>, NotFound>> Handle(
            int householdId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Lazy seed: first read for a household with no blueprints mints the starter so the
            // feature works on first tap (and existing households get it too). Idempotent.
            var anyExist = await db.SortBlueprints.AnyAsync(b => b.HouseholdId == householdId && b.IsActive, ct);
            if (!anyExist)
            {
                db.SortBlueprints.Add(SortBlueprint.CreateDefault(householdId));
                await db.SaveChangesAsync(ct);
            }

            var response = await db.SortBlueprints
                .Where(b => b.HouseholdId == householdId && b.IsActive)
                .OrderBy(b => b.CreatedAt)
                .ThenBy(b => b.Id)
                .Select(b => new SortBlueprintResponse(
                    b.Id,
                    b.Name,
                    b.Categories.OrderBy(c => c.OrderIndex).Select(c => c.Category).ToList()))
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
```

- [ ] **Step 3: Write `CreateBlueprint`**

`CreateBlueprint.cs`:
```csharp
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Households.Blueprints
{
    public sealed record CreateBlueprintRequest(string Name, IReadOnlyList<ProductCategory> Categories);

    public static class CreateBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapCreateBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateBlueprint")
               .Produces<SortBlueprintResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<SortBlueprintResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            CreateBlueprintRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var categories = request.Categories ?? Array.Empty<ProductCategory>();
            var result = SortBlueprint.Create(householdId, request.Name ?? string.Empty, categories, membership.Role);
            if (result.IsFailed)
            {
                if (result.HasError<AccessDeniedError>())
                {
                    return TypedResults.Forbid();
                }
                return result.ToValidationProblem();
            }

            db.SortBlueprints.Add(result.Value);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(SortBlueprintResponse.From(result.Value));
        }
    }
}
```

- [ ] **Step 4: Write `UpdateBlueprint`**

`UpdateBlueprint.cs`:
```csharp
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Blueprints
{
    public sealed record UpdateBlueprintRequest(string Name, IReadOnlyList<ProductCategory> Categories);

    public static class UpdateBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapPut("/{blueprintId:int}", Handle)
               .WithName("UpdateBlueprint")
               .Produces<SortBlueprintResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<SortBlueprintResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            int blueprintId,
            UpdateBlueprintRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Include Categories so the wholesale replace orphan-deletes the old rows.
            var blueprint = await db.SortBlueprints
                .Include(b => b.Categories)
                .FirstOrDefaultAsync(b => b.Id == blueprintId && b.HouseholdId == householdId && b.IsActive, ct);
            if (blueprint is null)
            {
                return TypedResults.NotFound();
            }

            var categories = request.Categories ?? Array.Empty<ProductCategory>();
            var result = blueprint.Update(request.Name ?? string.Empty, categories, membership.Role);
            if (result.IsFailed)
            {
                if (result.HasError<AccessDeniedError>())
                {
                    return TypedResults.Forbid();
                }
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(SortBlueprintResponse.From(blueprint));
        }
    }
}
```

- [ ] **Step 5: Write `DeleteBlueprint`**

`DeleteBlueprint.cs`:
```csharp
using FluentResults;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Blueprints
{
    public static class DeleteBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapDeleteBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/{blueprintId:int}", Handle)
               .WithName("DeleteBlueprint")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> Handle(
            int householdId,
            int blueprintId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var blueprint = await db.SortBlueprints
                .FirstOrDefaultAsync(b => b.Id == blueprintId && b.HouseholdId == householdId && b.IsActive, ct);
            if (blueprint is null)
            {
                return TypedResults.NotFound();
            }

            var result = blueprint.SoftDelete(membership.Role);
            if (result.IsFailed)
            {
                if (result.HasError<AccessDeniedError>())
                {
                    return TypedResults.Forbid();
                }
                return TypedResults.NotFound();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
```

- [ ] **Step 6: Wire the group in `Program.cs`**

After the `householdSettings` group block (line 329), add:
```csharp
var blueprints = app.MapGroup("/api/household/{householdId:int}/blueprints")
    .RequireAuthorization()
    .WithTags("Blueprints");
blueprints.MapGetBlueprints();
blueprints.MapCreateBlueprint();
blueprints.MapUpdateBlueprint();
blueprints.MapDeleteBlueprint();
```
Add the matching `using Frigorino.Features.Households.Blueprints;` near the other feature usings at the top (e.g. after line 6 `using Frigorino.Features.Households.Settings;`).

- [ ] **Step 7: Build to verify**

Run: `dotnet build Application/Frigorino.sln`
Expected: 0 errors. `ClientApp/src/lib/openapi.json` WILL now change (4 new endpoints) — that's expected; it is regenerated and committed in Task 6.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Features/Households/Blueprints Application/Frigorino.Web/Program.cs
git commit -m "feat(api): blueprint CRUD slices with lazy default seed"
```

---

## Task 5: `BlueprintSorter` + `ApplyBlueprint` slice

**Files:**
- Create: `Application/Frigorino.Features/Lists/Blueprints/BlueprintSorter.cs`
- Create: `Application/Frigorino.Features/Lists/Blueprints/ApplyBlueprint.cs`
- Test: `Application/Frigorino.Test/Features/BlueprintSorterTests.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (`lists.MapApplyBlueprint()`)

- [ ] **Step 1: Write the failing `BlueprintSorter` tests**

`BlueprintSorterTests.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Features.Lists.Blueprints;

namespace Frigorino.Test.Features
{
    public class BlueprintSorterTests
    {
        // Items carry Id + Rank only (the sorter reads those two). Rank order here is a < b < c < d.
        private static ListItem Item(int id, string rank) => new() { Id = id, Rank = rank };

        [Fact]
        public void Order_SortsByBlueprintRank_ThenStableByExistingRank()
        {
            var items = new[]
            {
                Item(1, "a0"), // Pantry
                Item(2, "a1"), // Produce
                Item(3, "a2"), // Produce
                Item(4, "a3"), // Bakery
            };
            var categoryByItemId = new Dictionary<int, ProductCategory>
            {
                [1] = ProductCategory.Pantry,
                [2] = ProductCategory.Produce,
                [3] = ProductCategory.Produce,
                [4] = ProductCategory.Bakery,
            };
            var blueprint = new[] { ProductCategory.Produce, ProductCategory.Bakery, ProductCategory.Pantry };

            var ordered = BlueprintSorter.OrderUncheckedItemIds(items, categoryByItemId, blueprint);

            // Produce (2 then 3, stable by rank), then Bakery (4), then Pantry (1).
            Assert.Equal(new[] { 2, 3, 4, 1 }, ordered);
        }

        [Fact]
        public void Order_UncategorizedSinkToBottom_StableAmongThemselves()
        {
            var items = new[]
            {
                Item(1, "a0"), // Snacks — not in blueprint → bottom
                Item(2, "a1"), // Produce
                Item(3, "a2"), // Unknown (unclassified) → bottom
            };
            var categoryByItemId = new Dictionary<int, ProductCategory>
            {
                [1] = ProductCategory.Snacks,
                [2] = ProductCategory.Produce,
                // item 3 deliberately absent → treated as Unknown
            };
            var blueprint = new[] { ProductCategory.Produce };

            var ordered = BlueprintSorter.OrderUncheckedItemIds(items, categoryByItemId, blueprint);

            // Produce first; then the two uncategorized in their original rank order (1 then 3).
            Assert.Equal(new[] { 2, 1, 3 }, ordered);
        }

        [Fact]
        public void Order_SentinelCategories_SinkToBottom()
        {
            var items = new[] { Item(1, "a0"), Item(2, "a1") };
            var categoryByItemId = new Dictionary<int, ProductCategory>
            {
                [1] = ProductCategory.Other,
                [2] = ProductCategory.Produce,
            };
            var blueprint = new[] { ProductCategory.Produce };

            var ordered = BlueprintSorter.OrderUncheckedItemIds(items, categoryByItemId, blueprint);

            Assert.Equal(new[] { 2, 1 }, ordered);
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~BlueprintSorterTests"`
Expected: FAIL — `BlueprintSorter` not defined.

- [ ] **Step 3: Implement `BlueprintSorter`**

`BlueprintSorter.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Features.Lists.Blueprints
{
    // Pure ordering of a list's active unchecked items by a blueprint's walk-order. Items whose
    // category is not in the blueprint (or is a sentinel / unclassified → Unknown) sink to the
    // bottom. Ties within a category — and the whole uncategorized bucket — keep their existing
    // Rank order (stable). No EF, no I/O: the handler resolves categories and passes them in.
    public static class BlueprintSorter
    {
        public static IReadOnlyList<int> OrderUncheckedItemIds(
            IReadOnlyList<ListItem> uncheckedItems,
            IReadOnlyDictionary<int, ProductCategory> categoryByItemId,
            IReadOnlyList<ProductCategory> blueprintOrder)
        {
            var rankByCategory = new Dictionary<ProductCategory, int>();
            for (var i = 0; i < blueprintOrder.Count; i++)
            {
                rankByCategory[blueprintOrder[i]] = i;
            }

            return uncheckedItems
                .Select(item =>
                {
                    var category = categoryByItemId.TryGetValue(item.Id, out var c)
                        ? c
                        : ProductCategory.Unknown;
                    var primary = rankByCategory.TryGetValue(category, out var rank)
                        ? rank
                        : int.MaxValue;
                    return (item, primary);
                })
                .OrderBy(x => x.primary)
                .ThenBy(x => x.item.Rank, StringComparer.Ordinal)
                .Select(x => x.item.Id)
                .ToList();
        }
    }
}
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~BlueprintSorterTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Implement `ApplyBlueprint` slice**

`ApplyBlueprint.cs`:
```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Items;
using Frigorino.Features.Lists.Items;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Blueprints
{
    public sealed record ApplyBlueprintRequest(int BlueprintId);

    public static class ApplyBlueprintEndpoint
    {
        public static IEndpointRouteBuilder MapApplyBlueprint(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{listId:int}/apply-blueprint", Handle)
               .WithName("ApplyBlueprint")
               .Produces<ListItemResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        // Any active member may apply (it is just a reorder of a shared list). Reads the unchecked
        // items, resolves each item's category from the Product catalog, computes the blueprint
        // order, and bulk re-ranks via List.ApplyOrder. RankRetry guards a concurrent append/reorder
        // racing the re-rank on the partial unique index.
        private static async Task<Results<Ok<ListItemResponse[]>, NotFound>> Handle(
            int householdId,
            int listId,
            ApplyBlueprintRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var response = await RankRetry.SaveWithRetryAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var list = await db.Lists
                    .Include(l => l.ListItems)
                    .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                if (list is null)
                {
                    return (ListItemResponse[]?)null;
                }

                var blueprint = await db.SortBlueprints
                    .Include(b => b.Categories)
                    .FirstOrDefaultAsync(b => b.Id == request.BlueprintId && b.HouseholdId == householdId && b.IsActive, ct);
                if (blueprint is null)
                {
                    return null;
                }

                var uncheckedItems = list.ListItems
                    .Where(i => i.IsActive && !i.Status)
                    .ToList();

                // Resolve each item's effective category by normalized-name lookup on Product.
                var normalizedByItemId = uncheckedItems.ToDictionary(
                    i => i.Id,
                    i => ProductName.Normalize(i.Text));
                var names = normalizedByItemId.Values
                    .Where(n => n.Length > 0)
                    .Distinct()
                    .ToList();
                var products = await db.Products
                    .Where(p => p.HouseholdId == householdId && names.Contains(p.NormalizedName))
                    .ToListAsync(ct);
                var categoryByName = products.ToDictionary(p => p.NormalizedName, p => p.EffectiveCategory);
                var categoryByItemId = normalizedByItemId.ToDictionary(
                    kv => kv.Key,
                    kv => categoryByName.TryGetValue(kv.Value, out var cat) ? cat : ProductCategory.Unknown);

                var orderedIds = BlueprintSorter.OrderUncheckedItemIds(
                    uncheckedItems, categoryByItemId, blueprint.OrderedCategories());

                var applyResult = list.ApplyOrder(orderedIds);
                if (applyResult.IsFailed)
                {
                    // The id set is built from the same loaded aggregate, so this only trips on a
                    // genuine bug — surface it rather than silently 404.
                    throw new InvalidOperationException(
                        $"ApplyOrder failed unexpectedly: {applyResult.Errors[0].Message}");
                }

                await db.SaveChangesAsync(ct);

                return list.ListItems
                    .Where(i => i.IsActive)
                    .OrderBy(i => i.Status)
                    .ThenBy(i => i.Rank, StringComparer.Ordinal)
                    .ThenBy(i => i.Id)
                    .Select(ListItemResponse.From)
                    .ToArray();
            });

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
}
```

- [ ] **Step 6: Wire the apply endpoint in `Program.cs`**

In the `lists` group block (after `lists.MapSkipPromotion();`, line 341), add:
```csharp
lists.MapApplyBlueprint();
```
Add `using Frigorino.Features.Lists.Blueprints;` near the top with the other feature usings.

- [ ] **Step 7: Build + run the new unit tests**

Run: `dotnet build Application/Frigorino.sln`
Expected: 0 errors. (`openapi.json` changes again — committed in Task 6.)

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Features/Lists/Blueprints Application/Frigorino.Test/Features/BlueprintSorterTests.cs Application/Frigorino.Web/Program.cs
git commit -m "feat(api): apply-blueprint slice + pure BlueprintSorter"
```

---

## Task 6: Regenerate the API client

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/lib/openapi.json` + `src/lib/api/**` (generated)

- [ ] **Step 1: Regenerate**

From `Application/Frigorino.Web/ClientApp`:
```bash
cd Application/Frigorino.Web/ClientApp && npm run api
```
This rebuilds the backend (emits `openapi.json`) and regenerates the TS client. Expected new exports in `src/lib/api/@tanstack/react-query.gen.ts`: `getBlueprintsOptions`, `getBlueprintsQueryKey`, `createBlueprintMutation`, `updateBlueprintMutation`, `deleteBlueprintMutation`, `applyBlueprintMutation`; and in `types.gen.ts` a `ProductCategory` string-union type plus `SortBlueprintResponse`, `CreateBlueprintData`, `UpdateBlueprintData`, `ApplyBlueprintData`.

- [ ] **Step 2: Type-check the generated client**

From `ClientApp`:
```bash
npm run tsc
```
Expected: no errors.

- [ ] **Step 3: Commit the generated client**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib
git commit -m "chore(api): regenerate client for blueprint endpoints"
```

---

## Task 7: Frontend — aisle metadata + i18n keys

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/blueprints/aisles.ts`
- Modify: `public/locales/en/translation.json`, `public/locales/de/translation.json`

- [ ] **Step 1: Write the aisle metadata module**

`src/features/blueprints/aisles.ts`:
```typescript
import type { ProductCategory } from "../../lib/api/types.gen";

// All 23 real aisles in the canonical default walk-order. Excludes the Unknown/Other sentinels —
// they are never part of a blueprint; items in those categories (or unclassified) sink to the
// bottom on apply. Keep in sync with SortBlueprint.DefaultOrder on the backend.
export const ALL_AISLES: ProductCategory[] = [
    "Produce",
    "Bakery",
    "DeliAndColdCuts",
    "Meat",
    "Fish",
    "DairyAndEggs",
    "Cheese",
    "Frozen",
    "Cereal",
    "Pantry",
    "CannedGoods",
    "Sauces",
    "OilsAndVinegar",
    "Spices",
    "Spreads",
    "Snacks",
    "Sweets",
    "Beverages",
    "Alcohol",
    "HouseholdAndCleaning",
    "HealthAndBeauty",
    "Baby",
    "Pet",
];

// i18n key for an aisle's display name (e.g. "blueprints.aisles.Produce").
export const aisleLabelKey = (category: ProductCategory): string =>
    `blueprints.aisles.${category}`;
```

- [ ] **Step 2: Add the `blueprints` section to `en/translation.json`**

Add a new top-level `"blueprints"` object (sibling of `"lists"`/`"settings"`):
```json
"blueprints": {
    "title": "Sort blueprints",
    "manage": "Sort blueprints",
    "manageHint": "Arrange supermarket aisles into your store's walk-order, then apply a blueprint to any list.",
    "newBlueprint": "New blueprint",
    "nameLabel": "Blueprint name",
    "namePlaceholder": "e.g. My local store",
    "included": "In this blueprint",
    "available": "Available aisles",
    "noAislesYet": "Add aisles from below to build your walk-order.",
    "save": "Save",
    "saved": "Blueprint saved",
    "saveFailed": "Couldn't save the blueprint",
    "duplicate": "Duplicate",
    "delete": "Delete",
    "deleted": "Blueprint deleted",
    "deleteFailed": "Couldn't delete the blueprint",
    "copySuffix": "(copy)",
    "readOnlyHint": "Only owners and admins can edit blueprints.",
    "empty": "No blueprints yet.",
    "sortByCategory": "Sort by category",
    "pickBlueprint": "Choose a blueprint",
    "applied": "List sorted",
    "applyFailed": "Couldn't sort the list",
    "noBlueprintsToApply": "Create a blueprint first to sort by category.",
    "aisles": {
        "Produce": "Fruit & vegetables",
        "Bakery": "Bakery",
        "DeliAndColdCuts": "Deli & cold cuts",
        "Meat": "Meat & poultry",
        "Fish": "Fish & seafood",
        "DairyAndEggs": "Dairy & eggs",
        "Cheese": "Cheese",
        "Frozen": "Frozen",
        "Cereal": "Cereal & oats",
        "Pantry": "Pantry staples",
        "CannedGoods": "Canned & jarred",
        "Sauces": "Sauces & dressings",
        "OilsAndVinegar": "Oils & vinegar",
        "Spices": "Spices & baking",
        "Spreads": "Spreads",
        "Snacks": "Snacks",
        "Sweets": "Sweets",
        "Beverages": "Beverages",
        "Alcohol": "Alcohol",
        "HouseholdAndCleaning": "Household & cleaning",
        "HealthAndBeauty": "Health & beauty",
        "Baby": "Baby",
        "Pet": "Pet"
    }
}
```

- [ ] **Step 3: Add the same `blueprints` section to `de/translation.json`** (German strings, same keys)

```json
"blueprints": {
    "title": "Sortier-Vorlagen",
    "manage": "Sortier-Vorlagen",
    "manageHint": "Ordne die Supermarkt-Bereiche in deiner Lauf-Reihenfolge und wende eine Vorlage auf jede Liste an.",
    "newBlueprint": "Neue Vorlage",
    "nameLabel": "Vorlagenname",
    "namePlaceholder": "z. B. Mein Laden",
    "included": "In dieser Vorlage",
    "available": "Verfügbare Bereiche",
    "noAislesYet": "Füge unten Bereiche hinzu, um deine Reihenfolge zu erstellen.",
    "save": "Speichern",
    "saved": "Vorlage gespeichert",
    "saveFailed": "Vorlage konnte nicht gespeichert werden",
    "duplicate": "Duplizieren",
    "delete": "Löschen",
    "deleted": "Vorlage gelöscht",
    "deleteFailed": "Vorlage konnte nicht gelöscht werden",
    "copySuffix": "(Kopie)",
    "readOnlyHint": "Nur Eigentümer und Admins können Vorlagen bearbeiten.",
    "empty": "Noch keine Vorlagen.",
    "sortByCategory": "Nach Kategorie sortieren",
    "pickBlueprint": "Vorlage wählen",
    "applied": "Liste sortiert",
    "applyFailed": "Liste konnte nicht sortiert werden",
    "noBlueprintsToApply": "Erstelle zuerst eine Vorlage, um zu sortieren.",
    "aisles": {
        "Produce": "Obst & Gemüse",
        "Bakery": "Backwaren",
        "DeliAndColdCuts": "Wurst & Aufschnitt",
        "Meat": "Fleisch & Geflügel",
        "Fish": "Fisch & Meeresfrüchte",
        "DairyAndEggs": "Milchprodukte & Eier",
        "Cheese": "Käse",
        "Frozen": "Tiefkühl",
        "Cereal": "Müsli & Haferflocken",
        "Pantry": "Vorratskammer",
        "CannedGoods": "Konserven",
        "Sauces": "Saucen & Dressings",
        "OilsAndVinegar": "Öle & Essig",
        "Spices": "Gewürze & Backen",
        "Spreads": "Aufstriche",
        "Snacks": "Snacks",
        "Sweets": "Süßigkeiten",
        "Beverages": "Getränke",
        "Alcohol": "Alkohol",
        "HouseholdAndCleaning": "Haushalt & Reinigung",
        "HealthAndBeauty": "Gesundheit & Schönheit",
        "Baby": "Baby",
        "Pet": "Haustier"
    }
}
```

- [ ] **Step 4: Validate JSON + commit**

From `ClientApp`: `npm run tsc` (catches the `aisles.ts` import). Manually confirm both JSON files parse (no trailing commas).
```bash
git add Application/Frigorino.Web/ClientApp/src/features/blueprints/aisles.ts Application/Frigorino.Web/ClientApp/public/locales
git commit -m "feat(web): aisle metadata + blueprint i18n keys (en/de)"
```

---

## Task 8: Frontend — blueprint hooks

**Files:**
- Create: `src/features/blueprints/useSortBlueprints.ts`, `useCreateSortBlueprint.ts`, `useUpdateSortBlueprint.ts`, `useDeleteSortBlueprint.ts`, `useApplyBlueprint.ts`

- [ ] **Step 1: Query hook**

`useSortBlueprints.ts`:
```typescript
import { useQuery } from "@tanstack/react-query";
import { getBlueprintsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useSortBlueprints = (householdId: number, enabled = true) =>
    useQuery({
        ...getBlueprintsOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 5,
    });
```

- [ ] **Step 2: Create / Update / Delete mutation hooks**

`useCreateSortBlueprint.ts`:
```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createBlueprintMutation,
    getBlueprintsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useCreateSortBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getBlueprintsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
```

`useUpdateSortBlueprint.ts` (identical shape, `updateBlueprintMutation`):
```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getBlueprintsQueryKey,
    updateBlueprintMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateSortBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getBlueprintsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
```

`useDeleteSortBlueprint.ts` (identical shape, `deleteBlueprintMutation`):
```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    deleteBlueprintMutation,
    getBlueprintsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useDeleteSortBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...deleteBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getBlueprintsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
```

- [ ] **Step 3: Apply hook (non-optimistic — invalidate list items on success)**

`useApplyBlueprint.ts`:
```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    applyBlueprintMutation,
    getItemsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useApplyBlueprint = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...applyBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            });
        },
    });
};
```

- [ ] **Step 4: Type-check + commit**

From `ClientApp`: `npm run tsc` → no errors.
```bash
git add Application/Frigorino.Web/ClientApp/src/features/blueprints
git commit -m "feat(web): blueprint query/mutation hooks"
```

---

## Task 9: Frontend — blueprint editor, card, page, route, settings link

**Files:**
- Create: `src/features/blueprints/components/BlueprintEditor.tsx`
- Create: `src/features/blueprints/components/BlueprintCard.tsx`
- Create: `src/features/blueprints/pages/BlueprintsPage.tsx`
- Create: `src/routes/household/blueprints.tsx`
- Modify: `src/features/households/pages/ManageHouseholdPage.tsx`

- [ ] **Step 1: Write `BlueprintEditor` (two-block dnd-kit variant)**

`BlueprintEditor.tsx`:
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
    arrayMove,
    SortableContext,
    useSortable,
    verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { Add, DragHandle, RemoveCircleOutline } from "@mui/icons-material";
import {
    Box,
    IconButton,
    List,
    ListItem,
    ListItemButton,
    ListItemText,
    Paper,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ProductCategory } from "../../../lib/api/types.gen";
import { aisleLabelKey, ALL_AISLES } from "../aisles";

interface Props {
    included: ProductCategory[];
    onChange: (next: ProductCategory[]) => void;
    disabled?: boolean;
}

function IncludedAisleRow({
    category,
    onRemove,
    disabled,
}: {
    category: ProductCategory;
    onRemove: () => void;
    disabled?: boolean;
}) {
    const { t } = useTranslation();
    const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
        useSortable({ id: category, disabled });
    const style = {
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.5 : 1,
    };

    return (
        <ListItem
            ref={setNodeRef}
            style={style}
            data-testid={`blueprint-included-${category}`}
            disablePadding
            sx={{ mb: 0.5 }}
            secondaryAction={
                <IconButton
                    edge="end"
                    onClick={onRemove}
                    disabled={disabled}
                    data-testid={`blueprint-remove-${category}`}
                >
                    <RemoveCircleOutline />
                </IconButton>
            }
        >
            <ListItemButton
                {...attributes}
                {...listeners}
                disabled={disabled}
                sx={{ cursor: disabled ? "default" : "grab" }}
            >
                <DragHandle fontSize="small" sx={{ mr: 1, color: "text.secondary" }} />
                <ListItemText primary={t(aisleLabelKey(category))} />
            </ListItemButton>
        </ListItem>
    );
}

export function BlueprintEditor({ included, onChange, disabled = false }: Props) {
    const { t } = useTranslation();
    const sensors = useSensors(
        useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
        useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 5 } }),
    );
    const available = ALL_AISLES.filter((c) => !included.includes(c));

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        if (!over || active.id === over.id) {
            return;
        }
        const from = included.indexOf(active.id as ProductCategory);
        const to = included.indexOf(over.id as ProductCategory);
        if (from === -1 || to === -1) {
            return;
        }
        onChange(arrayMove(included, from, to));
    };

    return (
        <Box>
            <Typography variant="subtitle2" sx={{ mt: 1, mb: 0.5 }}>
                {t("blueprints.included")}
            </Typography>
            {included.length === 0 ? (
                <Paper variant="outlined" sx={{ p: 2, textAlign: "center" }}>
                    <Typography variant="body2" color="text.secondary">
                        {t("blueprints.noAislesYet")}
                    </Typography>
                </Paper>
            ) : (
                <DndContext
                    sensors={sensors}
                    collisionDetection={closestCenter}
                    onDragEnd={handleDragEnd}
                >
                    <SortableContext items={included} strategy={verticalListSortingStrategy}>
                        <List data-testid="blueprint-included-list" sx={{ py: 0 }}>
                            {included.map((category) => (
                                <IncludedAisleRow
                                    key={category}
                                    category={category}
                                    disabled={disabled}
                                    onRemove={() =>
                                        onChange(included.filter((c) => c !== category))
                                    }
                                />
                            ))}
                        </List>
                    </SortableContext>
                </DndContext>
            )}

            <Typography variant="subtitle2" sx={{ mt: 2, mb: 0.5 }}>
                {t("blueprints.available")}
            </Typography>
            <List data-testid="blueprint-available-list" sx={{ py: 0 }}>
                {available.map((category) => (
                    <ListItem
                        key={category}
                        data-testid={`blueprint-available-${category}`}
                        disablePadding
                        sx={{ mb: 0.5 }}
                        secondaryAction={
                            <IconButton
                                edge="end"
                                disabled={disabled}
                                onClick={() => onChange([...included, category])}
                                data-testid={`blueprint-add-${category}`}
                            >
                                <Add />
                            </IconButton>
                        }
                    >
                        <ListItemButton
                            disabled={disabled}
                            onClick={() => onChange([...included, category])}
                        >
                            <ListItemText primary={t(aisleLabelKey(category))} />
                        </ListItemButton>
                    </ListItem>
                ))}
            </List>
        </Box>
    );
}
```

- [ ] **Step 2: Write `BlueprintCard` (one blueprint: name + editor + save/duplicate/delete)**

`BlueprintCard.tsx`:
```tsx
import { ContentCopy, Delete, Save } from "@mui/icons-material";
import {
    Box,
    Button,
    Card,
    CardContent,
    IconButton,
    Stack,
    TextField,
    Tooltip,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type {
    ProductCategory,
    SortBlueprintResponse,
} from "../../../lib/api/types.gen";
import { useCreateSortBlueprint } from "../useCreateSortBlueprint";
import { useDeleteSortBlueprint } from "../useDeleteSortBlueprint";
import { useUpdateSortBlueprint } from "../useUpdateSortBlueprint";
import { BlueprintEditor } from "./BlueprintEditor";

interface Props {
    householdId: number;
    canManage: boolean;
    // Existing blueprint, or null for an unsaved draft (create flow).
    blueprint: SortBlueprintResponse | null;
    onCreated?: () => void;
}

export function BlueprintCard({ householdId, canManage, blueprint, onCreated }: Props) {
    const { t } = useTranslation();
    const create = useCreateSortBlueprint();
    const update = useUpdateSortBlueprint();
    const remove = useDeleteSortBlueprint();

    const [name, setName] = useState(blueprint?.name ?? "");
    const [included, setIncluded] = useState<ProductCategory[]>(
        blueprint?.categories ?? [],
    );

    const isSaving = create.isPending || update.isPending;
    const canSave = canManage && name.trim().length > 0 && included.length > 0 && !isSaving;

    const handleSave = async () => {
        try {
            if (blueprint) {
                await update.mutateAsync({
                    path: { householdId, blueprintId: blueprint.id },
                    body: { name: name.trim(), categories: included },
                });
            } else {
                await create.mutateAsync({
                    path: { householdId },
                    body: { name: name.trim(), categories: included },
                });
                onCreated?.();
            }
            toast.success(t("blueprints.saved"));
        } catch {
            toast.error(t("blueprints.saveFailed"));
        }
    };

    const handleDuplicate = async () => {
        try {
            await create.mutateAsync({
                path: { householdId },
                body: {
                    name: `${name.trim()} ${t("blueprints.copySuffix")}`,
                    categories: included,
                },
            });
            toast.success(t("blueprints.saved"));
        } catch {
            toast.error(t("blueprints.saveFailed"));
        }
    };

    const handleDelete = async () => {
        if (!blueprint) {
            return;
        }
        try {
            await remove.mutateAsync({
                path: { householdId, blueprintId: blueprint.id },
            });
            toast.success(t("blueprints.deleted"));
        } catch {
            toast.error(t("blueprints.deleteFailed"));
        }
    };

    return (
        <Card elevation={2} sx={{ mb: { xs: 2, sm: 3 } }} data-testid="blueprint-card">
            <CardContent>
                <Stack direction="row" spacing={1} alignItems="center">
                    <TextField
                        fullWidth
                        size="small"
                        label={t("blueprints.nameLabel")}
                        placeholder={t("blueprints.namePlaceholder")}
                        value={name}
                        disabled={!canManage || isSaving}
                        onChange={(e) => setName(e.target.value)}
                        slotProps={{
                            htmlInput: { "data-testid": "blueprint-name-input" },
                        }}
                    />
                    {blueprint && canManage && (
                        <>
                            <Tooltip title={t("blueprints.duplicate")}>
                                <IconButton
                                    onClick={handleDuplicate}
                                    disabled={isSaving}
                                    data-testid="blueprint-duplicate"
                                >
                                    <ContentCopy />
                                </IconButton>
                            </Tooltip>
                            <Tooltip title={t("blueprints.delete")}>
                                <IconButton
                                    color="error"
                                    onClick={handleDelete}
                                    disabled={remove.isPending}
                                    data-testid="blueprint-delete"
                                >
                                    <Delete />
                                </IconButton>
                            </Tooltip>
                        </>
                    )}
                </Stack>

                <BlueprintEditor
                    included={included}
                    onChange={setIncluded}
                    disabled={!canManage || isSaving}
                />

                {canManage && (
                    <Box sx={{ mt: 2, display: "flex", justifyContent: "flex-end" }}>
                        <Button
                            variant="contained"
                            startIcon={<Save />}
                            disabled={!canSave}
                            onClick={handleSave}
                            data-testid="blueprint-save"
                        >
                            {t("blueprints.save")}
                        </Button>
                    </Box>
                )}
            </CardContent>
        </Card>
    );
}
```

- [ ] **Step 3: Write `BlueprintsPage`**

`BlueprintsPage.tsx` (mirrors `ManageHouseholdPage`'s household-id + role resolution):
```tsx
import { Add } from "@mui/icons-material";
import { Alert, Box, Button, Container, Skeleton, Typography } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
import { HouseholdRoleValue, roleRank } from "../../households/householdRole";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { BlueprintCard } from "../components/BlueprintCard";
import { useSortBlueprints } from "../useSortBlueprints";

export function BlueprintsPage() {
    const { t } = useTranslation();
    const { currentHousehold, isLoading, error, hasActiveHousehold } =
        useCurrentHouseholdWithDetails();
    const [showDraft, setShowDraft] = useState(false);

    const householdId = currentHousehold?.householdId ?? 0;
    const role = currentHousehold?.role;
    const canManage = !!role && roleRank[role] >= roleRank[HouseholdRoleValue.Admin];

    const { data: blueprints, isLoading: blueprintsLoading } = useSortBlueprints(
        householdId,
        householdId > 0,
    );

    if (isLoading || (householdId > 0 && blueprintsLoading)) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Skeleton variant="rectangular" height={200} />
            </Container>
        );
    }

    if (error || !hasActiveHousehold || householdId <= 0) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">{t("common.createOrSelectHouseholdFirst")}</Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={t("blueprints.manage")}
                section="household"
                maxWidth="md"
                directActions={[]}
                menuActions={[]}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    {t("blueprints.manageHint")}
                </Typography>

                {(blueprints ?? []).map((blueprint) => (
                    <BlueprintCard
                        key={blueprint.id}
                        householdId={householdId}
                        canManage={canManage}
                        blueprint={blueprint}
                    />
                ))}

                {showDraft && (
                    <BlueprintCard
                        householdId={householdId}
                        canManage={canManage}
                        blueprint={null}
                        onCreated={() => setShowDraft(false)}
                    />
                )}

                {canManage && !showDraft && (
                    <Box sx={{ display: "flex", justifyContent: "center", mt: 2 }}>
                        <Button
                            variant="outlined"
                            startIcon={<Add />}
                            onClick={() => setShowDraft(true)}
                            data-testid="blueprint-new"
                        >
                            {t("blueprints.newBlueprint")}
                        </Button>
                    </Box>
                )}
            </Container>
        </>
    );
}
```

- [ ] **Step 4: Write the route**

`src/routes/household/blueprints.tsx`:
```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { BlueprintsPage } from "../../features/blueprints/pages/BlueprintsPage";

export const Route = createFileRoute("/household/blueprints")({
    beforeLoad: requireAuth,
    component: BlueprintsPage,
});
```
(`routeTree.gen.ts` is regenerated by the vite plugin on `npm run dev` / `npm run build` — do not edit by hand.)

- [ ] **Step 5: Link to the page from `ManageHouseholdPage`**

In `ManageHouseholdPage.tsx`, import the router `Link` and add a navigation affordance under `HouseholdSettingsCard`. Add near the top imports:
```tsx
import { Link } from "@tanstack/react-router";
import { Button } from "@mui/material";
import { Sort } from "@mui/icons-material";
```
(Extend the existing `@mui/material` import rather than duplicating it.) Then, immediately after the `<HouseholdSettingsCard ... />` element (line 118), add:
```tsx
                <Box sx={{ mt: { xs: 2, sm: 3 } }}>
                    <Button
                        component={Link}
                        to="/household/blueprints"
                        variant="outlined"
                        startIcon={<Sort />}
                        data-testid="household-manage-blueprints-link"
                    >
                        {t("blueprints.manage")}
                    </Button>
                </Box>
```

- [ ] **Step 6: Type-check + lint + commit**

From `ClientApp`:
```bash
npm run tsc
npm run lint
```
Expected: no errors. (If the route type isn't yet known to `tsc`, run `npm run build` once to regenerate `routeTree.gen.ts`, then re-run `tsc`.)
```bash
git add Application/Frigorino.Web/ClientApp/src/features/blueprints Application/Frigorino.Web/ClientApp/src/routes/household/blueprints.tsx Application/Frigorino.Web/ClientApp/src/features/households/pages/ManageHouseholdPage.tsx Application/Frigorino.Web/ClientApp/src/routeTree.gen.ts
git commit -m "feat(web): blueprint editor, manage page, route + settings link"
```

---

## Task 10: Frontend — "Sort by category" on the list page

**Files:**
- Create: `src/features/blueprints/components/ApplyBlueprintDialog.tsx`
- Modify: `src/features/lists/pages/ListViewPage.tsx`

- [ ] **Step 1: Write `ApplyBlueprintDialog`**

`ApplyBlueprintDialog.tsx`:
```tsx
import {
    Dialog,
    DialogContent,
    DialogTitle,
    List,
    ListItemButton,
    ListItemText,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useSortBlueprints } from "../useSortBlueprints";
import { useApplyBlueprint } from "../useApplyBlueprint";

interface Props {
    open: boolean;
    onClose: () => void;
    householdId: number;
    listId: number;
}

export function ApplyBlueprintDialog({ open, onClose, householdId, listId }: Props) {
    const { t } = useTranslation();
    const { data: blueprints } = useSortBlueprints(householdId, open && householdId > 0);
    const apply = useApplyBlueprint();

    const handlePick = async (blueprintId: number) => {
        try {
            await apply.mutateAsync({
                path: { householdId, listId },
                body: { blueprintId },
            });
            toast.success(t("blueprints.applied"));
            onClose();
        } catch {
            toast.error(t("blueprints.applyFailed"));
        }
    };

    return (
        <Dialog open={open} onClose={apply.isPending ? undefined : onClose} maxWidth="xs" fullWidth>
            <DialogTitle>{t("blueprints.pickBlueprint")}</DialogTitle>
            <DialogContent>
                {(blueprints ?? []).length === 0 ? (
                    <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
                        {t("blueprints.noBlueprintsToApply")}
                    </Typography>
                ) : (
                    <List data-testid="apply-blueprint-list">
                        {(blueprints ?? []).map((blueprint) => (
                            <ListItemButton
                                key={blueprint.id}
                                disabled={apply.isPending}
                                onClick={() => handlePick(blueprint.id)}
                                data-testid={`apply-blueprint-${blueprint.id}`}
                            >
                                <ListItemText primary={blueprint.name} />
                            </ListItemButton>
                        ))}
                    </List>
                )}
            </DialogContent>
        </Dialog>
    );
}
```

- [ ] **Step 2: Wire the menu action into `ListViewPage`**

In `ListViewPage.tsx`:
1. Add imports:
```tsx
import { Sort } from "@mui/icons-material";
import { ApplyBlueprintDialog } from "../../blueprints/components/ApplyBlueprintDialog";
```
2. Add dialog state alongside the page's other `useState` hooks:
```tsx
    const [sortDialogOpen, setSortDialogOpen] = useState(false);
```
3. Push a menu action onto the existing `menuActions` array (it is currently `[]`):
```tsx
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("blueprints.sortByCategory"),
            icon: <Sort fontSize="small" />,
            onClick: () => setSortDialogOpen(true),
            testId: "list-sort-by-category",
        },
    ];
```
4. Render the dialog near the page's other dialogs (use the same `householdId` and `listId` already in scope that are passed to `ListContainer`):
```tsx
            <ApplyBlueprintDialog
                open={sortDialogOpen}
                onClose={() => setSortDialogOpen(false)}
                householdId={householdId}
                listId={listId}
            />
```

> Note: confirm the exact names of the in-scope household/list id variables by reading the top of `ListViewPage` — they are the same ones already handed to `<ListContainer householdId={...} listId={...} />`. Use those identifiers verbatim.

- [ ] **Step 3: Type-check + lint + commit**

From `ClientApp`:
```bash
npm run tsc
npm run lint
```
Expected: no errors.
```bash
git add Application/Frigorino.Web/ClientApp/src/features/blueprints/components/ApplyBlueprintDialog.tsx Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx
git commit -m "feat(web): sort-by-category action + apply dialog on list page"
```

---

## Task 11: Frontend verification + manual browser check

**Files:** none (verification only)

- [ ] **Step 1: Full frontend verify**

From `ClientApp`:
```bash
npm run lint
npm run tsc
npm run prettier
```
(`npm run prettier` writes formatting; if it changes files, `git add` + amend the relevant commit or make a `style:` commit.) Expected: lint clean, tsc clean.

- [ ] **Step 2: Build the SPA (so the integration harness + manual check see new testids)**

From `ClientApp`:
```bash
npm run build
```
Expected: `tsc -b` + `vite build` succeed; output in `ClientApp/build`.

- [ ] **Step 3: Manual browser verification (dev-up + Playwright MCP)**

Bring up the local stack (`/dev-up` skill, or `scripts/dev-up.ps1`). Then drive the SPA via Playwright MCP (`--isolated`), re-querying MUI inputs before typing (MUI regenerates `:r:` ids):
1. Navigate to household management → "Sort blueprints". Confirm the seeded **"Supermarket"** blueprint shows all 23 aisles in the top block and an empty available pool.
2. Create a new blueprint: name it, add 3–4 aisles from the available pool, drag to reorder the top block, Save. Confirm it persists (reload).
3. On a list with a few classified items (add e.g. "milk", "bread", "apples"; wait for background classification), open the overflow menu → "Sort by category" → pick a blueprint. Confirm the unchecked items reorder by aisle and the checked section is untouched.
4. Confirm an unclassified/uncategorized item sinks to the bottom.

Record findings. If a runtime bug surfaces (e.g. dnd-kit id mismatch, object-URL, StrictMode double-effect), fix and re-verify before proceeding.

- [ ] **Step 4: Tear down the dev stack** (`/dev-down`) so its running backend doesn't lock `bin/Debug` DLLs for the next `dotnet` build/test.

---

## Task 12: Integration test (API-level apply correctness)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/Blueprints.Api.feature`
- Modify/Create: a step-binding class under `Application/Frigorino.IntegrationTests/` (see Step 2)

- [ ] **Step 1: Read the existing API-level bindings to mirror**

Read `Application/Frigorino.IntegrationTests/Slices/Lists/Classification.Api.feature` (done — see spec) and find its step-binding class (grep the `IntegrationTests` project for the bindings behind `Given there is a list named`, `When I POST an item with text ... via the API`, and `Then the product ... is categorized as ...`). Identify the authenticated `HttpClient` + JSON helpers they use. The new bindings reuse that same HTTP/auth context.

- [ ] **Step 2: Write the feature**

`Blueprints.Api.feature`:
```gherkin
Feature: Category Blueprint Sorting API

  Background:
    Given I am logged in with an active household

  Scenario: Applying a blueprint reorders unchecked items by aisle
    Given there is a list named "Weekly Groceries"
    And I POST an item with text "Spülmittel" to "Weekly Groceries" via the API
    And I POST an item with text "Milch" to "Weekly Groceries" via the API
    And the product "spülmittel" is categorized as "HouseholdAndCleaning"
    And the product "milch" is categorized as "DairyAndEggs"
    When I create a blueprint named "My Store" ordered "DairyAndEggs, HouseholdAndCleaning" via the API
    And I apply blueprint "My Store" to "Weekly Groceries" via the API
    Then the unchecked items of "Weekly Groceries" are ordered "milch, spülmittel"
```

> The stub classifier (`StubItemClassifier`) maps `milch → DairyAndEggs` and `spülmittel → HouseholdAndCleaning`, both deterministic and network-free, so the post-apply order is fixed.

- [ ] **Step 3: Add the three new step bindings**

Mirror the existing bindings' HTTP/auth helpers. Add steps for:
- `When I create a blueprint named {string} ordered {string} via the API` — split the ordered string into `ProductCategory[]`, `POST /api/household/{id}/blueprints` with `{ name, categories }`, store the returned id by name.
- `When I apply blueprint {string} to {string} via the API` — resolve the blueprint id + list id by name, `POST /api/household/{id}/lists/{listId}/apply-blueprint` with `{ blueprintId }`.
- `Then the unchecked items of {string} are ordered {string}` — `GET .../items`, filter `status == false`, order by `rank` ordinal, assert the `text` sequence equals the expected comma-split list (case-insensitive on the normalized text). Use retrying assertion semantics consistent with the existing `Then the product ... is categorized as ...` step (which already waits out background classification).

- [ ] **Step 4: Run the integration test**

```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Blueprint"
```
Expected: PASS. (Needs the Docker daemon for Postgres Testcontainers — if it errors with daemon-unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests
git commit -m "test(it): apply-blueprint reorders unchecked items by aisle"
```

---

## Task 13: Final verification gate + finish branch

**Files:** none (verification)

- [ ] **Step 1: Confirm `openapi.json` is committed and the tree is clean**

```bash
git status --porcelain
```
Expected: empty. If `openapi.json` or `src/lib/api/**` shows as modified, it drifted — run `npm run api` from `ClientApp`, re-commit (Task 6 message style), and recheck.

- [ ] **Step 2: Full solution test**

Ensure no dev stack is running (locks DLLs / Testcontainers port). Then:
```bash
dotnet test Application/Frigorino.sln
```
Expected: all green (existing suite + new `SortBlueprintTests`, `ListApplyOrderTests`, `BlueprintSorterTests`, `Blueprints.Api.feature`). Read the pass/fail summary lines — do not trust a piped tail exit code.

- [ ] **Step 3: Docker build (catches Dockerfile/SPA/pipeline drift)**

```bash
docker build -f Application/Dockerfile -t frigorino .
```
Expected: success. (No new projects were added, so the Dockerfile needs no edit — but the build proves the SPA + backend still publish together.)

- [ ] **Step 4: Holistic review (opus)**

Run a holistic review subagent on **opus** over the full diff (`git diff stage...feat/category-blueprints`): check the apply re-rank collision-safety, the lazy-seed race, role gating on every blueprint slice, EF orphan-delete on category replace, and that no consumer of the old behavior broke. Address any blocking findings.

- [ ] **Step 5: Finish the branch**

Invoke **superpowers:finishing-a-development-branch**. Tests already verified in Steps 2–3. Present the 4 options; default expectation is **Option 2 (push + PR to `stage`)** to match how the taxonomy part shipped (PR #111). Do not push to `stage` directly.

---

## Self-review notes (author)

- **Spec coverage:** library of named blueprints (Tasks 1,4,8,9) ✓; curated ordered subset / child table (Tasks 1,3) ✓; seed one default lazily (Tasks 1,4) ✓; sentinel exclusion + uncategorized-to-bottom + stable tiebreak (Tasks 1,2,5) ✓; apply = unchecked-only bulk reorder, checked untouched, one-shot (Tasks 2,5) ✓; Admin/Owner manage vs any-member apply (Tasks 4,5) ✓; two-block editor / manage page / list popup, non-optimistic (Tasks 9,10) ✓; EF migration + `npm run api` (Tasks 3,6) ✓; i18n aisle names, testid-only tests (Tasks 7,12) ✓; verification incl. full sln + docker (Task 13) ✓.
- **Type consistency:** `SortBlueprint.OrderedCategories()`, `BlueprintSorter.OrderUncheckedItemIds(items, categoryByItemId, blueprintOrder)`, `List.ApplyOrder(IReadOnlyList<int>)`, hook names `useSortBlueprints/useCreate|Update|DeleteSortBlueprint/useApplyBlueprint`, generated `getBlueprints*/createBlueprintMutation/applyBlueprintMutation`, path params `blueprintId`/`listId` — all used consistently across tasks.
- **Known soft spot (acceptable):** the lazy default-seed in `GetBlueprints` is not transactionally guarded against two concurrent first-loads creating two defaults — rare (first ever load) and harmless (user can delete the dup). Not worth a unique constraint.

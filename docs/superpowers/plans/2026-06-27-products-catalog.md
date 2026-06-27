# User-editable Products Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let household Owners/Admins view and correct the AI classifier's per-product verdicts (category, expiry handling, shelf life) and reset a product back to the AI's verdict.

**Architecture:** Add a nullable *override layer* to the `Product` aggregate (`Effective = Override ?? Classification`), exactly as the entity's own roadmap comment anticipates. Three new household-scoped vertical slices expose read / override / reset. The startup backfill and live classify job treat an overridden row as up-to-date so a `ClassifierVersion` bump never clobbers a user correction. A dedicated SPA sub-page under household management lists the catalog and edits a product via a sheet. Downstream consumers (`PromoteSuggestion`, `ApplyBlueprint`) already read `Effective*`, so the override propagates with zero changes there.

**Tech Stack:** .NET 10 minimal-API vertical slices, EF Core (Postgres), FluentResults; React 19 + Vite + TanStack Router/Query + MUI; xUnit + FakeItEasy (unit), Reqnroll + Postgres Testcontainers (integration).

**Spec:** `docs/superpowers/specs/2026-06-27-products-catalog-design.md`
**Branch:** `feat/products-catalog` (already created off `stage`).

## Global Constraints

- **C# braces:** always block style `{ }`, even single-line `if`/namespaces.
- **Business-logic branching:** `if/else` over ternary; ternary only for trivial value selection (the inline EF projections here are the only ternaries allowed). Name multi-clause boolean conditions.
- **Enums on the wire:** serialize as **string names** (existing `JsonStringEnumConverter`) → TS string unions. Request/response DTOs use the CLR enum types; clients send/read the string name.
- **Override columns are written atomically** (all three set, or all three null) — only ever via `Product.OverrideClassification` / `Product.ResetToAiClassification`. `IsOverridden => OverrideExpiryHandling.HasValue`.
- **EF can't translate the computed `Effective*`/`IsOverridden` getters** — inside `.Select(...)` projections use the column expressions (`p.OverrideExpiryHandling != null`, `?? `); use the getters only on already-loaded entities.
- **DB-behavior tests use Postgres Testcontainers** in `Frigorino.IntegrationTests`. `Frigorino.Test` is pure unit/aggregate logic + the legacy EF-InMemory `ClassifyProductJob` tests (don't extend InMemory for new coverage beyond that existing file).
- **Tests never assert on translated text** — API IT asserts JSON; any UI checks use testids/`data-*`.
- **Frontend tooling via npm scripts only:** `npm run tsc`, `npm run lint`, `npm run prettier`, `npm run api`, `npm run routes:gen`, `npm run build`. Never raw `npx`.
- **No new dependencies.** No Co-Authored-By trailers in commits. Conventional-commit messages.
- **Verify exit codes**, don't trust a piped tail (`${PIPESTATUS[0]}` or read the pass/fail summary line).
- **Knowledge doc:** update `knowledge/AI_Classification.md` in the same change (override layer + backfill shield).

## File Structure

**Backend — new (`Application/Frigorino.Features/Products/`):**
- `ProductCatalogItem.cs` — read/response DTO + `From(Product)` factory.
- `GetProducts.cs` — `GET ""` slice (read, any member).
- `OverrideProductClassification.cs` — `PUT "{productId:int}/classification"` slice + request DTO (Owner/Admin).
- `ResetProductClassification.cs` — `DELETE "{productId:int}/classification"` slice (Owner/Admin).

**Backend — modified:**
- `Frigorino.Domain/Entities/Product.cs` — override columns, accessors, two methods.
- `Frigorino.Infrastructure/EntityFramework/Configurations/ProductConfiguration.cs` — three property lines.
- `Frigorino.Infrastructure/Migrations/*` — generated migration.
- `Frigorino.Infrastructure/Tasks/ProductClassificationGaps.cs` — `ExistingProduct.IsOverridden` + shield in `SelectGaps`.
- `Frigorino.Infrastructure/Tasks/BackfillProductClassification.cs` — projection adds override flag.
- `Frigorino.Infrastructure/Services/ClassifyProductJob.cs` — short-circuit on override.
- `Frigorino.Web/Program.cs` — `products` route group.

**Backend — tests:** extend `Frigorino.Test/Domain/ProductAggregateTests.cs`, `Frigorino.Test/Infrastructure/ProductClassificationGapsTests.cs`, `Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs`.

**Frontend — new (`Application/Frigorino.Web/ClientApp/src/`):**
- `features/products/productClassificationOptions.ts` — selectable enum value arrays.
- `features/products/useProducts.ts`, `useOverrideProductClassification.ts`, `useResetProductClassification.ts` — hooks.
- `features/products/components/ProductEditSheet.tsx` — edit dialog.
- `features/products/pages/ProductCatalogPage.tsx` — list + search.
- `routes/household/products.tsx` — route shell.

**Frontend — modified:** `features/households/pages/ManageHouseholdPage.tsx` (link); `public/locales/{en,de}/translation.json`; `types/i18next.d.ts`; generated `lib/api/*`, `lib/openapi.json`, `routeTree.gen.ts`.

**Integration tests — new (`Application/Frigorino.IntegrationTests/Slices/Products/`):** `Products.Api.feature`, `ProductsApiSteps.cs`; modify `Infrastructure/TestApiClient.cs` (3 methods).

**Docs:** `knowledge/AI_Classification.md` (update); `IDEAS.md` (remove the catalog entry if present).

---

# Phase 1 — Domain & persistence

### Task 1: `Product` override layer (columns, accessors, methods)

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/Product.cs`
- Test: `Application/Frigorino.Test/Domain/ProductAggregateTests.cs`

**Interfaces:**
- Produces: `Product.OverrideClassification(ProductClassification)`, `Product.ResetToAiClassification()`, `Product.IsOverridden` (bool), updated `Product.EffectiveCategory` / `Product.EffectiveExpiry`, and nullable properties `OverrideProductCategory` (`ProductCategory?`), `OverrideExpiryHandling` (`ExpiryHandling?`), `OverrideShelfLifeDays` (`int?`).

- [ ] **Step 1: Write the failing tests**

Append to `ProductAggregateTests.cs` (inside the class, after `EffectiveExpiry_ReconstructsProfileFromColumns`):

```csharp
[Fact]
public void OverrideClassification_SetsOverrideLayer_AndFlipsEffective()
{
    var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;

    product.OverrideClassification(
        new ProductClassification(ProductCategory.Pantry,
            ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 14).Value));

    Assert.True(product.IsOverridden);
    Assert.Equal(ProductCategory.Pantry, product.EffectiveCategory);
    Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product.EffectiveExpiry.Handling);
    Assert.Equal(14, product.EffectiveExpiry.ShelfLifeDays);
    // AI layer is preserved underneath.
    Assert.Equal(ProductCategory.DairyAndEggs, product.ClassificationProductCategory);
    Assert.Equal(7, product.ClassificationShelfLifeDays);
}

[Fact]
public void OverrideToNonPerishable_NullsEffectiveShelfLife()
{
    var product = Product.Create(HouseholdId, "salt", AiClassification(7), 1).Value;

    product.OverrideClassification(
        new ProductClassification(ProductCategory.Pantry, ExpiryProfile.NonPerishable));

    Assert.Equal(ExpiryHandling.NonPerishable, product.EffectiveExpiry.Handling);
    Assert.Null(product.EffectiveExpiry.ShelfLifeDays);
}

[Fact]
public void ResetToAiClassification_RestoresAiVerdict()
{
    var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;
    product.OverrideClassification(
        new ProductClassification(ProductCategory.Pantry, ExpiryProfile.NonPerishable));

    product.ResetToAiClassification();

    Assert.False(product.IsOverridden);
    Assert.Equal(ProductCategory.DairyAndEggs, product.EffectiveCategory);
    Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product.EffectiveExpiry.Handling);
    Assert.Equal(7, product.EffectiveExpiry.ShelfLifeDays);
}

[Fact]
public void IsOverridden_IsFalseUntilOverridden()
{
    var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;
    Assert.False(product.IsOverridden);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductAggregateTests"`
Expected: FAIL — `Product` has no `OverrideClassification` / `ResetToAiClassification` / `IsOverridden` (compile error).

- [ ] **Step 3: Implement the override layer**

In `Product.cs`, add the three columns after `ClassifierVersion` (line ~21):

```csharp
        // User Override layer (additive, nullable). Set/cleared atomically via
        // OverrideClassification / ResetToAiClassification. Presence shields the row from
        // backfill re-classification; EffectiveX prefers it over the AI Classification layer.
        public ProductCategory? OverrideProductCategory { get; set; }
        public ExpiryHandling? OverrideExpiryHandling { get; set; }
        public int? OverrideShelfLifeDays { get; set; }
```

Replace the existing `EffectiveCategory` and `EffectiveExpiry` getters (lines ~73-81) with:

```csharp
        // Atomic: the three override columns are written/cleared together, so any one is a
        // valid presence flag.
        public bool IsOverridden => OverrideExpiryHandling.HasValue;

        // Effective category the rest of the app reads: user override wins over the AI layer.
        public ProductCategory EffectiveCategory =>
            OverrideProductCategory ?? ClassificationProductCategory;

        // Effective expiry the rest of the app reads. Expiry is taken as a WHOLE facet, not
        // column-by-column: a NonPerishable override must null the days, never fall back to the
        // AI's shelf life. Safe .Value — both layers are written through a validated ExpiryProfile.
        public ExpiryProfile EffectiveExpiry =>
            OverrideExpiryHandling.HasValue
                ? ExpiryProfile.Create(OverrideExpiryHandling.Value, OverrideShelfLifeDays).Value
                : ExpiryProfile.Create(ClassificationExpiryHandling, ClassificationShelfLifeDays).Value;
```

Add the two methods after `ApplyClassification` (after line ~71):

```csharp
        // User override: take ownership of this product's classification. UpdatedAt is auto-stamped
        // by ApplicationDbContext.SaveChangesAsync. The AI Classification layer is left untouched.
        public void OverrideClassification(ProductClassification classification)
        {
            OverrideProductCategory = classification.Category;
            OverrideExpiryHandling = classification.Expiry.Handling;
            OverrideShelfLifeDays = classification.Expiry.ShelfLifeDays;
        }

        // Reset to AI: drop the override; EffectiveCategory/EffectiveExpiry fall back to the
        // preserved AI layer immediately. A stale ClassifierVersion re-enters the backfill gap
        // set on the next cold start.
        public void ResetToAiClassification()
        {
            OverrideProductCategory = null;
            OverrideExpiryHandling = null;
            OverrideShelfLifeDays = null;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductAggregateTests"`
Expected: PASS (all, including the 4 new tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Product.cs Application/Frigorino.Test/Domain/ProductAggregateTests.cs
git commit -m "feat(products): add user override layer to Product aggregate"
```

---

### Task 2: EF configuration + migration

**Files:**
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ProductConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/Migrations/<timestamp>_AddProductOverrideColumns.cs` (generated)

- [ ] **Step 1: Add the property lines**

In `ProductConfiguration.Configure`, after the `ClassifierVersion` property (line ~32), add (matching the file's "every property enumerated" convention; nullable-by-type → no `.IsRequired()`):

```csharp
            builder.Property(p => p.OverrideProductCategory);
            builder.Property(p => p.OverrideExpiryHandling);
            builder.Property(p => p.OverrideShelfLifeDays);
```

- [ ] **Step 2: Generate the migration**

Run:
```bash
dotnet ef migrations add AddProductOverrideColumns --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: a new migration file created; build succeeds.

- [ ] **Step 3: Verify the migration is purely additive**

Open the generated migration. Confirm `Up()` contains exactly three `AddColumn<int>(...)` calls (`OverrideProductCategory`, `OverrideExpiryHandling`, `OverrideShelfLifeDays`), all `nullable: true`, and `Down()` drops them. No other table changes, no data migration.

- [ ] **Step 4: Build to confirm**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/Configurations/ProductConfiguration.cs Application/Frigorino.Infrastructure/Migrations/
git commit -m "feat(products): migration for Product override columns"
```

---

# Phase 2 — Backfill shield

### Task 3: Gap-selector shields overridden products

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Tasks/ProductClassificationGaps.cs`
- Modify: `Application/Frigorino.Infrastructure/Tasks/BackfillProductClassification.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ProductClassificationGapsTests.cs`

**Interfaces:**
- Consumes: `ExistingProduct` (from Task 0 baseline).
- Produces: `ExistingProduct(int HouseholdId, string NormalizedName, int ClassifierVersion, bool IsOverridden = false)` — a 4th param defaulted `false` so existing callers/tests compile.

- [ ] **Step 1: Write the failing test**

Append to `ProductClassificationGapsTests.cs`:

```csharp
[Fact]
public void OverriddenProduct_IsSkipped_EvenWhenStale()
{
    var candidates = new[] { new ListItemNameCandidate(10, "Milk") };
    var existing = new[]
    {
        new ExistingProduct(10, "milk", ClassifierVersion: CurrentVersion - 1, IsOverridden: true),
    };

    var gaps = ProductClassificationGaps.SelectGaps(candidates, existing, CurrentVersion);

    Assert.Empty(gaps);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductClassificationGapsTests"`
Expected: FAIL — `ExistingProduct` has no `IsOverridden` (compile error).

- [ ] **Step 3: Add the field and the shield**

In `ProductClassificationGaps.cs`, change the `ExistingProduct` record:

```csharp
    public sealed record ExistingProduct(
        int HouseholdId, string NormalizedName, int ClassifierVersion, bool IsOverridden = false);
```

In `SelectGaps`, after building `versionByName`, add an overridden-name set:

```csharp
            var overriddenNames = new HashSet<(int Household, string Name)>();
            foreach (var product in existingProducts)
            {
                if (product.IsOverridden)
                {
                    overriddenNames.Add((product.HouseholdId, product.NormalizedName));
                }
            }
```

Then in the candidate loop, replace the `isUpToDate` computation:

```csharp
                var hasOverride = overriddenNames.Contains(key);
                var isUpToDate = hasOverride
                    || (versionByName.TryGetValue(key, out var version) && version >= currentClassifierVersion);
                if (!isUpToDate)
                {
                    gaps.Add(new ClassificationGap(candidate.HouseholdId, candidate.RawText));
                }
```

- [ ] **Step 4: Update the backfill projection**

In `BackfillProductClassification.Run`, change the `existing` projection (uses the **column expression**, not the computed getter, so EF can translate it):

```csharp
            var existing = await _dbContext.Products
                .Select(p => new ExistingProduct(
                    p.HouseholdId, p.NormalizedName, p.ClassifierVersion, p.OverrideExpiryHandling != null))
                .ToListAsync(cancellationToken);
```

- [ ] **Step 5: Run tests + build**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductClassificationGapsTests"`
Expected: PASS (all, including the new test).
Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/Tasks/ProductClassificationGaps.cs Application/Frigorino.Infrastructure/Tasks/BackfillProductClassification.cs Application/Frigorino.Test/Infrastructure/ProductClassificationGapsTests.cs
git commit -m "feat(products): backfill skips user-overridden products"
```

---

### Task 4: Classify job leaves overridden products alone

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Services/ClassifyProductJob.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `ClassifyProductJobTests.cs`:

```csharp
[Fact]
public async Task Run_OverriddenProduct_SkipsClassifier()
{
    var dbName = Guid.NewGuid().ToString();
    using (var seed = NewContext(dbName))
    {
        var product = Product.Create(HouseholdId, "milk", AiResult(7).Value, 1).Value;
        product.OverrideClassification(
            new ProductClassification(ProductCategory.Pantry, ExpiryProfile.NonPerishable));
        seed.Products.Add(product);
        await seed.SaveChangesAsync();
    }

    // A newer classifier version would normally re-classify a stale row — but the override wins.
    var classifier = new FakeClassifier(AiResult(7), version: 2);
    using (var db = NewContext(dbName))
    {
        var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
        await job.Run(HouseholdId, "milk", CancellationToken.None);
    }

    Assert.Equal(0, classifier.Calls);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ClassifyProductJobTests"`
Expected: FAIL — `classifier.Calls` is 1 (stale version re-classifies despite the override).

- [ ] **Step 3: Add the override guard**

In `ClassifyProductJob.Run`, change the cache-hit short-circuit (line ~37) to also honor an override (`existing` is a loaded entity, so the computed getter is fine here):

```csharp
            if (existing is not null && (existing.IsOverridden || existing.ClassifierVersion >= _classifier.Version))
            {
                // Cache hit, or a user override we must not clobber.
                return;
            }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ClassifyProductJobTests"`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/ClassifyProductJob.cs Application/Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs
git commit -m "feat(products): classify job preserves user override"
```

---

# Phase 3 — API slices

### Task 5: `GetProducts` read slice + DTO + route group

**Files:**
- Create: `Application/Frigorino.Features/Products/ProductCatalogItem.cs`
- Create: `Application/Frigorino.Features/Products/GetProducts.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

**Interfaces:**
- Produces: `ProductCatalogItem` record + `ProductCatalogItem.From(Product)`; `GetProductsEndpoint.MapGetProducts(this IEndpointRouteBuilder)` mapping `GET ""` named `GetProducts`.

- [ ] **Step 1: Create the response DTO**

`ProductCatalogItem.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Features.Products
{
    // One catalog row. Effective* is what the app uses (override ?? AI). The Ai* fields carry the
    // preserved AI verdict so the edit UI can show "AI suggested X" and make Reset meaningful.
    public sealed record ProductCatalogItem(
        int Id,
        string Name,
        ProductCategory EffectiveCategory,
        ExpiryHandling EffectiveExpiryHandling,
        int? EffectiveShelfLifeDays,
        bool IsOverridden,
        ProductCategory AiCategory,
        ExpiryHandling AiExpiryHandling,
        int? AiShelfLifeDays)
    {
        // For already-loaded entities only (uses computed getters EF can't translate).
        public static ProductCatalogItem From(Product p) => new(
            p.Id,
            p.NormalizedName,
            p.EffectiveCategory,
            p.EffectiveExpiry.Handling,
            p.EffectiveExpiry.ShelfLifeDays,
            p.IsOverridden,
            p.ClassificationProductCategory,
            p.ClassificationExpiryHandling,
            p.ClassificationShelfLifeDays);
    }
}
```

- [ ] **Step 2: Create the read slice**

`GetProducts.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Products
{
    public static class GetProductsEndpoint
    {
        public static IEndpointRouteBuilder MapGetProducts(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetProducts")
               .Produces<List<ProductCatalogItem>>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<List<ProductCatalogItem>>, NotFound>> Handle(
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

            // Inline projection — EF can't translate the Effective*/IsOverridden getters, so the
            // override-wins logic is expressed in column terms here.
            var items = await db.Products
                .Where(p => p.HouseholdId == householdId)
                .OrderBy(p => p.NormalizedName)
                .Select(p => new ProductCatalogItem(
                    p.Id,
                    p.NormalizedName,
                    p.OverrideProductCategory ?? p.ClassificationProductCategory,
                    p.OverrideExpiryHandling ?? p.ClassificationExpiryHandling,
                    p.OverrideExpiryHandling != null ? p.OverrideShelfLifeDays : p.ClassificationShelfLifeDays,
                    p.OverrideExpiryHandling != null,
                    p.ClassificationProductCategory,
                    p.ClassificationExpiryHandling,
                    p.ClassificationShelfLifeDays))
                .ToListAsync(ct);

            return TypedResults.Ok(items);
        }
    }
}
```

- [ ] **Step 3: Wire the route group**

In `Program.cs`, after the `householdSettings` group block (line ~350), add:

```csharp
var products = app.MapGroup("/api/household/{householdId:int}/products")
    .RequireAuthorization()
    .WithTags("Products");
products.MapGetProducts();
```

- [ ] **Step 4: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features/Products/ProductCatalogItem.cs Application/Frigorino.Features/Products/GetProducts.cs Application/Frigorino.Web/Program.cs
git commit -m "feat(products): GET household products catalog slice"
```

---

### Task 6: `OverrideProductClassification` write slice

**Files:**
- Create: `Application/Frigorino.Features/Products/OverrideProductClassification.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

**Interfaces:**
- Consumes: `ProductCatalogItem.From`, `ExpiryProfile.Create`, `Product.OverrideClassification`, `HouseholdRoleExtensions.CanManageSettings`, `ResultExtensions.ToValidationProblem`.
- Produces: `OverrideProductClassificationRequest(ProductCategory Category, ExpiryHandling ExpiryHandling, int? ShelfLifeDays)`; `MapOverrideProductClassification`.

- [ ] **Step 1: Create the slice**

`OverrideProductClassification.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Products
{
    public sealed record OverrideProductClassificationRequest(
        ProductCategory Category,
        ExpiryHandling ExpiryHandling,
        int? ShelfLifeDays);

    public static class OverrideProductClassificationEndpoint
    {
        public static IEndpointRouteBuilder MapOverrideProductClassification(this IEndpointRouteBuilder app)
        {
            app.MapPut("{productId:int}/classification", Handle)
               .WithName("OverrideProductClassification")
               .Produces<ProductCatalogItem>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<ProductCatalogItem>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            int productId,
            OverrideProductClassificationRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            if (!membership.Role.CanManageSettings())
            {
                return TypedResults.Forbid();
            }

            var product = await db.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.HouseholdId == householdId, ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            var profile = ExpiryProfile.Create(request.ExpiryHandling, request.ShelfLifeDays);
            if (profile.IsFailed)
            {
                return profile.ToValidationProblem();
            }

            product.OverrideClassification(new ProductClassification(request.Category, profile.Value));
            await db.SaveChangesAsync(ct);

            return TypedResults.Ok(ProductCatalogItem.From(product));
        }
    }
}
```

- [ ] **Step 2: Wire it**

In `Program.cs`, in the `products` group block, add after `products.MapGetProducts();`:

```csharp
products.MapOverrideProductClassification();
```

- [ ] **Step 3: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Products/OverrideProductClassification.cs Application/Frigorino.Web/Program.cs
git commit -m "feat(products): override product classification slice"
```

---

### Task 7: `ResetProductClassification` write slice

**Files:**
- Create: `Application/Frigorino.Features/Products/ResetProductClassification.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

**Interfaces:**
- Produces: `MapResetProductClassification` mapping `DELETE "{productId:int}/classification"` named `ResetProductClassification`, returning `ProductCatalogItem`.

- [ ] **Step 1: Create the slice**

`ResetProductClassification.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Products
{
    public static class ResetProductClassificationEndpoint
    {
        public static IEndpointRouteBuilder MapResetProductClassification(this IEndpointRouteBuilder app)
        {
            app.MapDelete("{productId:int}/classification", Handle)
               .WithName("ResetProductClassification")
               .Produces<ProductCatalogItem>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden);
            return app;
        }

        private static async Task<Results<Ok<ProductCatalogItem>, NotFound, ForbidHttpResult>> Handle(
            int householdId,
            int productId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            if (!membership.Role.CanManageSettings())
            {
                return TypedResults.Forbid();
            }

            var product = await db.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.HouseholdId == householdId, ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            product.ResetToAiClassification();
            await db.SaveChangesAsync(ct);

            return TypedResults.Ok(ProductCatalogItem.From(product));
        }
    }
}
```

- [ ] **Step 2: Wire it**

In `Program.cs`, in the `products` group block, add:

```csharp
products.MapResetProductClassification();
```

- [ ] **Step 3: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Products/ResetProductClassification.cs Application/Frigorino.Web/Program.cs
git commit -m "feat(products): reset product classification slice"
```

---

### Task 8: Regenerate the TS API client

**Files:**
- Generated: `Application/Frigorino.Web/ClientApp/src/lib/openapi.json`, `src/lib/api/**`

- [ ] **Step 1: Regenerate**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run api
```
Expected: backend builds, `openapi.json` is rewritten, `src/lib/api/**` regenerates with no errors.

- [ ] **Step 2: Verify the new surface exists**

Run (from `ClientApp/`):
```bash
grep -l "getProductsOptions\|overrideProductClassificationMutation\|resetProductClassificationMutation" src/lib/api/@tanstack/react-query.gen.ts
grep "ProductCatalogItem\|OverrideProductClassificationRequest" src/lib/api/types.gen.ts
```
Expected: the react-query file matches all three helper names; `types.gen.ts` defines `ProductCatalogItem` and `OverrideProductClassificationData`/`Request` types.

- [ ] **Step 3: Type-check**

Run: `npm run tsc`
Expected: exits 0 (no type errors from the regenerated client).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/openapi.json Application/Frigorino.Web/ClientApp/src/lib/api
git commit -m "chore(products): regenerate API client"
```

---

# Phase 4 — Frontend

> **No JS test runner exists.** Each frontend task verifies with `npm run tsc` + `npm run lint`. Runtime behavior is covered by the API IT (Phase 5) plus a manual UI verification gate at the end of this phase.

### Task 9: Selectable enum options + i18n

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/products/productClassificationOptions.ts`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/src/types/i18next.d.ts`

**Interfaces:**
- Produces: `PRODUCT_CATEGORY_OPTIONS: ProductCategory[]`, `EXPIRY_HANDLING_OPTIONS: ExpiryHandling[]`; i18n sections `products`, `productCategories`, `expiryHandlings` (the enum-label sections mirror the existing `quantityUnits` convention — keyed by enum value).

- [ ] **Step 1: Create the options module**

`productClassificationOptions.ts`:

```ts
import type { ExpiryHandling, ProductCategory } from "../../lib/api/types.gen";

// Selectable values exclude the Unknown sentinel — a user never deliberately picks
// "couldn't classify". Order is the catalog/aisle order from the backend enum.
export const PRODUCT_CATEGORY_OPTIONS: ProductCategory[] = [
    "Other",
    "Produce",
    "Bakery",
    "Meat",
    "Fish",
    "DairyAndEggs",
    "Cheese",
    "DeliAndColdCuts",
    "Frozen",
    "Pantry",
    "CannedGoods",
    "Sauces",
    "OilsAndVinegar",
    "Spices",
    "Cereal",
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

export const EXPIRY_HANDLING_OPTIONS: ExpiryHandling[] = [
    "NonPerishable",
    "UserEntersFromPackage",
    "AiRecommendsShelfLife",
];
```

- [ ] **Step 2: Add the i18n sections (en)**

In `public/locales/en/translation.json`, add three top-level sections (keep JSON valid — comma after the previous top-level section):

```json
    "products": {
        "title": "Product catalog",
        "subtitle": "Review and correct how products are classified",
        "search": "Search products",
        "empty": "No products yet",
        "overridden": "Edited",
        "edit": "Edit classification",
        "reset": "Reset to AI",
        "save": "Save",
        "cancel": "Cancel",
        "category": "Category",
        "expiry": "Expiry",
        "shelfLifeDays": "Shelf life (days)",
        "aiSuggests": "AI suggested: {{value}}",
        "readOnlyHint": "Only owners and admins can edit the catalog",
        "saved": "Classification updated",
        "saveFailed": "Could not update classification",
        "resetDone": "Reset to the AI classification"
    },
    "productCategories": {
        "Other": "Other",
        "Produce": "Produce",
        "Bakery": "Bakery",
        "Meat": "Meat",
        "Fish": "Fish",
        "DairyAndEggs": "Dairy & eggs",
        "Cheese": "Cheese",
        "DeliAndColdCuts": "Deli & cold cuts",
        "Frozen": "Frozen",
        "Pantry": "Pantry",
        "CannedGoods": "Canned goods",
        "Sauces": "Sauces",
        "OilsAndVinegar": "Oils & vinegar",
        "Spices": "Spices",
        "Cereal": "Cereal",
        "Spreads": "Spreads",
        "Snacks": "Snacks",
        "Sweets": "Sweets",
        "Beverages": "Beverages",
        "Alcohol": "Alcohol",
        "HouseholdAndCleaning": "Household & cleaning",
        "HealthAndBeauty": "Health & beauty",
        "Baby": "Baby",
        "Pet": "Pet"
    },
    "expiryHandlings": {
        "NonPerishable": "Doesn't expire",
        "UserEntersFromPackage": "Date on the package",
        "AiRecommendsShelfLife": "Estimated shelf life"
    }
```

- [ ] **Step 3: Add the i18n sections (de)**

In `public/locales/de/translation.json`, add the same three sections with German values:

```json
    "products": {
        "title": "Produktkatalog",
        "subtitle": "Überprüfe und korrigiere die Produktklassifizierung",
        "search": "Produkte suchen",
        "empty": "Noch keine Produkte",
        "overridden": "Bearbeitet",
        "edit": "Klassifizierung bearbeiten",
        "reset": "Auf KI zurücksetzen",
        "save": "Speichern",
        "cancel": "Abbrechen",
        "category": "Kategorie",
        "expiry": "Haltbarkeit",
        "shelfLifeDays": "Haltbarkeit (Tage)",
        "aiSuggests": "KI-Vorschlag: {{value}}",
        "readOnlyHint": "Nur Eigentümer und Admins können den Katalog bearbeiten",
        "saved": "Klassifizierung aktualisiert",
        "saveFailed": "Klassifizierung konnte nicht aktualisiert werden",
        "resetDone": "Auf KI-Klassifizierung zurückgesetzt"
    },
    "productCategories": {
        "Other": "Sonstiges",
        "Produce": "Obst & Gemüse",
        "Bakery": "Backwaren",
        "Meat": "Fleisch",
        "Fish": "Fisch",
        "DairyAndEggs": "Milchprodukte & Eier",
        "Cheese": "Käse",
        "DeliAndColdCuts": "Wurst & Aufschnitt",
        "Frozen": "Tiefkühl",
        "Pantry": "Vorratskammer",
        "CannedGoods": "Konserven",
        "Sauces": "Saucen",
        "OilsAndVinegar": "Öle & Essig",
        "Spices": "Gewürze",
        "Cereal": "Müsli & Cerealien",
        "Spreads": "Aufstriche",
        "Snacks": "Snacks",
        "Sweets": "Süßigkeiten",
        "Beverages": "Getränke",
        "Alcohol": "Alkohol",
        "HouseholdAndCleaning": "Haushalt & Reinigung",
        "HealthAndBeauty": "Gesundheit & Pflege",
        "Baby": "Baby",
        "Pet": "Haustier"
    },
    "expiryHandlings": {
        "NonPerishable": "Verdirbt nicht",
        "UserEntersFromPackage": "Datum auf der Verpackung",
        "AiRecommendsShelfLife": "Geschätzte Haltbarkeit"
    }
```

- [ ] **Step 4: Register the namespaces in the typed keys**

In `src/types/i18next.d.ts`, add three lines inside the `translation: { ... }` block (after `copyToList`):

```ts
                products: Record<string, string>;
                productCategories: Record<string, string>;
                expiryHandlings: Record<string, string>;
```

- [ ] **Step 5: Verify JSON + types**

Run (from `ClientApp/`): `npm run tsc`
Expected: exits 0. (A JSON syntax error or a missing namespace line will fail here.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/products/productClassificationOptions.ts Application/Frigorino.Web/ClientApp/public/locales Application/Frigorino.Web/ClientApp/src/types/i18next.d.ts
git commit -m "feat(products): catalog i18n + selectable enum options"
```

---

### Task 10: Query + mutation hooks

**Files:**
- Create: `src/features/products/useProducts.ts`
- Create: `src/features/products/useOverrideProductClassification.ts`
- Create: `src/features/products/useResetProductClassification.ts`

**Interfaces:**
- Produces: `useProducts(householdId, enabled?)`; `useOverrideProductClassification()`; `useResetProductClassification()`. Mutation callers pass `{ path: { householdId, productId }, body }` (override) / `{ path: { householdId, productId } }` (reset).

- [ ] **Step 1: Query hook**

`useProducts.ts`:

```ts
import { useQuery } from "@tanstack/react-query";
import { getProductsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useProducts = (householdId: number, enabled = true) =>
    useQuery({
        ...getProductsOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 5,
    });
```

- [ ] **Step 2: Override mutation hook**

`useOverrideProductClassification.ts`:

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getProductsQueryKey,
    overrideProductClassificationMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useOverrideProductClassification = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...overrideProductClassificationMutation(),
        onSettled: (_data, _error, variables) => {
            queryClient.invalidateQueries({
                queryKey: getProductsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
```

- [ ] **Step 3: Reset mutation hook**

`useResetProductClassification.ts`:

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getProductsQueryKey,
    resetProductClassificationMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useResetProductClassification = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...resetProductClassificationMutation(),
        onSettled: (_data, _error, variables) => {
            queryClient.invalidateQueries({
                queryKey: getProductsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
```

- [ ] **Step 4: Type-check**

Run: `npm run tsc`
Expected: exits 0.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/products/useProducts.ts Application/Frigorino.Web/ClientApp/src/features/products/useOverrideProductClassification.ts Application/Frigorino.Web/ClientApp/src/features/products/useResetProductClassification.ts
git commit -m "feat(products): catalog query + mutation hooks"
```

---

### Task 11: `ProductEditSheet` component

**Files:**
- Create: `src/features/products/components/ProductEditSheet.tsx`

**Interfaces:**
- Consumes: `ProductCatalogItem` (from `lib/api/types.gen`), `PRODUCT_CATEGORY_OPTIONS`, `EXPIRY_HANDLING_OPTIONS`, `useOverrideProductClassification`, `useResetProductClassification`.
- Produces: `ProductEditSheet({ open, onClose, householdId, product })` — rendered only for Owner/Admin; receives a non-null `product` when open.

- [ ] **Step 1: Create the component**

`ProductEditSheet.tsx` (the `key`-remount seeds form state from the product via `useState` initializers — same pattern as `HouseholdSettingsCard`; the shelf-life field is shown only for `AiRecommendsShelfLife`, and switching handling clears it):

```tsx
import {
    Box,
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    FormControl,
    InputLabel,
    MenuItem,
    Select,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type {
    ExpiryHandling,
    ProductCatalogItem,
    ProductCategory,
} from "../../../lib/api/types.gen";
import {
    EXPIRY_HANDLING_OPTIONS,
    PRODUCT_CATEGORY_OPTIONS,
} from "../productClassificationOptions";
import { useOverrideProductClassification } from "../useOverrideProductClassification";
import { useResetProductClassification } from "../useResetProductClassification";

interface Props {
    open: boolean;
    onClose: () => void;
    householdId: number;
    product: ProductCatalogItem | null;
}

export function ProductEditSheet({ open, onClose, householdId, product }: Props) {
    // Remount the inner form per opened product so fields seed from the row via useState
    // initializers (no reset-in-effect).
    return (
        <Dialog open={open} onClose={onClose} fullWidth maxWidth="xs">
            {product && (
                <ProductEditSheetInner
                    key={product.id}
                    onClose={onClose}
                    householdId={householdId}
                    product={product}
                />
            )}
        </Dialog>
    );
}

interface InnerProps {
    onClose: () => void;
    householdId: number;
    product: ProductCatalogItem;
}

function ProductEditSheetInner({ onClose, householdId, product }: InnerProps) {
    const { t } = useTranslation();
    const override = useOverrideProductClassification();
    const reset = useResetProductClassification();

    const [category, setCategory] = useState<ProductCategory>(
        product.effectiveCategory,
    );
    const [handling, setHandling] = useState<ExpiryHandling>(
        product.effectiveExpiryHandling,
    );
    const [days, setDays] = useState(() =>
        product.effectiveShelfLifeDays != null
            ? String(product.effectiveShelfLifeDays)
            : "",
    );

    const showDays = handling === "AiRecommendsShelfLife";

    const onHandlingChange = (next: ExpiryHandling) => {
        setHandling(next);
        if (next !== "AiRecommendsShelfLife") {
            setDays("");
        }
    };

    const save = async () => {
        let shelfLifeDays: number | null = null;
        if (handling === "AiRecommendsShelfLife") {
            const parsed = Number(days);
            if (!Number.isInteger(parsed) || parsed < 1 || parsed > 365) {
                toast.error(t("products.saveFailed"));
                return;
            }
            shelfLifeDays = parsed;
        }

        try {
            await override.mutateAsync({
                path: { householdId, productId: product.id },
                body: { category, expiryHandling: handling, shelfLifeDays },
            });
            toast.success(t("products.saved"));
            onClose();
        } catch {
            toast.error(t("products.saveFailed"));
        }
    };

    const resetToAi = async () => {
        try {
            await reset.mutateAsync({
                path: { householdId, productId: product.id },
            });
            toast.success(t("products.resetDone"));
            onClose();
        } catch {
            toast.error(t("products.saveFailed"));
        }
    };

    const busy = override.isPending || reset.isPending;

    return (
        <>
            <DialogTitle sx={{ textTransform: "capitalize" }}>
                {product.name}
            </DialogTitle>
            <DialogContent>
                <Stack spacing={2} sx={{ mt: 1 }}>
                    <FormControl fullWidth size="small" data-testid="product-category-control">
                        <InputLabel>{t("products.category")}</InputLabel>
                        <Select
                            label={t("products.category")}
                            value={category}
                            onChange={(e) => setCategory(e.target.value as ProductCategory)}
                        >
                            {PRODUCT_CATEGORY_OPTIONS.map((c) => (
                                <MenuItem key={c} value={c}>
                                    {t(`productCategories.${c}`)}
                                </MenuItem>
                            ))}
                        </Select>
                    </FormControl>

                    <FormControl fullWidth size="small" data-testid="product-expiry-control">
                        <InputLabel>{t("products.expiry")}</InputLabel>
                        <Select
                            label={t("products.expiry")}
                            value={handling}
                            onChange={(e) =>
                                onHandlingChange(e.target.value as ExpiryHandling)
                            }
                        >
                            {EXPIRY_HANDLING_OPTIONS.map((h) => (
                                <MenuItem key={h} value={h}>
                                    {t(`expiryHandlings.${h}`)}
                                </MenuItem>
                            ))}
                        </Select>
                    </FormControl>

                    {showDays && (
                        <TextField
                            type="number"
                            size="small"
                            fullWidth
                            label={t("products.shelfLifeDays")}
                            value={days}
                            onChange={(e) => setDays(e.target.value)}
                            slotProps={{
                                htmlInput: {
                                    min: 1,
                                    max: 365,
                                    "data-testid": "product-shelf-life-input",
                                },
                            }}
                        />
                    )}

                    <Typography variant="caption" color="text.secondary">
                        {t("products.aiSuggests", {
                            value: t(`productCategories.${product.aiCategory}`),
                        })}
                    </Typography>
                </Stack>
            </DialogContent>
            <DialogActions sx={{ justifyContent: "space-between" }}>
                <Box>
                    {product.isOverridden && (
                        <Button
                            color="inherit"
                            disabled={busy}
                            onClick={resetToAi}
                            data-testid="product-reset-button"
                        >
                            {t("products.reset")}
                        </Button>
                    )}
                </Box>
                <Box>
                    <Button color="inherit" disabled={busy} onClick={onClose}>
                        {t("products.cancel")}
                    </Button>
                    <Button
                        variant="contained"
                        disabled={busy}
                        onClick={save}
                        data-testid="product-save-button"
                    >
                        {t("products.save")}
                    </Button>
                </Box>
            </DialogActions>
        </>
    );
}
```

- [ ] **Step 2: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: both exit 0.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/products/components/ProductEditSheet.tsx
git commit -m "feat(products): product classification edit sheet"
```

---

### Task 12: `ProductCatalogPage` + route + manage-page link

**Files:**
- Create: `src/features/products/pages/ProductCatalogPage.tsx`
- Create: `src/routes/household/products.tsx`
- Modify: `src/features/households/pages/ManageHouseholdPage.tsx`
- Generated: `src/routeTree.gen.ts`

**Interfaces:**
- Consumes: `useCurrentHouseholdWithDetails`, `useProducts`, `ProductEditSheet`, `HouseholdRoleValue`/`roleRank` (from `features/households/householdRole`), `pageContainerSx`.

- [ ] **Step 1: Create the page**

`ProductCatalogPage.tsx` (client-side search; rows tappable only for Owner/Admin; effective category/expiry shown via the label namespaces; an "Edited" chip when overridden):

```tsx
import {
    Alert,
    Chip,
    Container,
    List,
    ListItemButton,
    ListItemText,
    Skeleton,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ProductCatalogItem } from "../../../lib/api/types.gen";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { HouseholdRoleValue, roleRank } from "../../households/householdRole";
import { ProductEditSheet } from "../components/ProductEditSheet";
import { useProducts } from "../useProducts";

export function ProductCatalogPage() {
    const { t } = useTranslation();
    const { currentHousehold, hasActiveHousehold } =
        useCurrentHouseholdWithDetails();

    const householdId = currentHousehold?.householdId ?? 0;
    const role = currentHousehold?.role;
    // ponytail: client-side filter; add server-side paging if a household exceeds a few hundred products.
    const canManage =
        !!role && roleRank[role] >= roleRank[HouseholdRoleValue.Admin];

    const { data: products, isLoading } = useProducts(householdId);

    const [query, setQuery] = useState("");
    const [selected, setSelected] = useState<ProductCatalogItem | null>(null);

    const filtered = useMemo(() => {
        const q = query.trim().toLowerCase();
        const rows = products ?? [];
        if (q.length === 0) {
            return rows;
        }
        return rows.filter((p) => p.name.toLowerCase().includes(q));
    }, [products, query]);

    if (!hasActiveHousehold || householdId === 0) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    return (
        <Container maxWidth="md" sx={pageContainerSx}>
            <Typography variant="h5" sx={{ mb: 0.5 }}>
                {t("products.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                {t("products.subtitle")}
            </Typography>

            <TextField
                fullWidth
                size="small"
                placeholder={t("products.search")}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                sx={{ mb: 2 }}
                slotProps={{ htmlInput: { "data-testid": "product-search-input" } }}
            />

            {isLoading && <Skeleton variant="rectangular" height={240} />}

            {!isLoading && filtered.length === 0 && (
                <Alert severity="info">{t("products.empty")}</Alert>
            )}

            {!isLoading && filtered.length > 0 && (
                <List data-testid="product-catalog-list">
                    {filtered.map((p) => {
                        const expiryLabel =
                            p.effectiveExpiryHandling === "AiRecommendsShelfLife" &&
                            p.effectiveShelfLifeDays != null
                                ? `${t(`expiryHandlings.${p.effectiveExpiryHandling}`)} · ${p.effectiveShelfLifeDays}d`
                                : t(`expiryHandlings.${p.effectiveExpiryHandling}`);
                        return (
                            <ListItemButton
                                key={p.id}
                                disabled={!canManage}
                                onClick={() => canManage && setSelected(p)}
                                data-testid={`product-row-${p.id}`}
                                data-overridden={p.isOverridden ? "true" : "false"}
                            >
                                <ListItemText
                                    primary={
                                        <Stack
                                            direction="row"
                                            spacing={1}
                                            alignItems="center"
                                        >
                                            <span style={{ textTransform: "capitalize" }}>
                                                {p.name}
                                            </span>
                                            {p.isOverridden && (
                                                <Chip
                                                    size="small"
                                                    label={t("products.overridden")}
                                                    data-testid={`product-overridden-${p.id}`}
                                                />
                                            )}
                                        </Stack>
                                    }
                                    secondary={`${t(`productCategories.${p.effectiveCategory}`)} · ${expiryLabel}`}
                                />
                            </ListItemButton>
                        );
                    })}
                </List>
            )}

            {!canManage && (
                <Typography variant="caption" color="text.secondary">
                    {t("products.readOnlyHint")}
                </Typography>
            )}

            <ProductEditSheet
                open={selected !== null}
                onClose={() => setSelected(null)}
                householdId={householdId}
                product={selected}
            />
        </Container>
    );
}
```

- [ ] **Step 2: Create the route shell**

`src/routes/household/products.tsx`:

```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { ProductCatalogPage } from "../../features/products/pages/ProductCatalogPage";

export const Route = createFileRoute("/household/products")({
    beforeLoad: requireAuth,
    component: ProductCatalogPage,
});
```

- [ ] **Step 3: Regenerate the route tree**

Run (from `ClientApp/`): `npm run routes:gen`
Expected: `src/routeTree.gen.ts` updates to include `/household/products`.

- [ ] **Step 4: Add the link on the manage page**

In `ManageHouseholdPage.tsx`: add `Inventory2` to the `@mui/icons-material` import, and after the blueprints-link `<Box>` (line ~131) add:

```tsx
                <Box sx={{ mt: { xs: 2, sm: 3 } }}>
                    <Button
                        component={Link}
                        to="/household/products"
                        variant="outlined"
                        startIcon={<Inventory2 />}
                        data-testid="household-manage-products-link"
                    >
                        {t("products.title")}
                    </Button>
                </Box>
```

(Change the icon import line `import { Delete, Sort } from "@mui/icons-material";` to `import { Delete, Inventory2, Sort } from "@mui/icons-material";`.)

- [ ] **Step 5: Type-check + lint**

Run: `npm run tsc && npm run lint`
Expected: both exit 0.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/products/pages/ProductCatalogPage.tsx Application/Frigorino.Web/ClientApp/src/routes/household/products.tsx Application/Frigorino.Web/ClientApp/src/routeTree.gen.ts Application/Frigorino.Web/ClientApp/src/features/households/pages/ManageHouseholdPage.tsx
git commit -m "feat(products): catalog page, route, and manage-page link"
```

---

### Task 13: Manual UI verification gate

Not a code task — a checkpoint. The page/sheet have no automated UI test (no JS runner; API behavior is covered in Phase 5). Verify in a browser before moving on.

- [ ] **Step 1: Build the SPA** (so a running stack/IT serves current assets)

Run (from `ClientApp/`): `npm run build`
Expected: `tsc -b && vite build` succeeds.

- [ ] **Step 2: Drive the UI** (hand off to the user, or `/dev-up` if running unattended)

Checklist on `/household/products` as an Owner/Admin with at least one classified product:
- Search filters the list by name.
- Tapping a row opens the sheet; the shelf-life field shows only for "Estimated shelf life" and hides on switching to "Doesn't expire".
- Saving updates the row and shows the "Edited" chip.
- "Reset to AI" appears on an edited product and restores the AI values.
- As a Member, rows are not tappable and the read-only hint shows.

---

# Phase 5 — Integration test + final verification

### Task 14: Products API integration test

> **Deviation from spec (flagged):** the spec named Playwright; this uses an **API-level** Reqnroll IT (`Products.Api.feature`) mirroring `Settings.Api.feature`. It covers the override/reset/403/404 contract against real Postgres without the MUI-Select/toast flakiness that has repeatedly bitten UI ITs here. UI is covered by Task 13's manual gate.

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Products/Products.Api.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Products/ProductsApiSteps.cs`

**Interfaces:**
- Consumes: `ctx.HouseholdId`, `ctx.Factory.Services`, `ctx.BrowserContext.APIRequest`, existing household-setup Givens (`I am logged in with an active household`, `I am logged in as`, `an existing household ... owned by ... with me as a ...`, `... that I am not a member of`), shared `the API response status is {int}` / `... validation error for {string}`.
- Produces: `TestApiClient.TryGetProductsAsync`, `TryOverrideProductClassificationAsync`, `TryResetProductClassificationAsync`.

- [ ] **Step 1: Add the API client methods**

Append to `TestApiClient.cs` (enums cross the wire as **string names**, so pass strings):

```csharp
    public Task<IAPIResponse> TryGetProductsAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/products",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryOverrideProductClassificationAsync(
        int productId, string category, string expiryHandling, int? shelfLifeDays, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PutAsync(
            $"/api/household/{targetHouseholdId}/products/{productId}/classification",
            new APIRequestContextOptions
            {
                DataObject = new { category, expiryHandling, shelfLifeDays },
                Headers = AuthHeaders,
            });
    }

    public Task<IAPIResponse> TryResetProductClassificationAsync(int productId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.DeleteAsync(
            $"/api/household/{targetHouseholdId}/products/{productId}/classification",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }
```

- [ ] **Step 2: Write the feature**

`Products.Api.feature` (step phrases are globally unique across the IT assembly; `Given` for seeding, `When` for actions, `Then` for assertions):

```gherkin
Feature: Products catalog API

  Background:
    Given I am logged in with an active household

  Scenario: Owner overrides a product to a longer shelf life
    Given a classified product "milk" with AI shelf life 7
    When I PUT a product override with category "DairyAndEggs" expiry "AiRecommendsShelfLife" shelf life 14
    Then the API response status is 200
    And the product API response is overridden
    And the product API response effective shelf life is 14

  Scenario: Overriding to non-perishable drops the shelf life
    Given a classified product "salt" with AI shelf life 7
    When I PUT a product override with category "Pantry" expiry "NonPerishable" and no shelf life
    Then the API response status is 200
    And the product API response effective expiry is "NonPerishable"
    And the product API response has no effective shelf life

  Scenario: Reset restores the AI verdict
    Given a classified product "milk" with AI shelf life 7
    When I PUT a product override with category "Pantry" expiry "NonPerishable" and no shelf life
    Then the API response status is 200
    And the product API response is overridden
    When I DELETE the product override
    Then the API response status is 200
    And the product API response is not overridden
    And the product API response effective expiry is "AiRecommendsShelfLife"
    And the product API response effective shelf life is 7

  Scenario: Shelf life out of bounds is rejected
    Given a classified product "milk" with AI shelf life 7
    When I PUT a product override with category "DairyAndEggs" expiry "AiRecommendsShelfLife" shelf life 0
    Then the API response status is 400
    And the API response has a validation error for "ShelfLifeDays"

  Scenario: Member cannot override a product
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    And a classified product "milk" with AI shelf life 7
    When I PUT a product override with category "Pantry" expiry "NonPerishable" and no shelf life
    Then the API response status is 403

  Scenario: Non-member cannot read the catalog
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the product catalog via the API
    Then the API response status is 404
```

- [ ] **Step 3: Write the step bindings**

`ProductsApiSteps.cs` (seeds directly via a DI scope; keeps the seeded id in an instance field — one binding instance per scenario):

```csharp
using System.Text.Json;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Products;

[Binding]
public class ProductsApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    private int _productId;

    [Given("a classified product {string} with AI shelf life {int}")]
    public async Task GivenAClassifiedProductWithAiShelfLife(string normalizedName, int days)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = Product.Create(
            ctx.HouseholdId,
            normalizedName,
            new ProductClassification(
                ProductCategory.DairyAndEggs,
                ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value),
            classifierVersion: 1).Value;
        db.Products.Add(product);
        await db.SaveChangesAsync();
        _productId = product.Id;
    }

    [When("I PUT a product override with category {string} expiry {string} shelf life {int}")]
    public async Task WhenIPutAProductOverrideWithShelfLife(string category, string expiry, int days)
    {
        ctx.LastApiResponse = await api.TryOverrideProductClassificationAsync(_productId, category, expiry, days);
    }

    [When("I PUT a product override with category {string} expiry {string} and no shelf life")]
    public async Task WhenIPutAProductOverrideNoShelfLife(string category, string expiry)
    {
        ctx.LastApiResponse = await api.TryOverrideProductClassificationAsync(_productId, category, expiry, null);
    }

    [When("I DELETE the product override")]
    public async Task WhenIDeleteTheProductOverride()
    {
        ctx.LastApiResponse = await api.TryResetProductClassificationAsync(_productId);
    }

    [When("I GET the product catalog via the API")]
    public async Task WhenIGetTheProductCatalogViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetProductsAsync();
    }

    [Then("the product API response is overridden")]
    public async Task ThenTheProductApiResponseIsOverridden()
    {
        var body = await ReadBodyAsync();
        Assert.True(body.GetProperty("isOverridden").GetBoolean());
    }

    [Then("the product API response is not overridden")]
    public async Task ThenTheProductApiResponseIsNotOverridden()
    {
        var body = await ReadBodyAsync();
        Assert.False(body.GetProperty("isOverridden").GetBoolean());
    }

    [Then("the product API response effective expiry is {string}")]
    public async Task ThenTheProductApiResponseEffectiveExpiryIs(string expected)
    {
        var body = await ReadBodyAsync();
        Assert.Equal(expected, body.GetProperty("effectiveExpiryHandling").GetString());
    }

    [Then("the product API response effective shelf life is {int}")]
    public async Task ThenTheProductApiResponseEffectiveShelfLifeIs(int expected)
    {
        var body = await ReadBodyAsync();
        Assert.Equal(expected, body.GetProperty("effectiveShelfLifeDays").GetInt32());
    }

    [Then("the product API response has no effective shelf life")]
    public async Task ThenTheProductApiResponseHasNoEffectiveShelfLife()
    {
        var body = await ReadBodyAsync();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("effectiveShelfLifeDays").ValueKind);
    }

    private async Task<JsonElement> ReadBodyAsync()
    {
        Assert.NotNull(ctx.LastApiResponse);
        var body = await ctx.LastApiResponse.JsonAsync();
        Assert.NotNull(body);
        return body.Value;
    }
}
```

- [ ] **Step 4: Run the products IT** (filter on title words, not the file name)

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~ProductsCatalogApi"`
Expected: 6 scenarios, all pass. (If 0 run, the FQN filter built from the sanitized Feature title `Products catalog API` differs — adjust to `~Productscatalog` or run the whole IT assembly and confirm the new scenarios appear in the output.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs Application/Frigorino.IntegrationTests/Slices/Products/
git commit -m "test(products): API integration coverage for override/reset/role gate"
```

---

### Task 15: Docs + full verification

**Files:**
- Modify: `knowledge/AI_Classification.md`
- Modify: `IDEAS.md` (if a catalog entry exists)

- [ ] **Step 1: Update the knowledge doc**

In `knowledge/AI_Classification.md`, under "Product domain", add a short paragraph documenting the override layer:

> **User override layer.** `Product` carries nullable `OverrideProductCategory` / `OverrideExpiryHandling` / `OverrideShelfLifeDays`, set/cleared atomically via `OverrideClassification` / `ResetToAiClassification`. `EffectiveCategory` / `EffectiveExpiry` read `Override ?? Classification` (expiry as a whole facet). `IsOverridden` (`OverrideExpiryHandling.HasValue`) shields the row: `ProductClassificationGaps.SelectGaps` and `ClassifyProductJob` treat an overridden row as up-to-date regardless of `ClassifierVersion`, so a version bump never clobbers a correction. Surfaced via the `Products` slices (`GET/PUT/DELETE /api/household/{id}/products...`) and the catalog page under household management.

- [ ] **Step 2: Remove the shipped idea entry**

Check `IDEAS.md` for the "User-editable Products catalog" entry; if present, delete just that entry.

```bash
grep -n " products catalog\|override AI classification" IDEAS.md
```

- [ ] **Step 3: Full solution test**

Run: `dotnet test Application/Frigorino.sln`
Expected: all `Frigorino.Test` + `Frigorino.IntegrationTests` pass. Read the pass/fail summary lines — don't trust a piped tail.

- [ ] **Step 4: Frontend verify**

Run (from `ClientApp/`): `npm run tsc && npm run lint && npm run prettier:check`
Expected: all exit 0. (Run `npm run prettier` first if `prettier:check` flags formatting.)

- [ ] **Step 5: Docker build** (catches Dockerfile/pipeline drift; no project added here, so expected clean)

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: build succeeds. (If the Docker daemon is unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 6: Commit**

```bash
git add knowledge/AI_Classification.md IDEAS.md
git commit -m "docs(products): document override layer; drop shipped idea"
```

---

## Self-Review

**Spec coverage:**
- Override layer (nullable columns, `Override ?? Classification`, atomic, derived flag) → Task 1.
- Migration → Task 2.
- Backfill shield (gap-selector + job) → Tasks 3, 4.
- Three slices under `/api/household/{id}/products` with member-read / Owner-Admin-write + 404/403/validation → Tasks 5, 6, 7.
- Read DTO with Effective* + Ai* + isOverridden → Task 5.
- Client regen → Task 8.
- Frontend: route, page, sheet, hooks, link, coupled shelf-life field, Unknown excluded, client-side search, read-only members → Tasks 9-12.
- Testing: aggregate + gaps + job unit tests (Tasks 1, 3, 4), API IT (Task 14), manual UI gate (Task 13).
- Downstream zero-touch (`PromoteSuggestion`/`ApplyBlueprint` read `Effective*`) → no task needed; verified by the full test run in Task 15.
- Docs + idea cleanup → Task 15.

**Placeholder scan:** none — every step carries real code/commands.

**Type consistency:** `OverrideClassification`/`ResetToAiClassification`/`IsOverridden`/`EffectiveCategory`/`EffectiveExpiry` (Task 1) are used identically in Tasks 4-7. `ProductCatalogItem` fields (Task 5) match the IT JSON assertions (`isOverridden`, `effectiveExpiryHandling`, `effectiveShelfLifeDays`) in Task 14 and the SPA field reads (`effectiveCategory`, `aiCategory`, `name`) in Tasks 11-12. `OverrideProductClassificationRequest` (`Category`/`ExpiryHandling`/`ShelfLifeDays`) matches the mutation body `{ category, expiryHandling, shelfLifeDays }` (Task 11) and the IT `DataObject` (Task 14). `ExistingProduct`'s new 4th param is defaulted so existing constructions compile (Task 3).

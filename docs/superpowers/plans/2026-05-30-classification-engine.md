# AI Item Classification Engine (Cycle 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a list item's product name is added or edited, asynchronously classify how that product expires and store it in a per-household `Product` catalog — invisibly (no API/UI change), and as a no-op when no classifier key is configured.

**Architecture:** A pure-domain `ExpiryProfile` value object + `Product` aggregate (keyed `(HouseholdId, NormalizedName)`) hold classification metadata. The list-item slices call a thin `IProductClassificationTrigger`; the enabled implementation enqueues an `IClassifyProductJob` onto the Cycle 1 `IBackgroundTaskQueue`, which runs in a fresh DI scope, calls the `IItemClassifier` port (OpenAI adapter), and upserts the `Product`. One slim port (`IItemClassifier`) is the only AI abstraction; the OpenAI SDK is used directly behind it.

**Tech Stack:** .NET 10, EF Core (Postgres), FluentResults, FakeItEasy + xUnit + EF InMemory, Reqnroll + Postgres Testcontainers, official `OpenAI` .NET SDK (v2.8.0) with strict Structured Outputs.

**Spec:** `docs/superpowers/specs/2026-05-30-classification-engine-design.md`

---

## File Structure

**Domain (`Frigorino.Domain`):**
- Create `Products/ExpiryHandling.cs` — enum.
- Create `Products/ExpiryProfile.cs` — value object (invariant + `SuggestedExpiry`).
- Create `Products/ProductClassification.cs` — composite result record.
- Create `Products/ProductName.cs` — `Normalize` static.
- Create `Entities/Product.cs` — aggregate root.
- Create `Interfaces/IItemClassifier.cs`, `Interfaces/IClassifyProductJob.cs`, `Interfaces/IProductClassificationTrigger.cs`.

**Infrastructure (`Frigorino.Infrastructure`):**
- Modify `EntityFramework/ApplicationDbContext.cs` — `DbSet<Product>` + timestamp stamping.
- Create `EntityFramework/Configurations/ProductConfiguration.cs`.
- Create `Services/ClassifyProductJob.cs`.
- Create `Services/ProductClassificationTriggers.cs` — `Queueing` + `Null` impls.
- Create `Services/OpenAiItemClassifier.cs`.
- Create `Services/ItemClassificationDependencyInjection.cs` — `AddItemClassification`.
- Modify `Frigorino.Infrastructure.csproj` — add `OpenAI` package.
- Create `Migrations/<timestamp>_AddProductCatalog.cs` (generated).

**Web / Features:**
- Modify `Frigorino.Web/Program.cs` — `AddItemClassification`.
- Modify `Frigorino.Web/appsettings.json` — `Classifier` section.
- Modify `Frigorino.Features/Lists/Items/CreateItem.cs` and `UpdateItem.cs` — trigger call.

**Tests:**
- Create `Frigorino.Test/Domain/ExpiryProfileTests.cs`, `ProductNameTests.cs`, `ProductAggregateTests.cs`.
- Create `Frigorino.Test/Infrastructure/ProductPersistenceTests.cs`, `ClassifyProductJobTests.cs`, `ProductClassificationTriggerTests.cs`.
- Create `Frigorino.IntegrationTests/Infrastructure/StubItemClassifier.cs`; modify `TestWebApplicationFactory.cs`.
- Create `Frigorino.IntegrationTests/Slices/Lists/Classification.Api.feature` + `ClassificationApiSteps.cs`.

**Conventions to follow (from CLAUDE.md + memory):** block-style braces always; flat columns, no EF owned types; exact NuGet version pins; vendor-neutral `Classifier:*` config; run shell from repo root (`Application/...` paths); tests never assert translated text.

---

## Task 1: `ExpiryProfile` value object

**Files:**
- Create: `Application/Frigorino.Domain/Products/ExpiryHandling.cs`
- Create: `Application/Frigorino.Domain/Products/ExpiryProfile.cs`
- Create: `Application/Frigorino.Domain/Products/ProductClassification.cs`
- Test: `Application/Frigorino.Test/Domain/ExpiryProfileTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Domain/ExpiryProfileTests.cs`:

```csharp
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class ExpiryProfileTests
    {
        [Fact]
        public void Create_NonPerishable_WithNullShelfLife_Succeeds()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.NonPerishable, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(ExpiryHandling.NonPerishable, result.Value.Handling);
            Assert.Null(result.Value.ShelfLifeDays);
        }

        [Fact]
        public void Create_NonPerishable_WithShelfLife_Fails()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.NonPerishable, 10);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_UserEntersFromPackage_WithShelfLife_Fails()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.UserEntersFromPackage, 10);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_AiRecommends_WithNullShelfLife_Fails()
        {
            var result = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, null);

            Assert.True(result.IsFailed);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(366)]
        public void Create_AiRecommends_OutOfRange_Fails(int days)
        {
            var result = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days);

            Assert.True(result.IsFailed);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(365)]
        public void Create_AiRecommends_InRange_Succeeds(int days)
        {
            var result = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days);

            Assert.True(result.IsSuccess);
            Assert.Equal(days, result.Value.ShelfLifeDays);
        }

        [Fact]
        public void SuggestedExpiry_AiRecommends_ReturnsTodayPlusDays()
        {
            var profile = ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 7).Value;

            var suggested = profile.SuggestedExpiry(new DateOnly(2026, 1, 1));

            Assert.Equal(new DateOnly(2026, 1, 8), suggested);
        }

        [Fact]
        public void SuggestedExpiry_NonPerishable_ReturnsNull()
        {
            Assert.Null(ExpiryProfile.NonPerishable.SuggestedExpiry(new DateOnly(2026, 1, 1)));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryProfileTests"`
Expected: FAIL — `ExpiryProfile`/`ExpiryHandling` do not exist (compile error).

- [ ] **Step 3: Write the implementation**

Create `Application/Frigorino.Domain/Products/ExpiryHandling.cs`:

```csharp
namespace Frigorino.Domain.Products
{
    public enum ExpiryHandling
    {
        NonPerishable = 0,
        UserEntersFromPackage = 1,
        AiRecommendsShelfLife = 2,
    }
}
```

Create `Application/Frigorino.Domain/Products/ExpiryProfile.cs`:

```csharp
using FluentResults;

namespace Frigorino.Domain.Products
{
    // Pure domain value object: how a product expires. Persisted as flat columns on Product
    // (not an EF owned type), mirroring the Quantity VO approach.
    public readonly record struct ExpiryProfile
    {
        public const int ShelfLifeDaysMin = 1;
        public const int ShelfLifeDaysMax = 365;

        public ExpiryHandling Handling { get; }
        public int? ShelfLifeDays { get; }

        private ExpiryProfile(ExpiryHandling handling, int? shelfLifeDays)
        {
            Handling = handling;
            ShelfLifeDays = shelfLifeDays;
        }

        // default(ExpiryProfile) == NonPerishable with no shelf-life, which is a valid state.
        public static ExpiryProfile NonPerishable => new(ExpiryHandling.NonPerishable, null);

        // Invariant: ShelfLifeDays is set iff Handling == AiRecommendsShelfLife, range 1..365.
        public static Result<ExpiryProfile> Create(ExpiryHandling handling, int? shelfLifeDays)
        {
            if (handling == ExpiryHandling.AiRecommendsShelfLife)
            {
                if (shelfLifeDays is null)
                {
                    return Result.Fail<ExpiryProfile>(
                        new Error("Shelf-life days are required when AI recommends a shelf life.")
                            .WithMetadata("Property", nameof(ShelfLifeDays)));
                }
                if (shelfLifeDays < ShelfLifeDaysMin || shelfLifeDays > ShelfLifeDaysMax)
                {
                    return Result.Fail<ExpiryProfile>(
                        new Error($"Shelf-life days must be between {ShelfLifeDaysMin} and {ShelfLifeDaysMax}.")
                            .WithMetadata("Property", nameof(ShelfLifeDays)));
                }
            }
            else if (shelfLifeDays is not null)
            {
                return Result.Fail<ExpiryProfile>(
                    new Error("Shelf-life days are only valid when AI recommends a shelf life.")
                        .WithMetadata("Property", nameof(ShelfLifeDays)));
            }

            return Result.Ok(new ExpiryProfile(handling, shelfLifeDays));
        }

        // Only an AI-recommended shelf life yields a concrete suggested date.
        public DateOnly? SuggestedExpiry(DateOnly today)
        {
            return Handling == ExpiryHandling.AiRecommendsShelfLife
                ? today.AddDays(ShelfLifeDays!.Value)
                : null;
        }
    }
}
```

Create `Application/Frigorino.Domain/Products/ProductClassification.cs`:

```csharp
namespace Frigorino.Domain.Products
{
    // Composite classifier result. One facet today (Expiry); future facets (storage location,
    // default unit) are additive — add a field here + a column on Product + a schema line.
    public sealed record ProductClassification(ExpiryProfile Expiry);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryProfileTests"`
Expected: PASS (8 test cases).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Products Application/Frigorino.Test/Domain/ExpiryProfileTests.cs
git commit -m "feat: add ExpiryProfile value object and ProductClassification"
```

---

## Task 2: `ProductName.Normalize`

**Files:**
- Create: `Application/Frigorino.Domain/Products/ProductName.cs`
- Test: `Application/Frigorino.Test/Domain/ProductNameTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Domain/ProductNameTests.cs`:

```csharp
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class ProductNameTests
    {
        [Theory]
        [InlineData("Milk", "milk")]
        [InlineData("  Milk  ", "milk")]
        [InlineData("Whole   Milk", "whole milk")]
        [InlineData("WHOLE\tMILK", "whole milk")]
        [InlineData("Vollmilch", "vollmilch")]
        public void Normalize_LowercasesTrimsAndCollapsesWhitespace(string raw, string expected)
        {
            Assert.Equal(expected, ProductName.Normalize(raw));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Normalize_EmptyOrWhitespace_ReturnsEmpty(string? raw)
        {
            Assert.Equal(string.Empty, ProductName.Normalize(raw!));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductNameTests"`
Expected: FAIL — `ProductName` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Application/Frigorino.Domain/Products/ProductName.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Frigorino.Domain.Products
{
    // Normalization v1: lowercase + trim + collapse internal whitespace. Deliberately no
    // stemming / plural-stripping / article-stripping (language-dependent, bilingual en/de).
    public static class ProductName
    {
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return Whitespace.Replace(raw.Trim().ToLowerInvariant(), " ");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductNameTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Products/ProductName.cs Application/Frigorino.Test/Domain/ProductNameTests.cs
git commit -m "feat: add ProductName.Normalize (normalization v1)"
```

---

## Task 3: `Product` aggregate

**Files:**
- Create: `Application/Frigorino.Domain/Entities/Product.cs`
- Test: `Application/Frigorino.Test/Domain/ProductAggregateTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Domain/ProductAggregateTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    public class ProductAggregateTests
    {
        private const int HouseholdId = 42;

        private static ProductClassification AiClassification(int days) =>
            new(ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value);

        [Fact]
        public void Create_Valid_SetsColumnsAndVersion()
        {
            var result = Product.Create(HouseholdId, "milk", AiClassification(7), classifierVersion: 1);

            Assert.True(result.IsSuccess);
            var product = result.Value;
            Assert.Equal(HouseholdId, product.HouseholdId);
            Assert.Equal("milk", product.NormalizedName);
            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product.ClassificationExpiryHandling);
            Assert.Equal(7, product.ClassificationShelfLifeDays);
            Assert.Equal(1, product.ClassifierVersion);
        }

        [Fact]
        public void Create_EmptyNormalizedName_Fails()
        {
            var result = Product.Create(HouseholdId, "  ", AiClassification(7), 1);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void Create_InvalidHousehold_Fails()
        {
            var result = Product.Create(0, "milk", AiClassification(7), 1);

            Assert.True(result.IsFailed);
        }

        [Fact]
        public void ApplyClassification_OverwritesLayerAndVersion()
        {
            var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;

            product.ApplyClassification(new ProductClassification(ExpiryProfile.NonPerishable), classifierVersion: 2);

            Assert.Equal(ExpiryHandling.NonPerishable, product.ClassificationExpiryHandling);
            Assert.Null(product.ClassificationShelfLifeDays);
            Assert.Equal(2, product.ClassifierVersion);
        }

        [Fact]
        public void EffectiveExpiry_ReconstructsProfileFromColumns()
        {
            var product = Product.Create(HouseholdId, "milk", AiClassification(7), 1).Value;

            var effective = product.EffectiveExpiry;

            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, effective.Handling);
            Assert.Equal(7, effective.ShelfLifeDays);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductAggregateTests"`
Expected: FAIL — `Product` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Application/Frigorino.Domain/Entities/Product.cs`:

```csharp
using FluentResults;
using Frigorino.Domain.Products;

namespace Frigorino.Domain.Entities
{
    // Per-household product catalog row, keyed (HouseholdId, NormalizedName). Holds the AI
    // Classification layer as flat columns. A user Override layer is a future additive set of
    // nullable columns; EffectiveExpiry will become Override ?? Classification then.
    public class Product
    {
        public const int NormalizedNameMaxLength = 200;

        public int Id { get; set; }
        public int HouseholdId { get; set; }
        public string NormalizedName { get; set; } = string.Empty;

        // AI Classification layer (overwritten wholesale on (re)classification).
        public ExpiryHandling ClassificationExpiryHandling { get; set; }
        public int? ClassificationShelfLifeDays { get; set; }
        public int ClassifierVersion { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation: cascade-deleted with the household (configured FK-only, no Household nav).
        public Household Household { get; set; } = null!;

        // NormalizedName is expected pre-normalized by the caller (ProductName.Normalize).
        public static Result<Product> Create(
            int householdId, string normalizedName, ProductClassification classification, int classifierVersion)
        {
            var errors = new List<IError>();
            if (householdId <= 0)
            {
                errors.Add(new Error("Household id is required.")
                    .WithMetadata("Property", nameof(HouseholdId)));
            }
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                errors.Add(new Error("Normalized name is required.")
                    .WithMetadata("Property", nameof(NormalizedName)));
            }
            else if (normalizedName.Length > NormalizedNameMaxLength)
            {
                errors.Add(new Error($"Normalized name must be {NormalizedNameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(NormalizedName)));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<Product>(errors);
            }

            var product = new Product
            {
                HouseholdId = householdId,
                NormalizedName = normalizedName,
            };
            product.ApplyClassification(classification, classifierVersion);
            return Result.Ok(product);
        }

        // Overwrites the AI layer wholesale and re-stamps the version. UpdatedAt is auto-stamped
        // by ApplicationDbContext.SaveChangesAsync.
        public void ApplyClassification(ProductClassification classification, int classifierVersion)
        {
            ClassificationExpiryHandling = classification.Expiry.Handling;
            ClassificationShelfLifeDays = classification.Expiry.ShelfLifeDays;
            ClassifierVersion = classifierVersion;
        }

        // Effective expiry the rest of the app reads. Minimal today (Classification only); becomes
        // Override ?? Classification when override columns land. Safe .Value — columns are written
        // through a validated ExpiryProfile.
        public ExpiryProfile EffectiveExpiry =>
            ExpiryProfile.Create(ClassificationExpiryHandling, ClassificationShelfLifeDays).Value;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductAggregateTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Product.cs Application/Frigorino.Test/Domain/ProductAggregateTests.cs
git commit -m "feat: add Product catalog aggregate"
```

---

## Task 4: Domain ports

**Files:**
- Create: `Application/Frigorino.Domain/Interfaces/IItemClassifier.cs`
- Create: `Application/Frigorino.Domain/Interfaces/IClassifyProductJob.cs`
- Create: `Application/Frigorino.Domain/Interfaces/IProductClassificationTrigger.cs`

No unit test — these are interface declarations; the build is the check.

- [ ] **Step 1: Write the interfaces**

Create `Application/Frigorino.Domain/Interfaces/IItemClassifier.cs`:

```csharp
using FluentResults;
using Frigorino.Domain.Products;

namespace Frigorino.Domain.Interfaces
{
    // The ONLY AI abstraction. The OpenAI SDK never crosses this boundary into Domain/Features.
    public interface IItemClassifier
    {
        // Returns the classification for an already-normalized product name. Transient/API errors
        // return Result.Fail (the job drops the work item — lossy by design); a model refusal is
        // mapped to NonPerishable by the adapter, not surfaced as a failure.
        Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct);

        // Stamped onto Product.ClassifierVersion; bumped when the prompt/model changes to force
        // re-classification on the next reference.
        int Version { get; }
    }
}
```

Create `Application/Frigorino.Domain/Interfaces/IClassifyProductJob.cs`:

```csharp
namespace Frigorino.Domain.Interfaces
{
    // The unit of work enqueued onto the background runner. Resolved in a fresh DI scope.
    public interface IClassifyProductJob
    {
        Task Run(int householdId, string rawName, CancellationToken ct);
    }
}
```

Create `Application/Frigorino.Domain/Interfaces/IProductClassificationTrigger.cs`:

```csharp
namespace Frigorino.Domain.Interfaces
{
    // Called by the list-item slices when a product name is referenced. The enabled implementation
    // enqueues the classify job; the disabled implementation is a no-op. This seam is the localized
    // swap point if classification later moves to domain events.
    public interface IProductClassificationTrigger
    {
        void OnProductReferenced(int householdId, string rawName);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Domain`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Domain/Interfaces/IItemClassifier.cs Application/Frigorino.Domain/Interfaces/IClassifyProductJob.cs Application/Frigorino.Domain/Interfaces/IProductClassificationTrigger.cs
git commit -m "feat: add classifier, job, and trigger domain ports"
```

---

## Task 5: Product persistence (DbSet + timestamps + EF config)

**Files:**
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ProductConfiguration.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ProductPersistenceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Application/Frigorino.Test/Infrastructure/ProductPersistenceTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class ProductPersistenceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task SaveChanges_StampsTimestampsOnNewProduct()
        {
            using var db = NewContext();
            var classification = new ProductClassification(
                ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 7).Value);
            var product = Product.Create(42, "milk", classification, 1).Value;

            db.Products.Add(product);
            await db.SaveChangesAsync();

            Assert.NotEqual(default, product.CreatedAt);
            Assert.NotEqual(default, product.UpdatedAt);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductPersistenceTests"`
Expected: FAIL — `db.Products` does not exist (compile error).

- [ ] **Step 3: Add the DbSet and timestamp stamping**

In `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`, add the `DbSet` after the `InventoryItems` one (line 20):

```csharp
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Product> Products { get; set; }
```

In the same file, inside `SaveChangesAsync`, add a `Product` branch to the `Added` block (after the `InventoryItem` Added branch, ~line 86):

```csharp
                    if (entry.Entity is Product product && product.CreatedAt == default)
                    {
                        product.CreatedAt = now;
                        product.UpdatedAt = now;
                    }
```

…and to the `Modified` block (after the `InventoryItem` Modified branch, ~line 113):

```csharp
                    if (entry.Entity is Product product)
                    {
                        product.UpdatedAt = now;
                    }
```

- [ ] **Step 4: Add the EF configuration**

Create `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ProductConfiguration.cs`:

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Id)
                .ValueGeneratedOnAdd();

            builder.Property(p => p.HouseholdId)
                .IsRequired();

            builder.Property(p => p.NormalizedName)
                .HasMaxLength(Product.NormalizedNameMaxLength)
                .IsRequired();

            builder.Property(p => p.ClassificationExpiryHandling)
                .IsRequired();

            builder.Property(p => p.ClassificationShelfLifeDays);

            builder.Property(p => p.ClassifierVersion)
                .IsRequired();

            builder.Property(p => p.CreatedAt)
                .IsRequired();

            builder.Property(p => p.UpdatedAt)
                .IsRequired();

            // One catalog row per (household, normalized name) — the point-lookup key and the
            // arbiter of the concurrent-insert race.
            builder.HasIndex(p => new { p.HouseholdId, p.NormalizedName })
                .IsUnique();

            // FK-only relationship (no navigation added to Household) — cascade with the household.
            builder.HasOne(p => p.Household)
                .WithMany()
                .HasForeignKey(p => p.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => p.HouseholdId);
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductPersistenceTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework Application/Frigorino.Test/Infrastructure/ProductPersistenceTests.cs
git commit -m "feat: persist Product catalog (DbSet, timestamps, EF config)"
```

---

## Task 6: `ClassifyProductJob`

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/ClassifyProductJob.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs`:

```csharp
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class ClassifyProductJobTests
    {
        private const int HouseholdId = 42;

        // Deterministic classifier: returns a fixed result, records the calls it received.
        private sealed class FakeClassifier : IItemClassifier
        {
            private readonly Result<ProductClassification> _result;
            public int Version { get; }
            public int Calls { get; private set; }

            public FakeClassifier(Result<ProductClassification> result, int version)
            {
                _result = result;
                Version = version;
            }

            public Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
            {
                Calls++;
                return Task.FromResult(_result);
            }
        }

        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static Result<ProductClassification> AiResult(int days) =>
            Result.Ok(new ProductClassification(
                ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value));

        [Fact]
        public async Task Run_NewName_ClassifiesAndInserts()
        {
            var dbName = Guid.NewGuid().ToString();
            var classifier = new FakeClassifier(AiResult(7), version: 1);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "  Milk  ", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var product = await verify.Products.SingleAsync();
            Assert.Equal("milk", product.NormalizedName);
            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product.ClassificationExpiryHandling);
            Assert.Equal(7, product.ClassificationShelfLifeDays);
            Assert.Equal(1, product.ClassifierVersion);
            Assert.Equal(1, classifier.Calls);
        }

        [Fact]
        public async Task Run_AlreadyClassifiedAtCurrentVersion_SkipsClassifier()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var seed = NewContext(dbName))
            {
                seed.Products.Add(Product.Create(HouseholdId, "milk", AiResult(7).Value, 1).Value);
                await seed.SaveChangesAsync();
            }

            var classifier = new FakeClassifier(AiResult(7), version: 1);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "milk", CancellationToken.None);
            }

            Assert.Equal(0, classifier.Calls);
        }

        [Fact]
        public async Task Run_StaleVersion_ReclassifiesAndUpdates()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var seed = NewContext(dbName))
            {
                seed.Products.Add(Product.Create(HouseholdId, "milk", AiResult(7).Value, 1).Value);
                await seed.SaveChangesAsync();
            }

            var classifier = new FakeClassifier(
                Result.Ok(new ProductClassification(ExpiryProfile.NonPerishable)), version: 2);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "milk", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var product = await verify.Products.SingleAsync();
            Assert.Equal(ExpiryHandling.NonPerishable, product.ClassificationExpiryHandling);
            Assert.Equal(2, product.ClassifierVersion);
            Assert.Equal(1, classifier.Calls);
        }

        [Fact]
        public async Task Run_ClassifierFails_WritesNothing()
        {
            var dbName = Guid.NewGuid().ToString();
            var classifier = new FakeClassifier(
                Result.Fail<ProductClassification>("transient"), version: 1);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "milk", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            Assert.Equal(0, await verify.Products.CountAsync());
        }

        [Fact]
        public async Task Run_EmptyName_NoOp()
        {
            var dbName = Guid.NewGuid().ToString();
            var classifier = new FakeClassifier(AiResult(7), version: 1);
            using (var db = NewContext(dbName))
            {
                var job = new ClassifyProductJob(db, classifier, NullLogger<ClassifyProductJob>.Instance);
                await job.Run(HouseholdId, "   ", CancellationToken.None);
            }

            Assert.Equal(0, classifier.Calls);
            using var verify = NewContext(dbName);
            Assert.Equal(0, await verify.Products.CountAsync());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ClassifyProductJobTests"`
Expected: FAIL — `ClassifyProductJob` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Application/Frigorino.Infrastructure/Services/ClassifyProductJob.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Idempotent, cache-aware classify job. Runs in a fresh DI scope created by the background
    // runner. Lossy by design: any failure drops the work item; the next reference re-triggers it.
    public class ClassifyProductJob : IClassifyProductJob
    {
        private readonly ApplicationDbContext _db;
        private readonly IItemClassifier _classifier;
        private readonly ILogger<ClassifyProductJob> _logger;

        public ClassifyProductJob(
            ApplicationDbContext db, IItemClassifier classifier, ILogger<ClassifyProductJob> logger)
        {
            _db = db;
            _classifier = classifier;
            _logger = logger;
        }

        public async Task Run(int householdId, string rawName, CancellationToken ct)
        {
            var normalized = ProductName.Normalize(rawName);
            if (normalized.Length == 0)
            {
                return;
            }

            var existing = await _db.Products
                .FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.NormalizedName == normalized, ct);

            if (existing is not null && existing.ClassifierVersion >= _classifier.Version)
            {
                // Cache hit — already classified at the current version.
                return;
            }

            var result = await _classifier.ClassifyAsync(normalized, ct);
            if (result.IsFailed)
            {
                _logger.LogWarning(
                    "Classification failed for product '{NormalizedName}' (household {HouseholdId}); dropping.",
                    normalized, householdId);
                return;
            }

            if (existing is null)
            {
                var created = Product.Create(householdId, normalized, result.Value, _classifier.Version);
                if (created.IsFailed)
                {
                    return;
                }
                _db.Products.Add(created.Value);
            }
            else
            {
                existing.ApplyClassification(result.Value, _classifier.Version);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Benign unique-index race: another work item classified the same new name first.
                // The work is done; nothing more to do.
                _logger.LogDebug(
                    "Concurrent insert race for product '{NormalizedName}' (household {HouseholdId}); ignoring.",
                    normalized, householdId);
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ClassifyProductJobTests"`
Expected: PASS (5 tests).

> Note: the `DbUpdateException` catch is defensive — the InMemory provider does not enforce the unique index, so it is not exercised here. It is verified by code review and the Postgres integration path (Task 11).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/ClassifyProductJob.cs Application/Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs
git commit -m "feat: add ClassifyProductJob (cache-aware upsert)"
```

---

## Task 7: Classification triggers (Queueing + Null)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/ProductClassificationTriggers.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ProductClassificationTriggerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Infrastructure/ProductClassificationTriggerTests.cs`:

```csharp
using FakeItEasy;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class ProductClassificationTriggerTests
    {
        [Fact]
        public void Null_OnProductReferenced_DoesNotEnqueue()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var trigger = new NullProductClassificationTrigger();

            trigger.OnProductReferenced(42, "Milk");

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public void Queueing_OnProductReferenced_Enqueues()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var trigger = new QueueingProductClassificationTrigger(queue);

            trigger.OnProductReferenced(42, "Milk");

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustHaveHappenedOnceExactly();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductClassificationTriggerTests"`
Expected: FAIL — trigger types do not exist.

- [ ] **Step 3: Write the implementation**

Create `Application/Frigorino.Infrastructure/Services/ProductClassificationTriggers.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    // Enabled path: enqueue the classify job onto the Cycle 1 runner. The lambda resolves the job
    // from the fresh per-work-item scope the consumer creates.
    public class QueueingProductClassificationTrigger : IProductClassificationTrigger
    {
        private readonly IBackgroundTaskQueue _queue;

        public QueueingProductClassificationTrigger(IBackgroundTaskQueue queue)
        {
            _queue = queue;
        }

        public void OnProductReferenced(int householdId, string rawName)
        {
            _queue.TryEnqueue((sp, ct) =>
                sp.GetRequiredService<IClassifyProductJob>().Run(householdId, rawName, ct));
        }
    }

    // Disabled path: classification is off (no key configured). Do nothing.
    public class NullProductClassificationTrigger : IProductClassificationTrigger
    {
        public void OnProductReferenced(int householdId, string rawName)
        {
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ProductClassificationTriggerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/ProductClassificationTriggers.cs Application/Frigorino.Test/Infrastructure/ProductClassificationTriggerTests.cs
git commit -m "feat: add queueing and null classification triggers"
```

---

## Task 8: OpenAI adapter + DI extension

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj`
- Create: `Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs`
- Create: `Application/Frigorino.Infrastructure/Services/ItemClassificationDependencyInjection.cs`

No unit test — the adapter is the vendor boundary (tested via the integration stub, not the real API). The build is the check.

- [ ] **Step 1: Add the OpenAI package**

In `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj`, add inside the existing `<ItemGroup>` of `PackageReference`s:

```xml
    <PackageReference Include="OpenAI" Version="2.8.0" />
```

> Verify 2.8.0 is the current stable at implementation time (`dotnet add` will report newer). Pin exactly (no `*`/range), per the dependency rule.

- [ ] **Step 2: Restore to update the lock file**

Run: `dotnet restore Application/Frigorino.sln`
Expected: restore succeeds; `Frigorino.Infrastructure/packages.lock.json` updated.

- [ ] **Step 3: Write the OpenAI adapter**

Create `Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs`:

```csharp
using System.Text.Json;
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    // Vendor boundary. Uses the official OpenAI SDK directly with strict Structured Outputs.
    // Swapping vendor later = rewrite this one class behind the unchanged IItemClassifier port.
    public class OpenAiItemClassifier : IItemClassifier
    {
        // Bump when the prompt or schema changes to force re-classification on the next reference.
        public int Version => 1;

        private static readonly BinaryData Schema = BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "expiryHandling": {
                        "type": "string",
                        "enum": ["NonPerishable", "UserEntersFromPackage", "AiRecommendsShelfLife"]
                    },
                    "defaultShelfLifeDays": {
                        "type": ["integer", "null"],
                        "minimum": 1,
                        "maximum": 365
                    }
                },
                "required": ["expiryHandling", "defaultShelfLifeDays"],
                "additionalProperties": false
            }
            """u8.ToArray());

        private const string SystemPrompt =
            "You classify how a grocery/household product expires. Choose exactly one expiryHandling:\n" +
            "- NonPerishable: effectively never expires (e.g. salt/Salz, sugar/Zucker, dish soap/Spülmittel). defaultShelfLifeDays = null.\n" +
            "- UserEntersFromPackage: perishable with a printed date the user should read (e.g. yogurt/Joghurt, packaged meat/abgepacktes Fleisch). defaultShelfLifeDays = null.\n" +
            "- AiRecommendsShelfLife: perishable with a predictable typical shelf life you can estimate in days (e.g. fresh milk/Frischmilch ~7, bananas/Bananen ~5, lettuce/Salat ~4). defaultShelfLifeDays = that estimate, 1..365.\n" +
            "Respond only via the provided JSON schema.";

        private readonly ChatClient _client;
        private readonly ILogger<OpenAiItemClassifier> _logger;

        public OpenAiItemClassifier(ChatClient client, ILogger<OpenAiItemClassifier> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "product_classification",
                    jsonSchema: Schema,
                    jsonSchemaIsStrict: true),
            };

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(normalizedName),
            };

            try
            {
                var completion = await _client.CompleteChatAsync(messages, options, ct);

                // Refusal or empty content → treat as non-perishable rather than failing the job.
                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning(
                        "Classifier returned no usable content for '{Name}'; defaulting to non-perishable.",
                        normalizedName);
                    return Result.Ok(new ProductClassification(ExpiryProfile.NonPerishable));
                }

                using var json = JsonDocument.Parse(completion.Value.Content[0].Text);
                var root = json.RootElement;

                var handling = Enum.Parse<ExpiryHandling>(root.GetProperty("expiryHandling").GetString()!);
                int? days = root.GetProperty("defaultShelfLifeDays").ValueKind == JsonValueKind.Null
                    ? null
                    : root.GetProperty("defaultShelfLifeDays").GetInt32();

                var profile = ExpiryProfile.Create(handling, days);
                if (profile.IsFailed)
                {
                    // Model produced a schema-valid-but-semantically-inconsistent combination; be safe.
                    return Result.Ok(new ProductClassification(ExpiryProfile.NonPerishable));
                }

                return Result.Ok(new ProductClassification(profile.Value));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Classifier call failed for '{Name}'.", normalizedName);
                return Result.Fail<ProductClassification>("Classifier call failed.");
            }
        }
    }
}
```

- [ ] **Step 4: Write the DI extension**

Create `Application/Frigorino.Infrastructure/Services/ItemClassificationDependencyInjection.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    public static class ItemClassificationDependencyInjection
    {
        public static IServiceCollection AddItemClassification(
            this IServiceCollection services, IConfiguration configuration)
        {
            // The job is always registered — it is resolved only when something is enqueued, which
            // only the enabled (queueing) trigger does.
            services.AddScoped<IClassifyProductJob, ClassifyProductJob>();

            var enabled = configuration.GetValue<bool>("Classifier:Enabled");
            var apiKey = configuration["Classifier:ApiKey"];
            var model = configuration["Classifier:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-4.1-nano";
            }

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddSingleton(new ChatClient(model: model, apiKey: apiKey));
                services.AddScoped<IItemClassifier, OpenAiItemClassifier>();
                services.AddScoped<IProductClassificationTrigger, QueueingProductClassificationTrigger>();
            }
            else
            {
                // No key configured: classification is a no-op (nothing enqueued, nothing written).
                services.AddScoped<IProductClassificationTrigger, NullProductClassificationTrigger>();
            }

            return services;
        }
    }
}
```

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj Application/Frigorino.Infrastructure/packages.lock.json Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs Application/Frigorino.Infrastructure/Services/ItemClassificationDependencyInjection.cs
git commit -m "feat: add OpenAI item classifier adapter and DI extension"
```

---

## Task 9: Wire DI, config, and slice triggers

**Files:**
- Modify: `Application/Frigorino.Web/Program.cs`
- Modify: `Application/Frigorino.Web/appsettings.json`
- Modify: `Application/Frigorino.Features/Lists/Items/CreateItem.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`

No new unit tests — covered end-to-end by Task 11. The build is the check here.

- [ ] **Step 1: Register the DI extension**

In `Application/Frigorino.Web/Program.cs`, immediately after `builder.Services.AddBackgroundTaskQueue();` (line 59):

```csharp
builder.Services.AddBackgroundTaskQueue();
builder.Services.AddItemClassification(builder.Configuration);
builder.Services.AddMaintenanceServices();
```

(`Frigorino.Infrastructure.Services` is already imported in `Program.cs`, so no new `using`.)

- [ ] **Step 2: Add config placeholders**

In `Application/Frigorino.Web/appsettings.json`, add a top-level `Classifier` section (vendor-neutral keys; real values via user-secrets / env / Railway):

```json
  "Classifier": {
    "Enabled": false,
    "ApiKey": "",
    "Model": "gpt-4.1-nano"
  }
```

- [ ] **Step 3: Trigger from `CreateItem`**

In `Application/Frigorino.Features/Lists/Items/CreateItem.cs`, add the trigger parameter to `Handle` (after `ApplicationDbContext db`):

```csharp
            CreateItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IProductClassificationTrigger classificationTrigger,
            CancellationToken ct)
```

…and call it after `await db.SaveChangesAsync(ct);` (before building the response):

```csharp
            await db.SaveChangesAsync(ct);

            classificationTrigger.OnProductReferenced(householdId, request.Text);

            var response = ListItemResponse.From(result.Value);
```

(`Frigorino.Domain.Interfaces` is already imported in `CreateItem.cs`.)

- [ ] **Step 4: Trigger from `UpdateItem`**

In `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`, add the same parameter to `Handle` (after `ApplicationDbContext db`):

```csharp
            UpdateItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IProductClassificationTrigger classificationTrigger,
            CancellationToken ct)
```

…and call it after `await db.SaveChangesAsync(ct);`, only when a text value was supplied (a quantity/status-only edit must not re-classify; redundant fires on unchanged text are cache-safe — the job skips them):

```csharp
            await db.SaveChangesAsync(ct);

            if (request.Text is not null)
            {
                classificationTrigger.OnProductReferenced(householdId, request.Text);
            }

            return TypedResults.Ok(ListItemResponse.From(result.Value));
```

(`Frigorino.Domain.Interfaces` is already imported in `UpdateItem.cs`.)

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/Program.cs Application/Frigorino.Web/appsettings.json Application/Frigorino.Features/Lists/Items/CreateItem.cs Application/Frigorino.Features/Lists/Items/UpdateItem.cs
git commit -m "feat: wire classification DI, config, and list-item triggers"
```

---

## Task 10: EF migration

**Files:**
- Create (generated): `Application/Frigorino.Infrastructure/Migrations/<timestamp>_AddProductCatalog.cs`

- [ ] **Step 1: Add the migration**

Run:
```bash
dotnet ef migrations add AddProductCatalog --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: a new migration + an updated `ApplicationDbContextModelSnapshot.cs`.

- [ ] **Step 2: Inspect the generated migration**

Open the generated `<timestamp>_AddProductCatalog.cs`. Verify it:
- Creates a `Products` table with `Id`, `HouseholdId`, `NormalizedName`, `ClassificationExpiryHandling`, `ClassificationShelfLifeDays` (nullable), `ClassifierVersion`, `CreatedAt`, `UpdatedAt`.
- Adds a **unique** index on `(HouseholdId, NormalizedName)` and a non-unique index on `HouseholdId`.
- Adds an FK to `Households` with `onDelete: Cascade`.
- Touches **no other table** (`ListItem` etc. unchanged).

If anything is missing, fix the EF config (Task 5) and regenerate (`dotnet ef migrations remove ...` then re-add).

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Infrastructure/Migrations
git commit -m "feat: add AddProductCatalog EF migration"
```

---

## Task 11: Integration test (API scenario, real Postgres)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Infrastructure/StubItemClassifier.cs`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/Classification.Api.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/ClassificationApiSteps.cs`

This proves the wiring the unit tests can't: slice → trigger → queue → job → DB, on real Postgres.

- [ ] **Step 1: Add a deterministic stub classifier**

Create `Application/Frigorino.IntegrationTests/Infrastructure/StubItemClassifier.cs`:

```csharp
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;

namespace Frigorino.IntegrationTests.Infrastructure;

// Deterministic, network-free classifier for integration tests: "milk"/"milch" → Ai-recommended
// 7-day shelf life; everything else → non-perishable.
public sealed class StubItemClassifier : IItemClassifier
{
    public int Version => 1;

    public Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
    {
        var profile = normalizedName.Contains("milk") || normalizedName.Contains("milch")
            ? ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 7).Value
            : ExpiryProfile.NonPerishable;

        return Task.FromResult(Result.Ok(new ProductClassification(profile)));
    }
}
```

- [ ] **Step 2: Enable classification with the stub in the test host**

In `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`:

Add the two settings inside `ConfigureWebHost`, next to the existing `UseSetting` call (~line 27):

```csharp
        builder.UseSetting("ConnectionStrings:Database", ConnectionString);
        builder.UseSetting("Classifier:Enabled", "true");
        builder.UseSetting("Classifier:ApiKey", "integration-test-stub-key");
```

Add the stub registration at the end of the existing `builder.ConfigureServices(services => { ... })` block (after the `HttpsRedirectionOptions` line):

```csharp
            // Replace the real OpenAI classifier with a deterministic, network-free stub. The
            // QueueingProductClassificationTrigger is registered (Classifier:Enabled=true above), so
            // the full slice→trigger→queue→job→DB path runs without any network call.
            services.RemoveAll<IItemClassifier>();
            services.AddScoped<IItemClassifier, StubItemClassifier>();
```

Add the required usings at the top of the file:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
```

> `RemoveAll<T>` lives in `Microsoft.Extensions.DependencyInjection.Extensions`. The unused
> `ChatClient` singleton (built with the stub key) is never resolved, so it never makes a call.
> If `Frigorino.IntegrationTests.Infrastructure` is already the file's namespace, drop that using.

- [ ] **Step 3: Write the feature**

Create `Application/Frigorino.IntegrationTests/Slices/Lists/Classification.Api.feature`:

```gherkin
Feature: Product Classification API

  Background:
    Given I am logged in with an active household

  Scenario: Adding a perishable list item classifies the product
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Milk" to "Weekly Groceries" via the API
    Then the product catalog eventually contains "milk" with AI-recommended shelf life 7

  Scenario: Adding a non-perishable list item classifies it as non-perishable
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Salt" to "Weekly Groceries" via the API
    Then the product catalog eventually contains "salt" as non-perishable
```

> The `Given I am logged in with an active household` and `Given there is a list named "..."` steps
> already exist (see `ListSteps`/`HouseholdSteps` and `ctx.ListIds`). Only the two new `When`/`Then`
> steps below are added.

- [ ] **Step 4: Write the new step bindings**

Create `Application/Frigorino.IntegrationTests/Slices/Lists/ClassificationApiSteps.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.IntegrationTests.Infrastructure;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ClassificationApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I POST an item with text {string} to {string} via the API")]
    public async Task WhenIPostAnItemWithTextViaTheApi(string text, string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryCreateListItemAsync(listId, text);
        Assert.Equal(201, ctx.LastApiResponse.Status);
    }

    [Then("the product catalog eventually contains {string} with AI-recommended shelf life {int}")]
    public async Task ThenCatalogContainsWithShelfLife(string normalizedName, int days)
    {
        var product = await PollForProductAsync(normalizedName);
        Assert.NotNull(product);
        Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, product!.ClassificationExpiryHandling);
        Assert.Equal(days, product.ClassificationShelfLifeDays);
    }

    [Then("the product catalog eventually contains {string} as non-perishable")]
    public async Task ThenCatalogContainsNonPerishable(string normalizedName)
    {
        var product = await PollForProductAsync(normalizedName);
        Assert.NotNull(product);
        Assert.Equal(ExpiryHandling.NonPerishable, product!.ClassificationExpiryHandling);
    }

    // Classification is fire-and-forget; poll the catalog (real Postgres) until the row appears.
    private async Task<Product?> PollForProductAsync(string normalizedName)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.HouseholdId == ctx.HouseholdId && p.NormalizedName == normalizedName);
            if (product is not null)
            {
                return product;
            }
            await Task.Delay(100);
        }
        return null;
    }
}
```

> If `api.TryCreateListItemAsync` is not the exact existing method name/signature, mirror its use in
> `ListItemApiSteps.cs` (it is called there as `api.TryCreateListItemAsync(listId, "")`).

- [ ] **Step 5: Run the new scenarios**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Classification"`
Expected: PASS (2 scenarios). Requires Docker running (Testcontainers). If the daemon is
unreachable, ask the user to start Docker Desktop.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.IntegrationTests
git commit -m "test: add product classification integration scenarios"
```

---

## Final verification gate

- [ ] **Run the full solution test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: `Frigorino.Test` all green (existing + new), `Frigorino.IntegrationTests` all green. The
undo-toast IT is known-flaky — re-run once if it times out before suspecting a regression.

- [ ] **Build the Docker image (drift check)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: image builds. (No project added, so the Dockerfile should not need changes — but build to
confirm.)

- [ ] **Confirm no frontend churn**

No endpoint/DTO changed, so `ClientApp/src/lib/openapi.json` and the generated client must be
unchanged. Run: `git status` — expect no changes under `Application/Frigorino.Web/ClientApp/`.

---

## Self-Review (completed during planning)

- **Spec coverage:** `ExpiryProfile`+invariant (T1) · `ProductClassification` composite (T1) ·
  `ProductName.Normalize` (T2) · `Product` aggregate + `EffectiveExpiry` (T3) · three ports (T4) ·
  persistence/unique-index/cascade (T5) · cache-aware job + race catch (T6) · queueing/null triggers
  (T7) · OpenAI adapter + strict structured output + refusal fallback + graceful no-op DI (T8) ·
  Program/config/slice triggers, list-items-only, text-changed (T9) · migration (T10) ·
  integration wiring + async poll (T11). No denorm on `ListItem`; no override columns; no inventory
  trigger; no frontend — all honored.
- **Placeholders:** none — every code step has complete code. The two "verify exact name" notes
  (OpenAI package version; `TryCreateListItemAsync`) point at concrete, existing references.
- **Type consistency:** `IItemClassifier.ClassifyAsync`/`Version`, `IClassifyProductJob.Run`,
  `IProductClassificationTrigger.OnProductReferenced`, `Product.Create`/`ApplyClassification`/
  `EffectiveExpiry`, `ProductClassification.Expiry`, `ExpiryProfile.Create`/`SuggestedExpiry`/
  `NonPerishable` are used identically across tasks.
```

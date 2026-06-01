# User, Household & Inventory Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up storage + read/write seams for three separate preference scopes (user, household, inventory), each anchored on one real setting, with the household anchor replacing the hard-coded 30-day purge constant.

**Architecture:** One typed flat table per scope (`UserSettings`, `HouseholdSettings`, `InventorySettings`), lazy-created on first write, defaults returned on read-miss. Domain entities carry validating methods + default constants (mirroring `Household.NameMaxLength`). Settings are sibling entities (not folded into parent aggregates); writes are gated in the slice via the existing membership/role primitives. Two vertical slices per scope. Frontend: a dedicated user-settings page off the user menu, and settings cards on the household-manage and inventory-edit pages.

**Tech Stack:** .NET 10, EF Core (Postgres), FluentResults, ASP.NET minimal-API vertical slices, xUnit + EF InMemory, React 19 + TanStack Query/Router + MUI, hey-api generated client.

**Spec:** `docs/superpowers/specs/2026-06-01-user-household-settings-design.md`

---

## File Structure

**Domain (`Frigorino.Domain/Entities/`):**
- Create `UserSettings.cs`, `HouseholdSettings.cs`, `InventorySettings.cs`
- Modify `HouseholdRoleExtensions.cs` (add `CanManageSettings`)
- Modify `Inventory.cs` (extract `CanBeManagedBy` predicate, reuse in `Update`/`SoftDelete`)

**Infrastructure (`Frigorino.Infrastructure/`):**
- Create `EntityFramework/Configurations/{UserSettings,HouseholdSettings,InventorySettings}Configuration.cs`
- Modify `EntityFramework/ApplicationDbContext.cs` (3 DbSets + timestamp stamping)
- Create `Tasks/CheckedItemPurge.cs` (pure selector + candidate record)
- Modify `Tasks/DeleteInactiveItems.cs` (per-household retention)
- New EF migration `AddSettingsTables`

**Features (`Frigorino.Features/`):**
- Create `Me/Settings/{UserSettingsResponse,GetUserSettings,UpdateUserSettings}.cs`
- Create `Households/Settings/{HouseholdSettingsResponse,GetHouseholdSettings,UpdateHouseholdSettings}.cs`
- Create `Inventories/Settings/{InventorySettingsResponse,GetInventorySettings,UpdateInventorySettings}.cs`

**Web (`Frigorino.Web/`):**
- Modify `Program.cs` (wire 6 endpoints into `me` group + 2 new groups)

**Tests (`Frigorino.Test/`):**
- Create `Domain/{UserSettings,HouseholdSettings,InventorySettings}Tests.cs`
- Create `Domain/HouseholdRoleExtensionsTests.cs` (or extend existing aggregate test)
- Create `Infrastructure/CheckedItemPurgeTests.cs`

**Frontend (`ClientApp/src/`):**
- Create `features/settings/{useUserSettings,useUpdateUserSettings,useApplyPersistedLanguage}.ts` + `pages/UserSettingsPage.tsx`
- Create `routes/settings/index.tsx`
- Modify `components/layout/Navigation.tsx` (add Settings item, remove `LanguageSwitcher`)
- Delete `components/common/LanguageSwitcher.tsx`
- Modify `routes/__root.tsx` (call boot-time language hook)
- Create `features/households/{useHouseholdSettings,useUpdateHouseholdSettings}.ts` + `components/HouseholdSettingsCard.tsx`; modify `pages/ManageHouseholdPage.tsx`
- Create `features/inventories/{useInventorySettings,useUpdateInventorySettings}.ts` + `components/InventorySettingsCard.tsx`; modify `pages/InventoryEditPage.tsx`
- Modify `public/locales/{en,de}/translation.json` (settings keys)

---

## Phase A — Domain

### Task 1: `UserSettings` entity

**Files:**
- Create: `Application/Frigorino.Domain/Entities/UserSettings.cs`
- Test: `Application/Frigorino.Test/Domain/UserSettingsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class UserSettingsTests
    {
        private const string UserId = "user-1";

        [Fact]
        public void Create_SetsUserId_AndNullLanguage()
        {
            var settings = UserSettings.Create(UserId);

            Assert.Equal(UserId, settings.UserId);
            Assert.Null(settings.Language);
        }

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        public void SetLanguage_Supported_Succeeds(string lang)
        {
            var settings = UserSettings.Create(UserId);

            var result = settings.SetLanguage(lang);

            Assert.True(result.IsSuccess);
            Assert.Equal(lang, settings.Language);
        }

        [Fact]
        public void SetLanguage_Null_Succeeds_AndClears()
        {
            var settings = UserSettings.Create(UserId);
            settings.SetLanguage("de");

            var result = settings.SetLanguage(null);

            Assert.True(result.IsSuccess);
            Assert.Null(settings.Language);
        }

        [Fact]
        public void SetLanguage_Unsupported_Fails_WithLanguageProperty()
        {
            var settings = UserSettings.Create(UserId);

            var result = settings.SetLanguage("fr");

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(UserSettings.Language));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~UserSettingsTests"`
Expected: FAIL — `UserSettings` does not exist.

- [ ] **Step 3: Write the entity**

```csharp
using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class UserSettings
    {
        // Languages with a translation bundle under ClientApp/public/locales. Single source
        // of truth for both SetLanguage validation and the read-side default.
        public static readonly string[] SupportedLanguages = ["en", "de"];

        public string UserId { get; set; } = string.Empty;

        // null = no explicit choice; the client falls back to browser language detection.
        public string? Language { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public User User { get; set; } = null!;

        public static UserSettings Create(string userId)
        {
            return new UserSettings { UserId = userId };
        }

        // The "Property" metadata key duplicates Frigorino.Features.Results.ResultExtensions.PropertyMetadataKey
        // by convention — Domain stays free of a Features dependency.
        public Result SetLanguage(string? language)
        {
            if (language is not null && !SupportedLanguages.Contains(language))
            {
                return Result.Fail(new Error($"Language must be one of: {string.Join(", ", SupportedLanguages)}.")
                    .WithMetadata("Property", nameof(Language)));
            }

            Language = language;
            return Result.Ok();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~UserSettingsTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/UserSettings.cs Application/Frigorino.Test/Domain/UserSettingsTests.cs
git commit -m "feat: add UserSettings domain entity with language validation"
```

---

### Task 2: `HouseholdSettings` entity + `CanManageSettings`

**Files:**
- Create: `Application/Frigorino.Domain/Entities/HouseholdSettings.cs`
- Modify: `Application/Frigorino.Domain/Entities/HouseholdRoleExtensions.cs`
- Test: `Application/Frigorino.Test/Domain/HouseholdSettingsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class HouseholdSettingsTests
    {
        private const int HouseholdId = 7;

        [Fact]
        public void Create_DefaultsRetentionToDefaultConstant()
        {
            var settings = HouseholdSettings.Create(HouseholdId);

            Assert.Equal(HouseholdId, settings.HouseholdId);
            Assert.Equal(HouseholdSettings.DefaultCheckedItemRetentionDays, settings.CheckedItemRetentionDays);
        }

        [Theory]
        [InlineData(HouseholdSettings.MinRetentionDays)]
        [InlineData(30)]
        [InlineData(HouseholdSettings.MaxRetentionDays)]
        public void SetCheckedItemRetentionDays_InBounds_Succeeds(int days)
        {
            var settings = HouseholdSettings.Create(HouseholdId);

            var result = settings.SetCheckedItemRetentionDays(days);

            Assert.True(result.IsSuccess);
            Assert.Equal(days, settings.CheckedItemRetentionDays);
        }

        [Theory]
        [InlineData(HouseholdSettings.MinRetentionDays - 1)]
        [InlineData(HouseholdSettings.MaxRetentionDays + 1)]
        public void SetCheckedItemRetentionDays_OutOfBounds_Fails(int days)
        {
            var settings = HouseholdSettings.Create(HouseholdId);

            var result = settings.SetCheckedItemRetentionDays(days);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(HouseholdSettings.CheckedItemRetentionDays));
        }

        [Theory]
        [InlineData(HouseholdRole.Owner, true)]
        [InlineData(HouseholdRole.Admin, true)]
        [InlineData(HouseholdRole.Member, false)]
        public void CanManageSettings_MatchesRolePolicy(HouseholdRole role, bool expected)
        {
            Assert.Equal(expected, role.CanManageSettings());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~HouseholdSettingsTests"`
Expected: FAIL — `HouseholdSettings` and `CanManageSettings` do not exist.

- [ ] **Step 3: Write the entity**

```csharp
using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class HouseholdSettings
    {
        public const int DefaultCheckedItemRetentionDays = 30;
        public const int MinRetentionDays = 1;
        public const int MaxRetentionDays = 365;

        public int HouseholdId { get; set; }
        public int CheckedItemRetentionDays { get; set; } = DefaultCheckedItemRetentionDays;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Household Household { get; set; } = null!;

        public static HouseholdSettings Create(int householdId)
        {
            return new HouseholdSettings { HouseholdId = householdId };
        }

        public Result SetCheckedItemRetentionDays(int days)
        {
            if (days < MinRetentionDays || days > MaxRetentionDays)
            {
                return Result.Fail(new Error($"Retention must be between {MinRetentionDays} and {MaxRetentionDays} days.")
                    .WithMetadata("Property", nameof(CheckedItemRetentionDays)));
            }

            CheckedItemRetentionDays = days;
            return Result.Ok();
        }
    }
}
```

- [ ] **Step 4: Add `CanManageSettings` to `HouseholdRoleExtensions.cs`**

Insert after the `CanManageMembers` method (around line 11):

```csharp
        // Owner/Admin may edit household-wide settings; Members may only read them.
        public static bool CanManageSettings(this HouseholdRole role)
        {
            return role >= HouseholdRole.Admin;
        }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~HouseholdSettingsTests"`
Expected: PASS (8 tests).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Entities/HouseholdSettings.cs Application/Frigorino.Domain/Entities/HouseholdRoleExtensions.cs Application/Frigorino.Test/Domain/HouseholdSettingsTests.cs
git commit -m "feat: add HouseholdSettings entity and CanManageSettings role policy"
```

---

### Task 3: `InventorySettings` entity + `Inventory.CanBeManagedBy`

**Files:**
- Create: `Application/Frigorino.Domain/Entities/InventorySettings.cs`
- Modify: `Application/Frigorino.Domain/Entities/Inventory.cs`
- Test: `Application/Frigorino.Test/Domain/InventorySettingsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class InventorySettingsTests
    {
        private const int InventoryId = 3;
        private const string CreatorId = "user-creator";

        [Fact]
        public void Create_DefaultsLeadDaysToNull()
        {
            var settings = InventorySettings.Create(InventoryId);

            Assert.Equal(InventoryId, settings.InventoryId);
            Assert.Null(settings.ExpiryLeadDays);
        }

        [Fact]
        public void SetExpiryLeadDays_Null_Succeeds_Inherit()
        {
            var settings = InventorySettings.Create(InventoryId);
            settings.SetExpiryLeadDays(5);

            var result = settings.SetExpiryLeadDays(null);

            Assert.True(result.IsSuccess);
            Assert.Null(settings.ExpiryLeadDays);
        }

        [Theory]
        [InlineData(InventorySettings.MinExpiryLeadDays)]
        [InlineData(InventorySettings.MaxExpiryLeadDays)]
        public void SetExpiryLeadDays_InBounds_Succeeds(int days)
        {
            var settings = InventorySettings.Create(InventoryId);

            var result = settings.SetExpiryLeadDays(days);

            Assert.True(result.IsSuccess);
            Assert.Equal(days, settings.ExpiryLeadDays);
        }

        [Theory]
        [InlineData(InventorySettings.MinExpiryLeadDays - 1)]
        [InlineData(InventorySettings.MaxExpiryLeadDays + 1)]
        public void SetExpiryLeadDays_OutOfBounds_Fails(int days)
        {
            var settings = InventorySettings.Create(InventoryId);

            var result = settings.SetExpiryLeadDays(days);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(InventorySettings.ExpiryLeadDays));
        }

        [Theory]
        [InlineData(CreatorId, HouseholdRole.Member, true)]   // creator
        [InlineData("other", HouseholdRole.Admin, true)]      // admin
        [InlineData("other", HouseholdRole.Owner, true)]      // owner
        [InlineData("other", HouseholdRole.Member, false)]    // non-creator member
        public void CanBeManagedBy_MatchesPolicy(string callerId, HouseholdRole role, bool expected)
        {
            var inventory = Inventory.Create("Pantry", null, 1, CreatorId).Value;

            Assert.Equal(expected, inventory.CanBeManagedBy(callerId, role));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventorySettingsTests"`
Expected: FAIL — `InventorySettings` and `CanBeManagedBy` do not exist.

- [ ] **Step 3: Write the entity**

```csharp
using FluentResults;

namespace Frigorino.Domain.Entities
{
    public class InventorySettings
    {
        public const int MinExpiryLeadDays = 0;
        public const int MaxExpiryLeadDays = 365;

        public int InventoryId { get; set; }

        // null = inherit the user-level default (resolved by the notification feature).
        public int? ExpiryLeadDays { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Inventory Inventory { get; set; } = null!;

        public static InventorySettings Create(int inventoryId)
        {
            return new InventorySettings { InventoryId = inventoryId };
        }

        public Result SetExpiryLeadDays(int? days)
        {
            if (days is not null && (days < MinExpiryLeadDays || days > MaxExpiryLeadDays))
            {
                return Result.Fail(new Error($"Lead time must be between {MinExpiryLeadDays} and {MaxExpiryLeadDays} days.")
                    .WithMetadata("Property", nameof(ExpiryLeadDays)));
            }

            ExpiryLeadDays = days;
            return Result.Ok();
        }
    }
}
```

- [ ] **Step 4: Extract `CanBeManagedBy` in `Inventory.cs`**

Add this public predicate to `Inventory` (e.g. directly above the `Update` method at line ~86):

```csharp
        // Edit permission for the inventory and anything owned by it (settings, items metadata):
        // the creator, or an Admin+. Single home for the policy so Update/SoftDelete and the
        // settings slice share one gate.
        public bool CanBeManagedBy(string callerUserId, HouseholdRole callerRole)
        {
            return CreatedByUserId == callerUserId || callerRole >= HouseholdRole.Admin;
        }
```

Then replace the inline guard in `Update` (currently `if (CreatedByUserId != callerUserId && callerRole < HouseholdRole.Admin)`):

```csharp
            if (!CanBeManagedBy(callerUserId, callerRole))
            {
                return Result.Fail(
                    new AccessDeniedError("Only the inventory creator or an admin can edit this inventory."));
            }
```

And the identical guard in `SoftDelete`:

```csharp
            if (!CanBeManagedBy(callerUserId, callerRole))
            {
                return Result.Fail(
                    new AccessDeniedError("Only the inventory creator or an admin can delete this inventory."));
            }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventorySettingsTests|FullyQualifiedName~InventoryAggregateTests"`
Expected: PASS — new tests pass AND existing `InventoryAggregateTests` still pass (refactor preserved behaviour).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Entities/InventorySettings.cs Application/Frigorino.Domain/Entities/Inventory.cs Application/Frigorino.Test/Domain/InventorySettingsTests.cs
git commit -m "feat: add InventorySettings entity and reusable Inventory.CanBeManagedBy"
```

---

## Phase B — Infrastructure (EF + migration)

### Task 4: EF configurations, DbSets, timestamp stamping, migration

**Files:**
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/UserSettingsConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/HouseholdSettingsConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/InventorySettingsConfiguration.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`

- [ ] **Step 1: Create `UserSettingsConfiguration.cs`**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
    {
        public void Configure(EntityTypeBuilder<UserSettings> builder)
        {
            builder.HasKey(s => s.UserId);

            builder.Property(s => s.UserId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(s => s.Language)
                .HasMaxLength(8);

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();

            // 1:1 with User, no navigation on the principal side. Cascade so deleting a user
            // removes their settings row.
            builder.HasOne(s => s.User)
                .WithOne()
                .HasForeignKey<UserSettings>(s => s.UserId)
                .HasPrincipalKey<User>(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 2: Create `HouseholdSettingsConfiguration.cs`**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class HouseholdSettingsConfiguration : IEntityTypeConfiguration<HouseholdSettings>
    {
        public void Configure(EntityTypeBuilder<HouseholdSettings> builder)
        {
            builder.HasKey(s => s.HouseholdId);

            builder.Property(s => s.CheckedItemRetentionDays)
                .IsRequired()
                .HasDefaultValue(HouseholdSettings.DefaultCheckedItemRetentionDays);

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();

            builder.HasOne(s => s.Household)
                .WithOne()
                .HasForeignKey<HouseholdSettings>(s => s.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 3: Create `InventorySettingsConfiguration.cs`**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class InventorySettingsConfiguration : IEntityTypeConfiguration<InventorySettings>
    {
        public void Configure(EntityTypeBuilder<InventorySettings> builder)
        {
            builder.HasKey(s => s.InventoryId);

            builder.Property(s => s.ExpiryLeadDays);

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();

            builder.HasOne(s => s.Inventory)
                .WithOne()
                .HasForeignKey<InventorySettings>(s => s.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 4: Add DbSets + timestamp stamping in `ApplicationDbContext.cs`**

Add three DbSets after line 21 (`public DbSet<Product> Products`):

```csharp
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<HouseholdSettings> HouseholdSettings { get; set; }
        public DbSet<InventorySettings> InventorySettings { get; set; }
```

In `SaveChangesAsync`, inside the `EntityState.Added` block (after the `Product` block, ~line 93):

```csharp
                    if (entry.Entity is UserSettings userSettings && userSettings.CreatedAt == default)
                    {
                        userSettings.CreatedAt = now;
                        userSettings.UpdatedAt = now;
                    }

                    if (entry.Entity is HouseholdSettings householdSettings && householdSettings.CreatedAt == default)
                    {
                        householdSettings.CreatedAt = now;
                        householdSettings.UpdatedAt = now;
                    }

                    if (entry.Entity is InventorySettings inventorySettings && inventorySettings.CreatedAt == default)
                    {
                        inventorySettings.CreatedAt = now;
                        inventorySettings.UpdatedAt = now;
                    }
```

And inside the `EntityState.Modified` block (after the `Product` block, ~line 125):

```csharp
                    if (entry.Entity is UserSettings userSettings)
                    {
                        userSettings.UpdatedAt = now;
                    }

                    if (entry.Entity is HouseholdSettings householdSettings)
                    {
                        householdSettings.UpdatedAt = now;
                    }

                    if (entry.Entity is InventorySettings inventorySettings)
                    {
                        inventorySettings.UpdatedAt = now;
                    }
```

- [ ] **Step 5: Build to verify the model compiles**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: Build succeeded.

- [ ] **Step 6: Generate the migration**

Run: `dotnet ef migrations add AddSettingsTables --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web`
Expected: migration files created under `Application/Frigorino.Infrastructure/Migrations/`. Open the generated `*_AddSettingsTables.cs` and confirm it creates `UserSettings`, `HouseholdSettings`, `InventorySettings` tables with the cascade FKs and the `CheckedItemRetentionDays` default of 30. No other table changes.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework Application/Frigorino.Infrastructure/Migrations
git commit -m "feat: EF configuration + migration for settings tables"
```

---

## Phase C — Backend slices

### Task 5: User settings slices

**Files:**
- Create: `Application/Frigorino.Features/Me/Settings/UserSettingsResponse.cs`
- Create: `Application/Frigorino.Features/Me/Settings/GetUserSettings.cs`
- Create: `Application/Frigorino.Features/Me/Settings/UpdateUserSettings.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Create `UserSettingsResponse.cs`**

```csharp
namespace Frigorino.Features.Me.Settings
{
    public sealed record UserSettingsResponse(string? Language);
}
```

- [ ] **Step 2: Create `GetUserSettings.cs`**

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Me.Settings
{
    public static class GetUserSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapGetUserSettings(this IEndpointRouteBuilder app)
        {
            app.MapGet("/settings", Handle)
               .WithName("GetUserSettings")
               .Produces<UserSettingsResponse>();
            return app;
        }

        private static async Task<Ok<UserSettingsResponse>> Handle(
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var response = await db.UserSettings
                .Where(s => s.UserId == currentUser.UserId)
                .Select(s => new UserSettingsResponse(s.Language))
                .FirstOrDefaultAsync(ct);

            return TypedResults.Ok(response ?? new UserSettingsResponse(null));
        }
    }
}
```

- [ ] **Step 3: Create `UpdateUserSettings.cs`**

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Me.Settings
{
    public sealed record UpdateUserSettingsRequest(string? Language);

    public static class UpdateUserSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateUserSettings(this IEndpointRouteBuilder app)
        {
            app.MapPut("/settings", Handle)
               .WithName("UpdateUserSettings")
               .Produces<UserSettingsResponse>()
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<UserSettingsResponse>, ValidationProblem>> Handle(
            UpdateUserSettingsRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var settings = await db.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == currentUser.UserId, ct);

            if (settings is null)
            {
                settings = UserSettings.Create(currentUser.UserId);
                db.UserSettings.Add(settings);
            }

            var result = settings.SetLanguage(request.Language);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new UserSettingsResponse(settings.Language));
        }
    }
}
```

- [ ] **Step 4: Wire into the `me` group in `Program.cs`**

After line 339 (`me.MapSetActiveHousehold();`) add:

```csharp
me.MapGetUserSettings();
me.MapUpdateUserSettings();
```

Add the using at the top of `Program.cs` near the other feature usings (after `using Frigorino.Features.Me.ActiveHousehold;`):

```csharp
using Frigorino.Features.Me.Settings;
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded. (This also regenerates `ClientApp/src/lib/openapi.json` — that's expected; it's regenerated properly in Task 9.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Me/Settings Application/Frigorino.Web/Program.cs
git commit -m "feat: GET/PUT /api/me/settings slices (user language)"
```

---

### Task 6: Household settings slices

**Files:**
- Create: `Application/Frigorino.Features/Households/Settings/HouseholdSettingsResponse.cs`
- Create: `Application/Frigorino.Features/Households/Settings/GetHouseholdSettings.cs`
- Create: `Application/Frigorino.Features/Households/Settings/UpdateHouseholdSettings.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Create `HouseholdSettingsResponse.cs`**

```csharp
namespace Frigorino.Features.Households.Settings
{
    public sealed record HouseholdSettingsResponse(int CheckedItemRetentionDays);
}
```

- [ ] **Step 2: Create `GetHouseholdSettings.cs`**

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Settings
{
    public static class GetHouseholdSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapGetHouseholdSettings(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetHouseholdSettings")
               .Produces<HouseholdSettingsResponse>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<HouseholdSettingsResponse>, NotFound>> Handle(
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

            var response = await db.HouseholdSettings
                .Where(s => s.HouseholdId == householdId)
                .Select(s => new HouseholdSettingsResponse(s.CheckedItemRetentionDays))
                .FirstOrDefaultAsync(ct);

            return TypedResults.Ok(response
                ?? new HouseholdSettingsResponse(HouseholdSettings.DefaultCheckedItemRetentionDays));
        }
    }
}
```

- [ ] **Step 3: Create `UpdateHouseholdSettings.cs`**

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households.Settings
{
    public sealed record UpdateHouseholdSettingsRequest(int CheckedItemRetentionDays);

    public static class UpdateHouseholdSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateHouseholdSettings(this IEndpointRouteBuilder app)
        {
            app.MapPut("", Handle)
               .WithName("UpdateHouseholdSettings")
               .Produces<HouseholdSettingsResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<HouseholdSettingsResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            UpdateHouseholdSettingsRequest request,
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

            var settings = await db.HouseholdSettings
                .FirstOrDefaultAsync(s => s.HouseholdId == householdId, ct);

            if (settings is null)
            {
                settings = HouseholdSettings.Create(householdId);
                db.HouseholdSettings.Add(settings);
            }

            var result = settings.SetCheckedItemRetentionDays(request.CheckedItemRetentionDays);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new HouseholdSettingsResponse(settings.CheckedItemRetentionDays));
        }
    }
}
```

- [ ] **Step 4: Wire a new group in `Program.cs`**

After the `members` group block (after line 291) add:

```csharp
var householdSettings = app.MapGroup("/api/household/{householdId:int}/settings")
    .RequireAuthorization()
    .WithTags("HouseholdSettings");
householdSettings.MapGetHouseholdSettings();
householdSettings.MapUpdateHouseholdSettings();
```

Add the using near the other feature usings:

```csharp
using Frigorino.Features.Households.Settings;
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Households/Settings Application/Frigorino.Web/Program.cs
git commit -m "feat: GET/PUT /api/household/{id}/settings slices (checked-item retention)"
```

---

### Task 7: Inventory settings slices

**Files:**
- Create: `Application/Frigorino.Features/Inventories/Settings/InventorySettingsResponse.cs`
- Create: `Application/Frigorino.Features/Inventories/Settings/GetInventorySettings.cs`
- Create: `Application/Frigorino.Features/Inventories/Settings/UpdateInventorySettings.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Create `InventorySettingsResponse.cs`**

```csharp
namespace Frigorino.Features.Inventories.Settings
{
    public sealed record InventorySettingsResponse(int? ExpiryLeadDays);
}
```

- [ ] **Step 2: Create `GetInventorySettings.cs`**

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Settings
{
    public static class GetInventorySettingsEndpoint
    {
        public static IEndpointRouteBuilder MapGetInventorySettings(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetInventorySettings")
               .Produces<InventorySettingsResponse>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<InventorySettingsResponse>, NotFound>> Handle(
            int householdId,
            int inventoryId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var inventoryExists = await db.Inventories
                .AnyAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (!inventoryExists)
            {
                return TypedResults.NotFound();
            }

            var response = await db.InventorySettings
                .Where(s => s.InventoryId == inventoryId)
                .Select(s => new InventorySettingsResponse(s.ExpiryLeadDays))
                .FirstOrDefaultAsync(ct);

            return TypedResults.Ok(response ?? new InventorySettingsResponse(null));
        }
    }
}
```

- [ ] **Step 3: Create `UpdateInventorySettings.cs`**

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
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Settings
{
    public sealed record UpdateInventorySettingsRequest(int? ExpiryLeadDays);

    public static class UpdateInventorySettingsEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateInventorySettings(this IEndpointRouteBuilder app)
        {
            app.MapPut("", Handle)
               .WithName("UpdateInventorySettings")
               .Produces<InventorySettingsResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<InventorySettingsResponse>, NotFound, ForbidHttpResult, ValidationProblem>> Handle(
            int householdId,
            int inventoryId,
            UpdateInventorySettingsRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var inventory = await db.Inventories
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            if (!inventory.CanBeManagedBy(currentUser.UserId, membership.Role))
            {
                return TypedResults.Forbid();
            }

            var settings = await db.InventorySettings
                .FirstOrDefaultAsync(s => s.InventoryId == inventoryId, ct);

            if (settings is null)
            {
                settings = InventorySettings.Create(inventoryId);
                db.InventorySettings.Add(settings);
            }

            var result = settings.SetExpiryLeadDays(request.ExpiryLeadDays);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new InventorySettingsResponse(settings.ExpiryLeadDays));
        }
    }
}
```

- [ ] **Step 4: Wire a new group in `Program.cs`**

After the `inventoryItems` group block (after line 333) add:

```csharp
var inventorySettings = app.MapGroup("/api/household/{householdId:int}/inventories/{inventoryId:int}/settings")
    .RequireAuthorization()
    .WithTags("InventorySettings");
inventorySettings.MapGetInventorySettings();
inventorySettings.MapUpdateInventorySettings();
```

Add the using near the other feature usings:

```csharp
using Frigorino.Features.Inventories.Settings;
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Inventories/Settings Application/Frigorino.Web/Program.cs
git commit -m "feat: GET/PUT inventory settings slices (expiry lead-days override)"
```

---

## Phase D — Per-household purge

### Task 8: Make checked-item purge per-household

**Files:**
- Create: `Application/Frigorino.Infrastructure/Tasks/CheckedItemPurge.cs`
- Modify: `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`
- Test: `Application/Frigorino.Test/Infrastructure/CheckedItemPurgeTests.cs`

- [ ] **Step 1: Write the failing test (pure selector)**

```csharp
using Frigorino.Infrastructure.Tasks;

namespace Frigorino.Test.Infrastructure
{
    public class CheckedItemPurgeTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        private const int DefaultDays = 30;

        [Fact]
        public void UsesHouseholdRetention_WhenPresent()
        {
            var candidates = new[]
            {
                new CheckedItemCandidate(1, householdId: 10, updatedAt: Now.AddDays(-8)),  // > 7-day retention => purge
                new CheckedItemCandidate(2, householdId: 10, updatedAt: Now.AddDays(-3)),  // < 7-day retention => keep
            };
            var retention = new Dictionary<int, int> { [10] = 7 };

            var ids = CheckedItemPurge.SelectExpiredItemIds(candidates, retention, Now, DefaultDays);

            Assert.Equal(new[] { 1 }, ids);
        }

        [Fact]
        public void FallsBackToDefault_WhenHouseholdHasNoRow()
        {
            var candidates = new[]
            {
                new CheckedItemCandidate(1, householdId: 99, updatedAt: Now.AddDays(-31)), // > default 30 => purge
                new CheckedItemCandidate(2, householdId: 99, updatedAt: Now.AddDays(-10)), // < default 30 => keep
            };
            var retention = new Dictionary<int, int>();

            var ids = CheckedItemPurge.SelectExpiredItemIds(candidates, retention, Now, DefaultDays);

            Assert.Equal(new[] { 1 }, ids);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CheckedItemPurgeTests"`
Expected: FAIL — `CheckedItemPurge` / `CheckedItemCandidate` do not exist.

- [ ] **Step 3: Create `CheckedItemPurge.cs`**

```csharp
namespace Frigorino.Infrastructure.Tasks
{
    // A checked list item considered for retention-based purge, tagged with the household it
    // belongs to (resolved via ListItem -> List -> HouseholdId).
    public sealed record CheckedItemCandidate(int ItemId, int HouseholdId, DateTime UpdatedAt);

    // Pure retention decision: which checked items have aged past their household's retention
    // window. Kept free of EF so it is unit-testable without a database (the InMemory provider
    // does not support ExecuteDelete).
    public static class CheckedItemPurge
    {
        public static List<int> SelectExpiredItemIds(
            IReadOnlyCollection<CheckedItemCandidate> candidates,
            IReadOnlyDictionary<int, int> retentionByHousehold,
            DateTime now,
            int defaultRetentionDays)
        {
            var expired = new List<int>();
            foreach (var candidate in candidates)
            {
                var days = retentionByHousehold.TryGetValue(candidate.HouseholdId, out var d)
                    ? d
                    : defaultRetentionDays;

                if (candidate.UpdatedAt < now.AddDays(-days))
                {
                    expired.Add(candidate.ItemId);
                }
            }

            return expired;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CheckedItemPurgeTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Rewrite `DeleteInactiveItems.Run` to use it**

Replace the body of `Run` (lines 16-26) with:

```csharp
        public async Task Run(CancellationToken cancellationToken = default)
        {
            await _dbContext.Households.Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.Inventories.Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.Lists.Where(li => !li.IsActive).ExecuteDeleteAsync(cancellationToken);

            // Soft-deleted list items: purge unconditionally.
            await _dbContext.ListItems.Where(li => !li.IsActive).ExecuteDeleteAsync(cancellationToken);

            // Checked-off list items: purge past each household's retention window (default 30).
            var retention = await _dbContext.HouseholdSettings
                .ToDictionaryAsync(s => s.HouseholdId, s => s.CheckedItemRetentionDays, cancellationToken);

            var candidates = await _dbContext.ListItems
                .Where(li => li.Status)
                .Select(li => new CheckedItemCandidate(li.Id, li.List.HouseholdId, li.UpdatedAt))
                .ToListAsync(cancellationToken);

            var expiredIds = CheckedItemPurge.SelectExpiredItemIds(
                candidates, retention, DateTime.UtcNow, HouseholdSettings.DefaultCheckedItemRetentionDays);

            if (expiredIds.Count > 0)
            {
                await _dbContext.ListItems
                    .Where(li => expiredIds.Contains(li.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await _dbContext.InventoryItems.Where(h => !h.IsActive).ExecuteDeleteAsync(cancellationToken);
        }
```

Add `using Frigorino.Domain.Entities;` at the top of `DeleteInactiveItems.cs` (for `HouseholdSettings.DefaultCheckedItemRetentionDays`). `Microsoft.EntityFrameworkCore` is already imported.

> Note: the projection uses `li.List.HouseholdId` via the `ListItem.List` navigation. If `ListItem` has no `List` navigation property, join explicitly:
> `from li in _dbContext.ListItems where li.Status join l in _dbContext.Lists on li.ListId equals l.Id select new CheckedItemCandidate(li.Id, l.HouseholdId, li.UpdatedAt)`.

- [ ] **Step 6: Build + run the purge test once more**

Run: `dotnet build Application/Frigorino.Infrastructure && dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CheckedItemPurgeTests"`
Expected: Build succeeded; tests PASS.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Infrastructure/Tasks Application/Frigorino.Test/Infrastructure/CheckedItemPurgeTests.cs
git commit -m "feat: per-household checked-item retention in DeleteInactiveItems"
```

---

## Phase E — Regenerate the API client

### Task 9: Regenerate the TS client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/openapi.json`, `src/lib/api/**`

- [ ] **Step 1: Regenerate**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run api`
Expected: backend rebuilds, `openapi.json` is emitted, and `src/lib/api/**` regenerates. New helpers exist: `getUserSettingsOptions`, `updateUserSettingsMutation`, `getUserSettingsQueryKey`, `getHouseholdSettingsOptions`, `updateHouseholdSettingsMutation`, `getHouseholdSettingsQueryKey`, `getInventorySettingsOptions`, `updateInventorySettingsMutation`, `getInventorySettingsQueryKey`.

- [ ] **Step 2: Verify the new helpers are present**

Run (from `ClientApp/`): `grep -l "getUserSettingsOptions\|getHouseholdSettingsOptions\|getInventorySettingsOptions" src/lib/api/@tanstack/react-query.gen.ts`
Expected: the file path prints (all three names exist).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib
git commit -m "chore: regenerate API client with settings endpoints"
```

---

## Phase F — Frontend

### Task 10: User settings page + menu + boot-time language

**Files:**
- Create: `ClientApp/src/features/settings/useUserSettings.ts`
- Create: `ClientApp/src/features/settings/useUpdateUserSettings.ts`
- Create: `ClientApp/src/features/settings/useApplyPersistedLanguage.ts`
- Create: `ClientApp/src/features/settings/pages/UserSettingsPage.tsx`
- Create: `ClientApp/src/routes/settings/index.tsx`
- Modify: `ClientApp/src/components/layout/Navigation.tsx`
- Delete: `ClientApp/src/components/common/LanguageSwitcher.tsx`
- Modify: `ClientApp/src/routes/__root.tsx`
- Modify: `ClientApp/public/locales/en/translation.json`, `ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add translation keys**

In `public/locales/en/translation.json`, add a top-level `"settings"` block:

```json
    "settings": {
        "title": "Settings",
        "userSettings": "User Settings",
        "language": "Language",
        "languageHelp": "Choose your preferred language.",
        "languageSystemDefault": "Use device language",
        "householdSettings": "Household Settings",
        "checkedItemRetentionDays": "Keep checked items (days)",
        "checkedItemRetentionHelp": "Days to keep checked-off list items before they are removed.",
        "inventorySettings": "Inventory Settings",
        "expiryLeadOverride": "Override expiry reminder lead time",
        "expiryLeadDays": "Lead time (days)",
        "expiryLeadHelp": "Days before expiry to start reminders for this inventory.",
        "saved": "Settings saved",
        "saveFailed": "Could not save settings",
        "readOnlyHint": "Only owners and admins can change these."
    },
```

In `public/locales/de/translation.json`, add the matching block:

```json
    "settings": {
        "title": "Einstellungen",
        "userSettings": "Benutzereinstellungen",
        "language": "Sprache",
        "languageHelp": "Wähle deine bevorzugte Sprache.",
        "languageSystemDefault": "Gerätesprache verwenden",
        "householdSettings": "Haushaltseinstellungen",
        "checkedItemRetentionDays": "Erledigte Einträge behalten (Tage)",
        "checkedItemRetentionHelp": "Tage, die erledigte Listeneinträge aufbewahrt werden, bevor sie entfernt werden.",
        "inventorySettings": "Inventareinstellungen",
        "expiryLeadOverride": "Vorlaufzeit für Ablauf-Erinnerung überschreiben",
        "expiryLeadDays": "Vorlaufzeit (Tage)",
        "expiryLeadHelp": "Tage vor Ablauf, ab denen für dieses Inventar erinnert wird.",
        "saved": "Einstellungen gespeichert",
        "saveFailed": "Einstellungen konnten nicht gespeichert werden",
        "readOnlyHint": "Nur Eigentümer und Admins können dies ändern."
    },
```

- [ ] **Step 2: Create `useUserSettings.ts`**

```ts
import { useQuery } from "@tanstack/react-query";
import { getUserSettingsOptions } from "../../lib/api/@tanstack/react-query.gen";
import { useAuthStore } from "../../common/authProvider";

export const useUserSettings = () => {
    const { user } = useAuthStore();
    return useQuery({
        ...getUserSettingsOptions(),
        enabled: !!user,
        staleTime: 1000 * 60 * 5,
    });
};
```

- [ ] **Step 3: Create `useUpdateUserSettings.ts`**

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getUserSettingsQueryKey,
    updateUserSettingsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateUserSettings = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateUserSettingsMutation(),
        onSuccess: (data) => {
            queryClient.setQueryData(getUserSettingsQueryKey(), data);
        },
    });
};
```

- [ ] **Step 4: Create `useApplyPersistedLanguage.ts`**

```ts
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useUserSettings } from "./useUserSettings";

// Applies the server-persisted language once it loads, so the user's stored choice wins over
// browser detection. No-op when the user has no stored language (null) or it already matches.
export const useApplyPersistedLanguage = () => {
    const { i18n } = useTranslation();
    const { data } = useUserSettings();

    useEffect(() => {
        const lang = data?.language;
        if (lang && i18n.language !== lang) {
            void i18n.changeLanguage(lang);
        }
    }, [data?.language, i18n]);
};
```

- [ ] **Step 5: Create `pages/UserSettingsPage.tsx`**

```tsx
import {
    Card,
    CardContent,
    Container,
    MenuItem,
    TextField,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { pageContainerSx } from "../../../theme";
import { useUserSettings } from "../useUserSettings";
import { useUpdateUserSettings } from "../useUpdateUserSettings";

const LANGUAGES = [
    { code: "en", label: "English" },
    { code: "de", label: "Deutsch" },
];

export function UserSettingsPage() {
    const { t, i18n } = useTranslation();
    const { data, isLoading } = useUserSettings();
    const updateSettings = useUpdateUserSettings();

    const currentLanguage = data?.language ?? i18n.language;

    const handleLanguageChange = async (language: string) => {
        try {
            await updateSettings.mutateAsync({ body: { language } });
            await i18n.changeLanguage(language);
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    return (
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Typography variant="h5" sx={{ mb: { xs: 2, sm: 3 } }}>
                {t("settings.userSettings")}
            </Typography>

            <Card elevation={2}>
                <CardContent>
                    <TextField
                        select
                        fullWidth
                        size="small"
                        label={t("settings.language")}
                        helperText={t("settings.languageHelp")}
                        value={currentLanguage}
                        disabled={isLoading || updateSettings.isPending}
                        onChange={(e) => handleLanguageChange(e.target.value)}
                    >
                        {LANGUAGES.map((lang) => (
                            <MenuItem key={lang.code} value={lang.code}>
                                {lang.label}
                            </MenuItem>
                        ))}
                    </TextField>
                </CardContent>
            </Card>
        </Container>
    );
}
```

- [ ] **Step 6: Create the route `routes/settings/index.tsx`**

```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { UserSettingsPage } from "../../features/settings/pages/UserSettingsPage";

export const Route = createFileRoute("/settings/")({
    beforeLoad: requireAuth,
    component: UserSettingsPage,
});
```

- [ ] **Step 7: Update `Navigation.tsx` — add Settings item, remove `LanguageSwitcher`**

Remove the import (line 19): `import { LanguageSwitcher } from "../common/LanguageSwitcher";`
Remove the usage (line 53): `<LanguageSwitcher />`.
Add `Settings` to the icon import on line 1: `import { AccountCircle, Logout, Settings } from "@mui/icons-material";`
Add `Link` is already imported from `@tanstack/react-router`.
Add a Settings `MenuItem` inside the `Menu#user-menu`, immediately before the logout `MenuItem` (line 99):

```tsx
                                <MenuItem
                                    component={Link}
                                    to="/settings"
                                    onClick={handleMenuClose}
                                >
                                    <ListItemIcon>
                                        <Settings fontSize="small" />
                                    </ListItemIcon>
                                    <ListItemText
                                        primary={t("settings.title")}
                                    />
                                </MenuItem>
```

- [ ] **Step 8: Delete the now-unused `LanguageSwitcher.tsx`**

```bash
git rm Application/Frigorino.Web/ClientApp/src/components/common/LanguageSwitcher.tsx
```

- [ ] **Step 9: Apply persisted language at boot in `__root.tsx`**

In `routes/__root.tsx`, inside `RootComponent`, call the hook (add the import and the call at the top of the component body, before the `return`):

```tsx
import { useApplyPersistedLanguage } from "../features/settings/useApplyPersistedLanguage";
```

```tsx
function RootComponent() {
    useApplyPersistedLanguage();
    const { isAuthenticated } = useAuth();
    // ...rest unchanged
```

- [ ] **Step 10: Verify lint + types**

Run (from `ClientApp/`): `npm run lint && npm run tsc`
Expected: no errors. (`routeTree.gen.ts` regenerates automatically for the new `/settings/` route on the next `dev`/`build`; if `tsc` complains the route isn't in the tree, run `npm run build` once to regenerate, then re-run `tsc`.)

- [ ] **Step 11: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src Application/Frigorino.Web/ClientApp/public
git commit -m "feat: user settings page, menu entry, persisted language; remove header switcher"
```

---

### Task 11: Household settings card on Manage page

**Files:**
- Create: `ClientApp/src/features/households/useHouseholdSettings.ts`
- Create: `ClientApp/src/features/households/useUpdateHouseholdSettings.ts`
- Create: `ClientApp/src/features/households/components/HouseholdSettingsCard.tsx`
- Modify: `ClientApp/src/features/households/pages/ManageHouseholdPage.tsx`

- [ ] **Step 1: Create `useHouseholdSettings.ts`**

```ts
import { useQuery } from "@tanstack/react-query";
import { getHouseholdSettingsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useHouseholdSettings = (householdId: number, enabled = true) =>
    useQuery({
        ...getHouseholdSettingsOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 5,
    });
```

- [ ] **Step 2: Create `useUpdateHouseholdSettings.ts`**

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getHouseholdSettingsQueryKey,
    updateHouseholdSettingsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateHouseholdSettings = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateHouseholdSettingsMutation(),
        onSuccess: (data, variables) => {
            queryClient.setQueryData(
                getHouseholdSettingsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
                data,
            );
        },
    });
};
```

- [ ] **Step 3: Create `components/HouseholdSettingsCard.tsx`**

```tsx
import { Card, CardContent, TextField, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useHouseholdSettings } from "../useHouseholdSettings";
import { useUpdateHouseholdSettings } from "../useUpdateHouseholdSettings";

interface Props {
    householdId: number;
    canManage: boolean;
}

export function HouseholdSettingsCard({ householdId, canManage }: Props) {
    const { t } = useTranslation();
    const { data } = useHouseholdSettings(householdId);
    const updateSettings = useUpdateHouseholdSettings();
    const [value, setValue] = useState("");

    useEffect(() => {
        if (data) {
            setValue(String(data.checkedItemRetentionDays));
        }
    }, [data]);

    const commit = async () => {
        const days = Number(value);
        if (!Number.isInteger(days) || days < 1) {
            return;
        }
        if (data && days === data.checkedItemRetentionDays) {
            return;
        }
        try {
            await updateSettings.mutateAsync({
                path: { householdId },
                body: { checkedItemRetentionDays: days },
            });
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    return (
        <Card elevation={2} sx={{ mt: { xs: 2, sm: 3 } }}>
            <CardContent>
                <Typography variant="h6" sx={{ mb: 2 }}>
                    {t("settings.householdSettings")}
                </Typography>
                <TextField
                    type="number"
                    fullWidth
                    size="small"
                    label={t("settings.checkedItemRetentionDays")}
                    helperText={
                        canManage
                            ? t("settings.checkedItemRetentionHelp")
                            : t("settings.readOnlyHint")
                    }
                    value={value}
                    disabled={!canManage || updateSettings.isPending}
                    onChange={(e) => setValue(e.target.value)}
                    onBlur={commit}
                    slotProps={{ htmlInput: { min: 1, max: 365 } }}
                />
            </CardContent>
        </Card>
    );
}
```

- [ ] **Step 4: Render the card in `ManageHouseholdPage.tsx`**

Import at the top:

```tsx
import { HouseholdSettingsCard } from "../components/HouseholdSettingsCard";
import { roleRank, HouseholdRoleValue } from "../householdRole";
```

Compute `canManage` from the active role (the page already has `currentHousehold`):

```tsx
    const role = currentHousehold?.role;
    const canManageSettings =
        !!role && roleRank[role] >= roleRank[HouseholdRoleValue.Admin];
```

Render the card after the `MembersPanel` (within the same Container, before the `DeleteHouseholdDialog`):

```tsx
                <HouseholdSettingsCard
                    householdId={currentHousehold.householdId}
                    canManage={canManageSettings}
                />
```

- [ ] **Step 5: Verify lint + types**

Run (from `ClientApp/`): `npm run lint && npm run tsc`
Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/households
git commit -m "feat: household settings card (checked-item retention) on Manage page"
```

---

### Task 12: Inventory settings card on Inventory edit page

**Files:**
- Create: `ClientApp/src/features/inventories/useInventorySettings.ts`
- Create: `ClientApp/src/features/inventories/useUpdateInventorySettings.ts`
- Create: `ClientApp/src/features/inventories/components/InventorySettingsCard.tsx`
- Modify: `ClientApp/src/features/inventories/pages/InventoryEditPage.tsx`

- [ ] **Step 1: Create `useInventorySettings.ts`**

```ts
import { useQuery } from "@tanstack/react-query";
import { getInventorySettingsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useInventorySettings = (
    householdId: number,
    inventoryId: number,
    enabled = true,
) =>
    useQuery({
        ...getInventorySettingsOptions({ path: { householdId, inventoryId } }),
        enabled: enabled && householdId > 0 && inventoryId > 0,
        staleTime: 1000 * 60 * 5,
    });
```

- [ ] **Step 2: Create `useUpdateInventorySettings.ts`**

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getInventorySettingsQueryKey,
    updateInventorySettingsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateInventorySettings = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateInventorySettingsMutation(),
        onSuccess: (data, variables) => {
            queryClient.setQueryData(
                getInventorySettingsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        inventoryId: variables.path.inventoryId,
                    },
                }),
                data,
            );
        },
    });
};
```

- [ ] **Step 3: Create `components/InventorySettingsCard.tsx`**

```tsx
import {
    Card,
    CardContent,
    FormControlLabel,
    Switch,
    TextField,
    Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useInventorySettings } from "../useInventorySettings";
import { useUpdateInventorySettings } from "../useUpdateInventorySettings";

interface Props {
    householdId: number;
    inventoryId: number;
    canManage: boolean;
}

export function InventorySettingsCard({
    householdId,
    inventoryId,
    canManage,
}: Props) {
    const { t } = useTranslation();
    const { data } = useInventorySettings(householdId, inventoryId);
    const updateSettings = useUpdateInventorySettings();
    const [override, setOverride] = useState(false);
    const [value, setValue] = useState("7");

    useEffect(() => {
        if (data) {
            setOverride(data.expiryLeadDays !== null);
            if (data.expiryLeadDays !== null) {
                setValue(String(data.expiryLeadDays));
            }
        }
    }, [data]);

    const save = async (leadDays: number | null) => {
        try {
            await updateSettings.mutateAsync({
                path: { householdId, inventoryId },
                body: { expiryLeadDays: leadDays },
            });
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    const handleToggle = async (checked: boolean) => {
        setOverride(checked);
        await save(checked ? Number(value) : null);
    };

    const handleBlur = async () => {
        const days = Number(value);
        if (!override || !Number.isInteger(days) || days < 0) {
            return;
        }
        if (data && data.expiryLeadDays === days) {
            return;
        }
        await save(days);
    };

    return (
        <Card elevation={2} sx={{ mt: { xs: 2, sm: 3 } }}>
            <CardContent>
                <Typography variant="h6" sx={{ mb: 1 }}>
                    {t("settings.inventorySettings")}
                </Typography>
                <FormControlLabel
                    control={
                        <Switch
                            checked={override}
                            disabled={!canManage || updateSettings.isPending}
                            onChange={(e) => handleToggle(e.target.checked)}
                        />
                    }
                    label={t("settings.expiryLeadOverride")}
                />
                {override && (
                    <TextField
                        type="number"
                        fullWidth
                        size="small"
                        sx={{ mt: 1 }}
                        label={t("settings.expiryLeadDays")}
                        helperText={t("settings.expiryLeadHelp")}
                        value={value}
                        disabled={!canManage || updateSettings.isPending}
                        onChange={(e) => setValue(e.target.value)}
                        onBlur={handleBlur}
                        slotProps={{ htmlInput: { min: 0, max: 365 } }}
                    />
                )}
            </CardContent>
        </Card>
    );
}
```

- [ ] **Step 4: Render the card in `InventoryEditPage.tsx`**

Imports at the top:

```tsx
import { InventorySettingsCard } from "../components/InventorySettingsCard";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { roleRank, HouseholdRoleValue } from "../../households/householdRole";
import { useAuthStore } from "../../../common/authProvider";
```

Compute `canManage` (creator OR Admin+) in the component body:

```tsx
    const { currentHousehold } = useCurrentHouseholdWithDetails();
    const { user } = useAuthStore();
    const role = currentHousehold?.role;
    const isAdmin = !!role && roleRank[role] >= roleRank[HouseholdRoleValue.Admin];
    const canManageInventory =
        isAdmin || inventory.createdByUser.externalId === user?.uid;
```

Render the card after `<EditInventoryForm ... />` (before the `<Menu>`), guarding on a real id:

```tsx
            {inventory.id && (
                <InventorySettingsCard
                    householdId={householdId}
                    inventoryId={inventory.id}
                    canManage={canManageInventory}
                />
            )}
```

- [ ] **Step 5: Verify lint + types**

Run (from `ClientApp/`): `npm run lint && npm run tsc`
Expected: no errors. (Confirm `inventory.createdByUser.externalId` and `user?.uid` match the generated types / auth store; adjust the property access if the generated client names differ.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories
git commit -m "feat: inventory settings card (expiry lead-days override) on edit page"
```

---

## Phase G — Final verification

### Task 13: Full build, tests, frontend gates, Docker

- [ ] **Step 1: Backend — full solution build + tests**

Run: `dotnet build Application/Frigorino.sln`
Then: `dotnet test Application/Frigorino.sln`
Expected: Build succeeded; all tests pass (the new domain/purge tests plus the existing suite, including `Frigorino.IntegrationTests`). If a Testcontainers/Docker-daemon error appears, ask the user to start Docker Desktop rather than skipping.

- [ ] **Step 2: Frontend — lint, types, formatting**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run build && npm run lint && npm run prettier`
Expected: production build succeeds (regenerates `routeTree.gen.ts` with `/settings/`), lint clean, prettier writes no further changes (or run and commit the formatting).

- [ ] **Step 3: Docker image builds**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: image builds end-to-end (backend publish + SPA build copied to `wwwroot`). No new project was added, so no Dockerfile edit is expected — this is the drift check.

- [ ] **Step 4: Manual smoke (optional but recommended)**

Bring up the dev stack (`/dev-up`), then verify in the browser:
- User menu → **Settings** → change language to Deutsch → UI switches and persists across reload.
- Manage Household → **Household Settings** card → change retention as Admin (saves), and confirm a Member sees it read-only.
- Inventory edit → **Inventory Settings** card → toggle override, set lead days, save.
Tear down with `/dev-down` only if the user asks.

- [ ] **Step 5: Final commit (if formatting/routeTree changed)**

```bash
git add -A
git commit -m "chore: formatting + regenerated route tree for settings"
```

---

## Self-Review Notes

- **Spec coverage:** UserSettings (T1), HouseholdSettings + CanManageSettings (T2), InventorySettings + CanBeManagedBy (T3), EF/migration (T4), 6 slices (T5-T7), per-household purge with default-30 fallback (T8), client regen (T9), three UI surfaces + LanguageSwitcher removal + boot-time language (T10-T12), verification incl. Docker (T13). All spec sections map to a task.
- **Authz at the unit-tested layer:** 403/200 behaviour is determined by `CanManageSettings` (T2) and `CanBeManagedBy` (T3), both unit-tested; slices only dispatch. End-to-end authz is covered by the optional smoke (T13) / existing IT harness.
- **InMemory caveat:** the purge logic is unit-tested as a pure selector (T8) because EF InMemory cannot run `ExecuteDelete`.
- **Frontend property-name checks:** T11/T12 call out verifying `currentHousehold.role`, `inventory.createdByUser.externalId`, and `user?.uid` against the regenerated client / auth store, since those are the only spots depending on generated shapes.

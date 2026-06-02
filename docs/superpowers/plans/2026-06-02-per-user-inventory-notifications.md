# Per-User Inventory Expiry Notifications — Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single household-wide expiry *digest* with one notification **per inventory**, deep-linked to that inventory, where each user's subscription (mute + lead-time override) is **personal**.

**Architecture:** The notification unit becomes the *inventory*. Per-user preference lives in a new `UserInventoryNotificationSetting` aggregate (one row per user+inventory; absent row = subscribed, inherit global lead). The household-wide `InventorySettings` notification fields are removed (the entity + its GET/PUT endpoints are kept as an intentionally-empty placeholder for future household config). The de-dup ledger `NotificationDispatch` re-keys from `(UserId, HouseholdId, SentOn)` to `(UserId, InventoryId, SentOn)`. The planner groups candidates by inventory and emits one `DigestPlan` per (user, inventory); the composer deep-links to `/inventories/{inventoryId}/view`. New per-user GET/PUT slices back a repurposed (now personal) `InventorySettingsCard`.

**Tech Stack:** .NET 10 vertical slices (FluentResults, EF Core/Postgres), React 19 + TanStack Query + MUI, hey-api generated client, xUnit + FakeItEasy, Reqnroll + Playwright + Testcontainers.

**Branch status:** `feat/expiry-notifications`, NOT merged to stage and NOT deployed. The three notification migrations (`AddSettingsTables`, `AddExpiryNotificationPreferences`, `AddNotificationTables`) exist on this branch only and their tables are empty in every environment — so the schema may change freely. We add ONE new delta migration (do not hand-edit prior migrations or the model snapshot).

**Conventions to honor (from CLAUDE.md + project memory):**
- C# block-style braces always (even single-line `if`).
- Vertical slice = one file (request DTO + response DTO + endpoint registration + handler colocated). Domain rules in entity factories/aggregate methods returning `FluentResults.Result`. Slice dispatches errors via `result.ToValidationProblem()`.
- Reads = inline EF projection into the response DTO (no mapping libraries).
- Frontend hooks: never write `queryFn`/`mutationFn`/manual `queryKey`; spread generated `getXOptions`/`xMutation`/`getXQueryKey`. Mutation hooks arg-less; caller passes `{ path, body }`. Mirror canonical `features/lists/useList.ts` + `features/lists/useDeleteList.ts`.
- Tests assert on testids / `data-*` attributes, never translated text.
- API client regen: `npm run api` from `ClientApp/` (rebuilds backend, emits `openapi.json`, regenerates TS client). Frontend verify = `npm run tsc` + `npm run lint` + `npm run prettier`.
- Integration harness serves `ClientApp/build` — run `npm run build` after React edits or new testids won't appear.
- Remove dead code when found (delete unreferenced files/symbols as part of this change).

---

## File Structure

**Backend — create:**
- `Frigorino.Domain/Entities/UserInventoryNotificationSetting.cs` — new per-user aggregate.
- `Frigorino.Infrastructure/EntityFramework/Configurations/UserInventoryNotificationSettingConfiguration.cs` — EF config.
- `Frigorino.Features/Inventories/Notifications/GetMyInventoryNotification.cs` — GET slice.
- `Frigorino.Features/Inventories/Notifications/UpdateMyInventoryNotification.cs` — PUT slice.
- One new EF migration via `dotnet ef migrations add`.

**Backend — modify:**
- `Frigorino.Domain/Entities/InventorySettings.cs` — strip notification fields.
- `Frigorino.Domain/Entities/NotificationDispatch.cs` — `HouseholdId` → `InventoryId`.
- `Frigorino.Infrastructure/EntityFramework/Configurations/InventorySettingsConfiguration.cs` — drop the two property configs.
- `Frigorino.Infrastructure/EntityFramework/Configurations/NotificationDispatchConfiguration.cs` — new unique index.
- `Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs` — add `DbSet<UserInventoryNotificationSetting>`.
- `Frigorino.Features/Inventories/Settings/GetInventorySettings.cs` + `UpdateInventorySettings.cs` — empty placeholder DTOs/handlers.
- `Frigorino.Infrastructure/Notifications/ExpiryDigestPlanner.cs` — per-inventory grouping.
- `Frigorino.Infrastructure/Notifications/DigestMessageComposer.cs` — per-inventory title + deep link.
- `Frigorino.Infrastructure/Notifications/ExpiryNotificationScan.cs` — load per-user prefs + names; per-inventory ledger.
- `Frigorino.Web/Program.cs` — register new slice group.

**Frontend — create:**
- `ClientApp/src/features/inventories/useMyInventoryNotification.ts` — query hook.
- `ClientApp/src/features/inventories/useUpdateMyInventoryNotification.ts` — mutation hook.

**Frontend — modify:**
- `ClientApp/src/features/inventories/components/InventorySettingsCard.tsx` — personal prefs + loading/error.
- `ClientApp/public/locales/{en,de}/translation.json` — reword keys.

**Frontend — delete (dead after redesign):**
- `ClientApp/src/features/inventories/useInventorySettings.ts`
- `ClientApp/src/features/inventories/useUpdateInventorySettings.ts`

**Tests — modify:**
- `Frigorino.Test/Infrastructure/ExpiryDigestPlannerTests.cs`
- `Frigorino.Test/Infrastructure/DigestMessageComposerTests.cs`
- `Frigorino.Test/Infrastructure/ExpiryNotificationScanTests.cs`
- `Frigorino.Test/Infrastructure/NotificationPersistenceTests.cs`
- `Frigorino.Test/Domain/NotificationEntityTests.cs`
- `Frigorino.Test/Domain/InventorySettingsTests.cs`
- `Frigorino.IntegrationTests/Slices/Notifications/ExpiryScan.Api.feature` + `ExpiryScanApiSteps.cs`
- `Frigorino.IntegrationTests/Slices/Settings/*` (inventory-settings UI/api steps that asserted on the removed household toggle)

**Tests — create:**
- `Frigorino.Test/Domain/UserInventoryNotificationSettingTests.cs`
- `Frigorino.Test/Features/MyInventoryNotificationSliceTests.cs`

---

## Task 1: New aggregate `UserInventoryNotificationSetting`

**Files:**
- Create: `Application/Frigorino.Domain/Entities/UserInventoryNotificationSetting.cs`
- Test: `Application/Frigorino.Test/Domain/UserInventoryNotificationSettingTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Application/Frigorino.Test/Domain/UserInventoryNotificationSettingTests.cs` (mirror the style of the existing `InventorySettingsTests.cs` — xUnit `[Fact]`/`[Theory]`, FluentAssertions or the project's assertion style; check a sibling test file first and match it):

```csharp
using Frigorino.Domain.Entities;
using Xunit;

namespace Frigorino.Test.Domain
{
    public class UserInventoryNotificationSettingTests
    {
        [Fact]
        public void Create_Defaults_To_Enabled_And_Inherited_Lead()
        {
            var s = UserInventoryNotificationSetting.Create("user-1", 42);

            Assert.Equal("user-1", s.UserId);
            Assert.Equal(42, s.InventoryId);
            Assert.True(s.Enabled);
            Assert.Null(s.LeadDays);
        }

        [Fact]
        public void SetEnabled_Toggles_Flag()
        {
            var s = UserInventoryNotificationSetting.Create("user-1", 42);

            s.SetEnabled(false);

            Assert.False(s.Enabled);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(365)]
        [InlineData(null)]
        public void SetLeadDays_Accepts_In_Range_And_Null(int? days)
        {
            var s = UserInventoryNotificationSetting.Create("user-1", 42);

            var result = s.SetLeadDays(days);

            Assert.True(result.IsSuccess);
            Assert.Equal(days, s.LeadDays);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(366)]
        public void SetLeadDays_Rejects_Out_Of_Range(int days)
        {
            var s = UserInventoryNotificationSetting.Create("user-1", 42);

            var result = s.SetLeadDays(days);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(UserInventoryNotificationSetting.LeadDays),
                result.Errors[0].Metadata["Property"]);
        }
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~UserInventoryNotificationSettingTests"`
Expected: FAIL — `UserInventoryNotificationSetting` does not exist.

- [ ] **Step 3: Create the entity**

```csharp
using FluentResults;

namespace Frigorino.Domain.Entities
{
    // Per-user, per-inventory expiry-notification preference. A MISSING row is the default:
    // subscribed (Enabled = true) and inheriting the user's global lead time (LeadDays = null).
    // A user opts OUT of an inventory by setting Enabled = false, and may override the lead
    // time for that one inventory. Replaces the former household-wide InventorySettings flags.
    public class UserInventoryNotificationSetting
    {
        public const int MinLeadDays = 0;
        public const int MaxLeadDays = 365;

        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int InventoryId { get; set; }

        // Default true: a member is subscribed to every inventory in their households until
        // they explicitly mute it.
        public bool Enabled { get; set; } = true;

        // null = inherit the user's global ExpiryLeadDays.
        public int? LeadDays { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Inventory Inventory { get; set; } = null!;

        public static UserInventoryNotificationSetting Create(string userId, int inventoryId)
        {
            return new UserInventoryNotificationSetting
            {
                UserId = userId,
                InventoryId = inventoryId,
            };
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
        }

        public Result SetLeadDays(int? days)
        {
            if (days is not null && (days < MinLeadDays || days > MaxLeadDays))
            {
                return Result.Fail(new Error($"Lead time must be between {MinLeadDays} and {MaxLeadDays} days.")
                    .WithMetadata("Property", nameof(LeadDays)));
            }

            LeadDays = days;
            return Result.Ok();
        }
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~UserInventoryNotificationSettingTests"`
Expected: PASS (all 4 facts/theories).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/UserInventoryNotificationSetting.cs Application/Frigorino.Test/Domain/UserInventoryNotificationSettingTests.cs
git commit -m "feat: add per-user inventory notification preference aggregate"
```

---

## Task 2: Strip notification fields from `InventorySettings` (keep entity as placeholder)

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/InventorySettings.cs`
- Modify: `Application/Frigorino.Test/Domain/InventorySettingsTests.cs`

- [ ] **Step 1: Update the entity** — replace the entire file with:

```csharp
namespace Frigorino.Domain.Entities
{
    // Household-wide inventory settings. The former notification fields (per-inventory enable +
    // lead-time override) moved to the per-user UserInventoryNotificationSetting aggregate.
    // This entity + its GET/PUT endpoints are intentionally retained as an empty placeholder for
    // future household-wide inventory configuration, so they don't have to be reinvented later.
    public class InventorySettings
    {
        public int InventoryId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Inventory Inventory { get; set; } = null!;

        public static InventorySettings Create(int inventoryId)
        {
            return new InventorySettings { InventoryId = inventoryId };
        }
    }
}
```

- [ ] **Step 2: Update its tests** — `InventorySettingsTests.cs` currently tests `SetExpiryLeadDays` / `SetExpiryNotificationsEnabled`, which no longer exist. Delete those test methods. Keep (or add) only a `Create` smoke test:

```csharp
using Frigorino.Domain.Entities;
using Xunit;

namespace Frigorino.Test.Domain
{
    public class InventorySettingsTests
    {
        [Fact]
        public void Create_Sets_InventoryId()
        {
            var settings = InventorySettings.Create(7);

            Assert.Equal(7, settings.InventoryId);
        }
    }
}
```

- [ ] **Step 3: Build to surface every break**

Run: `dotnet build Application/Frigorino.sln`
Expected: compile errors ONLY in files that read the removed members — `InventorySettingsConfiguration.cs`, `GetInventorySettings.cs`, `UpdateInventorySettings.cs`, `ExpiryNotificationScan.cs`, planner. These are fixed in Tasks 3–8; do NOT chase them here beyond what this task owns. (If you prefer a green build at each commit, do Tasks 2–8 then build — but commit per task.) Acceptable to commit with the known downstream breaks listed, since the next tasks resolve them; note it in the commit body.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Domain/Entities/InventorySettings.cs Application/Frigorino.Test/Domain/InventorySettingsTests.cs
git commit -m "refactor: strip notification fields from InventorySettings (now an empty placeholder)"
```

---

## Task 3: Re-key `NotificationDispatch` to `(UserId, InventoryId, SentOn)`

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/NotificationDispatch.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/NotificationDispatchConfiguration.cs`
- Modify: `Application/Frigorino.Test/Domain/NotificationEntityTests.cs`

- [ ] **Step 1: Update test** — in `NotificationEntityTests.cs`, find the `NotificationDispatch.Create` test and change it to the inventory key:

```csharp
[Fact]
public void NotificationDispatch_Create_Sets_Keys()
{
    var d = NotificationDispatch.Create("user-1", 99, new DateOnly(2026, 6, 2));

    Assert.Equal("user-1", d.UserId);
    Assert.Equal(99, d.InventoryId);
    Assert.Equal(new DateOnly(2026, 6, 2), d.SentOn);
}
```

- [ ] **Step 2: Run test, verify fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~NotificationEntityTests"`
Expected: FAIL — `Create` still takes `householdId`; `InventoryId` doesn't exist.

- [ ] **Step 3: Update the entity** — replace the file with:

```csharp
namespace Frigorino.Domain.Entities
{
    // De-dup ledger: at most one notification per (user, inventory, day). A unique index on
    // (UserId, InventoryId, SentOn) makes the scan idempotent across re-triggers / double fires.
    public class NotificationDispatch
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int InventoryId { get; set; }
        public DateOnly SentOn { get; set; }

        public static NotificationDispatch Create(string userId, int inventoryId, DateOnly sentOn)
        {
            return new NotificationDispatch
            {
                UserId = userId,
                InventoryId = inventoryId,
                SentOn = sentOn,
            };
        }
    }
}
```

- [ ] **Step 4: Update the EF config** — in `NotificationDispatchConfiguration.cs`, replace the index line:

```csharp
            // At most one notification per user-inventory-day.
            builder.HasIndex(d => new { d.UserId, d.InventoryId, d.SentOn }).IsUnique();
```

- [ ] **Step 5: Run test, verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~NotificationEntityTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Entities/NotificationDispatch.cs Application/Frigorino.Infrastructure/EntityFramework/Configurations/NotificationDispatchConfiguration.cs Application/Frigorino.Test/Domain/NotificationEntityTests.cs
git commit -m "refactor: re-key NotificationDispatch ledger by inventory instead of household"
```

---

## Task 4: EF wiring + delta migration

**Files:**
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/UserInventoryNotificationSettingConfiguration.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/InventorySettingsConfiguration.cs`
- Create (generated): one migration under `Application/Frigorino.Infrastructure/Migrations/`

- [ ] **Step 1: Add the new EF config**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class UserInventoryNotificationSettingConfiguration : IEntityTypeConfiguration<UserInventoryNotificationSetting>
    {
        public void Configure(EntityTypeBuilder<UserInventoryNotificationSetting> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.UserId)
                .HasMaxLength(128)
                .IsRequired();

            // ValueGeneratedNever: always send the explicit CLR value on INSERT so the lazy-create
            // path can't lose a default-false (mute) to the OnAdd sentinel-skip (same pattern as the
            // old InventorySettings.ExpiryNotificationsEnabled config).
            builder.Property(s => s.Enabled)
                .HasDefaultValue(true)
                .ValueGeneratedNever();

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt).IsRequired();

            // One preference row per (user, inventory).
            builder.HasIndex(s => new { s.UserId, s.InventoryId }).IsUnique();

            builder.HasOne(s => s.Inventory)
                .WithMany()
                .HasForeignKey(s => s.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 2: Add the DbSet** — in `ApplicationDbContext.cs`, next to the existing `public DbSet<InventorySettings> InventorySettings { get; set; }`, add:

```csharp
        public DbSet<UserInventoryNotificationSetting> UserInventoryNotificationSettings { get; set; }
```

Confirm configurations are auto-applied (the context uses `ApplyConfigurationsFromAssembly` or registers each `IEntityTypeConfiguration` — check how `NotificationDispatchConfiguration` is picked up and follow the same mechanism; if configs are registered explicitly in `OnModelCreating`, add `new UserInventoryNotificationSettingConfiguration()` there).

- [ ] **Step 3: Trim `InventorySettingsConfiguration`** — remove the two notification property lines (`ExpiryLeadDays` and the `ExpiryNotificationsEnabled` `HasDefaultValue(true).ValueGeneratedNever()` block). Keep the key, the two timestamp `IsRequired()` lines, and the one-to-one FK to `Inventory`. Result:

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

- [ ] **Step 4: Generate the migration**

This requires the solution to compile. Tasks 5–8 also change code; if the build is still red from downstream consumers, generate the migration AFTER Task 8 (the planner/scan/slice edits) instead, and reorder this step to run last in the backend phase. The controller should sequence so the migration is generated against a compiling model.

Run (once the model compiles):
```bash
dotnet ef migrations add RedesignInventoryNotificationsPerUser --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: a new migration that (a) drops `ExpiryLeadDays` + `ExpiryNotificationsEnabled` from `InventorySettings`; (b) drops `HouseholdId` from `NotificationDispatch`, adds `InventoryId`, and replaces the unique index; (c) creates the `UserInventoryNotificationSettings` table with the `(UserId, InventoryId)` unique index + Inventory FK.

- [ ] **Step 5: Review the generated migration** — open it and confirm only the three expected changes above appear (no unrelated table churn). Do NOT hand-edit the model snapshot.

- [ ] **Step 6: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: success once Tasks 5–8 are also in. If sequencing this task last in the backend phase, expect green here.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/
git commit -m "feat: EF config + migration for per-user inventory notifications"
```

---

## Task 5: Rework `ExpiryDigestPlanner` to per-inventory grouping

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Notifications/ExpiryDigestPlanner.cs`
- Modify: `Application/Frigorino.Test/Infrastructure/ExpiryDigestPlannerTests.cs`

**New record shapes (note `ExpiryCandidate` gains `InventoryName`; `DigestPlan` gains `InventoryId` + `InventoryName`; a new keyed pref replaces `InventoryNotificationSetting`):**

- [ ] **Step 1: Rewrite the planner tests first.** Replace `ExpiryDigestPlannerTests.cs` test bodies to exercise per-inventory output. Key cases (write each as a `[Fact]`):

1. **Two inventories, one recipient → two plans.** Candidates: inv 1 (household 10, "Milk", expires today), inv 2 (household 10, "Eggs", expires tomorrow). Recipient: user-1 in household 10, lead 7. No prefs, none dispatched, grace 1. Expect 2 plans: one for inv 1 (1 line "Milk"), one for inv 2 (1 line "Eggs"), each carrying the right `InventoryId` + `InventoryName`.
2. **Muted inventory excluded.** Same as above but pref `(user-1, inv 1) = Enabled:false`. Expect 1 plan (inv 2 only).
3. **Per-inventory lead override.** Candidate inv 1 "Yogurt" expires in 10 days; user global lead 3; pref `(user-1, inv 1) = Enabled:true, LeadDays:14`. Expect 1 plan containing "Yogurt" (10 ≤ 14).
4. **Already-dispatched (user, inventory) skipped.** `alreadyDispatched` contains `(user-1, inv 1)`. Expect inv 1 produces no plan even though it has due items.
5. **Overdue grace lower bound.** Item expired 2 days ago, grace 1 → excluded; grace 3 → included (reuse existing overdue test, now scoped per inventory).
6. **Lines ordered by ExpiryDate then Text within an inventory's plan.**

Use this signature in the test setup (see Step 3 for the exact types):

```csharp
var plans = ExpiryDigestPlanner.Plan(
    candidates,
    userInventoryPrefs,   // IReadOnlyDictionary<(string, int), InventoryNotificationPref>
    recipients,
    alreadyDispatched,    // HashSet<(string UserId, int InventoryId)>
    today,
    overdueGraceDays: 1);
```

- [ ] **Step 2: Run tests, verify fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryDigestPlannerTests"`
Expected: FAIL to compile (records/signature changed).

- [ ] **Step 3: Rewrite the planner** — replace the whole file:

```csharp
namespace Frigorino.Infrastructure.Notifications
{
    // Input rows (plain — no EF types, so this is pure + unit-testable).
    public sealed record ExpiryCandidate(
        int InventoryId, int HouseholdId, string InventoryName, string Text, DateOnly ExpiryDate);

    // Per (user, inventory) override. A MISSING entry ⇒ subscribed, inherit (LeadDays null).
    public sealed record InventoryNotificationPref(bool Enabled, int? LeadDays);

    // A member who is globally opted-in AND has at least one active token, scoped to one household.
    public sealed record DigestRecipient(string UserId, int HouseholdId, int UserLeadDays, string? Language);

    public sealed record DigestLine(string Text, DateOnly ExpiryDate, int DaysUntil);

    // One notification per (user, inventory).
    public sealed record DigestPlan(
        string UserId, int InventoryId, string InventoryName, string? Language, IReadOnlyList<DigestLine> Lines);

    public static class ExpiryDigestPlanner
    {
        public static IReadOnlyList<DigestPlan> Plan(
            IReadOnlyCollection<ExpiryCandidate> candidates,
            IReadOnlyDictionary<(string UserId, int InventoryId), InventoryNotificationPref> userInventoryPrefs,
            IReadOnlyCollection<DigestRecipient> recipients,
            HashSet<(string UserId, int InventoryId)> alreadyDispatched,
            DateOnly today,
            int overdueGraceDays)
        {
            var plans = new List<DigestPlan>();

            // Candidates grouped by inventory, so each (recipient, inventory) is considered once.
            var byInventory = candidates
                .GroupBy(c => c.InventoryId)
                .ToList();

            foreach (var recipient in recipients)
            {
                foreach (var inventoryGroup in byInventory)
                {
                    var first = inventoryGroup.First();
                    if (first.HouseholdId != recipient.HouseholdId)
                    {
                        continue;
                    }

                    var inventoryId = inventoryGroup.Key;

                    if (alreadyDispatched.Contains((recipient.UserId, inventoryId)))
                    {
                        continue;
                    }

                    var hasPref = userInventoryPrefs.TryGetValue((recipient.UserId, inventoryId), out var pref);
                    var subscribed = !hasPref || pref!.Enabled;
                    if (!subscribed)
                    {
                        continue;
                    }

                    var effectiveLeadDays = (hasPref ? pref!.LeadDays : null) ?? recipient.UserLeadDays;

                    var lines = new List<DigestLine>();
                    foreach (var candidate in inventoryGroup)
                    {
                        var daysUntil = candidate.ExpiryDate.DayNumber - today.DayNumber;
                        // Upper bound: within the lead window. Lower bound: not more than the grace
                        // days overdue, so a permanently-overdue item eventually drops off.
                        if (daysUntil <= effectiveLeadDays && daysUntil >= -overdueGraceDays)
                        {
                            lines.Add(new DigestLine(candidate.Text, candidate.ExpiryDate, daysUntil));
                        }
                    }

                    if (lines.Count == 0)
                    {
                        continue;
                    }

                    var ordered = lines
                        .OrderBy(l => l.ExpiryDate)
                        .ThenBy(l => l.Text)
                        .ToList();
                    plans.Add(new DigestPlan(
                        recipient.UserId, inventoryId, first.InventoryName, recipient.Language, ordered));
                }
            }

            return plans;
        }
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryDigestPlannerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/ExpiryDigestPlanner.cs Application/Frigorino.Test/Infrastructure/ExpiryDigestPlannerTests.cs
git commit -m "feat: plan one expiry notification per inventory with per-user prefs"
```

---

## Task 6: Per-inventory title + deep link in `DigestMessageComposer`

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Notifications/DigestMessageComposer.cs`
- Modify: `Application/Frigorino.Test/Infrastructure/DigestMessageComposerTests.cs`

- [ ] **Step 1: Update composer tests** — assert the new title (includes inventory name) and the deep link `"/inventories/{InventoryId}/view"`. Build a `DigestPlan` with `InventoryId: 42, InventoryName: "Fridge"`, two lines, English; assert:
- `notification.Title` equals `"Fridge: 2 items expiring soon"`.
- `notification.DeepLinkPath` equals `"/inventories/42/view"`.
- German (`Language: "de"`): `Title` equals `"Fridge: 2 Artikel laufen bald ab"`.
- Body composition (first-3 + "+N more") unchanged — keep/adapt the existing body assertions.

- [ ] **Step 2: Run tests, verify fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~DigestMessageComposerTests"`
Expected: FAIL (title/deeplink differ; `DigestPlan` shape changed).

- [ ] **Step 3: Update the composer** — replace the file:

```csharp
using Frigorino.Domain.Notifications;

namespace Frigorino.Infrastructure.Notifications
{
    public static class DigestMessageComposer
    {
        private const int MaxNamesInBody = 3;

        public static ExpiryDigestNotification Compose(DigestPlan plan)
        {
            var german = string.Equals(plan.Language, "de", StringComparison.OrdinalIgnoreCase);
            var count = plan.Lines.Count;

            var title = german
                ? $"{plan.InventoryName}: {count} Artikel laufen bald ab"
                : $"{plan.InventoryName}: {count} item{(count == 1 ? "" : "s")} expiring soon";

            var named = plan.Lines
                .Take(MaxNamesInBody)
                .Select(l => $"{l.Text} {Phrase(l.DaysUntil, german)}");

            var body = string.Join(", ", named);

            var remaining = count - MaxNamesInBody;
            if (remaining > 0)
            {
                body += german ? $" und {remaining} weitere" : $", +{remaining} more";
            }

            // Deep-link straight to the inventory detail page so a click lands on the items.
            var deepLinkPath = $"/inventories/{plan.InventoryId}/view";

            return new ExpiryDigestNotification(title, body, deepLinkPath);
        }

        private static string Phrase(int daysUntil, bool german)
        {
            if (daysUntil < 0)
            {
                return german ? "überfällig" : "overdue";
            }
            if (daysUntil == 0)
            {
                return german ? "heute" : "today";
            }
            if (daysUntil == 1)
            {
                return german ? "morgen" : "tomorrow";
            }
            return german ? $"in {daysUntil} Tagen" : $"in {daysUntil} days";
        }
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~DigestMessageComposerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/DigestMessageComposer.cs Application/Frigorino.Test/Infrastructure/DigestMessageComposerTests.cs
git commit -m "feat: per-inventory notification title + deep link to inventory view"
```

---

## Task 7: Rework `ExpiryNotificationScan` for per-inventory dispatch

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Notifications/ExpiryNotificationScan.cs`
- Modify: `Application/Frigorino.Test/Infrastructure/ExpiryNotificationScanTests.cs`
- Modify: `Application/Frigorino.Test/Infrastructure/NotificationPersistenceTests.cs`

- [ ] **Step 1: Update the scan implementation.** Replace `RunAsync` so it: loads candidates WITH inventory name; loads per-user-inventory prefs into a tuple-keyed dictionary; keeps the same recipient query; loads already-dispatched `(UserId, InventoryId)`; and claims/sends per (user, inventory). Replace the body of `RunAsync` (constructor and class header unchanged) with:

```csharp
        public async Task RunAsync(DateOnly today, CancellationToken ct)
        {
            // 1. Candidate items: active, with an expiry date, from active inventories.
            var candidates = await _db.InventoryItems
                .Where(i => i.IsActive && i.ExpiryDate != null && i.Inventory.IsActive)
                .Select(i => new ExpiryCandidate(
                    i.InventoryId, i.Inventory.HouseholdId, i.Inventory.Name, i.Text, i.ExpiryDate!.Value))
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                _logger.LogInformation("Expiry scan: no candidate items.");
                return;
            }

            var householdIds = candidates.Select(c => c.HouseholdId).Distinct().ToList();
            var inventoryIds = candidates.Select(c => c.InventoryId).Distinct().ToList();

            // 2. Per-user, per-inventory preferences (mute + lead override) for those inventories.
            var prefRows = await _db.UserInventoryNotificationSettings
                .Where(s => inventoryIds.Contains(s.InventoryId))
                .Select(s => new { s.UserId, s.InventoryId, s.Enabled, s.LeadDays })
                .ToListAsync(ct);
            var userInventoryPrefs = prefRows.ToDictionary(
                p => (p.UserId, p.InventoryId),
                p => new InventoryNotificationPref(p.Enabled, p.LeadDays));

            // 3. Recipients: active members of those households who are globally opted-in AND have >=1 token.
            var recipients = await (
                from uh in _db.UserHouseholds
                where uh.IsActive && householdIds.Contains(uh.HouseholdId)
                join us in _db.UserSettings on uh.UserId equals us.UserId
                where us.ExpiryNotificationsEnabled
                where _db.FcmTokens.Any(t => t.UserId == uh.UserId)
                select new DigestRecipient(uh.UserId, uh.HouseholdId, us.ExpiryLeadDays, us.Language))
                .ToListAsync(ct);

            if (recipients.Count == 0)
            {
                _logger.LogInformation("Expiry scan: no eligible recipients.");
                return;
            }

            // 4. Already-dispatched (user, inventory) keys for today.
            var dispatchedToday = await _db.NotificationDispatches
                .Where(d => d.SentOn == today)
                .Select(d => new { d.UserId, d.InventoryId })
                .ToListAsync(ct);
            var alreadyDispatched = dispatchedToday
                .Select(d => (d.UserId, d.InventoryId))
                .ToHashSet();

            // 5. Plan + dispatch. Claim each slot (insert + commit the ledger row) BEFORE sending,
            // so a concurrent scan that lost the unique-index race never sends a duplicate.
            var plans = ExpiryDigestPlanner.Plan(
                candidates, userInventoryPrefs, recipients, alreadyDispatched, today, _settings.OverdueGraceDays);
            var sent = 0;
            foreach (var plan in plans)
            {
                var notification = DigestMessageComposer.Compose(plan);

                // Claim the slot first. The unique index on (UserId, InventoryId, SentOn) lets only one
                // concurrent scan win; the loser hits DbUpdateException and skips sending.
                var dispatch = NotificationDispatch.Create(plan.UserId, plan.InventoryId, today);
                _db.NotificationDispatches.Add(dispatch);
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
                {
                    // A concurrent scan already claimed this (user, inventory, day): the unique index
                    // rejected our insert with SQLSTATE 23505. Detach so the failed row is not retried
                    // on the next iteration's save, then skip the send. Any other DbUpdateException
                    // (transient fault / deadlock / timeout) is NOT caught here — it propagates.
                    _db.Entry(dispatch).State = EntityState.Detached;
                    _logger.LogInformation(
                        "Expiry scan: slot already claimed for user {UserId} inventory {InventoryId}; skipping send.",
                        plan.UserId, plan.InventoryId);
                    continue;
                }

                // Slot is claimed. Send synchronously inside the request. A failed send is the accepted,
                // now-rare lossy-tolerant case — the ledger row already committed, so we log and move on.
                try
                {
                    await _sender.SendDigestAsync(plan.UserId, notification, ct);
                    sent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Expiry scan: notification send failed for user {UserId} inventory {InventoryId} after slot claimed.",
                        plan.UserId, plan.InventoryId);
                    continue;
                }
            }

            _logger.LogInformation("Expiry scan: sent {Count} notification(s).", sent);
        }
```

Also update the class header comment: change "the NotificationDispatch ledger row ... on (UserId, HouseholdId, SentOn)" to "(UserId, InventoryId, SentOn)" and "the digest is sent" → "the notification is sent".

- [ ] **Step 2: Update scan tests** — `ExpiryNotificationScanTests.cs` and `NotificationPersistenceTests.cs` seed `InventorySettings` rows / assert on `(user, household)` dispatch. Update them to: seed `UserInventoryNotificationSettings` for mute/override cases; assert one `NotificationDispatch` per `(user, inventory)`; for the multi-inventory case assert multiple dispatch rows + multiple `SendDigestAsync` calls (FakeItEasy `A.CallTo(() => sender.SendDigestAsync(...)).MustHaveHappenedTwiceExactly()` or per-userId assertions). Keep the unique-index / 23505 concurrency test but key it on inventory. Ensure any seeded `Inventory` has a non-null `Name` (the candidate projection now reads `Inventory.Name`).

- [ ] **Step 3: Run tests, verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryNotificationScan|FullyQualifiedName~NotificationPersistence"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/ExpiryNotificationScan.cs Application/Frigorino.Test/Infrastructure/ExpiryNotificationScanTests.cs Application/Frigorino.Test/Infrastructure/NotificationPersistenceTests.cs
git commit -m "feat: expiry scan dispatches one notification per user per inventory"
```

---

## Task 8: New per-user inventory-notification slices + empty `InventorySettings` placeholders + wiring

**Files:**
- Create: `Application/Frigorino.Features/Inventories/Notifications/GetMyInventoryNotification.cs`
- Create: `Application/Frigorino.Features/Inventories/Notifications/UpdateMyInventoryNotification.cs`
- Modify: `Application/Frigorino.Features/Inventories/Settings/GetInventorySettings.cs`
- Modify: `Application/Frigorino.Features/Inventories/Settings/UpdateInventorySettings.cs`
- Modify: `Application/Frigorino.Web/Program.cs`
- Create: `Application/Frigorino.Test/Features/MyInventoryNotificationSliceTests.cs`

**Route:** `GET`/`PUT` `/api/household/{householdId}/inventories/{inventoryId}/notifications`. Auth: any active member (no `CanBeManagedBy` gate — it's a personal pref). The current user is implicit via `ICurrentUserService`.

- [ ] **Step 1: Create `GetMyInventoryNotification.cs`** (mirror `GetInventorySettings.cs` structure):

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Notifications
{
    public sealed record MyInventoryNotificationResponse(bool Enabled, int? LeadDays);

    public static class GetMyInventoryNotificationEndpoint
    {
        public static IEndpointRouteBuilder MapGetMyInventoryNotification(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetMyInventoryNotification")
               .Produces<MyInventoryNotificationResponse>()
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<MyInventoryNotificationResponse>, NotFound>> Handle(
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

            var response = await db.UserInventoryNotificationSettings
                .Where(s => s.InventoryId == inventoryId && s.UserId == currentUser.UserId)
                .Select(s => new MyInventoryNotificationResponse(s.Enabled, s.LeadDays))
                .FirstOrDefaultAsync(ct);

            // No row ⇒ subscribed by default, inherit lead-days.
            return TypedResults.Ok(response ?? new MyInventoryNotificationResponse(true, null));
        }
    }
}
```

- [ ] **Step 2: Create `UpdateMyInventoryNotification.cs`** (mirror `UpdateInventorySettings.cs`, minus the `CanBeManagedBy` gate):

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

namespace Frigorino.Features.Inventories.Notifications
{
    public sealed record UpdateMyInventoryNotificationRequest(bool Enabled, int? LeadDays);

    public static class UpdateMyInventoryNotificationEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateMyInventoryNotification(this IEndpointRouteBuilder app)
        {
            app.MapPut("", Handle)
               .WithName("UpdateMyInventoryNotification")
               .Produces<MyInventoryNotificationResponse>()
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<MyInventoryNotificationResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int inventoryId,
            UpdateMyInventoryNotificationRequest request,
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

            var settings = await db.UserInventoryNotificationSettings
                .FirstOrDefaultAsync(s => s.InventoryId == inventoryId && s.UserId == currentUser.UserId, ct);

            if (settings is null)
            {
                settings = UserInventoryNotificationSetting.Create(currentUser.UserId, inventoryId);
                db.UserInventoryNotificationSettings.Add(settings);
            }

            settings.SetEnabled(request.Enabled);
            var result = settings.SetLeadDays(request.LeadDays);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new MyInventoryNotificationResponse(settings.Enabled, settings.LeadDays));
        }
    }
}
```

- [ ] **Step 3: Reduce `InventorySettings` slices to empty placeholders.** In `GetInventorySettings.cs`: change the response record to `public sealed record InventorySettingsResponse();` (move it here if it lived in the Get file — confirm where it's declared; it's referenced by both Get and Update). Keep the membership + inventory-exists checks; return `TypedResults.Ok(new InventorySettingsResponse())`. Drop the `db.InventorySettings` projection. In `UpdateInventorySettings.cs`: change request to `public sealed record UpdateInventorySettingsRequest();`, keep membership + inventory + `CanBeManagedBy` checks, lazy-create the (empty) row, `SaveChangesAsync`, return `new InventorySettingsResponse()`. Remove now-invalid calls to the deleted aggregate methods. Add a `// Placeholder: notification prefs moved to per-user; retained for future household config.` comment to each.

- [ ] **Step 4: Wire the new group in `Program.cs`.** Find the existing inventory-settings group registration (`MapGroup(".../inventories/{inventoryId:int}/settings")` with `.MapGetInventorySettings()` / `.MapUpdateInventorySettings()`). Add a sibling group:

```csharp
var inventoryNotifications = app
    .MapGroup("/api/household/{householdId:int}/inventories/{inventoryId:int}/notifications")
    .RequireAuthorization()
    .WithTags("Inventories");
inventoryNotifications.MapGetMyInventoryNotification();
inventoryNotifications.MapUpdateMyInventoryNotification();
```

Match the EXACT MapGroup/`.WithTags(...)` style used by the adjacent settings group (copy its tag + authorization chain).

- [ ] **Step 5: Create `MyInventoryNotificationSliceTests.cs`** — mirror `FcmTokenSliceTests.cs` (`TestApplicationDbContext`, FakeItEasy `ICurrentUserService`). Cover: GET with no row → `(true, null)`; PUT creates a row (`Enabled:false, LeadDays:14`) → persisted + echoed; PUT then GET round-trips; GET/PUT for a non-member household → `NotFound`; PUT `LeadDays:400` → `ValidationProblem`. Call the handlers directly (they're `private static` — if not callable, follow how `FcmTokenSliceTests` invokes its slice; the register/unregister handlers are public static, so make these two `Handle` methods accessible the same way the existing sliced tests expect, or test via the minimal-API in the IT layer instead — prefer matching the existing FcmToken test approach).

- [ ] **Step 6: Build + run tests**

Run: `dotnet build Application/Frigorino.sln` then `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~MyInventoryNotificationSliceTests"`
Expected: build success (model now compiles — generate the Task 4 migration HERE if it was deferred), tests PASS.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Features/ Application/Frigorino.Web/Program.cs Application/Frigorino.Test/Features/MyInventoryNotificationSliceTests.cs
git commit -m "feat: per-user inventory notification GET/PUT slices; empty InventorySettings placeholder"
```

---

## Task 9: Regenerate the API client

**Files:**
- Modify (generated): `ClientApp/src/lib/openapi.json`, `ClientApp/src/lib/api/**`

- [ ] **Step 1: Regenerate**

Run (from `Application/Frigorino.Web/ClientApp`): `npm run api`
Expected: backend builds, `openapi.json` updated, TS client regenerated. New exports appear in `sdk.gen.ts` / `@tanstack/react-query.gen.ts`: `getMyInventoryNotification`, `updateMyInventoryNotification`, `getMyInventoryNotificationOptions`, `getMyInventoryNotificationQueryKey`, `updateMyInventoryNotificationMutation`. `InventorySettingsResponse` / `UpdateInventorySettingsRequest` become empty types.

- [ ] **Step 2: Sanity-check the generated names**

Run: `grep -rE "getMyInventoryNotification|updateMyInventoryNotification" src/lib/api`
Expected: matches in `sdk.gen.ts`, `types.gen.ts`, and `@tanstack/react-query.gen.ts`.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/
git commit -m "chore: regenerate API client for per-user inventory notifications"
```

---

## Task 10: Frontend hooks (create new, delete dead)

**Files:**
- Create: `ClientApp/src/features/inventories/useMyInventoryNotification.ts`
- Create: `ClientApp/src/features/inventories/useUpdateMyInventoryNotification.ts`
- Delete: `ClientApp/src/features/inventories/useInventorySettings.ts`
- Delete: `ClientApp/src/features/inventories/useUpdateInventorySettings.ts`

- [ ] **Step 1: Create `useMyInventoryNotification.ts`** (mirror the old `useInventorySettings.ts`):

```ts
import { useQuery } from "@tanstack/react-query";
import { getMyInventoryNotificationOptions } from "../../lib/api/@tanstack/react-query.gen";

export function useMyInventoryNotification(householdId: number, inventoryId: number) {
    return useQuery({
        ...getMyInventoryNotificationOptions({
            path: { householdId, inventoryId },
        }),
        enabled: householdId > 0 && inventoryId > 0,
        staleTime: 5 * 60 * 1000,
    });
}
```

- [ ] **Step 2: Create `useUpdateMyInventoryNotification.ts`** (mirror the old `useUpdateInventorySettings.ts` — arg-less mutation; `onSuccess` writes cache via `getMyInventoryNotificationQueryKey`):

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getMyInventoryNotificationQueryKey,
    updateMyInventoryNotificationMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export function useUpdateMyInventoryNotification() {
    const queryClient = useQueryClient();
    return useMutation({
        ...updateMyInventoryNotificationMutation(),
        onSuccess: (data, variables) => {
            queryClient.setQueryData(
                getMyInventoryNotificationQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        inventoryId: variables.path.inventoryId,
                    },
                }),
                data,
            );
        },
    });
}
```

(Confirm the old `useUpdateInventorySettings.ts` used `setQueryData` vs `invalidateQueries` and match that exact pattern.)

- [ ] **Step 3: Delete the dead hooks**

```bash
git rm Application/Frigorino.Web/ClientApp/src/features/inventories/useInventorySettings.ts Application/Frigorino.Web/ClientApp/src/features/inventories/useUpdateInventorySettings.ts
```

- [ ] **Step 4: Type-check (will fail until Task 11 updates the card)**

Run: `npm run tsc`
Expected: errors only in `InventorySettingsCard.tsx` (still imports the deleted hooks). Fixed in Task 11 — commit hooks + card together, or proceed to Task 11 before committing. Recommended: do Task 11, then commit Tasks 10+11 together.

---

## Task 11: Repurpose `InventorySettingsCard` to personal prefs + loading/error

**Files:**
- Modify: `ClientApp/src/features/inventories/components/InventorySettingsCard.tsx`
- Modify: `ClientApp/public/locales/en/translation.json`
- Modify: `ClientApp/public/locales/de/translation.json`

**Behavior:** Both controls become personal (drive the per-user endpoint). The notifications switch = "Notify me about this inventory" (default on). The lead override = personal lead time for this inventory. Remove the `canManage` gate (every member sets their own). Keep the global-off hint. Disable controls + show errors via the mutation's `isPending` + the existing `try/catch` → toast.

- [ ] **Step 1: Rewrite the card.** Replace the body to use `useMyInventoryNotification` + `useUpdateMyInventoryNotification`. Key changes from the current file:
  - Imports: drop `useInventorySettings`/`useUpdateInventorySettings`, add the two new hooks. Drop the `canManage` prop usage (keep the prop in `Props` only if still passed by parents; otherwise remove it and update call sites — see Step 3).
  - `const { data } = useMyInventoryNotification(householdId, inventoryId);`
  - `const update = useUpdateMyInventoryNotification();`
  - State seeding effect reads `data.enabled` / `data.leadDays` (was `data.expiryNotificationsEnabled` / `data.expiryLeadDays`).
  - `save(enabled, leadDays)` calls `update.mutateAsync({ path: { householdId, inventoryId }, body: { enabled, leadDays } })`, wrapped in the existing `try/catch` → `toast.success(t("settings.saved"))` / `toast.error(t("settings.saveFailed"))`.
  - Switch `disabled={update.isPending}` (drop `!canManage`). Same for the override switch + lead input.
  - Keep `data-testid="inventory-notifications-switch"`, `data-testid="inventory-expiry-override-switch"`, `data-testid="inventory-expiry-lead-input"`, and the global-off hint testid `inventory-notifications-global-off-hint` unchanged so existing IT selectors still resolve.
  - Labels: `inventoryNotificationsEnable` (reworded), `expiryLeadOverride` (reworded to personal). See Step 2.

- [ ] **Step 2: Reword translation keys.** In `en/translation.json` `settings`:
  - `"inventoryNotificationsEnable": "Notify me about this inventory"`
  - `"expiryLeadOverride": "Override my reminder lead time"`
  - `"expiryLeadHelp": "Days before expiry to remind me for this inventory."`
  - `"inventoryNotificationsRequiresGlobal"` — keep as-is.

  In `de/translation.json` `settings`:
  - `"inventoryNotificationsEnable": "Mich über dieses Inventar benachrichtigen"`
  - `"expiryLeadOverride": "Meine Vorlaufzeit überschreiben"`
  - `"expiryLeadHelp": "Tage vor Ablauf, an denen ich für dieses Inventar erinnert werde."`

- [ ] **Step 3: Update the card's call sites if the `canManage` prop is removed.** Find where `<InventorySettingsCard` is rendered:

```bash
grep -rn "InventorySettingsCard" Application/Frigorino.Web/ClientApp/src
```
If you removed the `canManage` prop, drop it from each render. (Simplest: keep the `Props` interface but stop using `canManage` internally — then no call-site change. Prefer removing it fully for clean code; update call sites accordingly.)

- [ ] **Step 4: Verify frontend**

Run (from `ClientApp`): `npm run tsc && npm run lint && npm run prettier`
Expected: all pass.

- [ ] **Step 5: Commit (Tasks 10 + 11 together)**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/ Application/Frigorino.Web/ClientApp/public/locales/
git commit -m "feat: personal per-inventory notification card + hooks"
```

---

## Task 12: Update integration tests + rebuild SPA

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Notifications/ExpiryScan.Api.feature` + `ExpiryScanApiSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Settings/*` (steps asserting the old household-wide inventory toggle / endpoint shape)

- [ ] **Step 1: Update the expiry-scan IT.** The `.feature` currently asserts a single household digest. Update scenarios to the per-inventory model: a household with two inventories each holding a due item → after the scan, the recipient receives a notification per inventory (assert via the test `INotificationSender` capture — confirm how the IT captures sends; likely a fake/log sender registered in the test host). Update the parallel-scan concurrency scenario to key on inventory. Update `ExpiryScanApiSteps.cs` accordingly. Update any step that seeds `InventorySettings` mute/lead to instead seed `UserInventoryNotificationSettings` (via API PUT `/notifications` or direct DB seed — match how the suite seeds settings today).

- [ ] **Step 2: Update settings IT.** Any scenario hitting `PUT .../settings` with `expiryNotificationsEnabled`/`expiryLeadDays`, or a UI step toggling the inventory notification switch as a household-wide setting, must move to the personal `/notifications` endpoint (API) or keep the UI testids (unchanged) but assert personal behavior. Remove assertions tied to the removed household fields. Keep assertions on testids/`data-*`, never translated text.

- [ ] **Step 3: Rebuild the SPA** (IT serves `ClientApp/build`):

Run (from `ClientApp`): `npm run build`
Expected: success; `build/` updated with the new card.

- [ ] **Step 4: Run the integration tests**

Run: `dotnet test Application/Frigorino.IntegrationTests`
Expected: PASS. (Docker must be running for Testcontainers — if it errors with daemon-unreachable, ask the user to start Docker Desktop. The "undo delete in toast" IT can flake — re-run once before suspecting a regression.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/
git commit -m "test: per-inventory expiry notifications integration coverage"
```

---

## Task 13: Full verification + housekeeping

- [ ] **Step 1: Full solution tests**

Run: `dotnet test Application/Frigorino.sln`
Expected: all green (Frigorino.Test + Frigorino.IntegrationTests). Capture `${PIPESTATUS[0]}` / read the pass-fail summary — do not trust a tail pipe.

- [ ] **Step 2: Frontend full verify**

Run (from `ClientApp`): `npm run tsc && npm run lint && npm run prettier && npm run build`
Expected: all pass.

- [ ] **Step 3: Docker build** (catches Dockerfile/pipeline/SPA drift)

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: success. (If the daemon is unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 4: Dead-code sweep.** Confirm no lingering references to removed symbols:

```bash
grep -rn "ExpiryNotificationsEnabled" Application/Frigorino.Domain/Entities/InventorySettings.cs
grep -rn "InventoryNotificationSetting\b" Application/Frigorino.Infrastructure   # old planner record name
grep -rn "useInventorySettings\|useUpdateInventorySettings" Application/Frigorino.Web/ClientApp/src
```
Expected: no matches (the first two should be gone; the hooks deleted).

- [ ] **Step 5: Commit any housekeeping**, then the feature is ready for the final review + `superpowers:finishing-a-development-branch`.

---

## Self-Review (run before dispatching)

**Spec coverage:**
- #2 per-inventory deep-link → Tasks 5, 6 (one plan per inventory; deep link `/inventories/{id}/view`). ✓
- #3 per-user mute + per-user lead (incl. lead days) → Tasks 1, 4, 8, 10, 11 (new aggregate, slices, card). ✓
- "Keep InventorySettings entity + endpoints, accept empty" → Tasks 2, 8 (entity stripped to placeholder; GET/PUT retained, empty DTOs). ✓
- Multiple notifications/day accepted → planner emits N plans; ledger keyed per inventory (Tasks 5, 7). ✓
- Default-on subscription → absent row = enabled (Task 1 semantics; planner `!hasPref || pref.Enabled`). ✓

**Type consistency:** `ExpiryCandidate` gains `InventoryName` (Task 5) and the scan projects `Inventory.Name` (Task 7) — consistent. `DigestPlan` carries `InventoryId`+`InventoryName`, consumed by the composer (Task 6). Planner pref dict key `(string UserId, int InventoryId)` matches the scan's dictionary build (Task 7) and `alreadyDispatched` hash key. `NotificationDispatch.Create(userId, inventoryId, sentOn)` signature matches the scan call (Task 7) and the index (Task 3). New slice `WithName` values match the generated hook names referenced in Tasks 10–11.

**Sequencing note for the controller:** The model does not fully compile until Tasks 2–8 land. Generate the Task 4 migration only once the model compiles (i.e. at the end of Task 8, or run Tasks 2,3,5,6,7,8 edits then Task 4's migration). Commit per task; it's acceptable for an intermediate commit to have known downstream breaks documented in its body, since the backend phase resolves them before any test/Docker gate.

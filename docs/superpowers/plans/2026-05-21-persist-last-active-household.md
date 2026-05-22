# Persist Last-Active-Household Per User — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a user picks an active household, remember the choice across browser sessions and server restarts by persisting it on the `User` row, while keeping the existing `HttpContext.Session` cache as a fast path.

**Architecture:** Add a nullable `LastActiveHouseholdId` FK on `User` (`ON DELETE SET NULL`). In `CurrentHouseholdService`, `SetCurrentHouseholdAsync` now writes both session and the user row; `GetCurrentHouseholdIdAsync` resolves in the order `session → User.LastActiveHouseholdId → role-based default`, calling `SetCurrentHouseholdAsync` to rehydrate the session whenever a non-session path resolves. No public API change, no frontend change.

**Tech Stack:** .NET 8, EF Core 8 (Postgres), xUnit + FakeItEasy + EF InMemory for unit tests, Reqnroll + Playwright + Testcontainers for integration tests.

---

## Progress (complete as of 2026-05-22)

**Branch:** `feat/persist-last-active-household` (off `stage`, merge-base `19e3c80`). Plan committed at `c7e5b90`. Head: `2c17f50`.

| Task | Status | Commit | Notes |
|------|--------|--------|-------|
| 1 — User entity property | ✅ done | `11e6262` | Clean. |
| 2 — EF FK + migration | ✅ done | `d090975` | Migration filename: `20260521212218_Persist_Last_Active_Household.cs`. `ProductVersion` in snapshot rebased 8.0.18 → 10.0.7 (benign — actual tooling version). |
| 3 — FakeSession helper | ✅ done | `1e51f22` | Added `[NotNullWhen(true)]` attribute on `TryGetValue` out param (matches `ISession` contract); block braces instead of expression bodies (matches project style). |
| 4 — TDD Set persistence | ✅ done | `a2563f2` | **Helper signature changed** from `(service, ctx)` to `(service, ctx, session, dbName)` — the plan's `ctx.Database.GetDbConnection().Database` throws on EF InMemory; the test now uses the saved `dbName` directly. Extra fields prefetched for Tasks 5–6 reuse. |
| 5 — TDD Get fallback | ✅ done | `0a74ec5` | 3-step chain (session → stored → role default), each non-session resolution calls `SetCurrentHouseholdAsync` to rehydrate. Red was meaningful (both failed `Expected: 20, Actual: 10`). |
| 6 — Inaccessible-stored edge case | ✅ done | `902339e` | Single new `[Fact]`; passes against the Task 5 implementation unchanged. |
| 7 — Reqnroll session-loss scenario | ✅ done | `2c17f50` | New Gherkin scenario + `WhenIClearMyBrowserSession` step (clears Playwright `IBrowserContext` cookies). Auth survives because tests inject `X-Test-*` headers per request, not cookies. |
| 8 — Full-stack verify | ✅ done | — | Unit 162/162, Integration 58/58 (one transient flake on unrelated `HouseholdSetup` scenario, clean on retry), `npm run tsc` clean, `docker build` clean. |

**Final review (`19e3c80..2c17f50`):** Ready to merge to `stage`. No critical or important issues; minor observations are pre-existing patterns (double access-check on cold reads, side-effecting `Get*`) or stale plan comments (line 52 "no index" — EF auto-creates `IX_Users_LastActiveHouseholdId` for the FK).

### Task 5 — Resume notes (historical, kept for reference)

When resuming, dispatch a fresh implementer with these explicit corrections to the plan's Task 5 text:

1. **Drop the reflection trick.** The plan shows:
   ```csharp
   var accessorField = service.GetType().GetField("_httpContextAccessor", ...);
   var accessor = (IHttpContextAccessor)accessorField.GetValue(service)!;
   accessor.HttpContext!.Session.Clear();
   ```
   Replace with simply: `session.Clear();` using the `session` field from the helper's tuple. The first test's tuple destructure becomes `var (service, ctx, _, _) = ...` and the second test's is `var (service, _, session, _) = ...`.

2. The implementer should keep TDD discipline (red before green), and at the end run the broader filter `dotnet test ... --filter "FullyQualifiedName~CurrentHouseholdServiceTests"` to confirm all 3 tests pass (the Task 4 test plus both new ones).

### File-state divergences from the original plan (for anyone reading the plan body below)

- **Task 4 Step 1 code block**: in the *committed* test file the helper returns `(CurrentHouseholdService service, TestApplicationDbContext ctx, FakeSession session, string dbName)` and the test destructures `var (service, ctx, _, _) = …`. The verification context is built via `NewContext(dbName)`, not `NewContext(ctx.Database.GetDbConnection().Database)`. The body in the plan below is the *original* spec — when reading the actual code, expect the 4-tuple shape.
- **Task 3 FakeSession**: actual file uses block braces and adds `[NotNullWhen(true)]` to `TryGetValue`'s out param; otherwise identical.

---

## File Structure

- **Modify** `Application/Frigorino.Domain/Entities/User.cs` — add `LastActiveHouseholdId` (nullable `int?`) and `LastActiveHousehold` nav property.
- **Modify** `Application/Frigorino.Infrastructure/EntityFramework/Configurations/UserConfiguration.cs` — configure the FK with `OnDelete(DeleteBehavior.SetNull)`. No index (lookups are always `Users.Find(userId)` via PK).
- **Create** `Application/Frigorino.Infrastructure/Migrations/<timestamp>_Persist_Last_Active_Household.cs` — generated by `dotnet ef migrations add`, no manual data backfill.
- **Modify** `Application/Frigorino.Infrastructure/Services/CurrentHouseholdService.cs` — extend `SetCurrentHouseholdAsync` (persist) and `GetCurrentHouseholdIdAsync` (new fallback chain).
- **Create** `Application/Frigorino.Test/TestInfrastructure/FakeSession.cs` — minimal in-memory `ISession` for service tests.
- **Create** `Application/Frigorino.Test/Infrastructure/CurrentHouseholdServiceTests.cs` — new xUnit class covering the new behavior end-to-end against `TestApplicationDbContext`.
- **Modify** `Application/Frigorino.IntegrationTests/Slices/CurrentHousehold/SwitchHousehold.feature` — add a scenario asserting the selection survives session loss (cookie clear).

The split keeps the persistence concern colocated with `CurrentHouseholdService` (the existing owner of "active household" state) and avoids touching the slice handlers / DTOs / frontend.

---

## Task 1: Add `LastActiveHouseholdId` to the `User` entity

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/User.cs`

- [x] **Step 1: Add the property + navigation** *(done — commit `11e6262`)*

Replace the body of `User` with:

```csharp
namespace Frigorino.Domain.Entities
{
    public class User
    {
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        public int? LastActiveHouseholdId { get; set; }

        // Navigation properties
        public Household? LastActiveHousehold { get; set; }
        public ICollection<UserHousehold> UserHouseholds { get; set; } = new List<UserHousehold>();
        public ICollection<Household> CreatedHouseholds { get; set; } = new List<Household>();
    }
}
```

- [x] **Step 2: Verify the project still builds** *(done — 0 warnings, 0 errors)*

Run: `dotnet build Application/Frigorino.Domain/Frigorino.Domain.csproj`
Expected: build succeeds (no consumers reference the new property yet).

- [x] **Step 3: Commit** *(done — `11e6262`)*

```bash
git add Application/Frigorino.Domain/Entities/User.cs
git commit -m "feat: add User.LastActiveHouseholdId domain property"
```

---

## Task 2: Configure the FK in EF + generate migration

**Files:**
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/UserConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/Migrations/<timestamp>_Persist_Last_Active_Household.cs` (auto-generated)
- Modify: `Application/Frigorino.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` (auto-updated)

- [x] **Step 1: Configure the FK** *(done — commit `d090975`)*

Inside `UserConfiguration.Configure`, immediately after the existing `IsActive` `.HasDefaultValue(true)` block and **before** the `// Configure relationships` comment, insert:

```csharp
            builder.Property(u => u.LastActiveHouseholdId)
                .IsRequired(false);
```

Then, inside the `// Configure relationships` region, append:

```csharp
            // User.LastActiveHouseholdId is a "last-selected" pointer, not ownership.
            // SetNull on delete so deleting a household does not delete the user.
            builder.HasOne(u => u.LastActiveHousehold)
                .WithMany()
                .HasForeignKey(u => u.LastActiveHouseholdId)
                .OnDelete(DeleteBehavior.SetNull);
```

- [x] **Step 2: Build to confirm the model compiles** *(done)*

Run: `dotnet build Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj`
Expected: build succeeds.

- [x] **Step 3: Generate the EF migration** *(done — `20260521212218_Persist_Last_Active_Household.cs`)*

Run from repo root:

```bash
dotnet ef migrations add Persist_Last_Active_Household \
    --project Application/Frigorino.Infrastructure \
    --startup-project Application/Frigorino.Web
```

Expected:
- Two new files appear under `Application/Frigorino.Infrastructure/Migrations/`:
  `<timestamp>_Persist_Last_Active_Household.cs` and `<timestamp>_Persist_Last_Active_Household.Designer.cs`.
- `ApplicationDbContextModelSnapshot.cs` is updated with the new column + FK.

- [x] **Step 4: Sanity-check the generated migration** *(done — AddColumn nullable, IX_Users_LastActiveHouseholdId, FK with `ReferentialAction.SetNull`, matching `Down`)*

Open `<timestamp>_Persist_Last_Active_Household.cs`. It must contain:
- `migrationBuilder.AddColumn<int>(name: "LastActiveHouseholdId", table: "Users", type: "integer", nullable: true ...)`
- `migrationBuilder.CreateIndex(name: "IX_Users_LastActiveHouseholdId", ...)` (EF adds an FK index automatically — keep it; it makes `ON DELETE SET NULL` cheap)
- `migrationBuilder.AddForeignKey(... principalTable: "Households" ... onDelete: ReferentialAction.SetNull)`
- A `Down` method that reverses all three.

If the migration looks wrong (e.g. nullable: false, or onDelete: Cascade), delete both new files, fix the configuration, and re-run Step 3.

- [x] **Step 5: Apply the migration locally + confirm app boots** *(done — `dotnet build Application/Frigorino.sln` clean)*

Run: `dotnet build Application/Frigorino.sln`
Expected: build succeeds.

The app applies migrations on startup via `context.Database.MigrateAsync()` (`Program.cs`). You don't need to run `dotnet ef database update` manually — but if you have a dev DB up and want to smoke-check, run it now:

```bash
dotnet ef database update \
    --project Application/Frigorino.Infrastructure \
    --startup-project Application/Frigorino.Web
```

Expected: `Done.` (Skip this step if no local DB; `dotnet build` already proved the model snapshot is consistent.)

- [x] **Step 6: Commit** *(done — `d090975`)*

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/Configurations/UserConfiguration.cs
git add Application/Frigorino.Infrastructure/Migrations/
git commit -m "feat: add LastActiveHouseholdId FK column with SetNull cascade"
```

---

## Task 3: Add `FakeSession` test helper

**Files:**
- Create: `Application/Frigorino.Test/TestInfrastructure/FakeSession.cs`

Service tests need an `ISession` that survives across calls in the same test. `Microsoft.AspNetCore.Http`'s default session implementation needs a backing store + DI we don't want to wire. A 30-line in-memory fake is the minimum.

- [x] **Step 1: Create the fake session** *(done — commit `1e51f22`. Implementer used block braces instead of expression bodies and added `[NotNullWhen(true)]` on the `TryGetValue` out param.)*

Write file:

```csharp
using Microsoft.AspNetCore.Http;

namespace Frigorino.Test.TestInfrastructure
{
    // Minimal in-memory ISession for service-level unit tests. Backing store is a Dictionary;
    // all async methods complete synchronously. Not thread-safe — tests are single-threaded.
    public sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[]? value)
            => _store.TryGetValue(key, out value);
    }
}
```

- [x] **Step 2: Build the test project** *(done — 0 warnings)*

Run: `dotnet build Application/Frigorino.Test/Frigorino.Test.csproj`
Expected: build succeeds.

- [x] **Step 3: Commit** *(done — `1e51f22`)*

```bash
git add Application/Frigorino.Test/TestInfrastructure/FakeSession.cs
git commit -m "test: add FakeSession helper for service-level tests"
```

---

## Task 4: Test-drive `SetCurrentHouseholdAsync` persists to the User row

**Files:**
- Create: `Application/Frigorino.Test/Infrastructure/CurrentHouseholdServiceTests.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/CurrentHouseholdService.cs`

- [x] **Step 1: Write the failing test** *(done — commit `a2563f2`. **NOTE:** the actual committed helper signature is `(CurrentHouseholdService service, TestApplicationDbContext ctx, FakeSession session, string dbName)` — see Progress section above for why. Read the real file rather than the block below.)*

Create `Application/Frigorino.Test/Infrastructure/CurrentHouseholdServiceTests.cs`:

```csharp
using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class CurrentHouseholdServiceTests
    {
        private const string UserId = "user-1";

        [Fact]
        public async Task SetCurrentHouseholdAsync_Success_PersistsToUserRow()
        {
            var (service, ctx) = await CreateServiceWithSeededUserAsync(seedHouseholdIds: new[] { 10, 20 });

            var result = await service.SetCurrentHouseholdAsync(20);

            Assert.True(result.IsSuccess);

            // Re-read the user from a fresh context to confirm the write hit the DB, not just the change tracker.
            await using var verifyCtx = NewContext(ctx.Database.GetDbConnection().Database);
            var user = await verifyCtx.Users.SingleAsync(u => u.ExternalId == UserId);
            Assert.Equal(20, user.LastActiveHouseholdId);
        }

        // ----- helpers -----

        private static async Task<(CurrentHouseholdService service, TestApplicationDbContext ctx)>
            CreateServiceWithSeededUserAsync(int[] seedHouseholdIds)
        {
            var dbName = Guid.NewGuid().ToString();
            var ctx = NewContext(dbName);

            var user = new User { ExternalId = UserId, Name = "User One", Email = "u1@example.com" };
            ctx.Users.Add(user);
            foreach (var hid in seedHouseholdIds)
            {
                var h = new Household
                {
                    Id = hid,
                    Name = $"H{hid}",
                    CreatedByUserId = UserId,
                };
                ctx.Households.Add(h);
                ctx.UserHouseholds.Add(new UserHousehold
                {
                    UserId = UserId,
                    HouseholdId = hid,
                    Role = HouseholdRole.Owner,
                    IsActive = true,
                });
            }
            await ctx.SaveChangesAsync();

            var session = new FakeSession();
            var httpContext = new DefaultHttpContext { Session = session };
            var accessor = A.Fake<IHttpContextAccessor>();
            A.CallTo(() => accessor.HttpContext).Returns(httpContext);

            var currentUser = A.Fake<ICurrentUserService>();
            A.CallTo(() => currentUser.UserId).Returns(UserId);

            var service = new CurrentHouseholdService(ctx, currentUser, accessor);
            return (service, ctx);
        }

        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }
    }
}
```

- [x] **Step 2: Run the test to confirm it fails** *(done — red was meaningful: `Assert.Equal() Failure: Values differ. Expected: 20, Actual: null`)*

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CurrentHouseholdServiceTests.SetCurrentHouseholdAsync_Success_PersistsToUserRow"`
Expected: FAIL — the verify step finds `user.LastActiveHouseholdId == null` because the service does not write to the User row yet.

- [x] **Step 3: Implement persistence in `SetCurrentHouseholdAsync`** *(done)*

In `Application/Frigorino.Infrastructure/Services/CurrentHouseholdService.cs`, replace the existing `SetCurrentHouseholdAsync` method (lines 63–76) with:

```csharp
    public async Task<Result> SetCurrentHouseholdAsync(int householdId)
    {
        if (!await HasHouseholdAccessAsync(householdId))
        {
            return Result.Fail(new AccessDeniedError("You don't have access to this household."));
        }

        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.Set(CurrentHouseholdSessionKey, BitConverter.GetBytes(householdId));
        }

        // Persist on the User row so the choice survives session loss / server restart.
        // ApplicationDbContext.SaveChangesAsync does not stamp any field on User during Modified,
        // so this write is scoped to LastActiveHouseholdId only.
        var userId = _currentUserService.UserId;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.ExternalId == userId);
        if (user is not null && user.LastActiveHouseholdId != householdId)
        {
            user.LastActiveHouseholdId = householdId;
            await _context.SaveChangesAsync();
        }

        return Result.Ok();
    }
```

- [x] **Step 4: Run the test to confirm it passes** *(done — 1 passed, 899 ms)*

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CurrentHouseholdServiceTests.SetCurrentHouseholdAsync_Success_PersistsToUserRow"`
Expected: PASS.

- [x] **Step 5: Commit** *(done — `a2563f2`)*

```bash
git add Application/Frigorino.Test/Infrastructure/CurrentHouseholdServiceTests.cs
git add Application/Frigorino.Infrastructure/Services/CurrentHouseholdService.cs
git commit -m "feat: persist active household to User row on Set"
```

---

## Task 5: Test-drive `GetCurrentHouseholdIdAsync` falls back to `User.LastActiveHouseholdId`

**Files:**
- Modify: `Application/Frigorino.Test/Infrastructure/CurrentHouseholdServiceTests.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/CurrentHouseholdService.cs`

- [ ] **Step 1: Write the failing tests**

Append two `[Fact]`s to `CurrentHouseholdServiceTests`, before the `// ----- helpers -----` line. **The helper actually committed in Task 4 returns a 4-tuple `(service, ctx, session, dbName)`** — destructure with `_` to discard the fields you don't need (no reflection required).

```csharp
        [Fact]
        public async Task GetCurrentHouseholdIdAsync_NoSession_ReturnsStoredLastActive()
        {
            var (service, ctx, _, _) = await CreateServiceWithSeededUserAsync(seedHouseholdIds: new[] { 10, 20 });

            // Simulate "user previously picked 20, then session was lost" — set the column directly.
            var user = await ctx.Users.SingleAsync(u => u.ExternalId == UserId);
            user.LastActiveHouseholdId = 20;
            await ctx.SaveChangesAsync();

            var id = await service.GetCurrentHouseholdIdAsync();

            Assert.Equal(20, id);
        }

        [Fact]
        public async Task GetCurrentHouseholdIdAsync_StoredAndSessionEmpty_RehydratesSession()
        {
            var (service, _, session, _) = await CreateServiceWithSeededUserAsync(seedHouseholdIds: new[] { 10, 20 });

            // First set via the service to seed the column …
            await service.SetCurrentHouseholdAsync(20);

            // … then wipe the session to simulate browser restart and re-read.
            session.Clear();

            var id = await service.GetCurrentHouseholdIdAsync();

            Assert.Equal(20, id);
            // Subsequent call should hit session; value must still be 20, not flip to the role default (10).
            Assert.Equal(20, await service.GetCurrentHouseholdIdAsync());
        }
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CurrentHouseholdServiceTests.GetCurrentHouseholdIdAsync"`
Expected: BOTH FAIL — the current fallback chain skips the user column and returns the role-based default (which would be 10, the lowest-id Owner household; ordering in `GetDefaultHouseholdIdAsync` is `Role DESC, JoinedAt ASC`, so for two equal-role rows the first-seeded wins).

- [ ] **Step 3: Insert the column fallback into `GetCurrentHouseholdIdAsync`**

Replace `GetCurrentHouseholdIdAsync` (currently lines 30–61 of `CurrentHouseholdService.cs`) with:

```csharp
    public async Task<int?> GetCurrentHouseholdIdAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null) return null;

        // 1. Session cache (hot path).
        if (session.TryGetValue(CurrentHouseholdSessionKey, out var householdIdBytes))
        {
            var householdId = BitConverter.ToInt32(householdIdBytes);
            if (await HasHouseholdAccessAsync(householdId))
            {
                return householdId;
            }
            // Stale session entry — remove and continue to durable fallback.
            session.Remove(CurrentHouseholdSessionKey);
        }

        // 2. Persisted last-active choice on the User row. Re-verify access in case the user
        //    was removed from the household between sessions.
        var userId = _currentUserService.UserId;
        var storedHouseholdId = await _context.Users
            .Where(u => u.ExternalId == userId)
            .Select(u => u.LastActiveHouseholdId)
            .FirstOrDefaultAsync();

        if (storedHouseholdId is int stored && await HasHouseholdAccessAsync(stored))
        {
            await SetCurrentHouseholdAsync(stored);
            return stored;
        }

        // 3. Role-based default.
        var defaultHouseholdId = await GetDefaultHouseholdIdAsync();
        if (defaultHouseholdId.HasValue)
        {
            await SetCurrentHouseholdAsync(defaultHouseholdId.Value);
            return defaultHouseholdId;
        }

        return null;
    }
```

- [ ] **Step 4: Run the tests to confirm they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CurrentHouseholdServiceTests.GetCurrentHouseholdIdAsync"`
Expected: BOTH PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Test/Infrastructure/CurrentHouseholdServiceTests.cs
git add Application/Frigorino.Infrastructure/Services/CurrentHouseholdService.cs
git commit -m "feat: fall back to User.LastActiveHouseholdId before role default"
```

---

## Task 6: Test-drive the inaccessible-stored-household edge case

The user might have left the household they last picked (membership soft-deleted, or household soft-deleted) before reopening the app. The lookup must skip the stored value in that case and continue to the role-based default — without throwing and without leaving the bad value in the user row.

**Files:**
- Modify: `Application/Frigorino.Test/Infrastructure/CurrentHouseholdServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `CurrentHouseholdServiceTests` (using the 4-tuple destructure, same as Task 5):

```csharp
        [Fact]
        public async Task GetCurrentHouseholdIdAsync_StoredHouseholdInaccessible_FallsBackToDefault()
        {
            var (service, ctx, _, _) = await CreateServiceWithSeededUserAsync(seedHouseholdIds: new[] { 10, 20 });

            // User's stored choice points to a household they're no longer a member of.
            var user = await ctx.Users.SingleAsync(u => u.ExternalId == UserId);
            user.LastActiveHouseholdId = 999; // never seeded → no access
            await ctx.SaveChangesAsync();

            var id = await service.GetCurrentHouseholdIdAsync();

            // The default is the highest-role / earliest-joined household. Both seeded households
            // are Owner; ties break on JoinedAt ASC; since seeding order is 10 then 20, expect 10.
            Assert.Equal(10, id);
        }
```

- [ ] **Step 2: Run the test**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CurrentHouseholdServiceTests.GetCurrentHouseholdIdAsync_StoredHouseholdInaccessible_FallsBackToDefault"`
Expected: PASS — the implementation from Task 5 already handles this branch (`HasHouseholdAccessAsync(stored)` returns false, code falls through to `GetDefaultHouseholdIdAsync`).

If it **fails**, the implementation from Task 5 is wrong: re-check the `if (storedHouseholdId is int stored && await HasHouseholdAccessAsync(stored))` guard. Do not weaken the access check — silently returning a household the user has no access to would be a security regression.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Test/Infrastructure/CurrentHouseholdServiceTests.cs
git commit -m "test: cover inaccessible stored household fallback"
```

---

## Task 7: Add a Reqnroll scenario for persistence across session loss

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/CurrentHousehold/SwitchHousehold.feature`
- Possibly modify: `Application/Frigorino.IntegrationTests/Slices/CurrentHousehold/CurrentHouseholdSteps.cs` — only if a "clear session" step doesn't yet exist.

The existing feature already reloads the page; we add a scenario that *clears cookies* before reloading, which is the real test of "session lost, DB column kicks in."

- [ ] **Step 1: Inspect existing step bindings**

Run: `grep -n "Given\|When\|Then" Application/Frigorino.IntegrationTests/Slices/CurrentHousehold/CurrentHouseholdSteps.cs | head -40`

Note which steps already exist. In particular, look for any step that clears browser storage or cookies. If one exists (e.g. `When I clear my browser session`), reuse it. If not, you'll add one in Step 3.

- [ ] **Step 2: Add the scenario to the feature file**

Append to `Application/Frigorino.IntegrationTests/Slices/CurrentHousehold/SwitchHousehold.feature`:

```gherkin

  Scenario: Active household survives session loss
    Given I am logged in as "owner"
    When I navigate to "/household/create"
    And I fill in the household name "Alpha"
    And I submit the household form
    Then I am redirected to "/"
    When I navigate to "/household/create"
    And I fill in the household name "Bravo"
    And I submit the household form
    Then I am redirected to "/"
    When I switch the active household to "Bravo"
    Then the active household should be "Bravo"
    When I clear my browser session
    And I reload the page
    Then the active household should be "Bravo"
```

- [ ] **Step 3: Add the missing step (only if Step 1 showed no match)**

Open `Application/Frigorino.IntegrationTests/Slices/CurrentHousehold/CurrentHouseholdSteps.cs` and add (inside the existing `[Binding]` class):

```csharp
        [When("I clear my browser session")]
        public async Task WhenIClearMyBrowserSession()
        {
            await _context.BrowserContext.ClearCookiesAsync();
        }
```

The exact `_context` / browser-context accessor name should match the surrounding step methods in this file — copy from a sibling binding. (Playwright `IBrowserContext.ClearCookiesAsync()` wipes both session and persistent cookies; the ASP.NET Core session cookie is one of them.)

If `_context.BrowserContext` doesn't match this codebase's wiring, follow the pattern from `HouseholdSteps.cs` instead (look at how it accesses the browser/page).

- [ ] **Step 4: Run the integration tests**

Run: `dotnet test Application/Frigorino.IntegrationTests`
Expected: all scenarios pass, including the new one. Failure modes:
- Step not bound → check the `[When(...)]` regex matches the feature text verbatim.
- `Browser context not initialized` → the step ran outside a scenario hook that creates the browser; reuse the pattern from a passing scenario.

**Per Frigorino convention** ([feedback_test_assertions_no_translated_text](../../memory/feedback_test_assertions_no_translated_text.md)): never assert on translated text. The existing `Then the active household should be "Bravo"` step looks up `data-testid="active-household-name"` (or similar) — do not change that.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/CurrentHousehold/
git commit -m "test: cover active household persistence across session loss"
```

---

## Task 8: Full-stack verify (per memory)

Per [feedback_verify_with_full_tests_and_docker.md](../../memory/feedback_verify_with_full_tests_and_docker.md) and [feedback_verify_with_integration_tests.md](../../memory/feedback_verify_with_integration_tests.md), run the whole suite + Docker build before declaring done.

**Files:** none — verification only.

- [ ] **Step 1: Unit tests**

Run: `dotnet test Application/Frigorino.Test`
Expected: all green. Architecture tests must still pass (the change adds no new project references).

- [ ] **Step 2: Integration tests**

Run: `dotnet test Application/Frigorino.IntegrationTests`
Expected: all green. If Docker daemon is unreachable, prompt the user to start Docker Desktop ([feedback_docker_daemon_check](../../memory/feedback_docker_daemon_check.md)) — do **not** skip these.

- [ ] **Step 3: Frontend type-check (cheap, catches accidental OpenAPI regeneration)**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc`
Expected: PASS. No SPA change is intended; this confirms it.

- [ ] **Step 4: Docker build**

Run from repo root: `docker build -f Application/Dockerfile -t frigorino-llah .`
Expected: build succeeds end-to-end (backend stage + SPA stage + final image). Catches Dockerfile drift before Railway does.

- [ ] **Step 5: Final commit (only if any tidy-ups were made during verify)**

If the previous steps were clean, skip. Otherwise:

```bash
git add -A
git commit -m "chore: post-verify tidy-up for last-active-household persistence"
```

---

## Out-of-scope (deliberately)

- **No API surface change.** `GET /active-household` and `PUT /active-household` keep their request/response DTOs; the persistence layer is the only thing changing.
- **No frontend change.** The SPA already calls `GET /active-household` on boot and updates its store; with persistence in place it just observes a non-null response more often.
- **No backfill.** Existing users get `NULL` for `LastActiveHouseholdId`; the role-based default in `GetDefaultHouseholdIdAsync` handles them exactly as it does today on their next request.
- **No "Testcontainers restart" integration test.** The user explicitly marked this secondary; the Task 7 cookie-clear scenario gives equivalent coverage of "session gone, value preserved" without restarting the backend.
- **No new index.** EF generates `IX_Users_LastActiveHouseholdId` automatically as an FK index; that's sufficient — every read is via PK lookup on `Users`, and the FK index only matters for the `SetNull` cascade when a household is deleted.

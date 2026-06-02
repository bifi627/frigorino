# Inventory Expiry Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Send opted-in users a once-daily push notification digesting their household's soon-to-expire inventory items, delivered via FCM and triggered by an external daily cron hitting a secured internal endpoint.

**Architecture:** A secured `POST /internal/expiry-scan` endpoint runs a scan job that selects expiring items per household, builds a per-recipient digest (respecting layered user/inventory preferences), composes localized text, enqueues each send onto the existing in-process background runner, and writes a de-dup ledger row. An `INotificationSender` port (FCM adapter) delivers to the user's stored FCM tokens via the Firebase Admin SDK. The frontend registers FCM tokens on a settings opt-in toggle and renders the digest through a push handler in a custom service worker.

**Tech Stack:** .NET 10 vertical slices, EF Core (Postgres), `System.Threading.Channels` runner (already built), Firebase Admin SDK (already referenced, v3.5.0) for FCM send, React 19 + `firebase` JS SDK v12 (already installed) + `vite-plugin-pwa` v1.3 (already installed) for the client + service worker, GitHub Actions cron for the daily trigger.

---

## ⚠️ Reconciliation with the shipped Settings feature (read first)

The design spec (`docs/superpowers/specs/2026-06-01-inventory-expiry-notifications-design.md`) assumed the **Settings feature** would add the notification preference fields **as columns on `User` and `Inventory`**, owned there and merely consumed here. **That is not what shipped to `stage`.** The settings feature (migration `20260601152338_AddSettingsTables`) introduced **separate 1:1 aggregate entities** — `UserSettings`, `HouseholdSettings`, `InventorySettings` — each lazily created on first write. Concretely:

- ✅ `InventorySettings.ExpiryLeadDays` (`int?`, validated `[0,365]`, `null` = inherit) **already exists** — this plan **consumes** it, does **not** create it.
- ❌ `InventorySettings.ExpiryNotificationsEnabled` — **does not exist**; this plan **adds** it (default `true`).
- ❌ `UserSettings.ExpiryNotificationsEnabled` + `UserSettings.ExpiryLeadDays` — **do not exist** (`UserSettings` only has `Language`); this plan **adds** them (defaults `false` / `3`).

**Net effect:** Phase A of this plan (Tasks 1–5) adds the notification preference fields to the **existing settings aggregates** and wires them into the **existing settings slices + settings UI**, following the established settings pattern exactly. This is extra work the spec deferred to "the settings feature," now correctly relocated here. Everything downstream (scan, sender, frontend) consumes those fields. No `User` or `Inventory` entity columns are added.

**Effective lead-days resolution (unchanged from spec):** `inventory.ExpiryLeadDays ?? user.ExpiryLeadDays`. Missing `InventorySettings` row ⇒ `ExpiryNotificationsEnabled = true`, `ExpiryLeadDays = null` (inherit). Missing `UserSettings` row ⇒ `ExpiryNotificationsEnabled = false` (opt-in), `ExpiryLeadDays = 3`.

---

## Prerequisites (human-supplied config — gather before Phase D manual verification)

These are **not blockers** for writing/compiling code, but the feature cannot deliver a real push without them:

1. **FCM Web Push VAPID public key.** Firebase Console → Project `frigorino-2acd1` → Project Settings → Cloud Messaging → **Web Push certificates** → *Generate key pair*. The resulting public key string is supplied to the client via `VITE_FCM_VAPID_KEY` (it is public; safe in the bundle). Without it `getToken` cannot mint a token.
2. **`MaintenanceSettings:TriggerToken`.** A long random secret (e.g. `openssl rand -hex 32`). Supplied to the server via env var / user-secrets (never committed). The same value goes into the GitHub Actions secret `MAINTENANCE_TRIGGER_TOKEN`.
3. **Server FCM credential** — none needed. The Firebase Admin SDK reuses `FirebaseSettings:AccessJson` (already configured); the service account already has Cloud Messaging permission.

---

## File Structure

**Backend — new files**

| File | Responsibility |
|---|---|
| `Application/Frigorino.Domain/Entities/FcmToken.cs` | Per-device FCM token entity + factory |
| `Application/Frigorino.Domain/Entities/NotificationDispatch.cs` | Per-(user,household,day) de-dup ledger entity + factory |
| `Application/Frigorino.Domain/Interfaces/INotificationSender.cs` | Delivery port (BCL-only) |
| `Application/Frigorino.Domain/Notifications/ExpiryDigestNotification.cs` | Composed payload record (Title/Body/DeepLinkPath) |
| `Application/Frigorino.Infrastructure/Notifications/ExpiryDigestPlanner.cs` | Pure: candidates + settings + recipients → per-recipient digest plans |
| `Application/Frigorino.Infrastructure/Notifications/DigestMessageComposer.cs` | Pure: digest plan + language → localized notification (en/de) |
| `Application/Frigorino.Infrastructure/Notifications/FcmTokenPruning.cs` | Pure: send results → dead token ids to delete |
| `Application/Frigorino.Infrastructure/Notifications/ExpiryNotificationScan.cs` | The scan job: EF queries → planner → compose → enqueue → ledger |
| `Application/Frigorino.Infrastructure/Notifications/FcmNotificationSender.cs` | `INotificationSender` FCM adapter (Firebase Admin SDK) |
| `Application/Frigorino.Infrastructure/Notifications/LogOnlyNotificationSender.cs` | Fallback sender (DevAuth / IntegrationTest / no Firebase) |
| `Application/Frigorino.Infrastructure/Notifications/MaintenanceSettings.cs` | Config POCO for the trigger token |
| `Application/Frigorino.Infrastructure/Notifications/NotificationDependencyInjection.cs` | `AddExpiryNotifications()` DI extension |
| `Application/Frigorino.Infrastructure/EntityFramework/Configurations/FcmTokenConfiguration.cs` | EF mapping |
| `Application/Frigorino.Infrastructure/EntityFramework/Configurations/NotificationDispatchConfiguration.cs` | EF mapping |
| `Application/Frigorino.Features/Me/Settings/UpdateUserNotificationSettings.cs` | `PUT /api/me/settings/notifications` slice |
| `Application/Frigorino.Features/Notifications/RegisterFcmToken.cs` | `POST /api/notifications/token` slice |
| `Application/Frigorino.Features/Notifications/UnregisterFcmToken.cs` | `DELETE /api/notifications/token` slice |
| `Application/Frigorino.Features/Notifications/TriggerExpiryScan.cs` | `POST /internal/expiry-scan` secured endpoint |
| `Application/Frigorino.Infrastructure/Migrations/*_AddExpiryNotificationPreferences.cs` | EF migration (settings columns) — generated |
| `Application/Frigorino.Infrastructure/Migrations/*_AddNotificationTables.cs` | EF migration (FcmToken + NotificationDispatch) — generated |

**Backend — modified files**

| File | Change |
|---|---|
| `Application/Frigorino.Domain/Entities/UserSettings.cs` | Add `ExpiryNotificationsEnabled`, `ExpiryLeadDays`, consts, `SetExpiryNotifications` |
| `Application/Frigorino.Domain/Entities/InventorySettings.cs` | Add `ExpiryNotificationsEnabled` + `SetExpiryNotificationsEnabled` |
| `Application/Frigorino.Infrastructure/EntityFramework/Configurations/UserSettingsConfiguration.cs` | Map new columns |
| `Application/Frigorino.Infrastructure/EntityFramework/Configurations/InventorySettingsConfiguration.cs` | Map new column |
| `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs` | DbSets for `FcmTokens`, `NotificationDispatches`; stamp `FcmToken` timestamps |
| `Application/Frigorino.Features/Me/Settings/UserSettingsResponse.cs` | Add notification fields |
| `Application/Frigorino.Features/Me/Settings/GetUserSettings.cs` | Project notification fields |
| `Application/Frigorino.Features/Me/Settings/UpdateUserSettings.cs` | Return notification fields in response |
| `Application/Frigorino.Features/Inventories/Settings/InventorySettingsResponse.cs` | Add `ExpiryNotificationsEnabled` |
| `Application/Frigorino.Features/Inventories/Settings/GetInventorySettings.cs` | Project `ExpiryNotificationsEnabled` |
| `Application/Frigorino.Features/Inventories/Settings/UpdateInventorySettings.cs` | Accept + set `ExpiryNotificationsEnabled` |
| `Application/Frigorino.Web/Program.cs` | DI wiring, sender gating, `FirebaseMessaging` registration, map endpoints |
| `Application/Frigorino.Web/appsettings.json` | `MaintenanceSettings:TriggerToken` placeholder |
| `Application/Frigorino.Infrastructure/Auth/FirebaseAuth.cs` | Register `FirebaseMessaging.DefaultInstance` singleton |

**Frontend — new files**

| File | Responsibility |
|---|---|
| `Application/Frigorino.Web/ClientApp/src/common/pushNotifications.ts` | Request permission, get/register/unregister FCM token, foreground `onMessage` |
| `Application/Frigorino.Web/ClientApp/src/features/notifications/useRegisterFcmToken.ts` | Mutation hook (register) |
| `Application/Frigorino.Web/ClientApp/src/features/notifications/useUnregisterFcmToken.ts` | Mutation hook (unregister) |
| `Application/Frigorino.Web/ClientApp/src/features/settings/useUpdateUserNotificationSettings.ts` | Mutation hook (notification prefs) |
| `Application/Frigorino.Web/ClientApp/src/sw.ts` | Custom service worker: precache + FCM background push + notificationclick |

**Frontend — modified files**

| File | Change |
|---|---|
| `Application/Frigorino.Web/ClientApp/src/common/auth.ts` | Export `firebaseApp` |
| `Application/Frigorino.Web/ClientApp/src/features/settings/pages/UserSettingsPage.tsx` | Notifications toggle + lead-days + iOS hint |
| `Application/Frigorino.Web/ClientApp/src/features/inventories/components/InventorySettingsCard.tsx` | Per-inventory enable toggle |
| `Application/Frigorino.Web/ClientApp/src/features/inventories/useUpdateInventorySettings.ts` | (no change if generic) — verify call shape |
| `Application/Frigorino.Web/ClientApp/vite.config.ts` | `strategies: 'injectManifest'`, `srcDir`, `filename` |
| `Application/Frigorino.Web/ClientApp/package.json` | `workbox-precaching`, `workbox-core` dev deps |
| `Application/Frigorino.Web/ClientApp/public/locales/{en,de}/translation.json` | `settings.notifications*` keys |
| `Application/Frigorino.Web/ClientApp/src/lib/api/**` | Regenerated by `npm run api` (committed) |

**Repo-level — new/modified**

| File | Change |
|---|---|
| `.github/workflows/expiry-scan.yml` | Daily cron POST to the scan endpoint |
| `CLAUDE.md` | Fix stale "Channels runner not built yet" |
| `IDEAS.md` | Fix stale "PWA plugin not wired"; slim the entry to point at spec+plan |

**Tests — new files**

| File | Covers |
|---|---|
| `Application/Frigorino.Test/Domain/UserSettingsTests.cs` (extend existing) | New `UserSettings` fields/method |
| `Application/Frigorino.Test/Domain/InventorySettingsTests.cs` (extend existing) | New `InventorySettings` field/method |
| `Application/Frigorino.Test/Domain/NotificationEntityTests.cs` | `FcmToken` / `NotificationDispatch` factories |
| `Application/Frigorino.Test/Infrastructure/ExpiryDigestPlannerTests.cs` | Window/override/overdue/disabled/dedup/recipient filtering |
| `Application/Frigorino.Test/Infrastructure/DigestMessageComposerTests.cs` | en/de composition + day phrasing |
| `Application/Frigorino.Test/Infrastructure/FcmTokenPruningTests.cs` | Dead-token selection |
| `Application/Frigorino.Test/Infrastructure/ExpiryNotificationScanTests.cs` | End-to-end scan over InMemory DB + fake queue |
| `Application/Frigorino.Test/Infrastructure/NotificationPersistenceTests.cs` | EF persistence + unique constraint |
| `Application/Frigorino.Test/Features/MaintenanceKeyTests.cs` | Constant-time key compare |
| `Application/Frigorino.Test/Features/FcmTokenSliceTests.cs` | Token upsert/reassign/delete handlers |

---

# Phase A — Settings preference fields (reconciliation)

### Task 1: Extend `UserSettings` with notification preferences

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/UserSettings.cs`
- Test: `Application/Frigorino.Test/Domain/UserSettingsTests.cs` (extend)

- [ ] **Step 1: Write the failing tests** — append to `UserSettingsTests.cs` (inside the existing `class UserSettingsTests`):

```csharp
[Fact]
public void Create_DefaultsNotificationsOffAndLeadDaysToDefault()
{
    var settings = UserSettings.Create("user-1");

    Assert.False(settings.ExpiryNotificationsEnabled);
    Assert.Equal(UserSettings.DefaultExpiryLeadDays, settings.ExpiryLeadDays);
}

[Theory]
[InlineData(UserSettings.MinExpiryLeadDays)]
[InlineData(UserSettings.MaxExpiryLeadDays)]
public void SetExpiryNotifications_InBounds_Succeeds(int days)
{
    var settings = UserSettings.Create("user-1");

    var result = settings.SetExpiryNotifications(enabled: true, leadDays: days);

    Assert.True(result.IsSuccess);
    Assert.True(settings.ExpiryNotificationsEnabled);
    Assert.Equal(days, settings.ExpiryLeadDays);
}

[Theory]
[InlineData(UserSettings.MinExpiryLeadDays - 1)]
[InlineData(UserSettings.MaxExpiryLeadDays + 1)]
public void SetExpiryNotifications_OutOfBounds_Fails(int days)
{
    var settings = UserSettings.Create("user-1");

    var result = settings.SetExpiryNotifications(enabled: true, leadDays: days);

    Assert.True(result.IsFailed);
    Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p)
        && (string?)p == nameof(UserSettings.ExpiryLeadDays));
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~UserSettingsTests"`
Expected: BUILD FAILURE — `UserSettings` has no `ExpiryNotificationsEnabled` / `ExpiryLeadDays` / `SetExpiryNotifications` / consts.

- [ ] **Step 3: Implement the fields + method** — edit `UserSettings.cs`. Add the consts near `SupportedLanguages`, the two properties after `Language`, and the method after `SetLanguage`:

```csharp
// Lead-time bounds + default for expiry notifications. The default applies to brand-new
// settings rows and to users who never opened notification settings.
public const int DefaultExpiryLeadDays = 3;
public const int MinExpiryLeadDays = 0;
public const int MaxExpiryLeadDays = 365;
```

```csharp
// Global opt-in. Default false: the user must explicitly enable (which also drives the
// browser push-permission grant on the client).
public bool ExpiryNotificationsEnabled { get; set; }

// Fallback lead window when an inventory does not override it.
public int ExpiryLeadDays { get; set; } = DefaultExpiryLeadDays;
```

```csharp
public Result SetExpiryNotifications(bool enabled, int leadDays)
{
    if (leadDays < MinExpiryLeadDays || leadDays > MaxExpiryLeadDays)
    {
        return Result.Fail(new Error($"Lead time must be between {MinExpiryLeadDays} and {MaxExpiryLeadDays} days.")
            .WithMetadata("Property", nameof(ExpiryLeadDays)));
    }

    ExpiryNotificationsEnabled = enabled;
    ExpiryLeadDays = leadDays;
    return Result.Ok();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~UserSettingsTests"`
Expected: PASS (all UserSettings tests green).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/UserSettings.cs Application/Frigorino.Test/Domain/UserSettingsTests.cs
git commit -m "feat(settings): add expiry-notification prefs to UserSettings aggregate"
```

---

### Task 2: Extend `InventorySettings` with per-inventory enable toggle

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/InventorySettings.cs`
- Test: `Application/Frigorino.Test/Domain/InventorySettingsTests.cs` (extend)

- [ ] **Step 1: Write the failing tests** — append inside the existing `class InventorySettingsTests`:

```csharp
[Fact]
public void Create_DefaultsNotificationsEnabledTrue()
{
    var settings = InventorySettings.Create(InventoryId);

    Assert.True(settings.ExpiryNotificationsEnabled);
}

[Theory]
[InlineData(true)]
[InlineData(false)]
public void SetExpiryNotificationsEnabled_Toggles(bool enabled)
{
    var settings = InventorySettings.Create(InventoryId);

    settings.SetExpiryNotificationsEnabled(enabled);

    Assert.Equal(enabled, settings.ExpiryNotificationsEnabled);
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventorySettingsTests"`
Expected: BUILD FAILURE — no `ExpiryNotificationsEnabled` / `SetExpiryNotificationsEnabled`.

- [ ] **Step 3: Implement** — edit `InventorySettings.cs`. Add the property after `ExpiryLeadDays` and the method after `SetExpiryLeadDays`:

```csharp
// Per-inventory enable. Default true so a newly-tracked inventory is discoverable
// (a user can mute a noisy one without losing alerts elsewhere).
public bool ExpiryNotificationsEnabled { get; set; } = true;
```

```csharp
public void SetExpiryNotificationsEnabled(bool enabled)
{
    ExpiryNotificationsEnabled = enabled;
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventorySettingsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/InventorySettings.cs Application/Frigorino.Test/Domain/InventorySettingsTests.cs
git commit -m "feat(settings): add per-inventory expiry-notification toggle"
```

---

### Task 3: EF mapping, timestamp stamping, and the settings-columns migration

**Files:**
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/UserSettingsConfiguration.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/InventorySettingsConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/Migrations/*_AddExpiryNotificationPreferences.cs` (generated)
- Test: `Application/Frigorino.Test/Infrastructure/NotificationPersistenceTests.cs` (create — settings half)

- [ ] **Step 1: Write the failing persistence test** — create `NotificationPersistenceTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class NotificationPersistenceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task UserSettings_PersistsNotificationFields()
        {
            using var db = NewContext();
            var settings = UserSettings.Create("user-1");
            settings.SetExpiryNotifications(enabled: true, leadDays: 5);
            db.UserSettings.Add(settings);
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            var loaded = await db.UserSettings.SingleAsync(s => s.UserId == "user-1");

            Assert.True(loaded.ExpiryNotificationsEnabled);
            Assert.Equal(5, loaded.ExpiryLeadDays);
        }

        [Fact]
        public async Task InventorySettings_PersistsEnabledFlag()
        {
            using var db = NewContext();
            var settings = InventorySettings.Create(42);
            settings.SetExpiryNotificationsEnabled(false);
            db.InventorySettings.Add(settings);
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            var loaded = await db.InventorySettings.SingleAsync(s => s.InventoryId == 42);

            Assert.False(loaded.ExpiryNotificationsEnabled);
        }
    }
}
```

- [ ] **Step 2: Run to verify pass** (EF InMemory infers the new scalar columns automatically — these should already pass once Tasks 1–2 compiled).

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~NotificationPersistenceTests"`
Expected: PASS. (If a column is missing from the entity, this fails — a useful guard.)

- [ ] **Step 3: Add explicit EF column mappings** (relational defaults so the migration emits DB defaults). Edit `UserSettingsConfiguration.cs` — add after the `Language` property block:

```csharp
builder.Property(s => s.ExpiryNotificationsEnabled)
    .HasDefaultValue(false);

builder.Property(s => s.ExpiryLeadDays)
    .HasDefaultValue(UserSettings.DefaultExpiryLeadDays);
```

Edit `InventorySettingsConfiguration.cs` — add after the `ExpiryLeadDays` property:

```csharp
builder.Property(s => s.ExpiryNotificationsEnabled)
    .HasDefaultValue(true);
```

Add the required using to both files if not present: `using Frigorino.Domain.Entities;` (already present in both).

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AddExpiryNotificationPreferences \
  --project Application/Frigorino.Infrastructure \
  --startup-project Application/Frigorino.Web
```
Expected: a new `*_AddExpiryNotificationPreferences.cs` whose `Up` calls `migrationBuilder.AddColumn<bool>("ExpiryNotificationsEnabled", "UserSettings", defaultValue: false)`, `AddColumn<int>("ExpiryLeadDays", "UserSettings", defaultValue: 3)`, and `AddColumn<bool>("ExpiryNotificationsEnabled", "InventorySettings", defaultValue: true)`.

- [ ] **Step 5: Inspect the generated migration** to confirm exactly those three `AddColumn` calls with those defaults and matching `Down` `DropColumn`s. No other tables touched.

- [ ] **Step 6: Build + run the persistence tests again**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~NotificationPersistenceTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/Configurations/UserSettingsConfiguration.cs \
        Application/Frigorino.Infrastructure/EntityFramework/Configurations/InventorySettingsConfiguration.cs \
        Application/Frigorino.Infrastructure/Migrations \
        Application/Frigorino.Test/Infrastructure/NotificationPersistenceTests.cs
git commit -m "feat(settings): map + migrate expiry-notification preference columns"
```

---

### Task 4: Surface user notification prefs through the user-settings slices

**Files:**
- Modify: `Application/Frigorino.Features/Me/Settings/UserSettingsResponse.cs`
- Modify: `Application/Frigorino.Features/Me/Settings/GetUserSettings.cs`
- Modify: `Application/Frigorino.Features/Me/Settings/UpdateUserSettings.cs`
- Create: `Application/Frigorino.Features/Me/Settings/UpdateUserNotificationSettings.cs`

> **Design note:** Language and notification prefs are independent controls. Rather than make `UpdateUserSettings` a partial-update (which is ambiguous because `Language = null` is a *meaningful* value), notification prefs get their own write endpoint `PUT /api/me/settings/notifications`. The shared **read** model (`UserSettingsResponse`) carries all fields.

- [ ] **Step 1: Extend the response record** — replace the body of `UserSettingsResponse.cs`:

```csharp
namespace Frigorino.Features.Me.Settings
{
    public sealed record UserSettingsResponse(
        string? Language,
        bool ExpiryNotificationsEnabled,
        int ExpiryLeadDays);
}
```

- [ ] **Step 2: Update `GetUserSettings.cs`** — change the projection + default. Replace the `Handle` body's query/return:

```csharp
var response = await db.UserSettings
    .Where(s => s.UserId == currentUser.UserId)
    .Select(s => new UserSettingsResponse(
        s.Language, s.ExpiryNotificationsEnabled, s.ExpiryLeadDays))
    .FirstOrDefaultAsync(ct);

return TypedResults.Ok(response
    ?? new UserSettingsResponse(null, false, UserSettings.DefaultExpiryLeadDays));
```

Add `using Frigorino.Domain.Entities;` to the file's usings.

- [ ] **Step 3: Update `UpdateUserSettings.cs` return** — the language PUT must return the full (now 3-field) response. Replace the final return:

```csharp
await db.SaveChangesAsync(ct);
return TypedResults.Ok(new UserSettingsResponse(
    settings.Language, settings.ExpiryNotificationsEnabled, settings.ExpiryLeadDays));
```

- [ ] **Step 4: Create the notifications write slice** — `UpdateUserNotificationSettings.cs`:

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
    public sealed record UpdateUserNotificationSettingsRequest(
        bool ExpiryNotificationsEnabled,
        int ExpiryLeadDays);

    public static class UpdateUserNotificationSettingsEndpoint
    {
        public static IEndpointRouteBuilder MapUpdateUserNotificationSettings(this IEndpointRouteBuilder app)
        {
            app.MapPut("/settings/notifications", Handle)
               .WithName("UpdateUserNotificationSettings")
               .Produces<UserSettingsResponse>()
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<UserSettingsResponse>, ValidationProblem>> Handle(
            UpdateUserNotificationSettingsRequest request,
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

            var result = settings.SetExpiryNotifications(
                request.ExpiryNotificationsEnabled, request.ExpiryLeadDays);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new UserSettingsResponse(
                settings.Language, settings.ExpiryNotificationsEnabled, settings.ExpiryLeadDays));
        }
    }
}
```

(Endpoint registration in `Program.cs` happens in Task 15.)

- [ ] **Step 5: Build the Features project**

Run: `dotnet build Application/Frigorino.Features`
Expected: SUCCESS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Me/Settings/
git commit -m "feat(settings): expose user expiry-notification prefs via slices"
```

---

### Task 5: Surface the per-inventory enable toggle through the inventory-settings slices

**Files:**
- Modify: `Application/Frigorino.Features/Inventories/Settings/InventorySettingsResponse.cs`
- Modify: `Application/Frigorino.Features/Inventories/Settings/GetInventorySettings.cs`
- Modify: `Application/Frigorino.Features/Inventories/Settings/UpdateInventorySettings.cs`

> **Design note:** The inventory card has two controls (enable + lead-days override). The bool has no null-ambiguity, so the existing single `PUT` request is extended to carry both fields; the card sends both on any change.

- [ ] **Step 1: Extend the response** — replace the body of `InventorySettingsResponse.cs`:

```csharp
namespace Frigorino.Features.Inventories.Settings
{
    public sealed record InventorySettingsResponse(
        bool ExpiryNotificationsEnabled,
        int? ExpiryLeadDays);
}
```

- [ ] **Step 2: Update `GetInventorySettings.cs`** — projection + default. Replace the query/return at the end of `Handle`:

```csharp
var response = await db.InventorySettings
    .Where(s => s.InventoryId == inventoryId)
    .Select(s => new InventorySettingsResponse(s.ExpiryNotificationsEnabled, s.ExpiryLeadDays))
    .FirstOrDefaultAsync(ct);

// No row ⇒ enabled by default, inherit lead-days.
return TypedResults.Ok(response ?? new InventorySettingsResponse(true, null));
```

- [ ] **Step 3: Update `UpdateInventorySettings.cs`** — extend request + set both fields. Replace the request record:

```csharp
public sealed record UpdateInventorySettingsRequest(
    bool ExpiryNotificationsEnabled,
    int? ExpiryLeadDays);
```

In `Handle`, replace the block from `var result = settings.SetExpiryLeadDays(...)` through the final return:

```csharp
settings.SetExpiryNotificationsEnabled(request.ExpiryNotificationsEnabled);
var result = settings.SetExpiryLeadDays(request.ExpiryLeadDays);
if (result.IsFailed)
{
    return result.ToValidationProblem();
}

await db.SaveChangesAsync(ct);
return TypedResults.Ok(new InventorySettingsResponse(
    settings.ExpiryNotificationsEnabled, settings.ExpiryLeadDays));
```

- [ ] **Step 4: Build**

Run: `dotnet build Application/Frigorino.Features`
Expected: SUCCESS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features/Inventories/Settings/
git commit -m "feat(settings): expose per-inventory notification toggle via slices"
```

---

# Phase B — Notification domain + infrastructure

### Task 6: `FcmToken` and `NotificationDispatch` entities

**Files:**
- Create: `Application/Frigorino.Domain/Entities/FcmToken.cs`
- Create: `Application/Frigorino.Domain/Entities/NotificationDispatch.cs`
- Test: `Application/Frigorino.Test/Domain/NotificationEntityTests.cs`

- [ ] **Step 1: Write the failing tests** — `NotificationEntityTests.cs`:

```csharp
using Frigorino.Domain.Entities;

namespace Frigorino.Test.Domain
{
    public class NotificationEntityTests
    {
        [Fact]
        public void FcmToken_Create_SetsUserAndToken()
        {
            var token = FcmToken.Create("user-1", "device-token-abc");

            Assert.Equal("user-1", token.UserId);
            Assert.Equal("device-token-abc", token.Token);
        }

        [Fact]
        public void NotificationDispatch_Create_SetsKey()
        {
            var sentOn = new DateOnly(2026, 6, 1);

            var dispatch = NotificationDispatch.Create("user-1", householdId: 7, sentOn);

            Assert.Equal("user-1", dispatch.UserId);
            Assert.Equal(7, dispatch.HouseholdId);
            Assert.Equal(sentOn, dispatch.SentOn);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~NotificationEntityTests"`
Expected: BUILD FAILURE — entities don't exist.

- [ ] **Step 3: Create `FcmToken.cs`**

```csharp
namespace Frigorino.Domain.Entities
{
    // One row per device/browser registration. A user has many. Globally unique by Token
    // (a registration string identifies one device); re-registering reassigns it to the
    // current user. Timestamps are stamped centrally in ApplicationDbContext.SaveChangesAsync.
    public class FcmToken
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastSeenAt { get; set; }

        // Navigation property
        public User User { get; set; } = null!;

        public static FcmToken Create(string userId, string token)
        {
            return new FcmToken { UserId = userId, Token = token };
        }
    }
}
```

- [ ] **Step 4: Create `NotificationDispatch.cs`**

```csharp
namespace Frigorino.Domain.Entities
{
    // De-dup ledger: at most one digest per (user, household, day). A unique index on
    // (UserId, HouseholdId, SentOn) makes the scan idempotent across re-triggers / double fires.
    public class NotificationDispatch
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int HouseholdId { get; set; }
        public DateOnly SentOn { get; set; }

        public static NotificationDispatch Create(string userId, int householdId, DateOnly sentOn)
        {
            return new NotificationDispatch
            {
                UserId = userId,
                HouseholdId = householdId,
                SentOn = sentOn,
            };
        }
    }
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~NotificationEntityTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Domain/Entities/FcmToken.cs \
        Application/Frigorino.Domain/Entities/NotificationDispatch.cs \
        Application/Frigorino.Test/Domain/NotificationEntityTests.cs
git commit -m "feat(notifications): add FcmToken + NotificationDispatch entities"
```

---

### Task 7: EF mappings, DbSets, timestamp stamping, and the notification-tables migration

**Files:**
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/FcmTokenConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/NotificationDispatchConfiguration.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/ApplicationDbContext.cs`
- Create: `Application/Frigorino.Infrastructure/Migrations/*_AddNotificationTables.cs` (generated)
- Test: `Application/Frigorino.Test/Infrastructure/NotificationPersistenceTests.cs` (extend)

- [ ] **Step 1: Write the failing tests** — append to `NotificationPersistenceTests.cs`:

```csharp
[Fact]
public async Task FcmToken_SaveChanges_StampsTimestamps()
{
    using var db = NewContext();
    var token = FcmToken.Create("user-1", "tok-1");
    db.FcmTokens.Add(token);
    await db.SaveChangesAsync();

    Assert.NotEqual(default, token.CreatedAt);
    Assert.NotEqual(default, token.LastSeenAt);
}

[Fact]
public async Task NotificationDispatch_Roundtrips()
{
    using var db = NewContext();
    var dispatch = NotificationDispatch.Create("user-1", 7, new DateOnly(2026, 6, 1));
    db.NotificationDispatches.Add(dispatch);
    await db.SaveChangesAsync();

    db.ChangeTracker.Clear();
    var loaded = await db.NotificationDispatches.SingleAsync();

    Assert.Equal("user-1", loaded.UserId);
    Assert.Equal(7, loaded.HouseholdId);
    Assert.Equal(new DateOnly(2026, 6, 1), loaded.SentOn);
}
```

Add `using` for the entities if not already present (the file already imports `Frigorino.Domain.Entities`).

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~NotificationPersistenceTests"`
Expected: BUILD FAILURE — `db.FcmTokens` / `db.NotificationDispatches` don't exist.

- [ ] **Step 3: Add DbSets + timestamp stamping** — edit `ApplicationDbContext.cs`. Add after the `InventorySettings` DbSet:

```csharp
public DbSet<FcmToken> FcmTokens { get; set; }
public DbSet<NotificationDispatch> NotificationDispatches { get; set; }
```

In `SaveChangesAsync`, inside the `EntityState.Added` block (after the `InventorySettings` stamping):

```csharp
if (entry.Entity is FcmToken fcmTokenAdded && fcmTokenAdded.CreatedAt == default)
{
    fcmTokenAdded.CreatedAt = now;
    fcmTokenAdded.LastSeenAt = now;
}
```

And inside the `EntityState.Modified` block (after the `InventorySettings` stamping):

```csharp
if (entry.Entity is FcmToken fcmTokenModified)
{
    fcmTokenModified.LastSeenAt = now;
}
```

(`NotificationDispatch` has no timestamps — nothing to stamp.)

- [ ] **Step 4: Create `FcmTokenConfiguration.cs`**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class FcmTokenConfiguration : IEntityTypeConfiguration<FcmToken>
    {
        public void Configure(EntityTypeBuilder<FcmToken> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.UserId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(t => t.Token)
                .HasMaxLength(512)
                .IsRequired();

            builder.Property(t => t.CreatedAt).IsRequired();
            builder.Property(t => t.LastSeenAt).IsRequired();

            // A device token is globally unique; re-registration reassigns the owner.
            builder.HasIndex(t => t.Token).IsUnique();

            builder.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .HasPrincipalKey<User>(u => u.ExternalId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 5: Create `NotificationDispatchConfiguration.cs`**

```csharp
using Frigorino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Frigorino.Infrastructure.EntityFramework.Configurations
{
    public class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
    {
        public void Configure(EntityTypeBuilder<NotificationDispatch> builder)
        {
            builder.HasKey(d => d.Id);

            builder.Property(d => d.UserId)
                .HasMaxLength(128)
                .IsRequired();

            // At most one digest per user-household-day.
            builder.HasIndex(d => new { d.UserId, d.HouseholdId, d.SentOn }).IsUnique();
        }
    }
}
```

- [ ] **Step 6: Generate the migration**

Run:
```bash
dotnet ef migrations add AddNotificationTables \
  --project Application/Frigorino.Infrastructure \
  --startup-project Application/Frigorino.Web
```
Expected: `Up` creates `FcmTokens` (Id identity PK, UserId, Token, CreatedAt, LastSeenAt, FK→Users cascade, unique index on Token) and `NotificationDispatches` (Id identity PK, UserId, HouseholdId, SentOn date, unique composite index).

- [ ] **Step 7: Inspect the generated migration** — confirm two `CreateTable`s, the unique indexes, the FK on `FcmTokens.UserId → Users.ExternalId` cascade, and `SentOn` mapped as `date`. No other tables touched.

- [ ] **Step 8: Run the persistence tests**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~NotificationPersistenceTests"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/ \
        Application/Frigorino.Infrastructure/Migrations/ \
        Application/Frigorino.Test/Infrastructure/NotificationPersistenceTests.cs
git commit -m "feat(notifications): map + migrate FcmToken and NotificationDispatch tables"
```

---

### Task 8: `INotificationSender` port + `ExpiryDigestNotification` payload

**Files:**
- Create: `Application/Frigorino.Domain/Notifications/ExpiryDigestNotification.cs`
- Create: `Application/Frigorino.Domain/Interfaces/INotificationSender.cs`

- [ ] **Step 1: Create the payload record** — `ExpiryDigestNotification.cs`:

```csharp
namespace Frigorino.Domain.Notifications
{
    // Composed, localized push payload. DeepLinkPath is a client-relative route (e.g. "/inventories")
    // the service worker opens on notification click.
    public sealed record ExpiryDigestNotification(string Title, string Body, string DeepLinkPath);
}
```

- [ ] **Step 2: Create the port** — `INotificationSender.cs` (BCL + domain types only, so it stays in Domain):

```csharp
using Frigorino.Domain.Notifications;

namespace Frigorino.Domain.Interfaces
{
    // Delivery boundary. The FCM adapter (Infrastructure) resolves the user's tokens, sends the
    // payload to each, and prunes any the provider reports as permanently invalid.
    public interface INotificationSender
    {
        Task SendDigestAsync(string userId, ExpiryDigestNotification notification, CancellationToken ct);
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Application/Frigorino.Domain`
Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Domain/Notifications/ExpiryDigestNotification.cs \
        Application/Frigorino.Domain/Interfaces/INotificationSender.cs
git commit -m "feat(notifications): add INotificationSender port + digest payload"
```

---

### Task 9: `ExpiryDigestPlanner` (pure selection logic)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Notifications/ExpiryDigestPlanner.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ExpiryDigestPlannerTests.cs`

> **Design note:** Mirrors the `CheckedItemPurge` precedent — a pure static function the DB-touching scan calls, so all selection rules are unit-tested without EF. The effective window is per `(inventory, recipient)` because each recipient may have a different `UserLeadDays` fallback.

- [ ] **Step 1: Write the failing tests** — `ExpiryDigestPlannerTests.cs`:

```csharp
using Frigorino.Infrastructure.Notifications;

namespace Frigorino.Test.Infrastructure
{
    public class ExpiryDigestPlannerTests
    {
        private static readonly DateOnly Today = new(2026, 6, 1);

        private static ExpiryCandidate Item(int inventoryId, int householdId, string text, int daysUntil) =>
            new(inventoryId, householdId, text, Today.AddDays(daysUntil));

        [Fact]
        public void IncludesItemsWithinUserDefaultLeadDays_AndExcludesBeyond()
        {
            var candidates = new[]
            {
                Item(1, 10, "Milk", 2),    // within 3 ⇒ include
                Item(1, 10, "Flour", 9),   // beyond 3 ⇒ exclude
            };
            var inventories = new Dictionary<int, InventoryNotificationSetting>(); // no rows ⇒ enabled, inherit
            var recipients = new[] { new DigestRecipient("u1", 10, UserLeadDays: 3, Language: "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates, inventories, recipients,
                alreadyDispatched: new HashSet<(string, int)>(), Today);

            var plan = Assert.Single(plans);
            Assert.Equal("u1", plan.UserId);
            var line = Assert.Single(plan.Lines);
            Assert.Equal("Milk", line.Text);
            Assert.Equal(2, line.DaysUntil);
        }

        [Fact]
        public void InventoryOverrideWidensWindow()
        {
            var candidates = new[] { Item(1, 10, "Frozen peas", 6) };
            var inventories = new Dictionary<int, InventoryNotificationSetting>
            {
                [1] = new InventoryNotificationSetting(Enabled: true, LeadDays: 7),
            };
            var recipients = new[] { new DigestRecipient("u1", 10, UserLeadDays: 3, Language: "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates, inventories, recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Single(Assert.Single(plans).Lines);
        }

        [Fact]
        public void DisabledInventoryIsExcluded()
        {
            var candidates = new[] { Item(1, 10, "Milk", 1) };
            var inventories = new Dictionary<int, InventoryNotificationSetting>
            {
                [1] = new InventoryNotificationSetting(Enabled: false, LeadDays: null),
            };
            var recipients = new[] { new DigestRecipient("u1", 10, UserLeadDays: 3, Language: "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates, inventories, recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Empty(plans);
        }

        [Fact]
        public void OverdueItemsAreIncluded()
        {
            var candidates = new[] { Item(1, 10, "Yogurt", -2) };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Equal(-2, Assert.Single(Assert.Single(plans).Lines).DaysUntil);
        }

        [Fact]
        public void AlreadyDispatchedRecipientIsSkipped()
        {
            var candidates = new[] { Item(1, 10, "Milk", 1) };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };
            var dispatched = new HashSet<(string, int)> { ("u1", 10) };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients, dispatched, Today);

            Assert.Empty(plans);
        }

        [Fact]
        public void RecipientWithNoMatchingItemsGetsNoPlan()
        {
            var candidates = new[] { Item(1, 10, "Flour", 30) };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients,
                new HashSet<(string, int)>(), Today);

            Assert.Empty(plans);
        }

        [Fact]
        public void LinesAreSortedByExpiry()
        {
            var candidates = new[]
            {
                Item(1, 10, "Later", 3),
                Item(1, 10, "Sooner", 1),
            };
            var recipients = new[] { new DigestRecipient("u1", 10, 3, "en") };

            var plans = ExpiryDigestPlanner.Plan(candidates,
                new Dictionary<int, InventoryNotificationSetting>(), recipients,
                new HashSet<(string, int)>(), Today);

            var lines = Assert.Single(plans).Lines;
            Assert.Equal("Sooner", lines[0].Text);
            Assert.Equal("Later", lines[1].Text);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryDigestPlannerTests"`
Expected: BUILD FAILURE — planner + records don't exist.

- [ ] **Step 3: Implement `ExpiryDigestPlanner.cs`**

```csharp
namespace Frigorino.Infrastructure.Notifications
{
    // Input rows (plain — no EF types, so this is pure + unit-testable).
    public sealed record ExpiryCandidate(int InventoryId, int HouseholdId, string Text, DateOnly ExpiryDate);

    // Missing inventory in the map ⇒ enabled, inherit (LeadDays null).
    public sealed record InventoryNotificationSetting(bool Enabled, int? LeadDays);

    // A member who is opted-in AND has at least one active token, scoped to one household.
    public sealed record DigestRecipient(string UserId, int HouseholdId, int UserLeadDays, string? Language);

    public sealed record DigestLine(string Text, DateOnly ExpiryDate, int DaysUntil);

    public sealed record DigestPlan(string UserId, int HouseholdId, string? Language, IReadOnlyList<DigestLine> Lines);

    public static class ExpiryDigestPlanner
    {
        public static IReadOnlyList<DigestPlan> Plan(
            IReadOnlyCollection<ExpiryCandidate> candidates,
            IReadOnlyDictionary<int, InventoryNotificationSetting> inventorySettings,
            IReadOnlyCollection<DigestRecipient> recipients,
            HashSet<(string UserId, int HouseholdId)> alreadyDispatched,
            DateOnly today)
        {
            var plans = new List<DigestPlan>();

            foreach (var recipient in recipients)
            {
                if (alreadyDispatched.Contains((recipient.UserId, recipient.HouseholdId)))
                {
                    continue;
                }

                var lines = new List<DigestLine>();
                foreach (var candidate in candidates)
                {
                    if (candidate.HouseholdId != recipient.HouseholdId)
                    {
                        continue;
                    }

                    var hasSetting = inventorySettings.TryGetValue(candidate.InventoryId, out var setting);
                    var enabled = !hasSetting || setting!.Enabled;
                    if (!enabled)
                    {
                        continue;
                    }

                    var effectiveLeadDays = (hasSetting ? setting!.LeadDays : null) ?? recipient.UserLeadDays;
                    var daysUntil = candidate.ExpiryDate.DayNumber - today.DayNumber;
                    if (daysUntil <= effectiveLeadDays)
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
                plans.Add(new DigestPlan(recipient.UserId, recipient.HouseholdId, recipient.Language, ordered));
            }

            return plans;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryDigestPlannerTests"`
Expected: PASS (all 7).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/ExpiryDigestPlanner.cs \
        Application/Frigorino.Test/Infrastructure/ExpiryDigestPlannerTests.cs
git commit -m "feat(notifications): add pure ExpiryDigestPlanner selection logic"
```

---

### Task 10: `DigestMessageComposer` (pure en/de composition)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Notifications/DigestMessageComposer.cs`
- Test: `Application/Frigorino.Test/Infrastructure/DigestMessageComposerTests.cs`

> **Design note:** Server-side composition uses the recipient's stored `UserSettings.Language` with a tiny two-language template (no i18next on the server). DeepLinkPath is `/inventories` (the household inventory overview — the digest spans a household's inventories).

- [ ] **Step 1: Write the failing tests** — `DigestMessageComposerTests.cs`:

```csharp
using Frigorino.Infrastructure.Notifications;

namespace Frigorino.Test.Infrastructure
{
    public class DigestMessageComposerTests
    {
        private static readonly DateOnly Today = new(2026, 6, 1);

        private static DigestPlan PlanWith(string? language, params (string text, int days)[] items)
        {
            var lines = items.Select(i => new DigestLine(i.text, Today.AddDays(i.days), i.days)).ToList();
            return new DigestPlan("u1", 10, language, lines);
        }

        [Fact]
        public void English_TitleCountsItems_BodyListsNames()
        {
            var plan = PlanWith("en", ("Milk", 1), ("Yogurt", 2));

            var msg = DigestMessageComposer.Compose(plan, Today);

            Assert.Contains("2", msg.Title);
            Assert.Contains("Milk", msg.Body);
            Assert.Contains("Yogurt", msg.Body);
            Assert.Equal("/inventories", msg.DeepLinkPath);
        }

        [Fact]
        public void German_UsesGermanCopy()
        {
            var plan = PlanWith("de", ("Milch", 0));

            var msg = DigestMessageComposer.Compose(plan, Today);

            Assert.Contains("Artikel", msg.Title); // German title template
            Assert.Contains("heute", msg.Body);    // 0 days ⇒ "heute"
        }

        [Fact]
        public void NullLanguage_FallsBackToEnglish()
        {
            var plan = PlanWith(null, ("Milk", 1));

            var msg = DigestMessageComposer.Compose(plan, Today);

            Assert.Contains("tomorrow", msg.Body); // 1 day ⇒ "tomorrow"
        }

        [Fact]
        public void OverdueItem_IsPhrasedAsOverdue()
        {
            var plan = PlanWith("en", ("Yogurt", -1));

            var msg = DigestMessageComposer.Compose(plan, Today);

            Assert.Contains("overdue", msg.Body);
        }

        [Fact]
        public void ManyItems_AreTruncatedWithMore()
        {
            var plan = PlanWith("en",
                ("A", 1), ("B", 1), ("C", 1), ("D", 1), ("E", 1));

            var msg = DigestMessageComposer.Compose(plan, Today);

            Assert.Contains("more", msg.Body); // "+N more"
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~DigestMessageComposerTests"`
Expected: BUILD FAILURE.

- [ ] **Step 3: Implement `DigestMessageComposer.cs`**

```csharp
using Frigorino.Domain.Notifications;

namespace Frigorino.Infrastructure.Notifications
{
    public static class DigestMessageComposer
    {
        private const int MaxNamesInBody = 3;
        private const string DeepLinkPath = "/inventories";

        public static ExpiryDigestNotification Compose(DigestPlan plan, DateOnly today)
        {
            var german = string.Equals(plan.Language, "de", StringComparison.OrdinalIgnoreCase);
            var count = plan.Lines.Count;

            var title = german
                ? $"{count} Artikel laufen bald ab"
                : $"{count} item{(count == 1 ? "" : "s")} expiring soon";

            var named = plan.Lines
                .Take(MaxNamesInBody)
                .Select(l => $"{l.Text} {Phrase(l.DaysUntil, german)}");

            var body = string.Join(german ? ", " : ", ", named);

            var remaining = count - MaxNamesInBody;
            if (remaining > 0)
            {
                body += german ? $" und {remaining} weitere" : $", +{remaining} more";
            }

            return new ExpiryDigestNotification(title, body, DeepLinkPath);
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

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~DigestMessageComposerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/DigestMessageComposer.cs \
        Application/Frigorino.Test/Infrastructure/DigestMessageComposerTests.cs
git commit -m "feat(notifications): add pure DigestMessageComposer (en/de)"
```

---

### Task 11: `FcmTokenPruning` (pure) + FCM and log-only senders

**Files:**
- Create: `Application/Frigorino.Infrastructure/Notifications/FcmTokenPruning.cs`
- Create: `Application/Frigorino.Infrastructure/Notifications/FcmNotificationSender.cs`
- Create: `Application/Frigorino.Infrastructure/Notifications/LogOnlyNotificationSender.cs`
- Test: `Application/Frigorino.Test/Infrastructure/FcmTokenPruningTests.cs`

> **Design note:** Only the *which-tokens-are-dead* decision is pure-tested (the precedent: `CheckedItemPurge`). The Firebase Admin `FirebaseMessaging` call is thin glue verified manually in Phase D (it has no fakeable seam without an extra wrapper, which YAGNI rejects for one call site).

- [ ] **Step 1: Write the failing test** — `FcmTokenPruningTests.cs`:

```csharp
using Frigorino.Infrastructure.Notifications;

namespace Frigorino.Test.Infrastructure
{
    public class FcmTokenPruningTests
    {
        [Fact]
        public void SelectsOnlyTokensReportedUnregistered()
        {
            var results = new[]
            {
                new FcmSendOutcome("tok-ok", Success: true, IsUnregistered: false),
                new FcmSendOutcome("tok-dead", Success: false, IsUnregistered: true),
                new FcmSendOutcome("tok-transient", Success: false, IsUnregistered: false),
            };

            var dead = FcmTokenPruning.SelectDeadTokens(results);

            Assert.Equal(new[] { "tok-dead" }, dead);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~FcmTokenPruningTests"`
Expected: BUILD FAILURE.

- [ ] **Step 3: Implement `FcmTokenPruning.cs`**

```csharp
namespace Frigorino.Infrastructure.Notifications
{
    // Outcome of sending to one token. IsUnregistered = the provider says the token is
    // permanently invalid (Unregistered / InvalidArgument) and should be deleted; a plain
    // failure (transient) is left alone.
    public sealed record FcmSendOutcome(string Token, bool Success, bool IsUnregistered);

    public static class FcmTokenPruning
    {
        public static IReadOnlyList<string> SelectDeadTokens(IEnumerable<FcmSendOutcome> outcomes)
        {
            return outcomes
                .Where(o => o.IsUnregistered)
                .Select(o => o.Token)
                .ToList();
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~FcmTokenPruningTests"`
Expected: PASS.

- [ ] **Step 5: Implement `FcmNotificationSender.cs`**

```csharp
using FirebaseAdmin.Messaging;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Notifications;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Notifications
{
    // FCM adapter. Sends a data-only message (the service worker renders it) to every active
    // token the user has, then prunes any the provider reports as permanently unregistered.
    public class FcmNotificationSender : INotificationSender
    {
        private readonly ApplicationDbContext _db;
        private readonly FirebaseMessaging _messaging;
        private readonly ILogger<FcmNotificationSender> _logger;

        public FcmNotificationSender(
            ApplicationDbContext db,
            FirebaseMessaging messaging,
            ILogger<FcmNotificationSender> logger)
        {
            _db = db;
            _messaging = messaging;
            _logger = logger;
        }

        public async Task SendDigestAsync(string userId, ExpiryDigestNotification notification, CancellationToken ct)
        {
            var tokens = await _db.FcmTokens
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .ToListAsync(ct);

            if (tokens.Count == 0)
            {
                return;
            }

            var message = new MulticastMessage
            {
                Tokens = tokens,
                // Data-only: the SW composes/show the notification (avoids duplicate auto-display).
                Data = new Dictionary<string, string>
                {
                    ["title"] = notification.Title,
                    ["body"] = notification.Body,
                    ["link"] = notification.DeepLinkPath,
                },
            };

            var response = await _messaging.SendEachForMulticastAsync(message, ct);

            var outcomes = new List<FcmSendOutcome>(tokens.Count);
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var r = response.Responses[i];
                var unregistered = r.Exception?.MessagingErrorCode
                    is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument;
                outcomes.Add(new FcmSendOutcome(tokens[i], r.IsSuccess, unregistered));
            }

            var dead = FcmTokenPruning.SelectDeadTokens(outcomes);
            if (dead.Count > 0)
            {
                await _db.FcmTokens.Where(t => dead.Contains(t.Token)).ExecuteDeleteAsync(ct);
                _logger.LogInformation("Pruned {Count} unregistered FCM token(s) for user {UserId}.", dead.Count, userId);
            }
        }
    }
}
```

- [ ] **Step 6: Implement `LogOnlyNotificationSender.cs`** (fallback when Firebase isn't initialized — DevAuth / IntegrationTest / build-time):

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Notifications
{
    // Used when Firebase Admin isn't initialized (DevAuth bypass, integration tests). Lets the
    // scan run end-to-end and logs what *would* have been sent, without a real FirebaseApp.
    public class LogOnlyNotificationSender : INotificationSender
    {
        private readonly ILogger<LogOnlyNotificationSender> _logger;

        public LogOnlyNotificationSender(ILogger<LogOnlyNotificationSender> logger)
        {
            _logger = logger;
        }

        public Task SendDigestAsync(string userId, ExpiryDigestNotification notification, CancellationToken ct)
        {
            _logger.LogInformation(
                "[LogOnlyNotificationSender] Would send to {UserId}: \"{Title}\" — {Body}",
                userId, notification.Title, notification.Body);
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 7: Build Infrastructure**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: SUCCESS (confirms the `FirebaseAdmin.Messaging` API surface compiles against v3.5.0).

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/FcmTokenPruning.cs \
        Application/Frigorino.Infrastructure/Notifications/FcmNotificationSender.cs \
        Application/Frigorino.Infrastructure/Notifications/LogOnlyNotificationSender.cs \
        Application/Frigorino.Test/Infrastructure/FcmTokenPruningTests.cs
git commit -m "feat(notifications): add FCM + log-only senders with token pruning"
```

---

### Task 12: `ExpiryNotificationScan` (the scan job)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Notifications/ExpiryNotificationScan.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ExpiryNotificationScanTests.cs`

> **Design note:** The scan materializes inputs via EF, calls the pure planner + composer, enqueues one send per plan onto `IBackgroundTaskQueue`, and writes the `NotificationDispatch` ledger row **only after a successful enqueue** (so a dropped enqueue leaves no ledger row and re-derives next day). `today` is `DateOnly.FromDateTime(DateTime.UtcNow)` — the v1 single-reference-TZ simplification.

- [ ] **Step 1: Write the failing tests** — `ExpiryNotificationScanTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Notifications;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class ExpiryNotificationScanTests
    {
        // Captures enqueued work items so the test can assert how many sends were scheduled.
        private sealed class CapturingQueue : IBackgroundTaskQueue
        {
            public List<Func<IServiceProvider, CancellationToken, Task>> Items { get; } = new();
            public bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work)
            {
                Items.Add(work);
                return true;
            }
        }

        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        // Seeds: one user, one household membership, one inventory with one item expiring in `daysUntil`.
        private static async Task SeedAsync(
            ApplicationDbContext db, DateOnly today, int daysUntil,
            bool userEnabled = true, bool hasToken = true)
        {
            db.Users.Add(new User { ExternalId = "u1", Name = "U", Email = "u@x.io" });
            db.Households.Add(new Household { Id = 10, Name = "H", CreatedByUserId = "u1" });
            db.UserHouseholds.Add(new UserHousehold { UserId = "u1", HouseholdId = 10, Role = HouseholdRole.Owner, IsActive = true });
            db.Inventories.Add(new Inventory { Id = 100, Name = "Fridge", HouseholdId = 10, CreatedByUserId = "u1", IsActive = true });
            db.InventoryItems.Add(new InventoryItem { Id = 1000, InventoryId = 100, Text = "Milk", ExpiryDate = today.AddDays(daysUntil), IsActive = true });

            var userSettings = UserSettings.Create("u1");
            userSettings.SetExpiryNotifications(userEnabled, leadDays: 3);
            db.UserSettings.Add(userSettings);

            if (hasToken)
            {
                db.FcmTokens.Add(FcmToken.Create("u1", "tok-1"));
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        private static ExpiryNotificationScan NewScan(ApplicationDbContext db, IBackgroundTaskQueue queue) =>
            new(db, queue, NullLogger<ExpiryNotificationScan>.Instance);

        [Fact]
        public async Task EnqueuesOneSend_AndWritesLedger_ForEligibleRecipient()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2);
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Single(queue.Items);
            Assert.Equal(1, await db.NotificationDispatches.CountAsync(d => d.UserId == "u1" && d.HouseholdId == 10 && d.SentOn == today));
        }

        [Fact]
        public async Task SkipsRecipient_WhenAlreadyDispatchedToday()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2);
            db.NotificationDispatches.Add(NotificationDispatch.Create("u1", 10, today));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Empty(queue.Items);
        }

        [Fact]
        public async Task SkipsRecipient_WhenUserDisabled()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2, userEnabled: false);
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Empty(queue.Items);
        }

        [Fact]
        public async Task SkipsRecipient_WhenNoToken()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 2, hasToken: false);
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Empty(queue.Items);
        }

        [Fact]
        public async Task SkipsItem_BeyondLeadWindow()
        {
            var today = new DateOnly(2026, 6, 1);
            using var db = NewContext();
            await SeedAsync(db, today, daysUntil: 30);
            var queue = new CapturingQueue();

            await NewScan(db, queue).RunAsync(today, CancellationToken.None);

            Assert.Empty(queue.Items);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryNotificationScanTests"`
Expected: BUILD FAILURE — `ExpiryNotificationScan` doesn't exist.

- [ ] **Step 3: Implement `ExpiryNotificationScan.cs`**

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Notifications
{
    // Invoked by the secured /internal/expiry-scan endpoint (and re-runnable idempotently).
    public class ExpiryNotificationScan
    {
        private readonly ApplicationDbContext _db;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<ExpiryNotificationScan> _logger;

        public ExpiryNotificationScan(
            ApplicationDbContext db,
            IBackgroundTaskQueue queue,
            ILogger<ExpiryNotificationScan> logger)
        {
            _db = db;
            _queue = queue;
            _logger = logger;
        }

        public async Task RunAsync(DateOnly today, CancellationToken ct)
        {
            // 1. Candidate items: active, with an expiry date.
            var candidates = await _db.InventoryItems
                .Where(i => i.IsActive && i.ExpiryDate != null && i.Inventory.IsActive)
                .Select(i => new ExpiryCandidate(
                    i.InventoryId, i.Inventory.HouseholdId, i.Text, i.ExpiryDate!.Value))
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                _logger.LogInformation("Expiry scan: no candidate items.");
                return;
            }

            var householdIds = candidates.Select(c => c.HouseholdId).Distinct().ToList();
            var inventoryIds = candidates.Select(c => c.InventoryId).Distinct().ToList();

            // 2. Inventory settings for those inventories.
            var inventorySettings = await _db.InventorySettings
                .Where(s => inventoryIds.Contains(s.InventoryId))
                .ToDictionaryAsync(
                    s => s.InventoryId,
                    s => new InventoryNotificationSetting(s.ExpiryNotificationsEnabled, s.ExpiryLeadDays),
                    ct);

            // 3. Recipients: active members of those households who opted in AND have >=1 token.
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

            // 4. Already-dispatched keys for today.
            var dispatchedToday = await _db.NotificationDispatches
                .Where(d => d.SentOn == today)
                .Select(d => new { d.UserId, d.HouseholdId })
                .ToListAsync(ct);
            var alreadyDispatched = dispatchedToday
                .Select(d => (d.UserId, d.HouseholdId))
                .ToHashSet();

            // 5. Plan + dispatch.
            var plans = ExpiryDigestPlanner.Plan(candidates, inventorySettings, recipients, alreadyDispatched, today);
            var enqueued = 0;
            foreach (var plan in plans)
            {
                var notification = DigestMessageComposer.Compose(plan, today);
                var userId = plan.UserId;

                var accepted = _queue.TryEnqueue((sp, token) =>
                    sp.GetRequiredService<INotificationSender>().SendDigestAsync(userId, notification, token));

                // Write the ledger row only on a successful enqueue (lossy-by-design ordering).
                if (accepted)
                {
                    _db.NotificationDispatches.Add(
                        NotificationDispatch.Create(plan.UserId, plan.HouseholdId, today));
                    enqueued++;
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Expiry scan: enqueued {Count} digest(s).", enqueued);
        }
    }
}
```

Note the `using Microsoft.Extensions.DependencyInjection;` is needed for `GetRequiredService` — add it to the file's usings.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryNotificationScanTests"`
Expected: PASS (all 5).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/ExpiryNotificationScan.cs \
        Application/Frigorino.Test/Infrastructure/ExpiryNotificationScanTests.cs
git commit -m "feat(notifications): add ExpiryNotificationScan job"
```

---

# Phase C — API surface + wiring

### Task 13: `MaintenanceSettings` config + secured `POST /internal/expiry-scan`

**Files:**
- Create: `Application/Frigorino.Infrastructure/Notifications/MaintenanceSettings.cs`
- Create: `Application/Frigorino.Features/Notifications/TriggerExpiryScan.cs`
- Test: `Application/Frigorino.Test/Features/MaintenanceKeyTests.cs`

- [ ] **Step 1: Write the failing test** — `MaintenanceKeyTests.cs` (covers the constant-time compare helper that backs the endpoint):

```csharp
using Frigorino.Features.Notifications;

namespace Frigorino.Test.Features
{
    public class MaintenanceKeyTests
    {
        [Fact]
        public void Matches_WhenEqual()
        {
            Assert.True(MaintenanceKey.Matches("secret-abc", "secret-abc"));
        }

        [Fact]
        public void DoesNotMatch_WhenDifferent()
        {
            Assert.False(MaintenanceKey.Matches("secret-abc", "secret-xyz"));
        }

        [Fact]
        public void DoesNotMatch_WhenProvidedNullOrEmpty()
        {
            Assert.False(MaintenanceKey.Matches(null, "secret-abc"));
            Assert.False(MaintenanceKey.Matches("", "secret-abc"));
        }

        [Fact]
        public void DoesNotMatch_WhenExpectedUnconfigured()
        {
            // An unconfigured token must never accept any key.
            Assert.False(MaintenanceKey.Matches("anything", ""));
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~MaintenanceKeyTests"`
Expected: BUILD FAILURE.

- [ ] **Step 3: Create `MaintenanceSettings.cs`**

```csharp
namespace Frigorino.Infrastructure.Notifications
{
    public class MaintenanceSettings
    {
        public const string SECTION_NAME = "MaintenanceSettings";

        // Shared secret the external scheduler sends in the X-Maintenance-Key header.
        // Empty ⇒ the scan endpoint rejects everything (returns 404).
        public string TriggerToken { get; set; } = "";
    }
}
```

- [ ] **Step 4: Create `TriggerExpiryScan.cs`** (endpoint + `MaintenanceKey` helper):

```csharp
using System.Security.Cryptography;
using System.Text;
using Frigorino.Infrastructure.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Frigorino.Features.Notifications
{
    // Constant-time comparison of the trigger key, isolated so it is unit-testable.
    public static class MaintenanceKey
    {
        public static bool Matches(string? provided, string expected)
        {
            if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            var a = Encoding.UTF8.GetBytes(provided);
            var b = Encoding.UTF8.GetBytes(expected);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }

    public static class TriggerExpiryScanEndpoint
    {
        private const string HeaderName = "X-Maintenance-Key";

        public static IEndpointRouteBuilder MapTriggerExpiryScan(this IEndpointRouteBuilder app)
        {
            // Machine-to-machine: not under the user-auth group, hidden from the OpenAPI client,
            // guarded by the trigger key. A wrong/missing key returns 404 (non-discoverable).
            app.MapPost("/internal/expiry-scan", Handle)
               .ExcludeFromDescription()
               .AllowAnonymous();
            return app;
        }

        private static async Task<Results<Ok, NotFound>> Handle(
            HttpRequest request,
            IOptions<MaintenanceSettings> settings,
            ExpiryNotificationScan scan,
            CancellationToken ct)
        {
            var provided = request.Headers[HeaderName].ToString();
            if (!MaintenanceKey.Matches(provided, settings.Value.TriggerToken))
            {
                return TypedResults.NotFound();
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await scan.RunAsync(today, ct);
            return TypedResults.Ok();
        }
    }
}
```

(The `ExpiryNotificationScan` is resolved from DI — registered scoped in Task 15.)

- [ ] **Step 5: Run to verify the helper test passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~MaintenanceKeyTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/MaintenanceSettings.cs \
        Application/Frigorino.Features/Notifications/TriggerExpiryScan.cs \
        Application/Frigorino.Test/Features/MaintenanceKeyTests.cs
git commit -m "feat(notifications): add secured /internal/expiry-scan trigger endpoint"
```

---

### Task 14: Token registration slices (`POST` / `DELETE /api/notifications/token`)

**Files:**
- Create: `Application/Frigorino.Features/Notifications/RegisterFcmToken.cs`
- Create: `Application/Frigorino.Features/Notifications/UnregisterFcmToken.cs`
- Test: `Application/Frigorino.Test/Features/FcmTokenSliceTests.cs`

- [ ] **Step 1: Write the failing tests** — `FcmTokenSliceTests.cs` (exercise the handlers directly against InMemory DB; uses a fake `ICurrentUserService`). The handlers are `public static` — matching the repo convention of testing public static slice helpers directly (e.g. `PromoteSuggestion.For` in `PromoteSuggestionTests.cs`); **no `InternalsVisibleTo` is used anywhere in this repo, so do not add one.**

```csharp
using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Notifications;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Features
{
    public class FcmTokenSliceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static ICurrentUserService UserNamed(string id)
        {
            var svc = A.Fake<ICurrentUserService>();
            A.CallTo(() => svc.UserId).Returns(id);
            return svc;
        }

        [Fact]
        public async Task Register_CreatesTokenForCurrentUser()
        {
            using var db = NewContext();
            db.Users.Add(new User { ExternalId = "u1", Name = "U" });
            await db.SaveChangesAsync();

            await RegisterFcmTokenEndpoint.Handle(
                new RegisterFcmTokenRequest("tok-1"), UserNamed("u1"), db, CancellationToken.None);

            var token = await db.FcmTokens.SingleAsync();
            Assert.Equal("u1", token.UserId);
            Assert.Equal("tok-1", token.Token);
        }

        [Fact]
        public async Task Register_ReassignsExistingTokenToCurrentUser()
        {
            using var db = NewContext();
            db.Users.Add(new User { ExternalId = "u1", Name = "U1" });
            db.Users.Add(new User { ExternalId = "u2", Name = "U2" });
            db.FcmTokens.Add(FcmToken.Create("u1", "shared-tok"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await RegisterFcmTokenEndpoint.Handle(
                new RegisterFcmTokenRequest("shared-tok"), UserNamed("u2"), db, CancellationToken.None);

            var token = await db.FcmTokens.SingleAsync();
            Assert.Equal("u2", token.UserId);
        }

        [Fact]
        public async Task Unregister_DeletesOwnToken()
        {
            using var db = NewContext();
            db.Users.Add(new User { ExternalId = "u1", Name = "U" });
            db.FcmTokens.Add(FcmToken.Create("u1", "tok-1"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await UnregisterFcmTokenEndpoint.Handle(
                new UnregisterFcmTokenRequest("tok-1"), UserNamed("u1"), db, CancellationToken.None);

            Assert.Empty(db.FcmTokens);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~FcmTokenSliceTests"`
Expected: BUILD FAILURE.

- [ ] **Step 3: Create `RegisterFcmToken.cs`**

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Notifications
{
    public sealed record RegisterFcmTokenRequest(string Token);

    public static class RegisterFcmTokenEndpoint
    {
        public static IEndpointRouteBuilder MapRegisterFcmToken(this IEndpointRouteBuilder app)
        {
            app.MapPost("/token", Handle)
               .WithName("RegisterFcmToken")
               .Produces(StatusCodes.Status200OK);
            return app;
        }

        // Public static so the unit test calls it directly (repo convention — no InternalsVisibleTo).
        public static async Task<Ok> Handle(
            RegisterFcmTokenRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var existing = await db.FcmTokens.FirstOrDefaultAsync(t => t.Token == request.Token, ct);
            if (existing is null)
            {
                db.FcmTokens.Add(FcmToken.Create(currentUser.UserId, request.Token));
            }
            else
            {
                // Re-register: claim the token for the current user (LastSeenAt stamped on save).
                existing.UserId = currentUser.UserId;
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok();
        }
    }
}
```

- [ ] **Step 4: Create `UnregisterFcmToken.cs`**

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Notifications
{
    public sealed record UnregisterFcmTokenRequest(string Token);

    public static class UnregisterFcmTokenEndpoint
    {
        public static IEndpointRouteBuilder MapUnregisterFcmToken(this IEndpointRouteBuilder app)
        {
            app.MapDelete("/token", Handle)
               .WithName("UnregisterFcmToken")
               .Produces(StatusCodes.Status204NoContent);
            return app;
        }

        // load-then-Remove (not ExecuteDeleteAsync): the EF InMemory provider used by the unit
        // test does not support ExecuteDeleteAsync, and the row count here is tiny.
        public static async Task<NoContent> Handle(
            UnregisterFcmTokenRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var rows = await db.FcmTokens
                .Where(t => t.Token == request.Token && t.UserId == currentUser.UserId)
                .ToListAsync(ct);
            db.FcmTokens.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~FcmTokenSliceTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Notifications/ \
        Application/Frigorino.Test/Features/FcmTokenSliceTests.cs
git commit -m "feat(notifications): add FCM token register/unregister slices"
```

---

### Task 15: Wire DI + endpoints in `Program.cs`, register `FirebaseMessaging`, add config placeholder

**Files:**
- Create: `Application/Frigorino.Infrastructure/Notifications/NotificationDependencyInjection.cs`
- Modify: `Application/Frigorino.Infrastructure/Auth/FirebaseAuth.cs`
- Modify: `Application/Frigorino.Web/Program.cs`
- Modify: `Application/Frigorino.Web/appsettings.json`

- [ ] **Step 1: Create `NotificationDependencyInjection.cs`** (everything except the sender, which is gated in Program.cs):

```csharp
using Frigorino.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class NotificationDependencyInjection
    {
        public static IServiceCollection AddExpiryNotifications(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MaintenanceSettings>(
                configuration.GetSection(MaintenanceSettings.SECTION_NAME));

            services.AddScoped<ExpiryNotificationScan>();
            return services;
        }
    }
}
```

- [ ] **Step 2: Register `FirebaseMessaging` in `FirebaseAuth.cs`** — add right after the existing `services.AddSingleton(FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance);` line:

```csharp
services.AddSingleton(FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance);
```

- [ ] **Step 3: Wire DI + sender gating in `Program.cs`** — in the auth gating block (currently `if (devAuthEnabled) { AddDevAuth } else { AddFirebaseAuth }`), register the matching sender in each branch. Replace that block:

```csharp
if (devAuthEnabled)
{
    builder.Services.AddDevAuth(builder.Configuration);
    builder.Services.AddScoped<INotificationSender, LogOnlyNotificationSender>();
}
else
{
    builder.Services.AddFirebaseAuth(builder.Configuration);
    builder.Services.AddScoped<INotificationSender, FcmNotificationSender>();
}
```

After the whole `if (!builder.Environment.IsEnvironment("IntegrationTest") && !isBuildTimeOpenApi)` block, add a safety fallback so IntegrationTest/build-time still resolve a sender:

```csharp
builder.Services.TryAddScoped<INotificationSender, LogOnlyNotificationSender>();
```

Add the notifications registration next to `AddMaintenanceServices()`:

```csharp
builder.Services.AddExpiryNotifications(builder.Configuration);
```

Add the required usings at the top of `Program.cs` — **only those not already present** (`Frigorino.Domain.Interfaces` and `Frigorino.Infrastructure.Services` are already imported; adding a duplicate `using` is a CS0105 warning):

```csharp
using Frigorino.Features.Notifications;
using Frigorino.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection.Extensions; // for TryAddScoped
```

(`Frigorino.Infrastructure.Services` — already imported — exposes `AddExpiryNotifications`; `Frigorino.Domain.Interfaces` — already imported — exposes `INotificationSender`.)

- [ ] **Step 4: Map the endpoints in `Program.cs`** — after the `me` group block (which ends with `me.MapUpdateUserSettings();`), add:

```csharp
me.MapUpdateUserNotificationSettings();

var notifications = app.MapGroup("/api/notifications")
    .RequireAuthorization()
    .WithTags("Notifications");
notifications.MapRegisterFcmToken();
notifications.MapUnregisterFcmToken();

// Machine-to-machine trigger (key-guarded inside the handler; not in the auth group).
app.MapTriggerExpiryScan();
```

Add `using Frigorino.Features.Me.Settings;` is already present; ensure `Frigorino.Features.Notifications` using is added (Step 3).

- [ ] **Step 5: Add the config placeholder** — in `appsettings.json`, add a top-level section (mirroring `FirebaseSettings`):

```json
"MaintenanceSettings": {
    "TriggerToken": ""
}
```

- [ ] **Step 6: Build the whole solution**

Run: `dotnet build Application/Frigorino.sln`
Expected: SUCCESS. (Confirms DI graph references resolve and ArchUnit layer references are still legal — `Features → Infrastructure` is allowed; `Infrastructure`/`Features` still don't reference `Web`.)

- [ ] **Step 7: Run the architecture + full unit test suite**

Run: `dotnet test Application/Frigorino.Test`
Expected: PASS (including `ArchitectureTests`).

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/NotificationDependencyInjection.cs \
        Application/Frigorino.Infrastructure/Auth/FirebaseAuth.cs \
        Application/Frigorino.Web/Program.cs \
        Application/Frigorino.Web/appsettings.json
git commit -m "feat(notifications): wire DI, FirebaseMessaging, endpoints, config"
```

---

### Task 16: Regenerate the API client

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/lib/api/**` (generated, committed)

- [ ] **Step 1: Regenerate** — from `Application/Frigorino.Web/ClientApp/`:

Run: `npm run api`
Expected: rebuilds the backend, emits `src/lib/openapi.json`, regenerates `src/lib/api`. New types appear: `RegisterFcmTokenRequest`, `UnregisterFcmTokenRequest`, `UpdateUserNotificationSettingsRequest`; `UserSettingsResponse` now has `expiryNotificationsEnabled` + `expiryLeadDays`; `InventorySettingsResponse` / `UpdateInventorySettingsRequest` now have `expiryNotificationsEnabled`. The `/internal/expiry-scan` endpoint is **absent** (excluded from description).

- [ ] **Step 2: Sanity-check the generated types**

Run: `git --no-pager diff --stat src/lib/api`
Expected: changes in `types.gen.ts`, `sdk.gen.ts`, `@tanstack/react-query.gen.ts`, `openapi.json`.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib
git commit -m "chore(api): regenerate client for notification + settings endpoints"
```

---

# Phase D — Frontend

### Task 17: Export `firebaseApp` + `pushNotifications.ts` module + token hooks

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/common/auth.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/common/pushNotifications.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/notifications/useRegisterFcmToken.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/notifications/useUnregisterFcmToken.ts`

- [ ] **Step 1: Export the Firebase app** — edit `auth.ts`. Change `const app = initializeApp(firebaseConfig);` to:

```typescript
export const firebaseApp = initializeApp(firebaseConfig);
```

Update the two following lines that reference `app`:

```typescript
window.console.log(firebaseApp);
const analytics = getAnalytics(firebaseApp);
```

- [ ] **Step 2: Create the mutation hooks.** `useRegisterFcmToken.ts`:

```typescript
import { useMutation } from "@tanstack/react-query";
import { registerFcmTokenMutation } from "../../lib/api/@tanstack/react-query.gen";

export const useRegisterFcmToken = () => useMutation({ ...registerFcmTokenMutation() });
```

`useUnregisterFcmToken.ts`:

```typescript
import { useMutation } from "@tanstack/react-query";
import { unregisterFcmTokenMutation } from "../../lib/api/@tanstack/react-query.gen";

export const useUnregisterFcmToken = () => useMutation({ ...unregisterFcmTokenMutation() });
```

> Verify the exact generated names with `git grep -n "registerFcmToken" src/lib/api/@tanstack/react-query.gen.ts` after Task 16; adjust if hey-api named them differently (e.g. `registerFcmTokenMutation`).

- [ ] **Step 3: Create `pushNotifications.ts`** — the imperative module the settings toggle calls:

```typescript
import { getMessaging, getToken, deleteToken, onMessage, isSupported } from "firebase/messaging";
import { firebaseApp } from "./auth";
import {
    registerFcmToken,
    unregisterFcmToken,
} from "../lib/api/sdk.gen";

const VAPID_KEY = import.meta.env.VITE_FCM_VAPID_KEY as string | undefined;

// True only where web push can actually work (Chrome/Edge/Firefox; iOS only as an
// installed Home-Screen PWA). Used to gate the toggle + show the iOS hint.
export async function pushSupported(): Promise<boolean> {
    try {
        return await isSupported();
    } catch {
        return false;
    }
}

// iOS Safari supports web push ONLY when launched from the Home Screen.
export function isIosNeedingInstall(): boolean {
    const ua = navigator.userAgent;
    const isIos = /iPad|iPhone|iPod/.test(ua);
    const standalone =
        window.matchMedia("(display-mode: standalone)").matches ||
        (navigator as unknown as { standalone?: boolean }).standalone === true;
    return isIos && !standalone;
}

async function swRegistration(): Promise<ServiceWorkerRegistration | undefined> {
    if (!("serviceWorker" in navigator)) {
        return undefined;
    }
    return navigator.serviceWorker.ready;
}

// Requests permission, mints an FCM token, registers it with the backend.
// Returns true on success; false if denied/unsupported/misconfigured.
export async function enablePush(): Promise<boolean> {
    if (!VAPID_KEY || !(await pushSupported())) {
        return false;
    }

    const permission = await Notification.requestPermission();
    if (permission !== "granted") {
        return false;
    }

    const registration = await swRegistration();
    const messaging = getMessaging(firebaseApp);
    const token = await getToken(messaging, {
        vapidKey: VAPID_KEY,
        serviceWorkerRegistration: registration,
    });
    if (!token) {
        return false;
    }

    await registerFcmToken({ body: { token } });

    // Foreground messages: surface a lightweight in-app notification.
    onMessage(messaging, (payload) => {
        const title = payload.data?.title;
        const body = payload.data?.body;
        if (title) {
            new Notification(title, { body });
        }
    });

    return true;
}

// Deletes the local token + unregisters it server-side.
export async function disablePush(): Promise<void> {
    if (!(await pushSupported())) {
        return;
    }
    const messaging = getMessaging(firebaseApp);
    let token: string | null = null;
    try {
        token = await getToken(messaging, { vapidKey: VAPID_KEY });
    } catch {
        token = null;
    }
    if (token) {
        await unregisterFcmToken({ body: { token } });
        await deleteToken(messaging);
    }
}
```

> Verify the generated SDK function names (`registerFcmToken`, `unregisterFcmToken`) against `src/lib/api/sdk.gen.ts` after Task 16; adjust imports if different.

- [ ] **Step 4: Type the new env var** — `src/vite-env.d.ts` currently contains only `/// <reference types="vite/client" />`. Replace its contents so `import.meta.env.VITE_FCM_VAPID_KEY` is typed (the `vite-plugin-pwa/client` ref is harmless now and needed in Task 20):

```typescript
/// <reference types="vite/client" />
/// <reference types="vite-plugin-pwa/client" />

interface ImportMetaEnv {
    readonly VITE_FCM_VAPID_KEY?: string;
    readonly VITE_DEV_AUTH?: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}
```

- [ ] **Step 5: Type-check**

Run (from `ClientApp/`): `npm run tsc`
Expected: no type errors.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/common/auth.ts \
        Application/Frigorino.Web/ClientApp/src/common/pushNotifications.ts \
        Application/Frigorino.Web/ClientApp/src/features/notifications \
        Application/Frigorino.Web/ClientApp/src/vite-env.d.ts
git commit -m "feat(web): add push-notification client module + token hooks"
```

---

### Task 18: Notifications section on the User Settings page

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/settings/useUpdateUserNotificationSettings.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/settings/pages/UserSettingsPage.tsx`

- [ ] **Step 1: Create the mutation hook** — `useUpdateUserNotificationSettings.ts` (mirrors `useUpdateUserSettings.ts`):

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getUserSettingsQueryKey,
    updateUserNotificationSettingsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateUserNotificationSettings = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateUserNotificationSettingsMutation(),
        onSuccess: (data) => {
            queryClient.setQueryData(getUserSettingsQueryKey(), data);
        },
    });
};
```

- [ ] **Step 2: Add the notifications card to `UserSettingsPage.tsx`.** Add imports:

```typescript
import { FormControlLabel, Switch, Alert } from "@mui/material";
import { useEffect, useState } from "react";
import { useUpdateUserNotificationSettings } from "../useUpdateUserNotificationSettings";
import {
    enablePush,
    disablePush,
    pushSupported,
    isIosNeedingInstall,
} from "../../../common/pushNotifications";
```

Inside `UserSettingsPage`, after the existing `updateSettings` hook, add state + handlers:

```typescript
const updateNotifications = useUpdateUserNotificationSettings();
const [enabled, setEnabled] = useState(false);
const [leadDays, setLeadDays] = useState("3");
const [supported, setSupported] = useState(true);
const [iosHint, setIosHint] = useState(false);

useEffect(() => {
    if (data) {
        setEnabled(data.expiryNotificationsEnabled);
        setLeadDays(String(data.expiryLeadDays));
    }
}, [data]);

useEffect(() => {
    pushSupported().then(setSupported);
    setIosHint(isIosNeedingInstall());
}, []);

const persistNotifications = async (nextEnabled: boolean, nextLeadDays: number) => {
    try {
        await updateNotifications.mutateAsync({
            body: {
                expiryNotificationsEnabled: nextEnabled,
                expiryLeadDays: nextLeadDays,
            },
        });
        toast.success(t("settings.saved"));
    } catch {
        toast.error(t("settings.saveFailed"));
    }
};

const handleToggleNotifications = async (checked: boolean) => {
    if (checked) {
        const ok = await enablePush();
        if (!ok) {
            toast.error(t("settings.notificationsPermissionDenied"));
            return;
        }
    } else {
        await disablePush();
    }
    setEnabled(checked);
    await persistNotifications(checked, Number(leadDays));
};

const handleLeadDaysBlur = async () => {
    const days = Number(leadDays);
    if (!Number.isInteger(days) || days < 0 || days > 365) {
        return;
    }
    if (data && data.expiryLeadDays === days) {
        return;
    }
    await persistNotifications(enabled, days);
};
```

Add the card JSX after the existing language `<Card>` (inside the `<Container>`):

```tsx
<Card elevation={2} sx={{ mt: { xs: 2, sm: 3 } }}>
    <CardContent>
        <Typography variant="h6" sx={{ mb: 1 }}>
            {t("settings.notifications")}
        </Typography>

        {iosHint && (
            <Alert severity="info" sx={{ mb: 2 }} data-testid="settings-ios-install-hint">
                {t("settings.notificationsIosHint")}
            </Alert>
        )}

        <FormControlLabel
            control={
                <Switch
                    data-testid="settings-notifications-switch"
                    checked={enabled}
                    disabled={!supported || updateNotifications.isPending}
                    onChange={(e) => handleToggleNotifications(e.target.checked)}
                />
            }
            label={t("settings.notificationsEnable")}
        />

        {enabled && (
            <TextField
                type="number"
                fullWidth
                size="small"
                sx={{ mt: 1 }}
                label={t("settings.notificationsLeadDays")}
                helperText={t("settings.notificationsLeadHelp")}
                value={leadDays}
                disabled={updateNotifications.isPending}
                onChange={(e) => setLeadDays(e.target.value)}
                onBlur={handleLeadDaysBlur}
                slotProps={{
                    htmlInput: {
                        min: 0,
                        max: 365,
                        "data-testid": "settings-notifications-lead-input",
                    },
                }}
            />
        )}
    </CardContent>
</Card>
```

- [ ] **Step 3: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/settings/
git commit -m "feat(web): add expiry-notification controls to user settings"
```

---

### Task 19: Per-inventory enable toggle on the Inventory Settings card

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/components/InventorySettingsCard.tsx`

> The `useUpdateInventorySettings` mutation hook is generic (passes `{ path, body }` through), so no hook change is needed — only the card sends the new `expiryNotificationsEnabled` field.

- [ ] **Step 1: Add enabled state + extend the save call.** In `InventorySettingsCard.tsx`, add to state:

```typescript
const [notificationsEnabled, setNotificationsEnabled] = useState(true);
```

Extend the `useEffect` that syncs from `data`:

```typescript
useEffect(() => {
    if (data) {
        setNotificationsEnabled(data.expiryNotificationsEnabled);
        setOverride(data.expiryLeadDays !== null);
        if (data.expiryLeadDays !== null) {
            setValue(String(data.expiryLeadDays));
        }
    }
}, [data]);
```

Replace the `save` function to send both fields, and add an enabled-toggle handler:

```typescript
const save = async (enabled: boolean, leadDays: number | null) => {
    try {
        await updateSettings.mutateAsync({
            path: { householdId, inventoryId },
            body: { expiryNotificationsEnabled: enabled, expiryLeadDays: leadDays },
        });
        toast.success(t("settings.saved"));
    } catch {
        toast.error(t("settings.saveFailed"));
    }
};

const handleNotificationsToggle = async (checked: boolean) => {
    setNotificationsEnabled(checked);
    await save(checked, override ? Number(value) : null);
};
```

Update the existing override handler + blur to pass `notificationsEnabled`:

```typescript
const handleToggle = async (checked: boolean) => {
    setOverride(checked);
    await save(notificationsEnabled, checked ? Number(value) : null);
};

const handleBlur = async () => {
    const days = Number(value);
    if (!override || !Number.isInteger(days) || days < 0) {
        return;
    }
    if (data && data.expiryLeadDays === days) {
        return;
    }
    await save(notificationsEnabled, days);
};
```

- [ ] **Step 2: Add the enable switch to the card JSX** — above the existing override `FormControlLabel`:

```tsx
<FormControlLabel
    control={
        <Switch
            data-testid="inventory-notifications-switch"
            checked={notificationsEnabled}
            disabled={!canManage || updateSettings.isPending}
            onChange={(e) => handleNotificationsToggle(e.target.checked)}
        />
    }
    label={t("settings.inventoryNotificationsEnable")}
/>
```

- [ ] **Step 3: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/components/InventorySettingsCard.tsx
git commit -m "feat(web): add per-inventory notification toggle to settings card"
```

---

### Task 20: Custom service worker (injectManifest) with FCM background push

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/vite.config.ts`
- Modify: `Application/Frigorino.Web/ClientApp/package.json` (add workbox dev deps)
- Create: `Application/Frigorino.Web/ClientApp/src/sw.ts`
- (`src/vite-env.d.ts` already updated in Task 17 — its `vite-plugin-pwa/client` ref types `virtual:pwa-register`/the SW)

> **Design note (verified via context7):** vite-plugin-pwa switches to a custom SW with `strategies: 'injectManifest'` + `srcDir`/`filename`; the SW must call `precacheAndRoute(self.__WB_MANIFEST)` (needs `workbox-precaching`). FCM background handling uses `getMessaging`/`onBackgroundMessage` from `firebase/messaging/sw`. **This area must be manually verified in a real browser (Task 24) — the generated SW + push path can't be unit-tested.**

- [ ] **Step 1: Add workbox dev deps** — from `ClientApp/`:

Run: `npm install -D workbox-precaching@^7.3.0 workbox-core@^7.3.0`
Expected: both added to `devDependencies`. (Match the major version vite-plugin-pwa bundles — 7.x; if the build later complains of a version mismatch, align to the version printed by `npm ls workbox-build`.)

- [ ] **Step 2: Switch VitePWA to injectManifest** — in `vite.config.ts`, update the `VitePWA({...})` options to add the strategy + source, keeping the existing manifest:

```typescript
VitePWA({
    strategies: "injectManifest",
    srcDir: "src",
    filename: "sw.ts",
    registerType: "autoUpdate",
    injectManifest: {
        // Firebase messaging SW imports push the bundle over the default 2 MiB limit.
        maximumFileSizeToCacheInBytes: 4 * 1024 * 1024,
    },
    devOptions: {
        enabled: true,
        type: "module",
    },
    manifest: {
        // ...unchanged from current config...
    },
}),
```

(Leave the `manifest` block exactly as it is today.)

- [ ] **Step 3: Create `src/sw.ts`**

```typescript
/// <reference lib="webworker" />
import { precacheAndRoute } from "workbox-precaching";
import { clientsClaim } from "workbox-core";
import { initializeApp } from "firebase/app";
import { getMessaging, onBackgroundMessage } from "firebase/messaging/sw";

declare const self: ServiceWorkerGlobalScope & {
    __WB_MANIFEST: Array<{ url: string; revision: string | null }>;
};

// Precache the Vite build (injected at build time).
precacheAndRoute(self.__WB_MANIFEST);
self.skipWaiting();
clientsClaim();

// Firebase config mirrors src/common/auth.ts (public values).
const firebaseApp = initializeApp({
    apiKey: "AIzaSyAXJH3Z66XYUA-_7rB7ZQzCDHENBlmUxjs",
    authDomain: "frigorino-2acd1.firebaseapp.com",
    projectId: "frigorino-2acd1",
    storageBucket: "frigorino-2acd1.firebasestorage.app",
    messagingSenderId: "97032277670",
    appId: "1:97032277670:web:970459c8367113abdc2e67",
    measurementId: "G-09LMYWB1XG",
});

const messaging = getMessaging(firebaseApp);

// Data-only messages from the server: render the notification ourselves.
onBackgroundMessage(messaging, (payload) => {
    const title = payload.data?.title ?? "Frigorino";
    const body = payload.data?.body ?? "";
    const link = payload.data?.link ?? "/";
    void self.registration.showNotification(title, {
        body,
        icon: "/192.png",
        data: { link },
    });
});

// Focus an existing tab or open the deep link on click.
self.addEventListener("notificationclick", (event) => {
    event.notification.close();
    const link = (event.notification.data as { link?: string } | null)?.link ?? "/";
    event.waitUntil(
        self.clients
            .matchAll({ type: "window", includeUncontrolled: true })
            .then((clients) => {
                for (const client of clients) {
                    if ("focus" in client) {
                        void client.focus();
                        if ("navigate" in client) {
                            void (client as WindowClient).navigate(link);
                        }
                        return undefined;
                    }
                }
                return self.clients.openWindow(link);
            }),
    );
});
```

- [ ] **Step 4: Confirm the SW client types ref** — `src/vite-env.d.ts` already includes `/// <reference types="vite-plugin-pwa/client" />` (added in Task 17), which types the PWA virtual modules. No change needed here.

- [ ] **Step 5: Build the SPA** (the real test that injectManifest + the SW bundle compile)

Run (from `ClientApp/`): `npm run build`
Expected: `tsc -b` passes and `vite build` emits `build/sw.js` (the bundled custom SW) plus the precache manifest. No "maximumFileSizeToCacheInBytes" error (raised in Step 2).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/vite.config.ts \
        Application/Frigorino.Web/ClientApp/package.json \
        Application/Frigorino.Web/ClientApp/package-lock.json \
        Application/Frigorino.Web/ClientApp/src/sw.ts \
        Application/Frigorino.Web/ClientApp/src/vite-env.d.ts
git commit -m "feat(web): custom service worker with FCM background push (injectManifest)"
```

---

### Task 21: i18n keys (en + de)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add keys to the `settings` object in `en/translation.json`** (insert before the closing `}` of `"settings"`):

```json
"notifications": "Notifications",
"notificationsEnable": "Expiry reminders",
"notificationsLeadDays": "Default lead time (days)",
"notificationsLeadHelp": "Days before expiry to notify, unless an inventory overrides it.",
"notificationsPermissionDenied": "Notification permission was denied.",
"notificationsIosHint": "Add Frigorino to your Home Screen to enable notifications on iOS.",
"inventoryNotificationsEnable": "Expiry reminders for this inventory"
```

(Add a trailing comma to the line currently before the insertion point — `"readOnlyHint"` — so JSON stays valid.)

- [ ] **Step 2: Add the German equivalents to `de/translation.json`'s `settings` object:**

```json
"notifications": "Benachrichtigungen",
"notificationsEnable": "Ablauf-Erinnerungen",
"notificationsLeadDays": "Standard-Vorlaufzeit (Tage)",
"notificationsLeadHelp": "Tage vor Ablauf benachrichtigen, sofern ein Inventar dies nicht überschreibt.",
"notificationsPermissionDenied": "Benachrichtigungsberechtigung wurde verweigert.",
"notificationsIosHint": "Füge Frigorino zum Startbildschirm hinzu, um Benachrichtigungen auf iOS zu aktivieren.",
"inventoryNotificationsEnable": "Ablauf-Erinnerungen für dieses Inventar"
```

- [ ] **Step 3: Validate JSON + prettier**

Run (from `ClientApp/`): `npm run prettier && npm run tsc`
Expected: both files reformatted cleanly, no errors. (A JSON syntax error surfaces immediately in prettier.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales
git commit -m "feat(web): add notification settings i18n keys (en + de)"
```

---

# Phase E — CI, housekeeping, verification

### Task 22: Daily GitHub Actions cron trigger

**Files:**
- Create: `.github/workflows/expiry-scan.yml`

- [ ] **Step 1: Create the workflow**

```yaml
name: Daily expiry scan

on:
  schedule:
    # 05:00 UTC ≈ 07:00 Europe (summer). Adjust if DST handling matters.
    - cron: "0 5 * * *"
  workflow_dispatch: {}

jobs:
  trigger:
    runs-on: ubuntu-latest
    steps:
      - name: POST /internal/expiry-scan
        env:
          SCAN_URL: ${{ vars.EXPIRY_SCAN_URL }}
          TRIGGER_TOKEN: ${{ secrets.MAINTENANCE_TRIGGER_TOKEN }}
        run: |
          if [ -z "$SCAN_URL" ] || [ -z "$TRIGGER_TOKEN" ]; then
            echo "EXPIRY_SCAN_URL var or MAINTENANCE_TRIGGER_TOKEN secret not set." >&2
            exit 1
          fi
          curl --fail --silent --show-error \
            -X POST "$SCAN_URL" \
            -H "X-Maintenance-Key: $TRIGGER_TOKEN"
```

- [ ] **Step 2: Document the required GitHub config** — note in the PR description (and tell the user) that they must set:
  - Repo **variable** `EXPIRY_SCAN_URL` = `https://<prod-host>/internal/expiry-scan`
  - Repo **secret** `MAINTENANCE_TRIGGER_TOKEN` = the same value as the server's `MaintenanceSettings:TriggerToken`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/expiry-scan.yml
git commit -m "ci: add daily expiry-scan cron trigger"
```

---

### Task 23: Housekeeping — fix stale docs

**Files:**
- Modify: `CLAUDE.md`
- Modify: `IDEAS.md`

- [ ] **Step 1: Fix the stale runner claim in `CLAUDE.md`** — find the sentence in the "Background jobs" section stating the Channels queue "is not built yet" and update it to reflect that `BackgroundTaskQueue` + `QueuedHostedService` now exist (`Frigorino.Infrastructure/Services/`), drained by a single consumer. Keep it terse (one clause).

Run to locate: `git grep -n "not built yet" CLAUDE.md`

- [ ] **Step 2: Slim the `IDEAS.md` entry** — the "User notifications on inventory item expiration" entry's research questions are now resolved and a spec + plan exist. Replace the long placeholder body with a one/two-line pointer:

```markdown
## User notifications on inventory item expiration

Designed + planned. See `docs/superpowers/specs/2026-06-01-inventory-expiry-notifications-design.md`
and `docs/superpowers/plans/2026-06-01-inventory-expiry-notifications.md`. Blocked-then-unblocked on
settings (now on `stage`). Also fix any remaining "PWA plugin not wired" wording — it is wired
(`vite-plugin-pwa` in `vite.config.ts`).
```

Also fix any standalone "Vite PWA plugin is not wired today" sentence elsewhere in `IDEAS.md`.

Run to locate: `git grep -n "PWA" IDEAS.md`

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md IDEAS.md
git commit -m "docs: correct stale runner/PWA notes; point IDEAS at expiry-notifications spec+plan"
```

---

### Task 24: Verification gate

**Files:** none (verification only).

- [ ] **Step 1: Full backend test suite** (Test + IntegrationTests in one run — they share Testcontainer/build state, so do not parallelize)

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS. Capture pass/fail counts; do not trust a piped tail — read the summary lines.

- [ ] **Step 2: Frontend verify** — from `ClientApp/`:

Run: `npm run lint && npm run tsc && npm run prettier:check && npm run build`
Expected: all clean; `build/` emits `sw.js` + assets. (Do not run this concurrently with `dotnet test` — both touch `ClientApp/build`.)

- [ ] **Step 3: Docker build** (new EF migrations + endpoints in existing projects; verify no Dockerfile/SPA/pipeline drift)

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: SUCCESS. (If the Docker daemon is unreachable, ask the user to start Docker Desktop — do not skip.)

- [ ] **Step 4: Manual end-to-end push verification** (the net for plan-baked runtime bugs — required because the SW + push path is untestable in unit tests):
  1. Supply `VITE_FCM_VAPID_KEY` (real Web Push cert key) and `MaintenanceSettings:TriggerToken` locally (env/user-secrets). Use the **real Firebase** flow (`dotnet run` + `npm run dev`), since DevAuth has no FirebaseApp and only the LogOnlySender.
  2. Sign in, open **Settings**, toggle **Expiry reminders** on, accept the browser permission prompt. Confirm a row appears in `FcmTokens`.
  3. Add an inventory item expiring within the lead window.
  4. `curl -X POST https://localhost:5001/internal/expiry-scan -H "X-Maintenance-Key: <token>" -k` → expect `200`; a system notification arrives. Re-run the same curl → expect `200` but **no second notification** (ledger de-dup). A wrong key → `404`.
  5. Click the notification → app opens/focuses at `/inventories`.
  6. (If a real iOS device is available) install to Home Screen, confirm the toggle works and the in-Safari-tab path shows the iOS hint instead.

- [ ] **Step 5: Report** — summarize test counts, build results, and manual-verification outcome. Only claim completion against observed output.

---

## Self-review checklist (completed by plan author)

- **Spec coverage:** daily external trigger (T13, T22) ✓; secured endpoint + API-key + 404 + constant-time (T13) ✓; idempotent ledger (T6/T7/T12) ✓; daily digest per household (T9/T12) ✓; layered user+inventory prefs (T1/T2/T4/T5/T9) ✓; opt-in = grant + toggle (T18) ✓; FcmToken pruning (T11) ✓; `INotificationSender` port (T8/T11) ✓; reuse runner (T12) ✓; reuse SW + Firebase credential (T15/T20) ✓; iOS hint (T17/T18) ✓; en/de copy (T10/T21) ✓; deep-link `/inventories` (T10/T20) ✓; housekeeping doc fixes (T23) ✓; verification incl. docker + manual (T24) ✓.
- **Reconciliation:** settings fields relocated from "settings feature" to Phase A here, because they did not ship — explicitly documented up top and consumed downstream.
- **Type consistency:** `ExpiryCandidate`, `InventoryNotificationSetting`, `DigestRecipient`, `DigestPlan`, `DigestLine`, `ExpiryDigestNotification`, `FcmSendOutcome` names used identically across planner/composer/scan/sender tasks. `SetExpiryNotifications` (user) vs `SetExpiryNotificationsEnabled` (inventory) are deliberately different signatures.
- **Known provider caveat called out:** `ExecuteDeleteAsync` not supported by EF InMemory → unregister handler uses load+Remove (T14 note).

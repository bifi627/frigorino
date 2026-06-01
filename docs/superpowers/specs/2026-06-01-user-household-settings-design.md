# User, Household & Inventory Settings — Infrastructure + Anchor Settings

**Date:** 2026-06-01
**Branch:** `feat/settings-infrastructure` (off `stage`)
**Status:** Design approved, pending implementation plan

## Goal

Stand up the storage + read/write seams for three structurally separate preference scopes
so future preferences are "add a field + a control," not "design the whole settings
subsystem." The scopes stay distinct from day one to avoid overloading one bag with
mode-dependent meaning:

- **User settings** — personal, per-signed-in-user, affect only that user across all their
  households. No role check (you can only edit your own).
- **Household settings** — household-wide, affect everyone in the household, editable only
  by Owner/Admin.
- **Inventory settings** — per-inventory, editable by the inventory creator or an Admin+
  (mirrors the existing inventory edit policy).

This is **infrastructure first**, but each scope is **anchored on one real setting** so the
full read/write path is exercised end-to-end rather than shipping untested plumbing:

| Scope | Anchor setting | Type | Drives behaviour in this spec? |
|-------|----------------|------|--------------------------------|
| User | `Language` (`en`/`de`) | `string?` | Yes — persists the language already toggled client-only today. |
| Household | `CheckedItemRetentionDays` | `int` | Yes — replaces the hard-coded 30-day purge constant. |
| Inventory | `ExpiryLeadDays` | `int?` | No — **storage only** here; consumed later by the notification spec. |

### Cross-spec boundary (important)

The inventory anchor `ExpiryLeadDays` is a **per-inventory override** of a user-level default.
The notification feature will resolve effective lead-days as
`inventory.ExpiryLeadDays ?? user.ExpiryLeadDays` and compute the scan window per inventory.

- **This spec owns:** storage + CRUD of `inventory.ExpiryLeadDays` (nullable; `null` = inherit).
- **The notification spec owns:** `user.ExpiryLeadDays`, the `??` resolution, the expiry scan,
  and any notification delivery. None of that is built here.

So `inventory.ExpiryLeadDays` is stored and editable in this spec but drives no behaviour yet —
it's exercised purely through its own settings card CRUD.

## Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Ship empty or anchored? | Anchored on one real setting per scope | Proves the path end-to-end; gives typed columns something concrete to carry. |
| Storage shape | Typed flat columns, one table per scope | Aligns with the flat-schema preference; queryable; all anchors have known shape. Revisit JSON only if settings sprawl into dozens of rarely-queried toggles. |
| Household-settings authz | **Sibling-entity pattern** (like Lists), NOT a `Household` aggregate method | `HouseholdAccessQueries.cs` carries an explicit note that the `Household` aggregate stays focused on membership/tenancy invariants and must not become a god-aggregate every sibling routes through. |
| Inventory-settings authz | **Sibling-entity pattern**, reusing the inventory edit policy (creator OR Admin+) | Matches `Inventory.Update`/`SoftDelete`. Extract the policy into a reusable predicate so the settings slice and the existing methods share one gate. |
| Row creation | Lazy — no row until first write; reads of a missing row return defaults | Mirrors the existing lazy `User`-row sync pattern. |

## 1. Data model (typed columns, three new tables)

### `UserSettings`
- **PK** `UserId` (string, FK → `User.ExternalId`, cascade delete).
- `Language string?` — nullable. **`null` = fall back to client browser detection**, so an
  unset user is behaviourally identical to today.
- `CreatedAt` / `UpdatedAt` — auto-stamped centrally in `ApplicationDbContext.SaveChangesAsync`.
- **No `IsActive`** — settings ride the parent's lifecycle; never independently soft-deleted.

### `HouseholdSettings`
- **PK** `HouseholdId` (int, FK → `Household.Id`, cascade delete).
- `CheckedItemRetentionDays int`.
- `CreatedAt` / `UpdatedAt`. No `IsActive`.

### `InventorySettings`
- **PK** `InventoryId` (int, FK → `Inventory.Id`, cascade delete).
- `ExpiryLeadDays int?` — nullable. **`null` = inherit the user-level default** (resolved by
  the notification spec). A non-null value is a per-inventory override.
- `CreatedAt` / `UpdatedAt`. No `IsActive`.

All three are **1:1 with their parent** and **lazy-created on first write**.

## 2. Domain (constants + validating methods)

- **`UserSettings`** (new entity, `Frigorino.Domain/Entities/`):
  - const `SupportedLanguages = ["en", "de"]`.
  - `Create(string userId)` factory.
  - `SetLanguage(string? lang)` → validates `lang ∈ SupportedLanguages || lang is null`;
    returns `Result` with `"Property"` metadata key on failure (→ `ValidationProblem`).
- **`HouseholdSettings`** (new entity):
  - consts `DefaultCheckedItemRetentionDays = 30`, `MinRetentionDays = 1`, `MaxRetentionDays = 365`.
  - `Create(int householdId)` factory.
  - `SetCheckedItemRetentionDays(int days)` → bounds-validates; `Result` with `"Property"` metadata.
- **`InventorySettings`** (new entity):
  - consts `MinExpiryLeadDays = 0`, `MaxExpiryLeadDays = 365`.
  - `Create(int inventoryId)` factory.
  - `SetExpiryLeadDays(int? days)` → `null` is valid (inherit); otherwise bounds-validates;
    `Result` with `"Property"` metadata.
- **`HouseholdRoleExtensions`**: add `CanManageSettings(this HouseholdRole role) => role >= HouseholdRole.Admin`.
- **`Inventory`**: extract the existing creator-OR-Admin gate (currently inline in `Update` /
  `SoftDelete`) into a predicate, e.g. `CanBeManagedBy(string callerUserId, HouseholdRole callerRole)`,
  and reuse it in those methods and the new settings slice. Small DRY that directly serves this change.

Default constants live on the entities (mirroring `Household.NameMaxLength`) and are the single
source of truth for both the aggregate methods and the read-side defaults.

## 3. Slices (6 endpoints, no controllers)

### Me group (`/api/me`)
- `GET /api/me/settings` — inline EF projection from `UserSettings`; defaults when no row.
  Identity via `ICurrentUserService.UserId`.
- `PUT /api/me/settings` — load-or-create `UserSettings`, `SetLanguage`, persist. No role check.
  Validation → `ValidationProblem`.

### Household group (`/api/household/{householdId:int}/settings`)
- `GET` — any active member reads: `FindActiveMembershipAsync` (null → 404), projection with defaults.
- `PUT` — `FindActiveMembershipAsync` (null → 404); `!Role.CanManageSettings()` → 403;
  load-or-create `HouseholdSettings`, `SetCheckedItemRetentionDays`, persist. Validation → `ValidationProblem`.

### Inventory group (`/api/household/{householdId:int}/inventories/{inventoryId:int}/settings`)
- `GET` — any active member of the household reads: `FindActiveMembershipAsync` (null → 404),
  load inventory (404 if not found/inactive), projection with defaults (`ExpiryLeadDays = null`).
- `PUT` — `FindActiveMembershipAsync` (null → 404); load inventory (404 if missing);
  `!inventory.CanBeManagedBy(userId, role)` → 403; load-or-create `InventorySettings`,
  `SetExpiryLeadDays`, persist. Validation → `ValidationProblem`.

Each slice = one file, request DTO + response DTO + registration + handler colocated, per
`knowledge/Vertical_Slices.md`. New groups wired in `Program.cs` alongside the existing
`/api/household/{householdId:int}/...` groups.

## 4. Purge wiring — the one real behaviour change

`DeleteInactiveItems` (`Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`) currently
hard-codes `DateTime.UtcNow.AddDays(-30)` for checked-off list items (lines 22-23). That branch
becomes **per-household**:

- Effective threshold per household = `now - COALESCE(settings.CheckedItemRetentionDays, 30) days`.
- Households **without** a settings row keep today's exact 30-day behaviour (default fallback).
- The `!IsActive` delete branches (households, inventories, lists, inventory items) are **untouched**.

**Implementation note (not a design fork):** per-row interval arithmetic is awkward in a single EF
`ExecuteDeleteAsync`. The plan will choose between (a) grouping households by distinct retention
value and running one delete per value (small N), or (b) a raw SQL / interval expression. Either
is acceptable; correctness + the default-30 fallback are the requirements.

## 5. Frontend

Three separate surfaces, each co-located with where that scope is already managed.

### User settings — its own page, from the user-icon menu
- New route `routes/settings/index.tsx` — thin shell (`requireAuth`) → `features/settings/pages/UserSettingsPage`.
- Reached via a new **"Settings"** item in the existing user-icon menu in `Navigation.tsx`
  (the `Menu#user-menu` that currently holds Logout).
- Page hosts the **language control** (en/de). On save → persist via `useUpdateUserSettings`
  **and** `i18n.changeLanguage(lang)`. Home for future personal prefs.
- **Remove the header `LanguageSwitcher`** — it currently calls `i18n.changeLanguage` without
  persisting and would diverge from the stored value. Only usage is in `Navigation.tsx`
  (import + one `<LanguageSwitcher />`); delete the component file too (dead-code cleanup).
- **App-boot wiring:** after auth, read user settings and apply `Language` if set; else keep
  i18next browser detection. This is the visible end-to-end proof of the user anchor.

### Household settings — a card on the Manage Household page
- Add a **Settings card** to `ManageHouseholdPage` (`routes/household/manage.tsx`), next to the
  rename (`HouseholdSummaryCard`) and `MembersPanel` cards. Hosts the retention-days control.
- **Read-only display for members; editable for Owner/Admin** (`CanManageSettings`), mirroring
  the GET-for-any-member / PUT-for-admin API.

### Inventory settings — a card on the Inventory edit page
- Add a **Settings card** to `InventoryEditPage` (`routes/inventories/$inventoryId/edit.tsx`).
- Control for `ExpiryLeadDays`: an **inherit (null) vs. override (number)** control — e.g. a
  "use default" toggle that reveals a day-count input when overriding. The resolved user default
  is not shown here (it lives in the notification spec).
- **Read-only for non-managers; editable for the inventory creator or Admin+.**

### Hooks (canonical one-per-file shape; never hand-write `queryFn`/`mutationFn`/`queryKey`)
- `features/settings/useUserSettings.ts` (query) / `useUpdateUserSettings.ts` (arg-less mutation).
- `features/households/useHouseholdSettings.ts` (path `householdId`) / `useUpdateHouseholdSettings.ts`.
- `features/inventories/useInventorySettings.ts` (path `householdId` + `inventoryId`) /
  `useUpdateInventorySettings.ts`.
- Query hooks guard `enabled` on each path id `> 0` and set a `staleTime`; spread the generated
  `getXOptions` / `xMutation` / `getXQueryKey`.

## 6. Testing

- **Domain units** (`Frigorino.Test`, xUnit + FakeItEasy):
  - `UserSettings.SetLanguage`: `en`/`de` ok, `null` ok, `fr` fails.
  - `HouseholdSettings.SetCheckedItemRetentionDays`: in-bounds ok, below-min / above-max fail.
  - `InventorySettings.SetExpiryLeadDays`: `null` ok (inherit), in-bounds ok, below-min / above-max fail.
  - `CanManageSettings`: Owner/Admin true, Member false.
  - `Inventory.CanBeManagedBy`: creator true, Admin+ true, non-creator Member false.
- **Slice behaviour:** household `PUT` as Member → 403, as Admin → 200; inventory `PUT` as
  non-creator Member → 403, as creator → 200, as Admin → 200; lazy-create on first write for all scopes.
- **Purge:** two households with different retention values purge checked items on their own
  thresholds; a household with no settings row uses 30.
- No JS test runner (unchanged).

## 7. Migration & scope

- **One EF migration** adding `UserSettings` + `HouseholdSettings` + `InventorySettings`;
  applied at startup via `context.Database.MigrateAsync()`.
- **No new project** ⇒ no `Dockerfile` change.
- API client regenerated via `npm run api` after the slices land.

### Out of scope
Theme, default-landing-view, notification prefs, `user.ExpiryLeadDays`, the
`inventory.ExpiryLeadDays ?? user.ExpiryLeadDays` resolution, the expiry scan / notification
delivery, and any knob beyond the three anchors. Each additional preference is a follow-up that
adds a field + a control on top of this infrastructure.

## Impact / cost

1 EF migration · 3 entities + 1 role predicate + 1 inventory-policy predicate · 6 slices ·
1 purge-task edit · frontend: 1 new user-settings page + 2 settings cards + 6 hooks, minus the
removed `LanguageSwitcher`. Small-to-medium, almost entirely enabling — no user-visible behaviour
beyond language persistence and the household retention knob until further settings land.

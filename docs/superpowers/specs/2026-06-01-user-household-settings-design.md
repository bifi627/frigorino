# User & Household Settings — Infrastructure + Two Anchor Settings

**Date:** 2026-06-01
**Branch:** `feat/settings-infrastructure` (off `stage`)
**Status:** Design approved, pending implementation plan

## Goal

Stand up the storage + read/write seams for two structurally separate preference scopes
so future preferences are "add a field + a control," not "design the whole settings
subsystem." The two scopes stay distinct from day one to avoid overloading one bag with
mode-dependent meaning:

- **User settings** — personal, per-signed-in-user, affect only that user across all their
  households. No role check (you can only edit your own).
- **Household settings** — household-wide, affect everyone in the household, editable only
  by Owner/Admin.

This is **infrastructure first**, but each scope is **anchored on one real setting** so the
full read/write path is exercised end-to-end rather than shipping untested plumbing:

- **User anchor:** `Language` (`en`/`de`) — i18n already exists; persisting it server-side
  per user is real and useful.
- **Household anchor:** `CheckedItemRetentionDays` — replaces the hard-coded 30-day purge
  constant in `DeleteInactiveItems` with a real shared knob.

## Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Ship empty or anchored? | Anchored on one real setting per scope | Proves the path end-to-end; gives typed columns something concrete to carry. |
| Storage shape | Typed flat columns, two tables | Aligns with the flat-schema preference; queryable; both anchors have known shape. Revisit JSON only if settings sprawl into dozens of rarely-queried toggles. |
| Household-settings authz placement | **Sibling-entity pattern** (like Lists), NOT a `Household` aggregate method | `HouseholdAccessQueries.cs` carries an explicit note that the `Household` aggregate stays focused on membership/tenancy invariants and must not become a god-aggregate every sibling routes through. Lists/Inventories already establish the sibling pattern. |
| Row creation | Lazy — no row until first write; reads of a missing row return defaults | Mirrors the existing lazy `User`-row sync pattern. |

## 1. Data model (typed columns, two new tables)

### `UserSettings`
- **PK** `UserId` (string, FK → `User.ExternalId`, cascade delete).
- `Language string?` — nullable. **`null` = fall back to client browser detection**, so an
  unset user is behaviourally identical to today.
- `CreatedAt` / `UpdatedAt` — auto-stamped centrally in `ApplicationDbContext.SaveChangesAsync`.
- **No `IsActive`** — settings ride the parent's lifecycle; they are not independently
  soft-deleted.

### `HouseholdSettings`
- **PK** `HouseholdId` (int, FK → `Household.Id`, cascade delete).
- `CheckedItemRetentionDays int`.
- `CreatedAt` / `UpdatedAt` — auto-stamped. No `IsActive`.

Both are **1:1 with their parent** and **lazy-created on first write**.

## 2. Domain (constants + validating methods)

- **`UserSettings`** (new entity, `Frigorino.Domain/Entities/`):
  - const `SupportedLanguages = ["en", "de"]`.
  - `Create(string userId)` factory.
  - `SetLanguage(string? lang)` → validates `lang ∈ SupportedLanguages || lang is null`;
    returns `Result` with `"Property"` metadata key on failure (→ `ValidationProblem`).
- **`HouseholdSettings`** (new entity):
  - consts `DefaultCheckedItemRetentionDays = 30`, `MinRetentionDays = 1`,
    `MaxRetentionDays = 365`.
  - `Create(int householdId)` factory.
  - `SetCheckedItemRetentionDays(int days)` → bounds-validates; returns `Result` with
    `"Property"` metadata on failure.
- **`HouseholdRoleExtensions`**: add `CanManageSettings(this HouseholdRole role) => role >= HouseholdRole.Admin`
  (the canonical home for household role policy).

Default constants live on the entities (mirroring `Household.NameMaxLength`) and are the
single source of truth for both the aggregate methods and the read-side defaults.

## 3. Slices (4 endpoints, no controllers)

### Me group (`/api/me`)
- `GET /api/me/settings` — inline EF projection from `UserSettings` into the response DTO;
  returns defaults when no row exists. Identity via `ICurrentUserService.UserId`.
- `PUT /api/me/settings` — load-or-create `UserSettings`, `SetLanguage`, persist. No role
  check. Validation failure → `ValidationProblem`.

### New household group (`/api/household/{householdId:int}/settings`)
Wired in `Program.cs` alongside the other `/api/household/{householdId:int}/...` groups.
- `GET` — any active member reads: `FindActiveMembershipAsync` (null → 404), inline
  projection with defaults when no row.
- `PUT` — `FindActiveMembershipAsync` (null → 404); `!membership.Role.CanManageSettings()`
  → 403; load-or-create `HouseholdSettings`, `SetCheckedItemRetentionDays`, persist.
  Validation failure → `ValidationProblem`.

Each slice = one file, request DTO + response DTO + registration + handler colocated, per
`knowledge/Vertical_Slices.md`.

## 4. Purge wiring — the one real behaviour change

`DeleteInactiveItems` (`Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs`) currently
hard-codes `DateTime.UtcNow.AddDays(-30)` for checked-off list items (lines 22-23). That
branch becomes **per-household**:

- Effective threshold per household = `now - COALESCE(settings.CheckedItemRetentionDays, 30) days`.
- Households **without** a settings row keep today's exact 30-day behaviour (the default
  fallback).
- The `!IsActive` delete branches (households, inventories, lists, inventory items) are
  **untouched**.

**Implementation note (not a design fork):** per-row interval arithmetic is awkward in a
single EF `ExecuteDeleteAsync`. The plan will choose between (a) grouping households by
distinct retention value and running one delete per value (small N), or (b) a raw
SQL / interval expression. Either is acceptable; correctness + the default-30 fallback are
the requirements.

## 5. Frontend

- `routes/settings/index.tsx` — thin shell (`createFileRoute` + `requireAuth`) importing the
  page from `features/settings/pages/`.
- `SettingsPage` — two sections:
  - **Personal:** language select (`en`/`de`).
  - **Household:** retention-days input. **Hidden / read-only when the active household role
    is not Owner/Admin.**
- Hooks (canonical one-per-file shape under `features/settings/`):
  - `useUserSettings` (query) / `useUpdateUserSettings` (arg-less mutation).
  - `useHouseholdSettings` (query, path `householdId` from active household, `enabled`
    guarded `> 0`) / `useUpdateHouseholdSettings` (arg-less mutation).
  - Never hand-write `queryFn`/`mutationFn`/`queryKey`; spread the generated
    `getXOptions` / `xMutation` / `getXQueryKey`.
- **Language payoff (visible end-to-end proof of the user anchor):**
  - On successful user-settings save → `i18n.changeLanguage(lang)`.
  - On app boot after auth → read user settings; apply `Language` if set, else keep i18next
    browser detection.
- Add a Settings entry point to the existing navigation.

## 6. Testing

- **Domain units** (`Frigorino.Test`, xUnit + FakeItEasy):
  - `UserSettings.SetLanguage`: `en`/`de` ok, `null` ok, `fr` fails.
  - `HouseholdSettings.SetCheckedItemRetentionDays`: in-bounds ok, below-min fails,
    above-max fails.
  - `CanManageSettings`: Owner/Admin true, Member false.
- **Slice behaviour:** household `PUT` as Member → 403, as Admin → 200; lazy-create on first
  write for both scopes.
- **Purge:** two households with different retention values purge checked items on their own
  thresholds; a household with no settings row uses 30.
- No JS test runner (unchanged).

## 7. Migration & scope

- **One EF migration** adding `UserSettings` + `HouseholdSettings`; applied at startup via
  `context.Database.MigrateAsync()`.
- **No new project** ⇒ no `Dockerfile` change.
- API client regenerated via `npm run api` after the slices land.

### Out of scope
Theme, default-landing-view, notification prefs, any knob beyond the two anchors, and any
behaviour beyond the retention-days purge. Each additional preference is a follow-up that
adds a field + a control on top of this infrastructure.

## Impact / cost

1 EF migration · 2 entities + 1 role predicate · 4 slices · 1 purge-task edit · frontend
Settings area with 4 hooks. Small-to-medium, almost entirely enabling — no user-visible
behaviour beyond the language persistence and the retention-days knob until further
settings are added.

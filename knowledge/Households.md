# Households

A **household** is the top-level multi-tenant container: every list, inventory, recipe, blueprint, and membership is scoped to one. It is the tenant boundary the whole app partitions on. This doc covers the household itself, its **settings**, its sort **blueprints**, and the **active-household / user-settings** surface under `/api/me`. Membership (roles, add/remove) is large enough to live in its own doc — see `Members.md`.

Same shape as the rest of the app: one vertical slice per endpoint, domain rules in the `Household` aggregate (factory + methods returning `FluentResults.Result`).

## Domain (`Frigorino.Domain/Entities/`)

| Entity | Key fields | Notes |
|---|---|---|
| `Household.cs` | `Name`, `Description?`, `CreatedByUserId` | Aggregate root **and** multi-tenant separator. `Create(name, description, ownerUserId)` factory seeds the owner's `UserHousehold` in the nav collection so one `SaveChanges` persists household + owner membership in one transaction. **Owns membership mutations** (`AddMember` / `RemoveMember` / `ChangeMemberRole` / `SoftDelete`) — see `Members.md`. `NameMaxLength = 255`, `DescriptionMaxLength = 1000`. |
| `UserHousehold.cs` | `UserId`, `HouseholdId`, `Role`, `JoinedAt` | Join entity carrying `HouseholdRole` (Member/Admin/Owner). Documented in `Members.md`. |
| `HouseholdSettings.cs` | `CheckedItemRetentionDays` (default 30, 1–365) | One row per household. `SetCheckedItemRetentionDays` validates the range. Drives the checked-list-item purge in `DeleteInactiveItems`. |
| `SortBlueprint.cs` + `SortBlueprintCategory.cs` | `Name`, ordered `Categories` (`ProductCategory[]`) | Household-scoped named **walk-order** over supermarket aisles. `Create`/`Update`/`SoftDelete`/`Restore`; `CreateDefault` seeds a 23-aisle "Supermarket" starter. Sentinels (`Unknown`/`Other`) may never be ranked. Any member may curate/apply — no role gate. |
| `UserSettings.cs` | `Language?` (en/de), `ExpiryNotificationsEnabled`, `ExpiryLeadDays` (default 3) | **User-level**, not household-scoped. Global expiry-notification opt-in + fallback lead window, plus UI language. `SetLanguage` / `SetExpiryNotifications` validate. |

## API surface (`Frigorino.Features/`)

All under `RequireAuthorization()`. Household-scoped groups check active membership in each handler via `db.FindActiveMembershipAsync(householdId, userId, ct)`.

- **Households** (`Households/`, `/api/household`): `POST /` (CreateHousehold — *the* canonical new-slice reference, `CreateHousehold.cs:1-20`), `GET /` (GetUserHouseholds), `DELETE /{id}` (DeleteHousehold — owner-only, cascades memberships). `GET /{id}` and `PUT /{id}` are deliberately absent (see decisions).
- **Settings** (`Households/Settings/`, `/api/household/{householdId}/settings`): `GET /`, `PUT /` (CheckedItemRetentionDays).
- **Blueprints** (`Households/Blueprints/`, `/api/household/{householdId}/blueprints`): `GET /`, `GET /{id}`, `POST /`, `PUT /{id}`, `DELETE /{id}`, `POST /{id}/restore`.
- **Me** (`Me/`, `/api/me`): `GET`/`PUT /active-household` (active-household switch), `GET`/`PUT /settings` (language), `PUT /settings/notifications` (expiry opt-in + lead days).

## Key flows

- **Active household.** `ICurrentHouseholdService` holds the active household id in the **HTTP session** and persists it to `User.LastActiveHouseholdId` as a durable fallback. `SetActiveHousehold` (`PUT /api/me/active-household`) mutates session state (Forbid if the caller isn't a member); it does **not** touch the JWT — which is why session middleware is mandatory (see `Backend_Architecture.md`).
- **Apply blueprint** (lives on the Lists side, `Lists/Blueprints/ApplyBlueprint.cs`): reads a list's unchecked items, resolves each item's category from the `Product` catalog by normalized name, and bulk re-ranks by the blueprint's category order. Sentinel/unclassified items sink to the bottom. See `Lists.md`.

## Key decisions & rationale

- **Household is the multi-tenant separator, not a god-aggregate.** It legitimately **owns membership** (the member mutations are `Household` aggregate methods because last-Owner protection, role policy, and reactivation are household invariants). But `List`, `Inventory`, and `Recipe` are **peer aggregates**, not children — they reference the household by id, and the auth-boundary question ("is the caller an active member?") is answered by a shared *query* (`HouseholdAccessQueries.FindActiveMembershipAsync`), not by loading the `Household`. Routing every sibling write through a method on `Household` would force loading that aggregate on every interaction and accrete `CreateList`/`CreateInventory`/… into a god-aggregate with tenant-wide lock contention. Small-aggregates DDD (Vernon, *Effective Aggregate Design*): aggregates reference each other by id; cross-aggregate checks are queries. **This is the canonical statement of that rationale — `Lists.md` / `Inventories.md` / `Members.md` link here.**
- **`GET /{id}` and `PUT /{id}` are deliberately dropped.** Both had zero hand-written frontend consumers (`useUpdateHousehold` was exported but never imported; details were composed client-side from the cached list). Dropped rather than migrated as orphan API surface. **Reintroduce only with a real consumer** (an inline-rename UI or a `/household/{id}/settings` rich screen): write the slice, add `Household.Update(name, description)` mirroring the `Create` factory, preserve Owner/Admin-only permission, and add the hook in the same change.
- **Active household lives in session + a durable column, not the JWT.** Switching households must not require re-minting a token; the session is authoritative for the request, `User.LastActiveHouseholdId` survives session expiry.
- **Blueprints have no role gate.** Applying one is just a reorder of a shared list; curating one is low-stakes. Any active member may create/edit/apply.

## Cross-feature touchpoints

- **Settings → maintenance**: `HouseholdSettings.CheckedItemRetentionDays` is read by the `DeleteInactiveItems` startup task to purge checked-off list items past the retention window. See `Backend_Architecture.md`.
- **User settings → push**: `UserSettings.ExpiryNotificationsEnabled` + `ExpiryLeadDays` are the global opt-in and fallback lead window the expiry scan reads (per-inventory overrides layer on top). See `Push_Notifications.md`.
- **Blueprints → Lists**: blueprints are curated here but applied to lists (`ApplyBlueprint` re-ranks by `Product` category). See `Lists.md` and `AI_Classification.md` (the product catalog).
- **Active household → everything**: every household-scoped slice resolves the tenant through `ICurrentHouseholdService` / `FindActiveMembershipAsync`.

## Frontend (`ClientApp/src/features/households/`)

Route shells under `src/routes/household/` delegate to `features/households/pages/`. One-hook-per-file: `useUserHouseholds`, `useCreateHousehold`, `useDeleteHousehold`, plus the active-household and settings hooks. First-run onboarding: `routes/index.tsx` redirects a signed-in user with no households to `/onboarding` (household-create + Skip), persisted via `features/households/onboardingSkip.ts`. Blueprints + household/user settings UIs live under `features/households/` and `features/settings/`.

## Links out

- Slice pattern: `Vertical_Slices.md`
- Request pipeline / session / maintenance tasks: `Backend_Architecture.md`
- API client + hook conventions: `API_Integration.md`
- Membership (roles, add/remove): `Members.md`
- Blueprint application + product catalog: `Lists.md`, `AI_Classification.md`
- Expiry notifications (consumes user settings): `Push_Notifications.md`

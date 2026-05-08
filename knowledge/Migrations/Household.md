# Household feature — vertical slice migration tracker

Status legend: ✅ Done · 🚧 In progress · ⬜ Not started · ❌ Dropped

This tracker covers Household-level CRUD only (`/api/household` and `/api/household/{id}`). Member, list, inventory, and active-household concerns have separate trackers (or aren't tracked yet). **Status: complete** — the three live slices (POST, GET-list, DELETE) shipped; the two single-household reads/writes (GET/{id}, PUT/{id}) were dropped because they had zero UI consumers. Re-open if a real consumer appears.

## Conventions

- Each slice follows the rules in `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`.
- Shared response DTO: `HouseholdResponse` at `Application/Frigorino.Features/Households/HouseholdResponse.cs`.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.
- Endpoint wiring: `Application/Frigorino.Web/Program.cs` (look for the `app.MapXxx()` block ~line 90).
- Frontend regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`.

## Slice inventory

### ✅ POST /api/household — CreateHousehold

File: `Application/Frigorino.Features/Households/CreateHousehold.cs` (canonical reference for new slices).

### ✅ GET /api/household — GetUserHouseholds

File: `Application/Frigorino.Features/Households/GetUserHouseholds.cs`. Inline EF projection, lean `HouseholdResponse[]`.

### ❌ GET /api/household/{id} — Dropped (not migrated)

The legacy endpoint had **zero hand-written frontend consumers** — `useCurrentHouseholdWithDetails` (`src/hooks/useHouseholdQueries.ts:193`) composes household details client-side by `.find()`-ing the active household inside the cached `useUserHouseholds()` list. No UI calls a single-household-by-id read, and the list-vs-detail shapes are identical today. So the endpoint was orphan API surface.

Decision: drop entirely rather than migrate. Both the legacy controller action and (briefly) a vertical-slice reimplementation were removed.

Reintroduce only when a real consumer appears (e.g. an invite-preview screen or a `HouseholdDetailResponse` rich shape that the list can no longer back). At that point write the slice and the hook in the same change.

**Carry-over status:** the private `HouseholdService.GetHouseholdAsync` and `HouseholdMappingExtensions.ToDto(this UserHousehold)` that initially survived this drop (kept alive by `UpdateHouseholdAsync`) were removed with the PUT drop — see below.

---

### ❌ PUT /api/household/{id} — Dropped (not migrated)

Same story as GET/{id}: the legacy endpoint had **zero hand-written frontend consumers**. `useUpdateHousehold` (`src/hooks/useHouseholdQueries.ts`) was exported but never imported anywhere — no inline-rename UI, no household-settings dialog, neither household route (`create.tsx`, `manage.tsx`) consumes it. The hook plus the entire `HouseholdController` → `HouseholdService.UpdateHouseholdAsync` chain was orphan API surface.

Decision: drop entirely. Removed in one purge: the legacy controller (file deleted — no actions left), `IHouseholdService.UpdateHouseholdAsync`, `HouseholdService.UpdateHouseholdAsync`, the GET-carry-over private `HouseholdService.GetHouseholdAsync`, `HouseholdMappingExtensions.ToDto(this UserHousehold)`, `HouseholdMappingExtensions.UpdateFromRequest`, the legacy `Frigorino.Domain.DTOs.HouseholdDto`, the legacy `Frigorino.Domain.DTOs.UpdateHouseholdRequest`, the frontend `useUpdateHousehold` hook, and the dead `householdKeys.list/details/detail` query selectors that fell out of use.

Reintroduce only when a real consumer appears (an inline-rename UI or a `/household/{id}/settings` screen). At that point write the slice, add `Household.Update(name, description) → Result<Household>` as an instance method on the entity (mirroring the existing `Create` factory), validate non-empty name with `WithMetadata("Property", nameof(Name))`, return `Results<Ok<HouseholdResponse>, NotFound, ForbidHttpResult, ValidationProblem>`, preserve the legacy permission rule (Owner/Admin only; Member → 403), and add the matching hook in the same change.

---

### ✅ DELETE /api/household/{id} — DeleteHousehold

File: `Application/Frigorino.Features/Households/DeleteHousehold.cs`. Owner-only soft-delete; flips `Household.IsActive` and loops included memberships in one save. `Results<NoContent, NotFound, ForbidHttpResult>`. Frontend `useDeleteHousehold` hook now calls `ClientApi.households.deleteHousehold(id)`.

---

## Cross-slice cleanup — done

Folded into the PUT drop in one purge: `HouseholdController.cs` deleted (no actions left), `HouseholdMappingExtensions` shrunk to `User.ToDto()` + `UserHousehold.ToMemberDto()` (the only ones the member methods still need), `Frigorino.Domain/DTOs/HouseholdDto.cs` shrunk to `UserDto`, `HouseholdMemberDto`, `AddMemberRequest`, `UpdateMemberRoleRequest`. `IHouseholdService` / `HouseholdService` keep their member-management methods until the Members tracker runs. Final `npm run api` confirmed: only `HouseholdsService.ts` (plural, from `WithTags("Households")`) is generated; the legacy singular `HouseholdService.ts` and the `HouseholdDto.ts` / `UpdateHouseholdRequest.ts` model files are gone.

## Deferred / out of scope

- **Lightweight CQRS** (`IHouseholdQueries` / `IHouseholdRepository`): see `IDEAS.md` entry. Not required for these three slices. Apply retroactively if/when desired.
- **`HouseholdDetailResponse`** (rich shape with `Members[]` + `MemberCount` + `CreatedByUser`): deferred. Open only if a real UI surface requires it.
- **MembersController endpoints** (5 endpoints): own tracker, run after Household CRUD lands.

## Cross-references

- Slice rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`
- Slice doc: `knowledge/Vertical_Slices.md`
- CQRS idea: `IDEAS.md` — "Lightweight CQRS: query repositories + domain repositories"
- API regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`

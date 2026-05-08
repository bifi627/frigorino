# Household feature — vertical slice migration tracker

Status legend: ✅ Done · 🚧 In progress · ⬜ Not started · ❌ Dropped

This tracker covers Household-level CRUD only (`/api/household` and `/api/household/{id}`). Member, list, inventory, and active-household concerns have separate trackers (or aren't tracked yet). When picking up work: read this file, find the next `⬜` slice, execute its scope, mark it `✅`, commit. The "Cross-slice cleanup" section runs after the remaining PUT slice lands.

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

**What stayed behind:** `HouseholdService.GetHouseholdAsync` (now `private`) and `HouseholdMappingExtensions.ToDto(this UserHousehold)` — both still consumed by the not-yet-migrated `UpdateHouseholdAsync`. They die naturally when the PUT slice migrates.

---

### ⬜ PUT /api/household/{id} — UpdateHousehold

**Permission rule** (preserve from legacy): only `Owner` or `Admin` can update; `Member` is 403.

**Domain**

- Add instance method `Household.Update(string name, string? description) → Result<Household>` mirroring the static `Create` factory. Validates non-empty `name` (`WithMetadata("Property", nameof(Name))`), trims fields, sets `UpdatedAt = UtcNow`.
- This replaces `HouseholdMappingExtensions.UpdateFromRequest` (removed in this slice).

**Backend**

- Create `Application/Frigorino.Features/Households/UpdateHousehold.cs`.
  - Sealed record `UpdateHouseholdRequest(string Name, string? Description)` in the slice file.
  - `MapPut("/api/household/{id:int}", Handle).RequireAuthorization().WithName("UpdateHousehold").WithTags("Households")`.
  - Return type: `Results<Ok<HouseholdResponse>, NotFound, ForbidHttpResult, ValidationProblem>`.
  - Handler steps:
    1. Load `UserHousehold` with `Include(uh => uh.Household)` filtered by `(currentUser.UserId, id, IsActive=true, Household.IsActive=true)`. `NotFound` if null.
    2. If `userHousehold.Role == Member`, `TypedResults.Forbid()`.
    3. Call `userHousehold.Household.Update(request.Name, request.Description)` → `ToValidationProblem()` on failure.
    4. `db.SaveChangesAsync(ct)` once.
    5. `Ok(HouseholdResponse.From(household, userHousehold.Role))`.
- Wire in `Program.cs`.
- Remove from legacy: `HouseholdController.UpdateHousehold`, `IHouseholdService.UpdateHouseholdAsync`, `HouseholdService.UpdateHouseholdAsync`, `HouseholdMappingExtensions.UpdateFromRequest`, `Frigorino.Domain.DTOs.UpdateHouseholdRequest`.
- Carried over from the GET decision (orphan endpoint dropped): `HouseholdService.GetHouseholdAsync` (now `private`, only consumer is `UpdateHouseholdAsync`) and `HouseholdMappingExtensions.ToDto(this UserHousehold)`. Both become dead the moment `UpdateHouseholdAsync` migrates — remove them in the same commit.

**Frontend**

- After regen: `ClientApi.household.putApiHousehold(id, body)` → `ClientApi.households.updateHousehold(id, body)`. Update `useUpdateHousehold` in `src/hooks/useHouseholdQueries.ts:96-117`.
- `useUpdateHousehold` is currently exported but **has no UI consumer** — backend rename only.
- Generated `UpdateHouseholdRequest` type shape unchanged (Name, Description); no caller adjustments.

**Tests** — optional: owner+admin can update; member returns 403; empty name returns 400 ValidationProblem.

---

### ✅ DELETE /api/household/{id} — DeleteHousehold

File: `Application/Frigorino.Features/Households/DeleteHousehold.cs`. Owner-only soft-delete; flips `Household.IsActive` and loops included memberships in one save. `Results<NoContent, NotFound, ForbidHttpResult>`. Frontend `useDeleteHousehold` hook now calls `ClientApi.households.deleteHousehold(id)`.

---

## Cross-slice cleanup (run after PUT lands)

1. **`HouseholdController.cs`** — empty by then; delete the file.
2. **`IHouseholdService` / `HouseholdService`** — keep. Member-management methods still live there until the Members tracker runs.
3. **`HouseholdMappingExtensions.cs`**:
   - Remove `UserHousehold.ToDto()` (already PUT-slice-deferred — see PUT entry above).
   - Remove `UpdateFromRequest` (PUT migration removes it).
   - Keep `User.ToDto()` and `UserHousehold.ToMemberDto()` — still consumed by member methods.
4. **`Frigorino.Domain/DTOs/HouseholdDto.cs`**:
   - Remove `HouseholdDto`. Verify with grep first.
   - Remove `UpdateHouseholdRequest` (replaced by the slice record).
   - Keep `UserDto`, `HouseholdMemberDto`, `AddMemberRequest`, `UpdateMemberRoleRequest` for the member methods.
5. **Frontend regen** — final `npm run api` after cleanup. Confirm the generated `src/lib/api/services/HouseholdService.ts` (singular, legacy controller) is gone and only `HouseholdsService.ts` (plural, from `WithTags("Households")`) remains.

## Recommended execution order

Only **PUT** remains. (DELETE done; GET/{id} dropped — see slice entries.)

PUT introduces the new `Household.Update` domain method (biggest design touch) and removes the most legacy code (`UpdateFromRequest`, the legacy `UpdateHouseholdRequest` DTO, plus the GET-decision carry-overs).

Run `dotnet build` + `dotnet test` + `npm run api` + `npm run tsc` before landing.

## Deferred / out of scope

- **Lightweight CQRS** (`IHouseholdQueries` / `IHouseholdRepository`): see `IDEAS.md` entry. Not required for these three slices. Apply retroactively if/when desired.
- **`HouseholdDetailResponse`** (rich shape with `Members[]` + `MemberCount` + `CreatedByUser`): deferred. Open only if a real UI surface requires it.
- **MembersController endpoints** (5 endpoints): own tracker, run after Household CRUD lands.

## Cross-references

- Slice rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`
- Slice doc: `knowledge/Vertical_Slices.md`
- CQRS idea: `IDEAS.md` — "Lightweight CQRS: query repositories + domain repositories"
- API regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`

# Members feature — vertical slice migration tracker

Status legend: ✅ Done · 🚧 In progress · ⬜ Not started · ❌ Dropped

This tracker covers household-member CRUD. **Status: complete** — all four live slices migrated (`GetMembers`, `AddMember`, `RemoveMember`, `UpdateMemberRole`); the `/leave` endpoint dropped (orphan); `MembersController`, `IHouseholdService`, `HouseholdService`, `Frigorino.Domain.DTOs.HouseholdMemberDto`/`UpdateMemberRoleRequest`/`AddMemberRequest`, and `HouseholdMappingExtensions.ToMemberDto` retired in the cross-slice cleanup that landed with `UpdateMemberRole`.

## Conventions

- Each slice follows the rules in `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`.
- Folder: `Application/Frigorino.Features/Households/Members/` (per `knowledge/Vertical_Slices.md` — sub-folder under `Households/` because the URL nests, the lifecycle is parent-bound, and member ops rarely change at the same time as household-level CRUD).
- Namespace: `Frigorino.Features.Households.Members`.
- Tag: `.WithTags("Members")` on every slice, so the generated frontend service stays at `ClientApi.members.*` and current call sites keep working.
- Shared response DTO: promote `MemberResponse` to a folder-level file `Members/MemberResponse.cs` — three of the four slices return it.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.
- Endpoint wiring: `Application/Frigorino.Web/Program.cs`, in the `app.MapXxx()` block (~line 90).
- Frontend regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`.
- **URL casing** — the legacy MVC route was `/api/household/{householdId}/Members` (capital `M`, controller-name slot). Slice routes use lowercase `/members` consistently with `/api/household`. The generated TS client picks the new path up automatically; no hand-written code hardcodes a URL string, so the wire-format change is invisible to call sites.
- **Tests required per slice** — each slice MUST land with its Playwright/Reqnroll scenarios in the same change. Pick at least one valuable scenario from the "Test scenarios" section of this tracker (see below) and implement it before marking the slice ✅. Add the testids the scenarios depend on to `HouseholdMembers.tsx` / `AddMemberDialog.tsx` as part of the slice — neither component has testids today. Reusable seeding/navigation steps live in `Slices/Households/HouseholdSteps.cs` + `Shared/NavigationSteps.cs`; member-specific bindings go in `Slices/Households/Members/MemberSteps.cs`.

## Legacy → consumer audit

Verified before drafting this tracker (grep across `ClientApp/src` for each generated method):

| Legacy endpoint | Generated TS method | Hand-written consumer |
|---|---|---|
| `GET    /api/household/{id}/Members`                | `getApiHouseholdMembers`       | `useHouseholdMembers` (consumed by `HouseholdMembers.tsx`, `manage.tsx`) |
| `POST   /api/household/{id}/Members`                | `postApiHouseholdMembers`      | `useAddMember` hook (unused; `AddMemberDialog.tsx` calls the generated method directly — minor inconsistency, fold into the hook during the slice migration) |
| `PUT    /api/household/{id}/Members/{userId}/role`  | `putApiHouseholdMembersRole`   | `useUpdateMemberRole` (consumed by `HouseholdMembers.tsx`) |
| `DELETE /api/household/{id}/Members/{userId}`       | `deleteApiHouseholdMembers`    | `useRemoveMember` (consumed by `HouseholdMembers.tsx`) |
| `POST   /api/household/{id}/Members/leave`          | `postApiHouseholdMembersLeave` | **none** — orphan, drop |

So four live endpoints, one orphan. The `DELETE /{userId}` endpoint already supports self-removal (legacy `RemoveMemberAsync` allows `targetUserId == userId` even for the Member role), so dropping `/leave` loses nothing.

## Slice inventory

### ✅ GET /api/household/{householdId}/members — GetMembers

Files: `Application/Frigorino.Features/Households/Members/GetMembers.cs` + `Members/MemberResponse.cs`. Membership-existence `AnyAsync` guard → `NotFound` for non-members; inline EF projection ordered Owner → Admin → Member, then by `JoinedAt`. Frontend `useHouseholdMembers` calls `ClientApi.members.getMembers(householdId)`; `HouseholdMembers.tsx` reads the flat shape (`member.externalId/name/email`). Testids added: `household-members-list`, `household-member-{externalId}`, `household-member-{externalId}-role`. Reusable seeding step `Given the household also has "{alias}" as a "{role}"` added to `HouseholdSteps.cs` (member/admin/owner) for downstream slices. Playwright scenario lives in `Slices/Households/Members/Members.feature` + `MemberSteps.cs`. `GetUserRoleInHouseholdAsync` private helper still alive — used by the surviving Add/Update/Remove members methods, retire when the last of those slices lands.

### ✅ POST /api/household/{householdId}/members — AddMember

File: `Application/Frigorino.Features/Households/Members/AddMember.cs`. Sealed record `AddMemberRequest(Email, Role?)` in the slice file. Email-shape validation, caller membership lookup (NotFound / Forbid for Member), case-insensitive target lookup, existing-membership branch (active → ValidationProblem `"already a member"`; inactive → reactivate + role/JoinedAt refresh), domain factory `UserHousehold.CreateMembership` (added on the entity, mirrors `Household.Create`). One `SaveChangesAsync`. Returns `Created<MemberResponse>` with `Location: /api/household/{id}/members/{externalId}`. Legacy `MembersController.AddMember`, `IHouseholdService.AddMemberAsync`, `HouseholdService.AddMemberAsync`, and `Frigorino.Domain.DTOs.AddMemberRequest` removed (the legacy class would have collided with the slice record on the OpenAPI schema name). Frontend: `useAddMember` now calls `ClientApi.members.addMember`; `AddMemberDialog.tsx` consumes the hook (was calling `ClientApi` directly), reads validation errors from `ApiError.body.errors.email[0]`. Testids added on the dialog: `household-add-member-button` (in `HouseholdMembers.tsx`), `-email-input`, `-role-select`, `-submit`, `-cancel`, `-error`. Reusable seeding step `Given a user "{alias}" exists` added to `HouseholdSteps.cs`.

### ✅ PUT /api/household/{householdId}/members/{userId}/role — UpdateMemberRole

File: `Application/Frigorino.Features/Households/Members/UpdateMemberRole.cs`. Sealed record `UpdateMemberRoleRequest(Role)` colocated. Caller membership lookup (`NotFound` / `Forbid` Member). Target lookup with `Include(User)` so the response can be projected without a second query. Owner-protection: only Owner can change another Owner. Last-Owner self-demote guard (`ValidationProblem` keyed `"role"`). One `SaveChangesAsync`. Returns `Ok<MemberResponse>`. Frontend: `useUpdateMemberRole` calls `ClientApi.members.updateMemberRole`. Testids added: `household-member-action-make-member`, `household-member-action-make-admin`.

### ✅ DELETE /api/household/{householdId}/members/{userId} — RemoveMember

File: `Application/Frigorino.Features/Households/Members/RemoveMember.cs`. Caller membership lookup → target membership lookup (`NotFound` for either absent / inactive). Self-removal short-circuits the role guard (any role can remove themselves). Cross-user removal requires Owner/Admin; Member→Forbid. Last-Owner protection: if target is Owner and active-Owner count ≤ 1 → `ValidationProblem` keyed on `"userId"`. Soft-delete via `target.IsActive = false`, one `SaveChangesAsync`. Frontend: `useRemoveMember` calls `ClientApi.members.removeMember`. Testids added: `household-member-{externalId}-menu-toggle`, `household-member-action-remove`, `household-member-remove-confirm`, `household-member-remove-cancel`.

### ✅ POST /api/household/{householdId}/members/leave — Dropped (with RemoveMember)

Folded into the RemoveMember drop: legacy `MembersController.LeaveHousehold`, `IHouseholdService.LeaveHouseholdAsync`, and `HouseholdService.LeaveHouseholdAsync` (the one-line `RemoveMemberAsync` wrapper) all removed. Self-removal is covered by the new DELETE slice (the role guard short-circuits when caller equals target).

## Cross-slice cleanup — done

Folded into the UpdateMemberRole drop (last live slice):

1. ✅ **`MembersController.cs`** — file deleted (empty after UpdateMemberRole moved out).
2. ✅ **`IHouseholdService.cs`** — file deleted (interface empty).
3. ✅ **`HouseholdService.cs`** — file deleted (incl. `GetUserRoleInHouseholdAsync` private helper that died with the last service method).
4. ✅ **`AddApplicationServices()`** — `services.AddScoped<IHouseholdService, HouseholdService>()` line dropped from `Frigorino.Application/DependencyInjection.cs`.
5. ✅ **`HouseholdMappingExtensions.cs`** — `UserHousehold.ToMemberDto()` removed. **Note:** `User.ToDto()` SURVIVED — still consumed by `ListMappingExtensions` and `InventoryMappingExtensions` (both `*.CreatedByUser = list/inventory.CreatedByUser.ToDto()`). The file shrank to that one extension.
6. ✅ **`Frigorino.Domain/DTOs/HouseholdDto.cs`** — `HouseholdMemberDto` and `UpdateMemberRoleRequest` removed (`AddMemberRequest` was already gone). **Note:** `UserDto` SURVIVED — still consumed by `ListDto.CreatedByUser`, `InventoryDto.CreatedByUser`, and `ListMappingExtensions`/`InventoryMappingExtensions`. The file shrank to that one type. Filename intentionally not renamed to keep diff churn low.
7. ✅ **Frontend regen** — final `npm run api`. Generated `MembersService.ts` exposes `getMembers`/`addMember`/`removeMember`/`updateMemberRole` on lowercase `/members` paths; legacy DTOs `HouseholdMemberDto.ts` no longer regenerate. `UserDto.ts` and `UpdateMemberRoleRequest.ts` are still emitted (they came back from the slice record + the surviving `UserDto`, same shapes, no consumer changes needed).

## Test scenarios (Reqnroll + Playwright)

The Households slices added a member-seeding step (`HouseholdSteps.cs` — `Given an existing household {string} owned by {string} with me as a {string}`) plus `TestApiClient.SetCurrentHouseholdAsync`. Members tests reuse that infrastructure — every scenario starts from a household seeded by the step, with a known role for the caller.

Add a **new feature file** `Application/Frigorino.IntegrationTests/Slices/Households/Members/Members.feature`, plus `Members/MemberSteps.cs` for member-specific bindings. Steps that are generic (login, navigation, household seeding) reuse the existing files.

**Testids to add to `HouseholdMembers.tsx` and `AddMemberDialog.tsx`** (no testids exist on these components yet — call this out in each slice's commit):

- `household-members-list` — wrapper around the `<List>` of members
- `household-member-{externalId}` — each row
- `household-member-{externalId}-role` — the role chip
- `household-member-{externalId}-menu-toggle` — kebab `IconButton`
- `household-member-action-make-member`, `-make-admin`, `-remove` — `MenuItem`s
- `household-member-remove-confirm`, `household-member-remove-cancel` — confirmation dialog buttons
- `household-add-member-button` — the "Add Member" button
- `household-add-member-email-input` — `TextField` (use `slotProps.htmlInput["data-testid"]`)
- `household-add-member-role-select` — `Select`
- `household-add-member-submit`, `household-add-member-cancel` — dialog buttons
- `household-add-member-error` — the `Alert` rendered when the mutation rejects

### Scenarios to add (one valuable test per slice + the high-leverage negatives)

The list below is intentionally **selective**. UI-guard combinatorics (canChangeRole × canRemoveMember × role × target-role) belong in unit tests, not Playwright runs. The scenarios below cover behavior that's load-bearing for users and easy to break without noticing.

#### GetMembers

- ✅ **Owner sees seeded members in role order** — Implemented as `Slices/Households/Members/Members.feature` → `Owner sees all members ordered Owner, Admin, Member`. Asserts row count, per-row role chip, and DOM order via `household-members-list` direct children.

#### AddMember

- ✅ **Owner adds an existing user by email** — Implemented as `Members.feature` → `Owner adds an existing user as a member`. Uses the new `Given a user "{alias}" exists` seeding step.
- ✅ **Adding an unknown email shows an inline error** — Implemented as `Adding an unknown email shows an inline error`. Asserts on `household-add-member-error` text + member-list count unchanged.
- ✅ **Adding an existing active member shows an inline error** — Implemented as `Adding an existing active member shows an inline error`.

#### UpdateMemberRole

- ✅ **Owner promotes a Member to Admin** — Implemented as `Members.feature` → `Owner promotes a Member to Admin`. Asserts the role chip text changes from "Member" to "Admin" via the retrying `ToHaveTextAsync` assertion (covers cache invalidation + refetch).
- ✅ **Last Owner cannot demote themselves** — Functionally equivalent to `Last Owner cannot be removed via the UI` (which is already implemented under RemoveMember). In a single-Owner household, both `canRemoveMember` and `canChangeRole` return false for the Owner row → the kebab `IconButton` itself is not rendered. The same testid assertion (`household-member-{owner-externalId}-menu-toggle` not visible) covers both demote-self and remove-self last-Owner cases. Skipping a duplicate scenario.

#### RemoveMember

- ✅ **Owner removes a Member** — Implemented as `Members.feature` → `Owner removes a Member`. Seeds Member, opens kebab, clicks remove, confirms; asserts list count drops to 1.
- ✅ **Member-role caller cannot see "Add Member" / cannot remove others** — Implemented as `Member-role caller cannot manage members`. Asserts `household-add-member-button` and `household-member-{owner}-menu-toggle` are not visible.
- ✅ **Last Owner cannot be removed via the UI** — Implemented as `Last Owner cannot be removed via the UI`. Single-owner household; asserts the kebab on the Owner row is not rendered (`canRemoveMember`/`canChangeRole` both false → no menu trigger).

### Scenarios deliberately skipped

- "Admin cannot demote another Admin" / "Admin cannot remove another Admin" — three more permutations of the same role-table. Cover one negative permission case (Member-can't-X) and trust unit tests in `Frigorino.Test` for the matrix.
- "User reactivation after soft-delete-then-readd" — the AddMember slice's reactivation branch is worth a unit test in `Frigorino.Test` (cheap, deterministic), not a Playwright scenario.
- Self-removal happy path. Worth verifying once manually before the slice lands; not worth the Playwright cost. If it breaks, users still have the per-household manage flow as a workaround.

## Deferred / out of scope

- **Lightweight CQRS** (`IMemberQueries` / `IMembershipRepository`): see `IDEAS.md`. Not required for these slices. Apply retroactively if/when desired.
- **Member invitations** (email-based "I invited X, they accept later" flow): not in current scope. The legacy `AddMember` is direct — caller knows the target's email and the target gets added immediately. An invite flow would need its own slice family and a new entity (e.g. `HouseholdInvitation`).
- **Audit log** of role changes / removals: out of scope. If added later, it's a cross-cutting `Frigorino.Features/Audit/` concern, not a Members slice change.

## Cross-references

- Slice rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`
- Slice doc: `knowledge/Vertical_Slices.md` (especially the "When to nest" section explaining why this lives at `Households/Members/`)
- Households tracker (predecessor): `knowledge/Migrations/Household.md`
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`
- Reqnroll/Playwright test infrastructure: `Application/Frigorino.IntegrationTests/Slices/Households/HouseholdSteps.cs` (the seeding step is the model for member tests)
- API regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`

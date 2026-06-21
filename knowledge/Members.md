# Household Members

Membership ties a user to a household with a **role**. It's a sub-area of Households (URL nests at `/api/household/{householdId}/members`, lifecycle is parent-bound) but documented separately because the role policy is substantial. The mutations live as **`Household` aggregate methods** — membership is a household invariant (last-Owner protection, role-grant rules), unlike `List`/`Inventory`/`Recipe` which are peer aggregates. See `Households.md` for the aggregate-boundary rationale.

## Domain (`Frigorino.Domain/Entities/`)

| Entity | Key fields | Notes |
|---|---|---|
| `UserHousehold.cs` | `UserId`, `HouseholdId`, `Role`, `JoinedAt`, `IsActive` | Join entity. `CreateMembership(userId, householdId, role)` factory. Soft-deleted (`IsActive = false`) on removal; an inactive row is **reactivated** rather than re-inserted on re-add. |
| `HouseholdRole` (enum) | `Member = 0`, `Admin = 1`, `Owner = 2` | Serializes as its **string name** on the wire (TS string union). DB stores the int. |
| `HouseholdRoleExtensions.cs` | `CanManageMembers()`, `CanGrantRole(role)` | The role policy in one place: Admin+ may manage members; only an Owner may grant/touch the Owner role. |

The mutation methods are on `Household` (`Household.cs`): `AddMember(callerUserId, targetUserId, role)`, `RemoveMember(callerUserId, targetUserId)`, `ChangeMemberRole(callerUserId, targetUserId, newRole)`, plus the cascade in `SoftDelete`. Each loads the caller from the household's membership collection, applies the role policy, and returns `Result`.

## API surface (`Frigorino.Features/Households/Members/`)

`MapGroup` `/api/household/{householdId:int}/members`, tag `Members`, all `RequireAuthorization()`. Shared `MemberResponse` (flat `ExternalId`/`Name`/`Email`/`Role`/`JoinedAt`).

- `GET /` — GetMembers. Membership-existence guard → 404 for non-members; projection ordered Owner → Admin → Member, then `JoinedAt`.
- `POST /` — AddMember. Body `{ email, role? }`. Resolves the target user by email (case-insensitive) in the handler, then calls `household.AddMember`. `Created<MemberResponse>`.
- `PUT /{userId}/role` — UpdateMemberRole. Body `{ role }`.
- `DELETE /{userId}` — RemoveMember (also serves self-removal).

## Key flows

- **AddMember**: handler does the cross-aggregate work — resolve the target `User` by email (404-ish via ValidationProblem if unknown) — then `household.AddMember` applies policy. Branches: active member already exists → ValidationProblem (`"already a member"`); **inactive** membership exists → reactivate + refresh role/`JoinedAt`; otherwise `UserHousehold.CreateMembership`. One `SaveChanges`.
- **RemoveMember / role change**: self-removal short-circuits the role guard (any role can remove themselves); cross-user ops require Admin+. Last active Owner is protected on both removal and self-demotion (ValidationProblem). Only an Owner can change another Owner.

## Key decisions & rationale

- **Member mutations are `Household` aggregate methods.** Membership is a household invariant — the last-Owner guard and role-grant rules need the whole membership set in hand. This is *not* the god-aggregate anti-pattern: `Household` owns memberships, but **does not** own `List`/`Inventory`/`Recipe` (those are peer aggregates checked via a query). See the canonical rationale in `Households.md`.
- **Cross-aggregate user resolution stays in the handler.** The aggregate takes an already-resolved target user id; resolving an email → `User` is a cross-aggregate read, so it lives in the slice, not the aggregate (which would otherwise need a `User` repository).
- **Role policy is centralized in `HouseholdRoleExtensions`.** `AddMember`/`RemoveMember`/`ChangeMemberRole` all defer to `CanManageMembers` / `CanGrantRole` so the matrix has a single source.
- **`POST /members/leave` was dropped.** `DELETE /{userId}` already covers self-removal (the role guard short-circuits when caller == target), so a dedicated leave endpoint was redundant.

## Cross-feature touchpoints

- **Auth boundary for the whole tenant**: `FindActiveMembershipAsync` (membership + role) is the gate every household-scoped slice uses. See `Households.md`.
- **Notification recipients**: the expiry scan's recipient set is the active household members (globally opted-in, with ≥1 device token). See `Push_Notifications.md`.

## Frontend (`ClientApp/src/features/households/`)

`HouseholdMembers.tsx` (list + per-row role chip + kebab actions) and `AddMemberDialog.tsx` (email + role, reads field-level `errors.email`). Hooks: `useHouseholdMembers`, `useAddMember`, `useUpdateMemberRole`, `useRemoveMember`. UI gates actions by the caller's role (no kebab on rows the caller can't manage; "Add Member" hidden for Members).

## Non-goals (recorded decisions)

- **Email invitations** ("invite X, they accept later") — would need a new `HouseholdInvitation` entity + slice family. Current `AddMember` is direct: the caller knows the target's email and they're added immediately.
- **Role-change audit log** — if added, it's a cross-cutting `Features/Audit/` concern, not a Members change.

## Links out

- Aggregate-boundary rationale: `Households.md`
- Slice pattern (esp. "when to nest"): `Vertical_Slices.md`
- API client + hook conventions: `API_Integration.md`
- Notification recipients: `Push_Notifications.md`

# Lists feature — vertical slice migration tracker

Status legend: ✅ Done · 🚧 In progress · ⬜ Not started · ❌ Dropped

This tracker covers List-level CRUD only (`/api/household/{householdId}/lists` and `/api/household/{householdId}/lists/{listId}`). ListItems (Add/Update/Delete/Toggle/Reorder/Compact) are still on the legacy `ListItemsController`/`ListItemService` and have their own future tracker. **Status: complete** — all five live slices shipped, no endpoints dropped.

## Conventions

- Each slice follows the rules in `Application/Frigorino.Features/Households/CreateHousehold.cs:1-20`.
- Shared response DTO: `ListResponse` (with colocated `ListCreatorResponse`) at `Application/Frigorino.Features/Lists/ListResponse.cs`.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.
- Endpoint wiring: `Application/Frigorino.Web/Program.cs` (the `lists` MapGroup, after `members`).
- Frontend regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`.

## Decisions

### List is its own aggregate root

Permission to edit/delete a list is **creator OR Admin+ in the household**. The role data lives on `UserHousehold` (a different aggregate). Two designs were considered:

1. Add `Household.UpdateList(listId, ...)` / `Household.DeleteList(listId, ...)` aggregate methods on Household — mirrors the Members pattern.
2. Make `List` its own aggregate with `list.Update(callerUserId, callerRole, ...)` — the handler queries the caller's `UserHousehold` separately, then passes the role in.

**Chose (2)** — keeps `Household` focused on membership invariants and avoids loading `household.Lists + UserHouseholds` for every mutation. The handler resolves the caller's role with one `FirstOrDefaultAsync` against `UserHouseholds`, then calls the aggregate method. Auth boundary ("are you a member at all?") stays in the handler; role policy ("creator OR Admin+?") stays on the aggregate.

### ListItems deferred

`ListItems*` (8 endpoints) stays on the legacy controller this round. Separate migration when scheduled — `List` aggregate is the natural place for `AddItem`/`UpdateItem`/`ReorderItem` etc., so the next round may promote some sort-order coordination logic from `ListItemService` onto `List`.

### Auth-boundary check is a query helper, not on the Household aggregate

Every Lists slice needs the same question answered: "is the caller an active member of this household?" To avoid duplicating `db.UserHouseholds.AnyAsync(...)` across five files, the check lives in `Application/Frigorino.Features/Households/HouseholdAccessQueries.cs` as a `DbContext` extension: `db.FindActiveMembershipAsync(householdId, userId, ct) → Task<UserHousehold?>`.

**Why a query, not a method on `Household` entity:** Household is the multi-tenant separator. If every sibling feature (Lists, Inventories, future) routed its auth check through a method on the `Household` aggregate, every interaction would force loading that aggregate — and over time Household would accumulate child methods (`CreateList`, `CreateInventory`, etc.), becoming a god aggregate with lock contention on every write across the tenant. Small-aggregates DDD (Vaughn Vernon, *Effective Aggregate Design*): aggregates reference each other by id; cross-aggregate queries are queries, not aggregate loads. List, Inventory, etc. stay peer aggregates to Household; the auth-boundary primitive is a shared read.

Read slices map a null result to `NotFound`; write slices use the returned `UserHousehold.Role` for the `creator-OR-Admin+` check inside `List.Update` / `List.SoftDelete`. Same predicate, single source — no duplication, no entity coupling.

### `ListCreatorResponse` is colocated, not reused from `UserDto`

The legacy `UserDto` (in `Frigorino.Domain/DTOs/HouseholdDto.cs`) survives because `InventoryService` + `InventoryItemService` still use it. The slice rule favors feature-local DTOs over shared cross-feature ones (the `Shared/` attractor anti-pattern from `Vertical_Slices.md`). So `ListCreatorResponse(ExternalId, Name, Email)` is colocated with `ListResponse`. Frontend client gets `ListCreatorResponse` as a fresh type — no rename pain because Lists was the first consumer to migrate.

## Slice inventory

### ✅ POST /api/household/{householdId}/lists — CreateList

File: `Application/Frigorino.Features/Lists/CreateList.cs`. Write-via-factory: `List.Create(name, description, householdId, createdByUserId)` returns `Result<List>` with property-keyed errors. Handler does the auth-boundary `UserHouseholds.AnyAsync` check first (NotFound if not a member), then resolves the creator User for the response. Returns `Created<ListResponse>`. Permission: any active member can create a list.

### ✅ GET /api/household/{householdId}/lists — GetLists

File: `Application/Frigorino.Features/Lists/GetLists.cs`. Read-via-projection: inline `Select` into `ListResponse` with `UncheckedCount`/`CheckedCount` computed via `.Count(...)`. Auth boundary via `UserHouseholds.AnyAsync` → NotFound. Ordered `OrderByDescending(CreatedAt)`.

### ✅ GET /api/household/{householdId}/lists/{listId} — GetList

File: `Application/Frigorino.Features/Lists/GetList.cs`. Read-via-projection, single result. Auth boundary same as GetLists. NotFound if list doesn't exist OR doesn't belong to the household.

### ✅ PUT /api/household/{householdId}/lists/{listId} — UpdateList

File: `Application/Frigorino.Features/Lists/UpdateList.cs`. Write-via-aggregate-method: handler loads caller's UserHousehold for role, then loads the List with `CreatedByUser` + `ListItems`, then calls `list.Update(callerUserId, callerRole, name, description)`. Aggregate enforces the creator-OR-Admin+ policy and the name/description validation. Returns `Results<Ok<ListResponse>, NotFound, ForbidHttpResult, ValidationProblem>`.

### ✅ DELETE /api/household/{householdId}/lists/{listId} — DeleteList

File: `Application/Frigorino.Features/Lists/DeleteList.cs`. Write-via-aggregate-method: `list.SoftDelete(callerUserId, callerRole)`. Same creator-OR-Admin+ rule. Returns `Results<NoContent, NotFound, ForbidHttpResult>`. Defensive `throw` on unmapped error types — current aggregate only emits `AccessDeniedError`, so adding a new invariant (e.g. "can't delete a list with active items") will force a compile-time touch.

---

## Domain changes

`Application/Frigorino.Domain/Entities/List.cs` grew the canonical aggregate shape:

- `const int NameMaxLength = 255`, `const int DescriptionMaxLength = 1000` — single source of truth, also referenced by `ListConfiguration` so EF and aggregate agree.
- `public static Result<List> Create(name, description, householdId, createdByUserId)` — validates all four inputs with property-keyed errors, trims `Name`/`Description`, stamps timestamps.
- `public Result Update(callerUserId, callerRole, name, description)` — role check first (returns `AccessDeniedError` on policy fail), then revalidates name/description, then mutates.
- `public Result SoftDelete(callerUserId, callerRole)` — role check, then `IsActive = false; UpdatedAt = utcNow`.

22 new unit tests in `Application/Frigorino.Test/Domain/ListAggregateTests.cs` lock the matrix.

## Frontend changes

- Regenerated client (`npm run api`) renamed types: `ListDto` → `ListResponse`. Method names changed too: `ClientApi.lists.getApiHouseholdLists` → `ClientApi.lists.getLists`, etc. (slice-derived names via `.WithName(...)`).
- Updated `src/hooks/useListQueries.ts` — straight rename: types + method calls.
- Updated `src/routes/lists/index.tsx` (`ListDto` → `ListResponse` for the menu-state type).
- Generated TS type for `description` is now `string | null` (codegen treats nullable C# strings this way). Updated `src/routes/lists/create.tsx` and `src/routes/lists/$listId/edit.tsx` to pass `null` instead of `undefined`.

## Deleted

- `Application/Frigorino.Web/Controllers/ListsController.cs`
- `Application/Frigorino.Application/Services/ListService.cs`
- `Application/Frigorino.Domain/Interfaces/IListService.cs`
- `Application/Frigorino.Application/Extensions/ListMappingExtensions.cs`
- `Application/Frigorino.Domain/DTOs/ListDto.cs` (legacy `ListDto`, `CreateListRequest`, `UpdateListRequest`)
- `services.AddScoped<IListService, ListService>()` from `Application/Frigorino.Application/DependencyInjection.cs`

`ArchitectureTests.cs` rebinding pinned `Frigorino.Application` assembly via `typeof(ListItemService).Assembly` (was `ListService.Assembly`). Update again when `ListItemService` migrates and only `Inventory*` services remain.

## Deferred / out of scope

- **ListItems migration** (8 endpoints: Get/Create/Update/Delete/Toggle/Reorder/Compact). Natural next slice batch. Will likely promote sort-order coordination onto `List` as an aggregate method.
- **Inventories migration** + **InventoryItems migration**. Same pattern when scheduled.
- **Renaming `HouseholdMappingExtensions.cs` / `HouseholdDto.cs`** — still consumed by Inventory layer. Defer until Inventories migrates.
- **Frontend feature-folder restructure for Lists** (mirror of `features/households/` work). Current `src/hooks/useListQueries.ts` stays as-is; reshape when the SPA gets time for the next features-folder round.

## Cross-references

- Slice rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-20`
- Slice doc: `knowledge/Vertical_Slices.md`
- Households tracker (precedent): `knowledge/Migrations/Household.md`
- Members tracker (precedent): `knowledge/Migrations/Members.md`
- API regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`

# Inventory feature — vertical slice migration tracker

Status legend: ✅ Done · 🚧 In progress · ⬜ Not started · ❌ Dropped

This tracker covers Inventory-level CRUD only (`/api/household/{householdId}/inventories` and `/api/household/{householdId}/inventories/{inventoryId}`). InventoryItems (Create/Get/Update/Delete/Reorder/Compact) has its own tracker at `knowledge/Migrations/InventoryItems.md`. **Status: complete** — all five live slices shipped, no endpoints dropped at the inventory level.

This migration completes the slice rollout for the four feature areas (Households, Lists, ListItems, Inventory, InventoryItems). The `Frigorino.Application` project itself was deleted — review feedback flagged the vestigial-shim `AddApplicationServices()` as dead code, so the project + reference from `Frigorino.Web`/`Frigorino.Test` + sln entry + `Application_Should_Not_Depend_On_Infrastructure` arch rule + corresponding Dockerfile COPY all went together.

## Conventions

- Each slice follows the rules in `Application/Frigorino.Features/Households/CreateHousehold.cs:1-20`.
- Shared response DTO: `InventoryResponse` (with colocated `InventoryCreatorResponse`) at `Application/Frigorino.Features/Inventories/InventoryResponse.cs`.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.
- Endpoint wiring: `Application/Frigorino.Web/Program.cs` (the `inventories` MapGroup, after `listItems`).
- Frontend regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`.

## Decisions

### Inventory is its own aggregate root (same as List)

Permission to edit/delete an inventory is **creator OR Admin+ in the household** — preserved verbatim from the legacy `InventoryService.UpdateInventoryAsync` / `DeleteInventoryAsync`. Same shape as `List.Update` / `List.SoftDelete`: handler queries the caller's `UserHousehold` via `db.FindActiveMembershipAsync(householdId, userId, ct)`, passes `Role` into the aggregate method, aggregate enforces the creator-OR-Admin+ rule and emits `AccessDeniedError` on failure. The slice handler dispatches to `Forbid()` for `AccessDeniedError`, `NotFound()` for missing membership/entity, `ValidationProblem` for property-keyed errors.

See `knowledge/Migrations/Lists.md` for the broader rationale on small-aggregates DDD and why `Household` isn't a god-aggregate.

### `TotalItems` + `ExpiringItems` projection on `InventoryResponse`

Differs from `ListResponse`'s `UncheckedCount` + `CheckedCount` because inventory items have no `Status` — they have `ExpiryDate` instead. The "expiring soon" threshold lives as `InventoryResponse.ExpiringWithinDays = 7`, used by both the `ExpiringItems` count projection here and the `IsExpiring` boolean on `InventoryItemResponse` — single source of truth, no drift between overview and per-item.

The EF projection (`InventoryResponse.ToProjection`) inlines `DateTime.UtcNow.AddDays(ExpiringWithinDays)` — EF translates `DateTime.UtcNow` to the SQL side's `now()`, so the threshold is consistently server-time across the projection.

### `InventoryCreatorResponse` is colocated, not shared with `ListCreatorResponse`

Same shape as `ListCreatorResponse` (`ExternalId, Name, Email`) but lives in its own file. Cross-folder DTO sharing is a smell per `knowledge/Vertical_Slices.md`. The generated TS client emits two separate types, frontend imports the inventory one in inventory call-sites — no rename pain.

### Auth-boundary check is `FindActiveMembershipAsync`

Same query helper used by Lists slices: `Application/Frigorino.Features/Households/HouseholdAccessQueries.cs`. Read slices map `null` → `NotFound`. Write slices use the returned `UserHousehold.Role` for the creator-OR-Admin+ check inside the aggregate method.

## Slice inventory

### ✅ POST /api/household/{householdId}/inventories — CreateInventory

File: `Application/Frigorino.Features/Inventories/CreateInventory.cs`. Write-via-factory: `Inventory.Create(name, description, householdId, createdByUserId)` returns `Result<Inventory>` with property-keyed errors. Handler does the auth-boundary `FindActiveMembershipAsync` first (NotFound if not a member), then resolves the creator `User` for the response. Returns `Created<InventoryResponse>` with `Location: /api/household/{h}/inventories/{id}`. Permission: any active member can create.

### ✅ GET /api/household/{householdId}/inventories — GetInventories

File: `Application/Frigorino.Features/Inventories/GetInventories.cs`. Read-via-projection: inline `Select` into `InventoryResponse` with `TotalItems`/`ExpiringItems` computed via `.Count(...)`. Auth boundary via `FindActiveMembershipAsync` → NotFound. Ordered `OrderByDescending(CreatedAt)`.

### ✅ GET /api/household/{householdId}/inventories/{inventoryId} — GetInventory

File: `Application/Frigorino.Features/Inventories/GetInventory.cs`. Read-via-projection, single result. Auth boundary same as GetInventories. NotFound if inventory doesn't exist OR doesn't belong to the household.

### ✅ PUT /api/household/{householdId}/inventories/{inventoryId} — UpdateInventory

File: `Application/Frigorino.Features/Inventories/UpdateInventory.cs`. Write-via-aggregate-method: handler loads caller's UserHousehold for role, then loads the Inventory with `CreatedByUser` + `InventoryItems`, then calls `inventory.Update(callerUserId, callerRole, name, description)`. Aggregate enforces creator-OR-Admin+ policy and name/description validation. Returns `Results<Ok<InventoryResponse>, NotFound, ForbidHttpResult, ValidationProblem>`.

### ✅ DELETE /api/household/{householdId}/inventories/{inventoryId} — DeleteInventory

File: `Application/Frigorino.Features/Inventories/DeleteInventory.cs`. Write-via-aggregate-method: `inventory.SoftDelete(callerUserId, callerRole)`. Same creator-OR-Admin+ rule. Returns `Results<NoContent, NotFound, ForbidHttpResult>`. Defensive `throw` on unmapped error types — current aggregate only emits `AccessDeniedError`, adding a new invariant later forces a compile-time touch.

---

## Domain changes

`Application/Frigorino.Domain/Entities/Inventory.cs` grew the canonical aggregate shape (mirrors `List`):

- `const int NameMaxLength = 255`, `const int DescriptionMaxLength = 1000` — single source of truth, also referenced by `InventoryConfiguration` so EF and aggregate agree.
- `public static Result<Inventory> Create(name, description, householdId, createdByUserId)` — validates all four inputs with property-keyed errors, trims `Name`/`Description`, stamps timestamps.
- `public Result Update(callerUserId, callerRole, name, description)` — role check first (returns `AccessDeniedError` on policy fail), then revalidates name/description, then mutates.
- `public Result SoftDelete(callerUserId, callerRole)` — role check, then `IsActive = false; UpdatedAt = utcNow`.

Plus six InventoryItem-coordination methods (see `knowledge/Migrations/InventoryItems.md`).

22 new unit tests in `Application/Frigorino.Test/Domain/InventoryAggregateTests.cs` lock the validation + role matrix.

## Frontend changes

### Slice-rename pass (shipped with backend migration, minimum-to-keep-build-green)

- Regenerated client (`npm run api`) renamed types: `InventoryDto` → `InventoryResponse`. Method names changed too: `ClientApi.inventories.getApiHouseholdInventories` → `ClientApi.inventories.getInventories`, etc.
- Updated `src/hooks/useInventoryQueries.ts` — straight rename: types + method calls. `householdId` was already plumbed through (inventory CRUD was already household-scoped in the legacy route).
- Generated TS type for `description` is now `string | null`. Updated `create.tsx` to pass `null` instead of `undefined`. Updated `edit.tsx` to pass `description: inventory.description ?? null` on update (the form only edits name; description is round-tripped).
- `routes/inventories/index.tsx`: `InventoryDto` → `InventoryResponse` type imports.

### Feature-folder restructure (shipped in follow-up round)

`src/features/inventories/` mirrors `features/lists/` one-to-one:

- `inventoryKeys.ts` query-key factory at the feature root.
- Per-slice hooks: `useHouseholdInventories.ts`, `useInventory.ts`, `useCreateInventory.ts`, `useUpdateInventory.ts`, `useDeleteInventory.ts`. The CRUD hooks dropped optimistic updates and switched to the simpler `invalidateQueries`-on-success shape used by Lists — matches the canonical reference in `features/lists/useCreateList.ts`.
- `pages/`: `InventoriesPage`, `CreateInventoryPage`, `InventoryEditPage`, `InventoryViewPage`. Route files (`routes/inventories/*.tsx`) collapsed to 7-line shells.
- `components/`: `InventorySummaryCard`, `InventoryActionsMenu`, `CreateInventoryForm`, `EditInventoryForm`, `DeleteInventoryConfirmDialog` — extracted from the fat route files and styled per `knowledge/Frontend_Styling.md` (dropped `borderRadius: 2`, manual `boxShadow`, responsive `fontSize` overrides, the Inventory2 banner card, hand-rolled `<Box>` surfaces; switched to `Card elevation={1|4}`, `pageContainerSx`, `<ConfirmDialog>`).
- `delete-inventory-button` testid moved to `InventoryActionsMenu`; `inventory-edit-save-button` testid lives on `EditInventoryForm`; `inventory-create-submit-button` on `CreateInventoryForm`.

Shared input primitives (`AddInput`, `QuantityPanel`, `DateInputPanel`, plus the colocated context/hooks/types/components subfolders) renamed `src/components/list/` → `src/components/inputs/`. Both `features/lists/items/components/ListFooter.tsx` and the new `features/inventories/items/components/InventoryFooter.tsx` now import from the shared location.

`src/components/inventory/*` deleted (`InventoryContainer`, `InventoryFooter`, `InventoryItemContent` → `features/inventories/items/components/`). `InventoryContainer` dropped the load-bearing-less `memo()` wrapper and the redundant `useCallback`s, matching the canonical `ListContainer` shape. `InventoryFooter` keeps `memo()` + the inner `useMemo`s because they feed `memo(AddInput)`'s shallow-prop compare.

`src/hooks/useInventoryQueries.ts` + `useInventoryItemQueries.ts` deleted.

## Deleted

- `Application/Frigorino.Web/Controllers/InventoriesController.cs`
- `Application/Frigorino.Application/Services/InventoryService.cs`
- `Application/Frigorino.Domain/Interfaces/IInventoryService.cs`
- `Application/Frigorino.Application/Extensions/InventoryMappingExtensions.cs`
- `Application/Frigorino.Application/Extensions/HouseholdMappingExtensions.cs` (only `User.ToDto()` remained; no consumer after this migration)
- `Application/Frigorino.Domain/DTOs/InventoryDto.cs` (all six types: `InventoryDto`, `CreateInventoryRequest`, `UpdateInventoryRequest`, `InventoryItemDto`, `CreateInventoryItemRequest`, `UpdateInventoryItemRequest`, `ReorderItemRequest`)
- `Application/Frigorino.Domain/DTOs/HouseholdDto.cs` (only `UserDto` remained; no consumer after this migration)
- `services.AddScoped<IInventoryService, ...>()` from `Application/Frigorino.Application/DependencyInjection.cs`

The `Frigorino.Application` project + `DependencyInjection.cs` deleted entirely. Removed: project from `Frigorino.sln`, `<ProjectReference>` from `Frigorino.Web.csproj` + `Frigorino.Test.csproj`, `using Frigorino.Application;` + `builder.Services.AddApplicationServices();` from `Program.cs`, `Application_Should_Not_Depend_On_Infrastructure` arch test, `COPY Application/Frigorino.Application/...` line from the Dockerfile.

`Application/Frigorino.Infrastructure/Tasks/RecalculateSortOrderTask.cs` was **deleted** in the post-review cleanup pass — it ran at every startup via `MaintenanceHostedService` and unconditionally stamped `UpdatedAt` + sort orders on every list/inventory item even when no gap had shrunk. Compaction now happens only via the explicit `POST .../items/compact` endpoints. (Mid-migration it had shed its `IInventoryService` dependency and called `inventory.CompactItems()` directly; the file is now gone.)

`Application/Frigorino.Test/Architecture/ArchitectureTests.cs` lost its `Application_Should_Not_Depend_On_Infrastructure` rule together with the project. Remaining rules pin `Domain → no infrastructure frameworks`, `Infrastructure → no Web`, `Features → no Web`.

## Deferred / out of scope

- **`MaintenanceHostedService` cleanup** — `RecalculateSortOrderTask` removed in the post-review cleanup pass; `DemoMaintenanceTask` + `DeleteInactiveItems` remain wired. Per CLAUDE.md the whole system is intended for Hangfire migration. Delete the rest in a focused cleanup pass.

## Cross-references

- Slice rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-20`
- Slice doc: `knowledge/Vertical_Slices.md`
- Households tracker (precedent): `knowledge/Migrations/Household.md`
- Lists tracker (immediate precedent): `knowledge/Migrations/Lists.md`
- InventoryItems tracker (companion): `knowledge/Migrations/InventoryItems.md`
- API regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`

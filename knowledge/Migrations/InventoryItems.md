# InventoryItems feature — vertical slice migration tracker

Status legend: ✅ Done · 🚧 In progress · ⬜ Not started · ❌ Dropped

This tracker covers InventoryItem CRUD nested under an Inventory (`/api/household/{householdId}/inventories/{inventoryId}/items/*`). **Status: complete** — six live slices migrated, one orphan endpoint dropped, sort-order coordination promoted onto the `Inventory` aggregate, legacy `InventoryItemsController`/`InventoryItemService` retired.

## Conventions

- Each slice follows the rules in `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`.
- Folder: `Application/Frigorino.Features/Inventories/Items/` (sub-folder under `Inventories/` because the URL nests, the lifecycle is parent-bound, and item ops are a distinct sub-area — same shape as `Lists/Items/`).
- Namespace: `Frigorino.Features.Inventories.Items`.
- Tag: `.WithTags("InventoryItems")` on every slice → generated frontend service stays at `ClientApi.inventoryItems.*`.
- Shared response DTO: `Inventories/Items/InventoryItemResponse.cs` — every slice returns it (singular or array) or no body.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.
- Endpoint wiring: `Application/Frigorino.Web/Program.cs` (the `inventoryItems` MapGroup, after `inventories`).
- Frontend regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`.

## Decisions

### URL nested under household (was unnested `/api/inventory/{id}/InventoryItems`)

Legacy route: `/api/inventory/{inventoryId}/InventoryItems` (no household scope, capitalized "InventoryItems" because the controller name leaked into the path).

New route: `/api/household/{householdId}/inventories/{inventoryId}/items` (mirrors `/api/household/{h}/lists/{l}/items`).

Three reasons:
1. **Auth boundary is a one-liner.** With `householdId` in the URL, the slice handler does `db.FindActiveMembershipAsync(householdId, userId, ct)` directly. The legacy controller had to first load the inventory to derive `householdId` before checking membership — extra query, extra branching.
2. **Folder structure matches URL.** Per `knowledge/Vertical_Slices.md` "URL guides the folder hierarchy". `Inventories/Items/` mirrors `/api/.../inventories/{id}/items` exactly.
3. **Consistency with ListItems.** The two features now mirror in routing and folder layout — easier for contributors to find the next file.

The frontend hook `useInventoryItemQueries.ts` was updated to plumb `householdId` through every mutation/query variable shape. Call sites in `routes/inventories/$inventoryId/view.tsx` + `components/inventory/InventoryContainer.tsx` were updated accordingly.

### Permission rule: any active member (legacy preserved)

Item mutations have no role gate. The legacy `InventoryItemService` only checked active membership — any member could add / update / reorder / delete / compact items. Preserved verbatim: aggregate methods on `Inventory` for item ops take no `callerRole` parameter, emit no `AccessDeniedError`. The auth-boundary check (active membership) stays in the handler via `db.FindActiveMembershipAsync(...)`, returning 404 for non-members.

Matches the ListItems decision exactly. Role gate sits at the Inventory level (creator-OR-Admin+ for Update/Delete), not the item level.

### Single sort-order section (no checked/unchecked split)

Inventory items have no `Status` field — they have `ExpiryDate` instead. All active items live in one section using `SortOrderCalculator.UncheckedMinRange + DefaultGap` (1M..9M with a 10k default gap). `CompactItems` calls `SortOrderCalculator.GenerateCompactedSortOrders(uncheckedCount: N, checkedCount: 0)` and ignores the checked output.

No toggle endpoint. The "Toggle" slice from ListItems has no analogue here.

### `UpdateItem` ExpiryDate is write-through (null clears); Text/Quantity preserve on null

Documented asymmetry inside `Inventory.UpdateItem`. The legacy `InventoryItemMappingExtensions.UpdateFromRequest` did `inventoryItem.ExpiryDate = request.ExpiryDate;` unconditionally — i.e. `null` cleared the existing value. The aggregate preserves this. Rationale: `ExpiryDate` is a first-class field the user explicitly sets/unsets via a date picker; sending `null` is "clear this date", not "leave unchanged". Text/Quantity follow the ListItem convention (null = preserve existing).

The `InventoryAggregateItemTests.UpdateItem_ExpiryDateNull_ClearsExistingValue` test locks this in.

### Reorder fallback behaviour preserved verbatim

The legacy `ReorderItemAsync` silently moved items to the top of section when `afterId` didn't resolve to a sibling. The aggregate method preserves this — the frontend's optimistic UI doesn't expect a 400. Self-anchor (`afterItemId == itemId`) is a no-op. Both branches covered in `InventoryAggregateItemTests.cs`.

### Orphan endpoint dropped: `GET /items/{itemId}` (singular)

Grep across `ClientApp/src` (excluding `lib/api/`) for the generated `getApiInventoryInventoryItems1` method returned zero hand-written consumers. Dropped, not migrated. Reintroduce only when a real consumer appears.

### Shared `Frigorino.Features.Items.ReorderItemRequest`

Both `ReorderInventoryItem.cs` and `ReorderItem.cs` (ListItem reorder) consume a single shared DTO at `Frigorino.Features/Items/ReorderItemRequest.cs`. The duplicate per-slice records that relied on OpenAPI same-name-same-shape dedup are gone — source-side single source of truth, so a future divergence (e.g. inventory reorder gains a `Section` field) won't silently collide in the generated TS client.

This replaces the legacy `Frigorino.Domain.DTOs.ReorderItemRequest` (in `InventoryDto.cs`) which served the dedup role pre-migration.

## Slice inventory

Group prefix `/api/household/{householdId:int}/inventories/{inventoryId:int}/items`.

### ✅ POST `""` — CreateInventoryItem

File: `Application/Frigorino.Features/Inventories/Items/CreateInventoryItem.cs`. Sealed record `CreateInventoryItemRequest(Text, Quantity?, ExpiryDate?)`. Calls `inventory.AddItem(text, quantity, expiryDate)` which validates text required + length, quantity length, then computes a sort order at the bottom of the section. Returns `Created<InventoryItemResponse>`. Union: `Results<Created<InventoryItemResponse>, NotFound, ValidationProblem>`.

### ✅ GET `""` — GetInventoryItems

File: `Application/Frigorino.Features/Inventories/Items/GetInventoryItems.cs`. Inline EF projection via `InventoryItemResponse.ToProjection`. Ordered `OrderBy(SortOrder).ThenByDescending(CreatedAt)` to match legacy. Auth-boundary + inventory-existence both checked before the projection. Union: `Results<Ok<InventoryItemResponse[]>, NotFound>`.

### ✅ PUT `"/{itemId}"` — UpdateInventoryItem

File: `Application/Frigorino.Features/Inventories/Items/UpdateInventoryItem.cs`. Sealed record `UpdateInventoryItemRequest(Text?, Quantity?, ExpiryDate?)`. Partial update for text/quantity (null preserves); ExpiryDate is write-through. Union: `Results<Ok<InventoryItemResponse>, NotFound, ValidationProblem>`.

### ✅ DELETE `"/{itemId}"` — DeleteInventoryItem

File: `Application/Frigorino.Features/Inventories/Items/DeleteInventoryItem.cs`. Calls `inventory.RemoveItem(itemId)` → soft-delete (`IsActive=false`, stamps `UpdatedAt`). Defensive throw on unmapped error types. Union: `Results<NoContent, NotFound>`.

### ✅ PATCH `"/{itemId}/reorder"` — ReorderInventoryItem

File: `Application/Frigorino.Features/Inventories/Items/ReorderInventoryItem.cs`. Consumes the shared `Frigorino.Features.Items.ReorderItemRequest(AfterId)` (see decision above). Calls `inventory.ReorderItem(itemId, afterItemId)`. Defensive throw on unmapped errors. Union: `Results<Ok<InventoryItemResponse>, NotFound>`.

### ✅ POST `"/compact"` — CompactInventoryItems

File: `Application/Frigorino.Features/Inventories/Items/CompactInventoryItems.cs`. Calls `inventory.CompactItems()` → rebuilds all active items' sort orders via `SortOrderCalculator.GenerateCompactedSortOrders` (single-section variant — passes 0 for checkedCount). Empty inventory is a no-op. Defensive throw on unmapped errors. Union: `Results<NoContent, NotFound>`.

### ❌ GET `"/{itemId}"` — dropped (orphan)

Zero hand-written consumers. See "Orphan endpoint dropped" decision above.

---

## Domain changes

`Application/Frigorino.Domain/Entities/Inventory.cs` grew the canonical aggregate shape for items:

- Five instance methods: `AddItem(text, quantity?, expiryDate?)`, `UpdateItem(itemId, text?, quantity?, expiryDate?)`, `RemoveItem(itemId)`, `ReorderItem(itemId, afterItemId)`, `CompactItems()`. Each returns `Result<T>` (or non-generic `Result` for void mutations).
- Private helper `ComputeAppendSortOrder()` single-section variant (no `targetStatus` parameter).
- All sort-order math delegates to `SortOrderCalculator`. Aggregate methods never call EF.

`Application/Frigorino.Domain/Entities/InventoryItem.cs` added length constants — used by `Inventory` for validation:

- `TextMaxLength = 255` — matches existing DB column width (deliberately smaller than `ListItem.TextMaxLength = 500` to avoid an EF migration; per-user preference: don't migrate for sibling-aggregate symmetry).
- `QuantityMaxLength = 100` — aggregate-level only, not propagated to EF config (Quantity column has no DB-level length constraint in either legacy or current schema; aggregate gate is sufficient).

`Application/Frigorino.Test/Domain/InventoryAggregateItemTests.cs` (new file) — 29 pure unit tests covering the matrix: AddItem (length / whitespace / trim / append-below-last / expiry storage), UpdateItem (partial / write-through expiry / not-found), RemoveItem (soft-delete / not-found / already-inactive), ReorderItem (top-of-section / midpoint / append / unknown-after fallback / self-anchor / not-found), CompactItems (clean gaps / empty no-op / skip inactive / preserves order).

## Frontend changes

### Slice-rename pass (shipped with backend migration, minimum-to-keep-build-green)

- `npm run api` regenerated the client. New TS types: `InventoryItemResponse`, `CreateInventoryItemRequest`, `UpdateInventoryItemRequest`. Old: `InventoryItemDto`, plus the renamed legacy variants. `ReorderItemRequest` survives (deduplicated with ListItems). Method names switched from path-based (`getApiInventoryInventoryItems`) to slice `WithName(...)` (`getInventoryItems`, `createInventoryItem`, etc.).
- `src/hooks/useInventoryItemQueries.ts` updated in place — type renames + method-name renames + `householdId` plumbed into every mutation/query variable shape (the new URL is nested under household).
- Consumer updates: `src/routes/inventories/$inventoryId/view.tsx` (passes `householdId` into hook calls + `InventoryContainer`), `src/components/inventory/InventoryContainer.tsx` (new `householdId` prop), `src/components/inventory/InventoryFooter.tsx` + `InventoryItemContent.tsx` (type rename only).
- `string | null` codegen quirk handled in `view.tsx` (`quantity ?? null`, `expiryDate?.toISOString() ?? null`) and `create.tsx` / `edit.tsx` (description: null).

### Feature-folder restructure (shipped in follow-up round)

`src/features/inventories/items/` mirrors `features/lists/items/`:

- `inventoryItemKeys.ts` sub-folder query-key factory. Same shape as `listItemKeys`: `byInventory(householdId, inventoryId)` for the collection cache, `detail(itemId)` for the item cache.
- Per-slice hooks: `useInventoryItems.ts`, `useCreateInventoryItem.ts`, `useUpdateInventoryItem.ts`, `useDeleteInventoryItem.ts`, `useReorderInventoryItem.ts`, `useCompactInventoryItems.ts`. Each follows the optimistic-update template from `features/lists/items/useCreateListItem.ts` and uses `useDebouncedInvalidation`. The optimistic patches mirror the server's sort-order math (single-section variant — no `status` filter; reorder formula uses `UNCHECKED_MIN = 1_000_000` directly).
- `useUpdateInventoryItem`'s optimistic patch encodes the server's write-through asymmetry: `text ?? item.text`, `quantity ?? item.quantity`, but `expiryDate: variables.data.expiryDate` (no `??` — null clears).
- `components/`: `InventoryContainer`, `InventoryFooter`, `InventoryItemContent` moved from `src/components/inventory/`. `InventoryContainer` dropped `memo()` + the redundant `useCallback`s to match `ListContainer`. `InventoryFooter` keeps `memo()` + inner `useMemo`s (load-bearing — feeds `memo(AddInput)`'s shallow-prop compare).

Shared input primitives renamed `src/components/list/` → `src/components/inputs/`. The whole subtree moved (`AddInput.tsx`, `DateInputPanel.tsx`, `QuantityPanel.tsx`, plus `components/`, `context/`, `hooks/`, `types/`). `ListFooter.tsx` + `InventoryFooter.tsx` both updated to the new path.

`src/hooks/useInventoryItemQueries.ts` deleted.

## Integration tests

Existing `Application/Frigorino.IntegrationTests/Slices/Inventories/Inventories.feature` (3 scenarios) preserved + extended with a rename scenario. New files added:

- `Inventories.Api.feature` — empty-name validation; non-member 404 on GET; non-creator Member 403 on DELETE; non-creator Admin 204 on DELETE. Mirrors `Lists.Api.feature` line-for-line.
- `InventoryItems.feature` — UI: add item; add multiple items; remove item via row menu. Mirrors `ListItems.feature` minus the toggle scenarios.
- `InventoryItems.Api.feature` — API: empty-text validation; non-member 404; delete-via-API; compact preserves order; reorder to top; reorder after another. Mirrors `ListItems.Api.feature` minus the toggle scenarios.
- New step files: `InventoryApiSteps.cs`, `InventoryItemSteps.cs`, `InventoryItemApiSteps.cs`. The existing `InventorySteps.cs` was extended with edit/save steps for the rename scenario.
- `TestApiClient` extended with `TryCreateInventoryAsync`, `TryGetInventoriesAsync`, `TryDeleteInventoryAsync` + item variants. `CreateInventoryAsync` URL normalized to lowercase `/inventories`. `CreateInventoryItemAsync` URL updated to the new nested route.
- `ScenarioContextHolder` gained `InventoryItemIds` dictionary.

## Deleted

- `Application/Frigorino.Web/Controllers/InventoryItemsController.cs`
- `Application/Frigorino.Application/Services/InventoryItemService.cs`
- `Application/Frigorino.Domain/Interfaces/IInventoryItemService.cs`
- `Application/Frigorino.Application/Extensions/InventoryItemMappingExtensions.cs`
- `services.AddScoped<IInventoryItemService, InventoryItemService>()` from `Application/Frigorino.Application/DependencyInjection.cs`

## Deferred / out of scope

- **`MaintenanceHostedService` cleanup** — `RecalculateSortOrderTask` was removed in the post-review cleanup pass (it stamped `UpdatedAt` on every list/inventory item at every startup with no gap-check). `DemoMaintenanceTask` + `DeleteInactiveItems` remain wired and are queued for the full system removal.

## Cross-references

- Slice rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`
- Slice doc: `knowledge/Vertical_Slices.md`
- Households tracker (precedent): `knowledge/Migrations/Household.md`
- Members tracker (precedent): `knowledge/Migrations/Members.md`
- Lists tracker: `knowledge/Migrations/Lists.md`
- ListItems tracker (immediate precedent): `knowledge/Migrations/ListItems.md`
- Inventory tracker (parent migration): `knowledge/Migrations/Inventory.md`
- API regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`

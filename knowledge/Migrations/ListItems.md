# ListItems feature — vertical slice migration tracker

Status legend: ✅ Done · 🚧 In progress · ⬜ Not started · ❌ Dropped

This tracker covers ListItem CRUD nested under a List (`/api/household/{householdId}/lists/{listId}/items/*`). **Status: complete** — all eight live slices migrated, sort-order coordination promoted onto the `List` aggregate, legacy `ListItemsController`/`ListItemService`/`ItemsController` retired, orphan `Frigorino.Application.Utilities.SortOrderCalculator` removed (replaced by `Frigorino.Domain.Entities.SortOrderCalculator`).

## Conventions

- Each slice follows the rules in `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`.
- Folder: `Application/Frigorino.Features/Lists/Items/` (sub-folder under `Lists/` because the URL nests, the lifecycle is parent-bound, and item ops are a distinct sub-area).
- Namespace: `Frigorino.Features.Lists.Items`.
- Tag: `.WithTags("ListItems")` on every slice → generated frontend service stays at `ClientApi.listItems.*`.
- Shared response DTO: `Lists/Items/ListItemResponse.cs` — every slice returns it (singular or array) or no body. Named `ListItemResponse` (not `ItemResponse`) for OpenAPI global uniqueness when InventoryItem slices land.
- Result→ValidationProblem helper: `Application/Frigorino.Features/Results/ResultExtensions.cs`.
- Endpoint wiring: `Application/Frigorino.Web/Program.cs` (the `listItems` MapGroup, after `lists`).
- Frontend regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`.

## Decisions

### Permission rule: any active member (legacy preserved)

Item mutations have no role gate. The legacy `ListItemService.ValidateListAccessAsync` only checked active membership — any member could add / toggle / reorder / delete / compact items. Preserved verbatim: aggregate methods on `List` take no `callerRole` parameter, emit no `AccessDeniedError`. The auth-boundary check (active membership) stays in the handler via `db.FindActiveMembershipAsync(...)`, returning 404 for non-members.

This intentionally differs from `List.Update`/`List.SoftDelete` (creator-OR-Admin+). Items in a collaborative grocery list should be editable by everyone in the household; the role gate sits at the list level, not the item level.

### URL prefix: `/items` (was `/listitems`)

Migrated route prefix is `/api/household/{householdId}/lists/{listId}/items`, matching the slice folder `Lists/Items/`. The generated TS client picks up the new path automatically. Hand-written hooks file (`useListItemQueries.ts`) was renamed call-site by call-site to the new slice-named methods (`getItems`, `createItem`, `toggleItemStatus`, etc.). `WithTags("ListItems")` (plural, unchanged) kept the generated service file name `ListItemsService.ts` stable.

### Sort-order coordination on the `List` aggregate

Multi-row sort-order invariants (two parallel ranges: unchecked 1M..9M, checked 10M..19M, default gap 10k) belong on the aggregate per `knowledge/Vertical_Slices.md` ("aggregate-internal logic in the slice handler" is an anti-pattern). The `List` aggregate grew six methods — `AddItem`, `UpdateItem`, `RemoveItem`, `ToggleItemStatus`, `ReorderItem`, `CompactItems` — each returning `Result<T>` with `EntityNotFoundError` for not-found and property-keyed `Error` for validation failures. Handlers `Include(l => l.ListItems)`, call the method, then `SaveChangesAsync` once.

`SortOrderCalculator` moved from `Application/Frigorino.Application/Utilities/` to `Application/Frigorino.Domain/Entities/`. The signature shrank — the old `out bool needRecalculation` parameter was dead (never consumed) and the alternative `CalculateSortOrderChecked` / `CalculateSortOrderUnchecked` methods were commented-out dead code. Cleaned up during the move. The Inventory services were updated to the new (5-arg) signature.

### Reorder fallback behaviour preserved verbatim

The legacy `ReorderItemAsync` silently moved items to the top of their own section when the supplied `afterId` didn't resolve to a sibling in the same status section. The aggregate method preserves this — the frontend's optimistic UI doesn't expect a 400 here. Validation errors are reserved for actual property violations (text/quantity), not for surprise rerouting.

### Orphan endpoints dropped

`ItemsController` (`/api/items/{itemId}` for direct item access, without list scoping) had **zero hand-written frontend consumers** — grep across `ClientApp/src` (excluding `lib/api/`) returned no matches. Dropped entirely rather than migrated. Reintroduce only when a real consumer appears.

### Auth-boundary check uses `FindActiveMembershipAsync`, list-existence is a second query

Item slices need to answer "is the caller an active member?" AND "does the list exist and belong to this household?". The handler does both — `FindActiveMembershipAsync` for the auth boundary (NotFound) followed by `db.Lists.FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive)` for list existence. The combined two-step is consistent with the pattern in `Lists/UpdateList.cs`.

## Slice inventory

### ✅ POST /api/household/{householdId}/lists/{listId}/items — CreateItem

File: `Application/Frigorino.Features/Lists/Items/CreateItem.cs`. Sealed record `CreateItemRequest(Text, Quantity?)`. Calls `list.AddItem(text, quantity)` which validates text required + length, quantity length, then computes a sort order at the bottom of the unchecked section. Returns `Created<ListItemResponse>` with `Location: /api/household/{h}/lists/{l}/items/{itemId}`. Union: `Results<Created<ListItemResponse>, NotFound, ValidationProblem>`.

### ✅ GET /api/household/{householdId}/lists/{listId}/items — GetItems

File: `Application/Frigorino.Features/Lists/Items/GetItems.cs`. Inline EF projection via `ListItemResponse.ToProjection`. Ordered `OrderBy(Status).ThenBy(SortOrder)` so unchecked items appear before checked. Auth-boundary + list-existence both checked before the projection. Union: `Results<Ok<ListItemResponse[]>, NotFound>`.

### ✅ GET /api/household/{householdId}/lists/{listId}/items/{itemId} — GetItem

File: `Application/Frigorino.Features/Lists/Items/GetItem.cs`. Single-result projection. The query filters `i.ListId == listId && i.List.HouseholdId == householdId && i.IsActive && i.List.IsActive`, so a mismatched list-id or inactive list is just NotFound. Union: `Results<Ok<ListItemResponse>, NotFound>`.

### ✅ PUT /api/household/{householdId}/lists/{listId}/items/{itemId} — UpdateItem

File: `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`. Sealed record `UpdateItemRequest(Text?, Quantity?, Status?)`. Partial update — null fields are preserved. Calls `list.UpdateItem(itemId, text, quantity, status)`; a status flip recomputes the sort order into the opposite section. Union: `Results<Ok<ListItemResponse>, NotFound, ValidationProblem>`.

### ✅ DELETE /api/household/{householdId}/lists/{listId}/items/{itemId} — DeleteItem

File: `Application/Frigorino.Features/Lists/Items/DeleteItem.cs`. Calls `list.RemoveItem(itemId)` → soft-delete (sets `IsActive=false`, stamps `UpdatedAt`). Defensive `throw` on unmapped error types — current aggregate only emits `EntityNotFoundError`, so adding a new invariant later forces a compile-time touch on the slice. Union: `Results<NoContent, NotFound>`.

### ✅ PATCH /api/household/{householdId}/lists/{listId}/items/{itemId}/toggle-status — ToggleItemStatus

File: `Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs`. Calls `list.ToggleItemStatus(itemId)` → flips status + relocates the item into the opposite section's range. Defensive throw on unmapped errors. Union: `Results<Ok<ListItemResponse>, NotFound>`.

### ✅ PATCH /api/household/{householdId}/lists/{listId}/items/{itemId}/reorder — ReorderItem

File: `Application/Frigorino.Features/Lists/Items/ReorderItem.cs`. Sealed record `ReorderItemRequest(AfterId)` colocated. Name retained to keep OpenAPI schema dedup against the surviving legacy `Frigorino.Domain.DTOs.ReorderItemRequest` (same shape `{ afterId: int }`) — same-name same-shape types deduplicate in the generated TS client, so the frontend sees a single `ReorderItemRequest` type. Calls `list.ReorderItem(itemId, afterItemId)`. Defensive throw on unmapped errors. Union: `Results<Ok<ListItemResponse>, NotFound>`.

### ✅ POST /api/household/{householdId}/lists/{listId}/items/compact — CompactItems

File: `Application/Frigorino.Features/Lists/Items/CompactItems.cs`. Calls `list.CompactItems()` → rebuilds all active items' sort orders to clean gaps via `SortOrderCalculator.GenerateCompactedSortOrders`. Empty list is a no-op. Defensive throw on unmapped errors. Union: `Results<NoContent, NotFound>`. Permission preserved-legacy (any member can compact — see decision).

---

## Domain changes

`Application/Frigorino.Domain/Entities/List.cs` grew the canonical aggregate shape for items:

- Six instance methods: `AddItem`, `UpdateItem(itemId, text?, quantity?, status?)`, `RemoveItem(itemId)`, `ToggleItemStatus(itemId)`, `ReorderItem(itemId, afterItemId)`, `CompactItems()`. Each returns `Result<T>` (or non-generic `Result` for void mutations).
- Private helper `ComputeAppendSortOrder(targetStatus)` consolidates the section-append math (unchecked appends below last; checked prepends above first; empty section starts at `MinRange + DefaultGap`).
- All sort-order math delegates to `SortOrderCalculator`. Aggregate methods never call EF.

`Application/Frigorino.Domain/Entities/ListItem.cs` added length constants — single source of truth, also referenced by `ListItemConfiguration`:

- `TextMaxLength = 500`
- `QuantityMaxLength = 100`

`Application/Frigorino.Domain/Entities/SortOrderCalculator.cs` is the new home of the calculator (moved from `Frigorino.Application/Utilities/`). Public surface trimmed: dead `InsertType` enum and commented-out alternative methods deleted; `out bool needRecalculation` parameter removed from `CalculateSortOrder` (it was always discarded). Range constants and `GenerateCompactedSortOrders` / `NeedsCompaction` preserved as-is.

`Application/Frigorino.Test/Domain/ListAggregateItemTests.cs` (new file) — 30 pure unit tests covering the matrix: AddItem (length / whitespace / trim / append-below-last), UpdateItem (partial update / status-change recalc / not-found), RemoveItem (soft-delete / not-found / already-inactive), ToggleItemStatus, ReorderItem (top-of-section / midpoint / append / cross-section silent fallback / not-found), CompactItems (clean gaps / empty no-op / skip inactive / section separation). Replaces the deleted `ListItemServiceSortOrderTests.cs` (9 service-level tests, several incomplete) and `ListItemServiceWorkflowTests.cs`.

## Frontend changes

### Initial slice-rename pass (shipped with backend migration)

- `npm run api` regenerated the client. New TS types: `ListItemResponse`, `CreateItemRequest`, `UpdateItemRequest`. Old: `ListItemDto`, `CreateListItemRequest`, `UpdateListItemRequest` retired. `ReorderItemRequest` survives (deduplicated with Inventory). Method names switched from path-based (`getApiHouseholdListsListItems`) to slice `WithName(...)` (`getItems`, `createItem`, etc.). `ItemsService.ts` (singular, from the orphan controller) gone.
- `src/hooks/useListItemQueries.ts` updated in place — type renames + method-name renames. Optimistic-update bodies unchanged in shape. Re-exports updated to the new type names.
- `string | null` codegen quirk handled in `ListViewPage.tsx` (`quantity ?? null`, explicit `status: null` on update) and `ListItemDialog.tsx` (`isEditing` branch now constructs `UpdateItemRequest` with `status` set).
- Consumer components renamed straight-through: `ListContainer.tsx`, `ListFooter.tsx`, `ListItemContent.tsx`, `ListItemDialog.tsx`, `ListViewPage.tsx`.

### Feature-folder restructure (follow-up pass)

Mirror of the `features/lists/` + `features/households/members/` shape. The bundled hooks file split into one-per-slice, list-item-locked components moved under `features/lists/items/components/`, dead code dropped. Shared input primitives (`AddInput`, `QuantityPanel`, `DateInputPanel` and their internal context/hooks) left at `src/components/list/` because `src/components/inventory/InventoryFooter.tsx` still imports them — that folder rename is bundled with the Inventory migration.

```
src/features/lists/items/
├── listItemKeys.ts                ← {all, byList(householdId, listId), detail(itemId)}
├── useListItems.ts                ← one hook per backend slice
├── useListItem.ts
├── useCreateListItem.ts
├── useUpdateListItem.ts
├── useDeleteListItem.ts
├── useToggleListItemStatus.ts
├── useReorderListItem.ts
├── useCompactListItems.ts
└── components/
    ├── ListContainer.tsx          ← moved from src/components/list/
    ├── ListFooter.tsx             ← moved from src/components/list/
    └── ListItemContent.tsx        ← moved from src/components/list/
```

Style cleanup applied per `knowledge/Frontend_Styling.md` during extraction (only on the 3 moved files — shared inputs untouched to avoid surprising InventoryFooter):

- `ListItemContent.tsx` — `InlineImage` now uses `<Paper component="img" elevation={2}>` instead of `<Box>` with hand-rolled `borderRadius: 1` + `boxShadow: "0 2px 8px rgba(0,0,0,0.1)"`; hover boxShadow flattened to MUI's `elevation: 4` token.
- The two outer `<Box>` wrappers around `ListItemText` in the original `ListItemContent` were redundant — `<ListItemText>` returned directly now.

`useListItemQueries.ts` was the single hook-bundle file; the new per-hook files preserve the optimistic-update logic verbatim — including the toggle-status hook's missing-sort-order-recompute behaviour, which is tracked separately in `TECH_DEBT.md` and links to a code comment in `useToggleListItemStatus.ts`.

Single consumer updated: `src/features/lists/pages/ListViewPage.tsx` swaps imports to the new paths and pulls `ListItemResponse` from `lib/api` directly (matching the precedent in `features/lists/useList.ts`).

### Deleted in the restructure pass

- `src/hooks/useListItemQueries.ts` — replaced by 9 files under `features/lists/items/`.
- `src/components/list/ListContainer.tsx` — moved.
- `src/components/list/ListFooter.tsx` — moved.
- `src/components/list/ListItemContent.tsx` — moved.
- `src/components/list/ListItemDialog.tsx` — dead code (only self-references; no UI consumer). Removing it dropped 8 hardcoded English strings without needing translation work.
- `src/components/list/PERFORMANCE_OPTIMIZATIONS.md` — aspirational notes, not gated to a real change.

## Integration tests

`Application/Frigorino.IntegrationTests/Features/ShoppingLists.feature` retained its two existing scenarios (add item, check off item). New scenarios added in the migration: see "Reqnroll scenarios" section below.

## Deleted

- `Application/Frigorino.Web/Controllers/ListItemsController.cs`
- `Application/Frigorino.Web/Controllers/ItemsController.cs` (orphan, no consumers)
- `Application/Frigorino.Application/Services/ListItemService.cs`
- `Application/Frigorino.Application/Utilities/SortOrderCalculator.cs` (moved to Domain)
- `Application/Frigorino.Application/Extensions/ListItemMappingExtensions.cs`
- `Application/Frigorino.Domain/Interfaces/IListItemService.cs`
- `Application/Frigorino.Domain/DTOs/ListItemDto.cs` (legacy `ListItemDto`, `CreateListItemRequest`, `UpdateListItemRequest`, `ToggleStatusRequest`. `ReorderItemRequest` moved to `InventoryDto.cs` since Inventory still consumes it.)
- `Application/Frigorino.Test/Services/ListItemServiceSortOrderTests.cs`
- `Application/Frigorino.Test/Services/ListItemServiceWorkflowTests.cs`
- `services.AddScoped<IListItemService, ListItemService>()` line in `Application/Frigorino.Application/DependencyInjection.cs`

`ArchitectureTests.cs` rebinding pinned `Frigorino.Application` assembly via `typeof(InventoryService).Assembly` (was `ListItemService.Assembly`). Swap again when Inventory migrates and only the orphan maintenance services remain.

`Application/Frigorino.Infrastructure/Tasks/RecalculateSortOrderTask.cs` had to lose its `IListItemService` dependency. The list branch now loads `Lists.Include(ListItems)` and calls the new `list.CompactItems()` aggregate method directly; the inventory branch is unchanged. The entire `MaintenanceHostedService` / `IMaintenanceTask` system remains scheduled for cleanup separately (per CLAUDE.md it was logically replaced by Hangfire but the registration survives).

## Deferred / out of scope

- **`src/components/list/` rename to `components/inputs/`** — `AddInput.tsx`, `QuantityPanel.tsx`, `DateInputPanel.tsx`, and everything under `components/list/components/`, `components/list/context/`, `components/list/hooks/`, `components/list/types/` are shared with `src/components/inventory/InventoryFooter.tsx`. Bundle the rename + import sweep into the Inventory migration round so Inventory's consumer changes in lockstep.
- **i18n cleanup of hardcoded strings in shared inputs** — `EditingHeader.tsx` ("Bearbeiten", "Completed") and `DateInputPanel.tsx` ("Datum", "Heute", "Löschen") live in *shared* files. Translate during the Inventory migration round to keep this PR's blast radius tight.
- **Inventories migration + InventoryItems migration** — same pattern when scheduled. Will free the second-to-last legacy service (`InventoryService`); after that `ArchitectureTests` assembly marker will need another swap.
- **MaintenanceHostedService / RecalculateSortOrderTask cleanup** — leave as-is for now. Per CLAUDE.md the system was replaced by Hangfire; this is dead orchestration kept registered. Delete in a focused cleanup pass.
- **Renaming `HouseholdMappingExtensions.cs` / `HouseholdDto.cs` / `InventoryDto.cs`** — still consumed by Inventory layer. Defer until Inventory migrates.

## Cross-references

- Slice rules: `Application/Frigorino.Features/Households/CreateHousehold.cs:1-13`
- Slice doc: `knowledge/Vertical_Slices.md`
- Households tracker (precedent): `knowledge/Migrations/Household.md`
- Members tracker (precedent): `knowledge/Migrations/Members.md`
- Lists tracker (immediate parent migration): `knowledge/Migrations/Lists.md`
- API regen: `npm run api` from `Application/Frigorino.Web/ClientApp/`

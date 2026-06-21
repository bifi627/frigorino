# Inventories

An **inventory** is a household-scoped stock location (Fridge, Pantry, Freezer…) whose items carry an **expiry date**. `Inventory` is its own aggregate root (a peer of `Household` and `List`); items are ordered by a fractional-index `Rank` exactly like list items, but have **no checked status** — the organizing axis is expiry, not done/not-done. Inventories receive items **promoted from lists**, surface an **expiry calendar** across the whole household, and drive the **expiry-digest push** (per-user opt-out lives here). Same shape as the rest of the app: one slice per endpoint, rules in the `Inventory` aggregate (`FluentResults.Result`).

## Domain (`Frigorino.Domain/Entities/`)

| Entity | Key fields | Notes |
|---|---|---|
| `Inventory.cs` | `Name`, `Description?`, `HouseholdId`, `CreatedByUserId` | Aggregate root. `Create` factory; `Update`/`SoftDelete` enforce **creator OR Admin+** (identical policy/shape to `List`). Owns the InventoryItem-coordination methods (add/update/remove/restore/reorder) ordering by fractional-index `Rank`. `NameMaxLength = 255`, `DescriptionMaxLength = 1000`. |
| `InventoryItem.cs` | `Text`, `QuantityValue?`/`QuantityUnit?`, `ExpiryDate?`, `Rank` | Quantity is both-columns-or-null (mirrors `ListItem`). **No `Status`** — `ExpiryDate` is the axis. `TextMaxLength = 255` (kept narrower than `ListItem`'s 500 to match the existing DB column and avoid a needless migration). |
| `InventorySettings.cs` | — | Per-inventory settings scaffold; the response is currently empty (endpoints exist for future per-inventory config). |
| `UserInventoryNotificationSetting.cs` | `UserId`, `InventoryId`, `Enabled`, `LeadDays?` | Per-(user, inventory) opt-out / lead-day override. No row = default. Documented in `Push_Notifications.md`. |

Shared response: `InventoryResponse` + colocated `InventoryCreatorResponse`; items use `InventoryItemResponse` (carries an `IsExpiring` flag); the calendar uses `ExpiryCalendarItemResponse`.

## API surface (`Frigorino.Features/Inventories/`)

All under `RequireAuthorization()`; each handler checks membership via `db.FindActiveMembershipAsync(...)`.

- **Inventories** (`/api/household/{householdId}/inventories`): `POST /`, `GET /`, `GET /{inventoryId}`, `GET /{inventoryId}/revision`, `PUT /{inventoryId}`, `DELETE /{inventoryId}`, plus the calendar below.
- **Expiry calendar**: `GET /calendar`, `GET /calendar/revision` — collection-level over **all** the household's inventories (note the literal `calendar` segment; the `{inventoryId:int}` constraint keeps it from colliding).
- **Items** (`Inventories/Items/`, `.../{inventoryId}/items`): `GET /`, `POST /`, `PUT /{itemId}`, `DELETE /{itemId}`, `POST /{itemId}/restore`, `PATCH /{itemId}/reorder`.
- **Settings** (`Inventories/Settings/`, `.../{inventoryId}/settings`): `GET /`, `PUT /`.
- **Notifications** (`Inventories/Notifications/`, `.../{inventoryId}/notifications`): `GET /`, `PUT /` — per-inventory `{ enabled, leadDays|null }`. See `Push_Notifications.md`.

## Key flows

- **Expiry projection.** `GetInventories` projects `TotalItems` + `ExpiringItems` per inventory; `InventoryItemResponse` carries `IsExpiring`. Both use the single threshold `InventoryResponse.ExpiringWithinDays = 7`. The projection inlines `DateTime.UtcNow.AddDays(ExpiringWithinDays)`, which EF translates to SQL `now()` — the "expiring soon" window is computed server-side and consistently between the overview count and the per-item flag.
- **Expiry calendar.** `GET /calendar` returns active items with an expiry date across all of the household's active inventories, ordered by `ExpiryDate` — the cross-inventory "what's going off soon" view.
- **Item ordering.** Same fractional-index `Rank` machinery as lists (server-minted, `RankRetry` on the partial unique index), but a single section per inventory (no checked/unchecked split).
- **Receiving promotions.** `Inventory.AddItem(text, quantity?, expiryDate?)` is what list promotion calls — promoted list items land here with their carried-over quantity and the user-chosen expiry. See `Lists.md`.

## Key decisions & rationale

- **`Inventory` is a peer aggregate, not a `Household` child** — creator-OR-Admin+ for inventory-level edit/delete, the handler resolves the role via `FindActiveMembershipAsync` and passes it in. Same rationale as lists; canonical statement in `Households.md`.
- **Items have no `Status`; the axis is `ExpiryDate`.** This is the deliberate difference from lists and is why the projection exposes `TotalItems`/`ExpiringItems` rather than `Unchecked`/`Checked`.
- **`ExpiringWithinDays = 7` is a single source of truth** for both the overview count and the per-item `IsExpiring` flag — no drift between the two surfaces.
- **`InventoryItem.TextMaxLength = 255`** matches the existing DB column width; it's intentionally narrower than `ListItem`'s 500 to avoid an unnecessary EF migration for sibling symmetry (memory `feedback_avoid_unnecessary_migrations`).
- **`InventoryCreatorResponse` is colocated**, not shared with `ListCreatorResponse` (cross-folder DTO sharing is a smell — `Vertical_Slices.md`).
- **No media or AI classification on inventory items.** Unlike list items, inventory items are plain text + quantity + expiry; the AI pipeline and media uploads are list/recipe concerns.

## Cross-feature touchpoints

- **Lists** (`Lists.md`): inventories are the promote target — list items flow in via `Inventory.AddItem`; the reverse (inventory→list re-order) creates a list item with the carried quantity.
- **Push / expiry scan** (`Push_Notifications.md`): the scan reads active inventory items with expiry dates; per-inventory opt-out (`UserInventoryNotificationSetting`) is edited through the notifications endpoints here; digests deep-link to `/inventories/{id}/view`.
- **Households** (`Households.md`): inventories are tenant-scoped; the global expiry lead/opt-in lives in `UserSettings`.

## Frontend (`ClientApp/src/features/inventories/`)

Mirrors `features/lists/` one-to-one: route shells under `src/routes/inventories/` → `features/inventories/pages/`; per-slice hooks (`useHouseholdInventories`, `useInventory`, `useCreateInventory`, `useUpdateInventory`, `useDeleteInventory`) using the simple invalidate-on-success shape; an `items/` sub-area; the per-inventory notification toggle. Composer + quantity/date input primitives come from the shared `src/components/inputs/`. The expiry-date picker uses MUI X DatePicker (see memory `reference_mui_x_datepicker_playwright` for the IT pattern).

## Links out

- Slice pattern: `Vertical_Slices.md`
- API client + hook conventions: `API_Integration.md`
- Styling: `Frontend_Styling.md`
- Promote source / aggregate sibling: `Lists.md`
- Expiry notifications + per-inventory settings: `Push_Notifications.md`
- Aggregate-boundary rationale + user settings: `Households.md`

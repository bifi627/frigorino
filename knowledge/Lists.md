# Lists

A **list** is a household-scoped shopping list. `List` is its own aggregate root (a peer of `Household`, not a child); items live on it and are ordered by a server-minted **fractional-index** `Rank`. Items can be plain **text** or **media** (image/PDF), carry an optional structured quantity (filled by AI extraction), can be **checked off**, **reordered**, **promoted to an inventory**, and bulk-reordered by an aisle **blueprint**. Same shape as the rest of the app: one slice per endpoint, rules in the `List` aggregate (`FluentResults.Result`).

## Domain (`Frigorino.Domain/Entities/`)

| Entity | Key fields | Notes |
|---|---|---|
| `List.cs` | `Name`, `Description?`, `HouseholdId`, `CreatedByUserId` | Aggregate root. `Create` factory; `Update`/`SoftDelete` enforce **creator OR Admin+**. Owns all ListItem-coordination methods (add/update/toggle/reorder/restore/promotion). `NameMaxLength = 255`, `DescriptionMaxLength = 1000`. |
| `ListItem.cs` | `Text`, `Type`, `Comment?`, media cols, `QuantityValue?`/`QuantityUnit?`, `Status`, `Rank`, promotion cols | `Status` false=unchecked / true=checked. Quantity is both-columns-or-null. `Rank` is a lexicographic fractional index scoped per (List, Status). Media columns (`StorageKey`/`ThumbnailStorageKey`/`ContentType`/…) null for text items. `TextMaxLength = 500`, `MaxFileSizeBytes = 25 MB`. |
| `ListItemType` (enum) | `Text` / `Image` / `Document` | Existing rows backfill to `Text`. |

`ListItem` promotion columns (`PromotionExpiryHandling`, `PromotionSuggestedExpiry`, `PromotionResolvedAt`) capture the promote-to-inventory candidacy. **Pending** (read-time) = `Status && PromotionExpiryHandling != null && PromotionResolvedAt == null && UpdatedAt >= UtcNow − PromoteWindowDays` (`ListItem.PromoteWindowDays = 7`). The window is read-time suppression only: past 7 days a checked candidate silently drops out of the pending count (so the `PromoteBar` stops climbing forever for households that never promote) — the DB columns are untouched and the checked-item retention sweep eventually purges it. No shared expression: the predicate is duplicated in lockstep across `GetPendingPromotions`, `ListResponse.ToProjection`, and `UpdateList` (three different query shapes; a single reusable `Expression` can't be invoked inside the projection `Count` without LinqKit). Columns stamped/cleared only by `List` methods (`ToggleItemStatus`, `ApplyPromotionSuggestion`, `ResolvePromotion`). Shared response: `ListResponse` + colocated `ListCreatorResponse`; items use `ListItemResponse` (carries `ExtractionPending`).

## API surface (`Frigorino.Features/Lists/`)

All under `RequireAuthorization()`; each handler checks membership via `db.FindActiveMembershipAsync(...)`.

- **Lists** (`/api/household/{householdId}/lists`): `POST /`, `GET /`, `GET /{listId}`, `PUT /{listId}`, `DELETE /{listId}`, `GET /{listId}/revision` (sync token), plus the promote + blueprint endpoints below.
- **Promote** (`Lists/Promote/`): `GET /{listId}/pending-promotions`, `POST /{listId}/promote`, `POST /{listId}/promote/skip`.
- **Blueprint** (`Lists/Blueprints/`): `POST /{listId}/apply-blueprint`.
- **Items** (`Lists/Items/`, `/api/household/{householdId}/lists/{listId}/items`): `GET /`, `GET /{itemId}`, `POST /` (text), `POST /media` (multipart), `GET /{itemId}/file`, `GET /{itemId}/thumbnail`, `PUT /{itemId}`, `DELETE /{itemId}`, `POST /{itemId}/restore`, `PATCH /{itemId}/toggle-status`, `PATCH /{itemId}/reorder`.

## Key flows

- **Ordering (fractional index).** `Rank` is an opaque string key (`FractionalIndex.GenerateKeyBetween`), scoped per (List, Status) — unchecked and checked are two independent sections. Add appends, toggle re-mints into the target section, reorder keys between neighbours, `ApplyOrder` bulk re-ranks the unchecked section. A partial unique index `(ListId, Status, Rank)` guards collisions; concurrent appends/reorders retry via `RankRetry.SaveWithRetryAsync` (reload fresh state, re-mint — blobs are not re-uploaded). This replaced the old integer sort-order + compaction scheme entirely (no compaction pass needed).
- **Create + quantity extraction.** `CreateItem` routes the text via `ItemTextRouter`; if it needs parsing it fires `IQuantityExtractionTrigger.OnItemRouted` and returns `ExtractionPending: true` (the SPA polls). When a structured quantity is **already supplied** (the inventory→list re-order path), routing/extraction is skipped and the quantity written directly. Extraction writes back via `List.ApplyExtractedQuantity` (no-op if nothing changed, to avoid bumping the revision token). See `AI_Classification.md`.
- **Promote to inventory.** Checking an item off (`ToggleItemStatus`) is when candidacy is captured: if the item's product (by normalized name) is perishable, the handler stamps `ApplyPromotionSuggestion` (handling + suggested expiry from the `Product` catalog) and the toggle response carries a `PromoteSuggestion` hint. `GetPendingPromotions` lists pending items; `PromoteListItems` writes selected ones into a target inventory (`inventory.AddItem`) then `ResolvePromotion`; `promote/skip` resolves without writing. All idempotent (first writer wins) so concurrent members sharing a list don't double-promote. Unchecking retracts all promotion state.
- **Media items.** `POST /media` takes a `type` (`Image`/`Document`) + file. **Images** re-encode to WebP + thumbnail via `IImageProcessor`; **documents** (PDF only, `application/pdf`) store the raw bytes with no thumbnail. Both store via `IFileStorage` (blob area `ListItem`) and save blobs **before** the row, compensating on failure (same orphan-safe ordering as recipe attachments). A content-type pre-filter rejects disallowed types before any blob is written. `AddMediaItem` enforces the type/thumbnail invariant (images have a thumbnail, documents don't). Documents are opened client-side in a new tab (authed blob fetch → object URL); there is no inline PDF preview. See `File_Storage.md`.
- **Apply blueprint.** `POST /{listId}/apply-blueprint` resolves each unchecked item's category from the `Product` catalog and bulk re-ranks (`ApplyOrder`) by the blueprint's aisle order; unclassified/sentinel items sink to the bottom.

## Key decisions & rationale

- **`List` is a peer aggregate, not a `Household` child.** Edit/delete of the *list* is creator-OR-Admin+ — the handler resolves the caller's role once via `FindActiveMembershipAsync` and passes it into `List.Update`/`SoftDelete` (role lives on `UserHousehold`, a different aggregate). This avoids loading `Household` per mutation; see the canonical small-aggregates rationale in `Households.md`.
- **List items have no role gate.** Any active member can add/toggle/reorder/delete items — the collaborative grocery-list UX. The handler enforces membership; the aggregate's item methods don't take a role.
- **Media caption reuses `Comment`; `Text` stays `""`.** Each field keeps one meaning (clean separation): `Text` is the parseable name, `Comment` is human prose / caption. `UpdateItem` rejects text/quantity edits on media items so the invariant holds regardless of client.
- **Promotion state is persisted on the item, not device-local.** It replaced a `localStorage` batch so the promote queue is shared across members and survives devices; resolution is idempotent.
- **`ListCreatorResponse` is colocated**, not shared with `InventoryCreatorResponse` (cross-folder DTO sharing is a smell — `Vertical_Slices.md`).

## Cross-feature touchpoints

- **Product catalog / AI** (`AI_Classification.md`): item text feeds quantity extraction and the product catalog; the catalog drives promote candidacy and blueprint sorting. **Easy-to-forget contract:** a slice that creates items with an already-supplied quantity (skipping extraction) must still ensure the product is referenced into the catalog, or promote-to-inventory silently breaks — this bit `CopyRecipeToList` in PR #130 (memory `project_listitem_slice_classification_contract`).
- **Inventories** (`Inventories.md`): promote writes into an inventory via `inventory.AddItem`; the inventory→list re-order path is the quantity-supplied create.
- **Households** (`Households.md`): blueprints are curated at household level and applied here; `HouseholdSettings.CheckedItemRetentionDays` drives the checked-item purge in `DeleteInactiveItems`.
- **Recipes** (`Recipes.md`): `CopyRecipeToList` adds items here via `List.AddItem`.

## Frontend (`ClientApp/src/features/lists/`)

Route shells under `src/routes/lists/` delegate to `features/lists/pages/`. Per-slice hooks (`useHouseholdLists`, `useList`, `useCreateList`, `useUpdateList`, `useDeleteList`) and an `items/` sub-area with optimistic toggle/reorder/create hooks (keep their `onMutate`/`onError`/`onSettled`). The composer + quantity/date input primitives live in the shared `src/components/inputs/` (used by both Lists and Inventories). Media items use `useAttachmentImage`-style object-URL handling (StrictMode rule: paired create+revoke effect, cache the Blob not the URL).

## Links out

- Slice pattern: `Vertical_Slices.md`
- API client + hook conventions (incl. optimistic updates): `API_Integration.md`
- Styling: `Frontend_Styling.md`
- Quantity extraction + product catalog: `AI_Classification.md`
- Media storage + image processing: `File_Storage.md`
- Promote target / aggregate sibling: `Inventories.md`
- Blueprints + retention settings + aggregate-boundary rationale: `Households.md`
- Recipe → list copy: `Recipes.md`

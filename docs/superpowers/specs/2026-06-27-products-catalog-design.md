# User-editable Products catalog (override AI classification)

**Date:** 2026-06-27
**Branch:** `feat/products-catalog` (off `stage`)
**Status:** Design approved, pending implementation plan

## Goal

Let a household see and correct the AI classifier's verdicts in its `Product` catalog.
Two concrete pains today have no remedy: (1) a **wrong shelf life** makes the expiry
reminder fire early/late with no way to adjust it; (2) a **wrong perishability** means the
promote-to-inventory expiry prompt never appears (or appears when it shouldn't). Any
classification error silently propagates to every future inventory item derived from that
product name. This feature gives Owners/Admins a catalog page to override category, expiry
handling, and shelf life per product — and to reset a product back to the AI's verdict.

## Key facts that shape the design

- **The override layer is already planned in the entity.** `Product.cs` documents it verbatim:
  *"A user Override layer is a future additive set of nullable columns; `EffectiveExpiry` will
  become `Override ?? Classification` then."* This spec implements exactly that, rather than the
  alternative (overwriting the AI columns + an `IsUserClassified` bool).
- **Two orthogonal facets.** `ProductClassification(Category, Expiry)`. `ProductCategory` has 25
  values today (2 sentinels + 23 aisles — the aisle taxonomy already shipped). `ExpiryProfile`
  carries `Handling` (`Unknown` / `NonPerishable` / `UserEntersFromPackage` /
  `AiRecommendsShelfLife`) + an optional `ShelfLifeDays` (range 1..365), with the invariant
  *days are set iff `AiRecommendsShelfLife`*.
- **Downstream already reads `Effective*`.** `Lists/Items/PromoteSuggestion.For` and
  `Lists/Blueprints/ApplyBlueprint` read `EffectiveExpiry` / `EffectiveCategory`. Routing those
  accessors through the override layer makes the user's correction propagate with **zero changes**
  to the classifier, jobs, or promote flow.
- **Backfill is the threat to overrides.** `BackfillProductClassification` (an `IMaintenanceTask`)
  re-enqueues classification for every `Product` below the current `ClassifierVersion` on each
  cold start. The override must survive a `ClassifierVersion` bump. Correctness is guaranteed by
  `Effective*` preferring the override; the gap-selector skip is a cost optimization on top.
- **Household settings UI already exists.** `ManageHouseholdPage` → `HouseholdSettingsCard`, with
  the `CanManageSettings()` (Owner/Admin) write gate used by `UpdateHouseholdSettings`. The
  catalog has a natural home and gate today — no settings-infrastructure prerequisite.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Storage | **Nullable override columns**, `Effective = Override ?? Classification` | Matches the entity roadmap; reset is an instant null-out that restores the preserved AI verdict and works even with AI disabled; no destructive overwrite. |
| Override granularity | **Atomic** (whole classification, set/cleared together) | One "user-owned" concept; simplest model and UI. Trade-off: overriding only shelf-life also freezes category at its current AI value. |
| `IsUserClassified` flag | **Derived**, not stored (`OverrideExpiryHandling.HasValue`) | The presence of an override *is* the flag; one fewer column. |
| Write access | **Owner/Admin** (`CanManageSettings()`) | The catalog is household-wide config affecting everyone's expiry prompts, like `CheckedItemRetentionDays`. Members read-only. |
| UI home | **Dedicated sub-page** linked from `ManageHouseholdPage` | Scales to a long catalog (every distinct list-item name). |
| Search | **Client-side filter** over the fetched list | A household's catalog is modest; server-side paging deferred until it isn't. |
| `Unknown` sentinel | **Excluded** from the edit pickers | A user never deliberately picks "couldn't classify". |

## 1. Domain — `Product` aggregate

Add three nullable columns, set/cleared atomically via two methods:

```csharp
public ProductCategory? OverrideProductCategory { get; set; }   // nullable enum -> nullable int column
public ExpiryHandling?  OverrideExpiryHandling  { get; set; }
public int?             OverrideShelfLifeDays   { get; set; }

public bool IsOverridden => OverrideExpiryHandling.HasValue;     // atomic => category set together

public ProductCategory EffectiveCategory =>
    OverrideProductCategory ?? ClassificationProductCategory;

// Expiry is taken as a WHOLE facet, not column-by-column: a NonPerishable override must null
// the days, never fall back to the AI's shelf life.
public ExpiryProfile EffectiveExpiry =>
    OverrideExpiryHandling.HasValue
        ? ExpiryProfile.Create(OverrideExpiryHandling.Value, OverrideShelfLifeDays).Value
        : ExpiryProfile.Create(ClassificationExpiryHandling, ClassificationShelfLifeDays).Value;

public void OverrideClassification(ProductClassification classification)
{
    OverrideProductCategory = classification.Category;
    OverrideExpiryHandling  = classification.Expiry.Handling;
    OverrideShelfLifeDays   = classification.Expiry.ShelfLifeDays;
}

public void ResetToAiClassification()
{
    OverrideProductCategory = null;
    OverrideExpiryHandling  = null;
    OverrideShelfLifeDays   = null;
}
```

- `OverrideClassification` takes a pre-validated `ProductClassification` VO (built by the slice via
  `ExpiryProfile.Create`), keeping the days/handling invariant in the domain. The `.Value` on
  `EffectiveExpiry` is safe because the override columns are only ever written through that VO.
- **Migration:** one migration, three nullable columns (default null). No data backfill.

## 2. API — `Frigorino.Features/Products/`, group `/api/household/{householdId:int}/products`

New slice folder; register a `products` group in `Program.cs` mirroring the existing
household-scoped groups. Every handler resolves membership with `FindActiveMembershipAsync`
(404 if not a member), and loads the `Product` verifying `p.HouseholdId == householdId` (404
otherwise) — same shape as `UpdateHouseholdSettings`.

| Slice | Method | Access | Behavior |
|---|---|---|---|
| `GetProducts` | `GET ""` | any member | inline EF projection into the list DTO, ordered by `NormalizedName` |
| `OverrideProductClassification` | `PUT "{productId:int}/classification"` | `CanManageSettings()` else 403 | `ExpiryProfile.Create(handling, days)` → `ValidationProblem` on failure; `new ProductClassification(category, profile)`; `product.OverrideClassification(...)`; save |
| `ResetProductClassification` | `DELETE "{productId:int}/classification"` | `CanManageSettings()` else 403 | `product.ResetToAiClassification()`; save |

Enums serialize as **string names** on the wire (existing `JsonStringEnumConverter`).

**Read DTO** (one row):
```
Id, Name (= NormalizedName),
EffectiveCategory, EffectiveExpiryHandling, EffectiveShelfLifeDays,
IsOverridden,
AiCategory, AiExpiryHandling, AiShelfLifeDays
```
The AI columns ride along (free — already on the row) so the edit sheet can show *"AI suggested
Frozen"* and make Reset meaningful.

**Override request DTO:** `(ProductCategory Category, ExpiryHandling ExpiryHandling, int? ShelfLifeDays)`.
Reset has no body.

## 3. Backfill shield — `ProductClassificationGaps` + `ClassifyProductJob`

- Add `IsOverridden` to the `ExistingProduct` projection in `BackfillProductClassification`.
- In `ProductClassificationGaps.SelectGaps`, an overridden row is **up-to-date regardless of
  `ClassifierVersion`**: `isUpToDate = hasOverride || version >= current`.
- Add the same guard to `ClassifyProductJob`'s short-circuit so the live path (item create/update
  referencing an overridden product) also leaves it alone.

After a reset, the row's `ClassifierVersion` may be stale → it re-enters the gap set → the next
cold-start backfill refreshes its AI layer. Correctness never depended on this skip (effective
reads prefer the override); it just stops wasting API calls on shadowed rows.

## 4. Frontend

- **Route:** `routes/household/products.tsx` — param-less, active-household pattern (like
  `manage.tsx`): `createFileRoute` + `requireAuth` + `ProductCatalogPage` from
  `features/products/pages/`. The page reads the active household id and passes it as
  `{ path: { householdId } }` to the generated hooks. Linked from a row/card on `ManageHouseholdPage`.
- **Hooks** (canonical shapes, no hand-written `queryFn`/`mutationFn`/`queryKey`):
  - `useProducts` — query; spreads `getProductsOptions({ path: { householdId } })`; `enabled` on
    `householdId > 0`; a `staleTime`.
  - `useOverrideProductClassification`, `useResetProductClassification` — arg-less mutations;
    caller passes `{ path, body }`; invalidate the products query via
    `getProductsQueryKey({ path: { householdId } })` in `onSettled`.
- **Catalog page:** client-side search filter over the fetched list
  (`// ponytail: client-side filter; add server-side paging if a household exceeds a few hundred products`).
  Rows show name + effective category/expiry + an "overridden" badge. Owner/Admin rows are
  tappable → edit sheet; Members get a read-only list (no edit affordance).
- **Edit sheet** (MUI `Dialog` / bottom sheet): Category `Select`, Expiry-handling `Select`, and a
  Shelf-life-days field **revealed only when `AiRecommendsShelfLife`** — switching to
  `NonPerishable` / `UserEntersFromPackage` hides and clears it, enforcing the `ExpiryProfile`
  coupling client-side. Pickers exclude the `Unknown` sentinel. The AI suggestion shows as helper
  text; a **Reset to AI** action appears when `isOverridden`.
- **i18n:** new keys in `en` + `de`; if introduced as a new `products` namespace, also register it
  in `src/types/i18next.d.ts` (the three-files rule). Tests never assert on translated text.

## 5. Testing

- **`Frigorino.Test`** (pure aggregate — extends `Domain/ProductAggregateTests.cs`):
  - `OverrideClassification` sets the override layer and flips `EffectiveCategory` / `EffectiveExpiry`.
  - `ResetToAiClassification` nulls the layer and restores the AI verdict.
  - `EffectiveExpiry` coupling: a `NonPerishable` override nulls days even when the AI had a shelf life.
  - `ProductClassificationGaps.SelectGaps`: an overridden row is never a gap despite a stale version.
- **`Frigorino.IntegrationTests`** (Reqnroll + Playwright, testids only — no translated text):
  Owner overrides a product in both expiry modes (days field appears/hides), the row badges as
  overridden, Reset restores the AI verdict; a Member sees the read-only list and the write
  endpoint returns 403. Scope to one happy-path override + reset + the role gate.

## Out of scope

Name editing (`NormalizedName` is the lookup key); taxonomy expansion (tracked in the
aisle-taxonomy / category-blueprints specs); bulk import/export of classifications;
cross-household shared catalogs; external product-database lookup; AI-suggested corrections
("did you mean Dairy?"); per-facet override (atomic only — see Decisions).

## Impact / cost

Small-to-medium. One migration (3 nullable columns). Two aggregate methods + two derived
accessors. Three slices + one read DTO + one request DTO. One frontend route, page, edit sheet,
and three hooks. One-line additions to `SelectGaps` and `ClassifyProductJob`. No changes to the
classifier, the background jobs, or the promote/blueprint flows — they already consume `Effective*`.

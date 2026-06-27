# AI: Product Classification & Quantity Extraction

Three independent, optional AI features, all backed by OpenAI through Domain-owned ports and all **off by default**:

1. **Product classification** — assigns a `Product` (per-household catalog entry) a retail aisle + expiry handling, so list items can be grouped and promoted to inventory with sensible expiry defaults.
2. **Quantity extraction** — turns a free-text item ("2 kg Äpfel", "two cups flour") into a clean name + structured `Quantity`.
3. **Recipe tag suggestion** — given a recipe's name + description + ingredients, suggests curated course/dietary tags the user can accept (see `Recipes.md`).

Classification and extraction run as **fire-and-forget jobs on the `BackgroundTaskQueue`** (request-triggered, best-effort), never inline. Recipe tag suggestion is the deliberate exception — it runs **synchronously in-request** because it's the user's primary action (a button tap), not a side-effect of a write. (The other synchronous AI-adjacent path, the expiry scan, is unrelated — see `Push_Notifications.md`.)

## Vendor-agnostic boundary

All contracts live in `Frigorino.Domain/Interfaces/` (`IItemClassifier`, `IQuantityExtractor`, `IRecipeTagSuggester`, the `I*Trigger`s, the `I*Job`s) and `Frigorino.Domain/{Products,Quantities}/` (value objects). The OpenAI SDK is referenced **only** in `Frigorino.Infrastructure/Services/` (`OpenAiItemClassifier`, `OpenAiQuantityExtractor`, `OpenAiRecipeTagSuggester`). Swapping vendors = a new adapter behind the unchanged port; DI and call sites don't move. (This is the standard "define `IXxx`, keep vendor selection separate" pattern.)

## Product domain (`Frigorino.Domain/`)

`Entities/Product.cs` — per-household catalog keyed by `(HouseholdId, NormalizedName)` (`Products/ProductName.Normalize` = lowercase + trim + collapse whitespace). Classification layer (overwritten wholesale on re-classify):

- `ClassificationProductCategory` (`Products/ProductCategory.cs` — ~23 retail aisles + `Unknown`/`Other`)
- `ClassificationExpiryHandling` (`Products/ExpiryHandling.cs` — `Unknown` / `NonPerishable` / `UserEntersFromPackage` / `AiRecommendsShelfLife`)
- `ClassificationShelfLifeDays?` (only meaningful with `AiRecommendsShelfLife`)
- `ClassifierVersion` (int) — idempotency stamp; bump it when the prompt/model/schema changes to invalidate cached classifications.

Value objects: `ProductClassification` (Category + `ExpiryProfile`), `ExpiryProfile` (`Handling` + optional `ShelfLifeDays`, invariant: days set only for `AiRecommendsShelfLife`; `.SuggestsInventoryTracking` for perishables). Enum **names** are interpolated into the OpenAI Structured-Outputs JSON schema, so renaming an enum member changes the model contract.

**User override layer.** `Product` carries nullable `OverrideProductCategory` / `OverrideExpiryHandling` / `OverrideShelfLifeDays`, set/cleared atomically via `OverrideClassification` / `ResetToAiClassification`. `EffectiveCategory` / `EffectiveExpiry` read `Override ?? Classification` (expiry as a whole facet — a `NonPerishable` override nulls the days, never falls back to the AI's). `IsOverridden` (`OverrideExpiryHandling.HasValue`) shields the row: `ProductClassificationGaps.SelectGaps` and `ClassifyProductJob` treat an overridden row as up-to-date regardless of `ClassifierVersion`, so a version bump never clobbers a user correction (after a reset, the stale version re-enters the gap set). Surfaced via the `Products` slices under `/api/household/{id}/products` — `GET` (any member), `PUT {id}/classification` (override) + `DELETE {id}/classification` (reset) + `DELETE {id}` (remove the product entirely), the writes Owner/Admin-only — and the catalog page under household management. Removing a product is a **hard delete** (no soft-delete column; nothing FK-references a `Product` — items resolve by normalized name), so the next reference re-creates and re-classifies it via `ClassifyProductJob`.

## Classification pipeline

`IProductClassificationTrigger.OnProductReferenced(householdId, rawName)` is the entry point. Call sites: `Lists/Items/CreateItem` + `UpdateItem` (via the quantity-extraction chain, below), `Recipes/CopyToList/CopyRecipeToList` (direct), and the startup backfill. The enabled impl (`QueueingProductClassificationTrigger`) enqueues `ClassifyProductJob`; the disabled impl (`NullProductClassificationTrigger`) is a no-op.

`ClassifyProductJob` (`Services/ClassifyProductJob.cs`) runs in its own DI scope: normalize → look up `Product` → if it exists with `ClassifierVersion >= current`, short-circuit (cache hit); otherwise call `IItemClassifier.ClassifyAsync` and upsert. A unique-index race on insert is a benign no-op. `OpenAiItemClassifier` uses Structured Outputs (strict schema), maps refusals/empties to `Unknown`/`NonPerishable` (a valid result, not an error), and logs its reasoning for diagnostics only (never persisted).

## Quantity-extraction pipeline

Triage is free and synchronous: `Quantities/ItemTextRouter.Analyze(text)` → `ItemTextAnalysis(Route, CleanName)` where `Route` is `SkipAi` (empty / URL / too long / no alphanumeric) or `NeedsExtraction`. Slices call `IQuantityExtractionTrigger.OnItemRouted(...)` after the item is saved.

- **Enabled** (`QueueingQuantityExtractionTrigger`): `NeedsExtraction` → enqueue `ExtractQuantityJob`.
- **Disabled** (`NullQuantityExtractionTrigger`): extraction off, but it still calls the classification trigger directly on the clean name — so classification can run even when extraction doesn't.

`ExtractQuantityJob` (`Services/ExtractQuantityJob.cs`): re-reads the item (guards against a stale queued value and against a concurrent edit landing mid-call), calls `OpenAiQuantityExtractor` (handles EN+DE, spelled-out amounts, keeps brand digits like "7up"), rewrites the item text to the clean name + sets the `Quantity`, then **chains `OnProductReferenced` on the clean name** so the catalog keys on "apples", not "20 apples". `Quantities/`: `QuantityExtraction` (clean name + `Quantity?`), `Quantity` (value > 0 + unit), `QuantityUnit` (currently Gram/Kilogram/Milliliter/Liter/Piece/Pack/Can/Bottle/Bag).

**Recipe variant** (`IRecipeQuantityExtractionTrigger` → `ExtractRecipeQuantityJob`) is identical **except it never chains classification** — recipe items don't create `Product` rows.

### Chaining summary

| From → To | Wired? |
|---|---|
| Classification → extraction | No (independent, gated separately) |
| List-item extraction → classification | Yes (on the clean name) |
| Recipe extraction → classification | Never (MVP: recipes don't classify) |
| Recipe tag suggestion → anything | Never (suggest-only; writes no `Product` rows, persists nothing) |

## Recipe tag suggestion

The odd one out: **synchronous, on-demand, stateless, suggest-only** — no trigger, no queue, no job. The `SuggestRecipeTags` slice (`POST /{id}/suggest-tags`) resolves `IRecipeTagSuggester` and awaits it in-request, returning the suggested tags directly to the caller; the user accepts a chip, which is a separate `PUT /{id}/tags` write. Nothing is persisted from the suggest call.

`OpenAiRecipeTagSuggester` uses Structured Outputs whose allowed `enum` values are interpolated from `Enum.GetNames<RecipeTag>()` (so the schema can't drift from the enum). Refusals / empties / **any error** map to an empty list — a valid "no confident suggestions" — so a user's button tap never 500s. `NullRecipeTagSuggester` (disabled path) always returns empty, so the endpoint is always safe to call. The port returns `IReadOnlyList<RecipeTag>` directly (not `Result<T>`): an empty list is the success-with-nothing case, not an error. See `Recipes.md`.

**Model choice — full `gpt-5.4`, on purpose.** Unlike the classifier (`mini`) and quantity extractor (`nano`), tag suggestion defaults to the **full** `gpt-5.4`. It's a deliberate per-feature trade-off: suggestion is **low-volume** (one synchronous call per explicit button tap, not a per-item background job) and **accuracy-sensitive** (a wrong dietary tag is worse than a missed one), so the extra cost buys real quality where volume can't make it expensive. This is exactly why each AI feature has its own `:Model` knob rather than a shared default — don't "normalize" this to `mini`/`nano` to match its siblings.

## Gating & DI

Config (`appsettings.json` → user-secrets / Railway env):

| Key | Effect |
|---|---|
| `Ai:ApiKey` | OpenAI key. **Required** to enable any feature. |
| `Ai:Classifier:Enabled` | Turns on classification (default off). |
| `Ai:Classifier:Model` | default `gpt-5.4-mini`. |
| `Ai:QuantityExtractor:Enabled` | Turns on extraction (default off). |
| `Ai:QuantityExtractor:Model` | default `gpt-5.4-nano`. |
| `Ai:RecipeTagSuggester:Enabled` | Turns on tag suggestion (default off). |
| `Ai:RecipeTagSuggester:Model` | default `gpt-5.4` (the **full** model, not mini/nano — see below). |

DI methods run **in order** in `Program.cs`: `AddItemClassification` → `AddQuantityExtraction` → `AddRecipeQuantityExtraction` → `AddRecipeTagSuggestion` (each of the first three reuses ports the previous registered, e.g. the disabled extractor needs the classification trigger; `AddRecipeTagSuggestion` is standalone — it depends on no other AI port). Each registers the keyed OpenAI `ChatClient` (`Services/AiKeys.cs`: `Classifier` / `Extractor` / `RecipeTagSuggester`) + real impls only when `ApiKey` **and** the feature's `Enabled` flag are set; otherwise the `Null*` impl. The classification/extraction `*Job` types are registered **only on the enabled path** (they depend on `IItemClassifier`/`IQuantityExtractor`, which don't exist when disabled — registering them unconditionally would fail `ValidateOnBuild`); the tag suggester has no job, and registers `IRecipeTagSuggester` on **both** paths (real or `Null`) so the slice always resolves it.

## Backfill

`Tasks/BackfillProductClassification.cs` (an `IMaintenanceTask`, runs at cold start, registered by `AddMaintenanceServices` **only when classification is enabled**): scans active list items and enqueues classification for names with no `Product` row or a stale `ClassifierVersion`, capped per run (overflow waits for the next cold start). This is how a `ClassifierVersion` bump propagates without a manual reclassify.

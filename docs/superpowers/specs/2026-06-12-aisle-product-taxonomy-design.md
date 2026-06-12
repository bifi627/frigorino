# Aisle-level Product Taxonomy + Re-classification

**Date:** 2026-06-12
**Branch:** `feat/aisle-product-taxonomy` (off `stage`)
**Status:** Design approved, pending implementation plan

## Goal

Replace the coarse `ProductCategory` (`Unknown` / `Other` / `Food` / `HouseholdSupply`)
with a **23-aisle supermarket taxonomy** (+ 2 sentinels) so a future "sort a list by store
walk-order" feature has a meaningful axis to sort on. The classifier already produces a
category facet per item; today it is **classified but unconsumed** — nothing in the app reads
it. This spec makes that facet *meaningful*; it does not add any consumer.

This is the **prerequisite first slice** of the larger "AI-assisted list sorting (category
blueprints)" idea. It ships independently and is independently useful (richer classification of
the product catalog). The household `SortBlueprint`, the blueprint editor UI, and the per-list
"Sort by category" apply action are a **separate follow-up spec** and are explicitly out of
scope here.

### Why this is a deliberately separate spec

The taxonomy change is the only piece that (a) touches the AI prompt and (b) triggers a full
catalog re-classification (real, if small, token spend on the live `stage` client). Landing it
on its own lets the classification change stabilize before any sorting UX depends on it. The
sort that *consumes* the taxonomy is designed in the follow-up.

## Key facts that shape the design

- **Category is unconsumed today.** `Product.EffectiveCategory` is read by no feature and
  appears in no response DTO or generated TS client. Inventory promotion is driven by the
  **orthogonal** `ExpiryProfile` facet, not by category. So redefining the enum is low-risk:
  no branching logic depends on the current values.
- **Two orthogonal facets, produced in one call.** `ProductClassification(Category, Expiry)`.
  This spec changes only the `Category` axis. `ExpiryProfile`
  (`NonPerishable` / `UserEntersFromPackage` / `AiRecommendsShelfLife` + `ShelfLifeDays`) is
  untouched, and inventory promotion keeps working exactly as today. Aisle and expiry
  correlate but are not redundant (Frozen pizza = `Frozen` aisle + `UserEntersFromPackage`;
  pasta = `Pantry` + `NonPerishable`).
- **The enum is the single source of truth for the model.** `OpenAiItemClassifier`'s strict
  Structured-Outputs schema derives its `productCategory.enum` from
  `Enum.GetNames<ProductCategory>()` with `jsonSchemaIsStrict: true` — the model literally
  cannot return a value outside the enum. Defining the taxonomy = the enum values + one prompt
  line per value. No hand-edit of the schema skeleton.
- **Re-classification rollout already exists.** `BackfillProductClassification`
  (an `IMaintenanceTask`, runs each cold start) scans active list-item names, finds every
  `Product` below the current `ClassifierVersion`, and enqueues re-classification (throttled to
  `BackgroundTaskQueue.Capacity` per run, remainder next cold start), plus lazy re-classification
  on item reference. Bumping the version is the entire trigger — no new rollout code.

## 1. Domain — redefine the enum

`Frigorino.Domain/Products/ProductCategory.cs`: replace the 4 values with **23 aisles + 2
sentinels**. Keep `Unknown = 0` (stays `default(ProductCategory)` and the safe default) and
`Other = 1`; aisles take `2..24`.

```
Unknown = 0               // couldn't classify / nonsense / unrecognized
Other = 1                 // recognized but not a stocked grocery/household good (a task, a one-off)
Produce = 2               // Obst & Gemüse
Bakery = 3                // Backwaren
Meat = 4                  // Fleisch
Fish = 5                  // Fisch
DairyAndEggs = 6          // Milch, Joghurt, Butter, Eier
Cheese = 7                // Käse
DeliAndColdCuts = 8       // Wurst, Aufschnitt, Kühltheke
Frozen = 9                // Tiefkühl
Pantry = 10               // Nudeln, Reis, Mehl (trockene Grundnahrung)
CannedGoods = 11          // Konserven / Dosen
Sauces = 12               // Soßen, Ketchup, Dressings, Passata
OilsAndVinegar = 13       // Öle & Essig
Spices = 14               // Gewürze, Salz, Backzutaten
Cereal = 15               // Müsli, Haferflocken, Cornflakes
Spreads = 16              // Aufstriche, Marmelade, Nutella, Honig
Snacks = 17               // Chips, herzhafte Snacks
Sweets = 18               // Schokolade, Süßigkeiten
Beverages = 19            // Getränke (alkoholfrei)
Alcohol = 20              // Bier, Wein, Spirituosen
HouseholdAndCleaning = 21 // Haushalt & Reinigung
HealthAndBeauty = 22      // Drogerie & Körperpflege
Baby = 23                 // Babynahrung, Windeln
Pet = 24                  // Tierbedarf, Tierfutter
```

Allocation notes (judgment calls, easy to tweak): `Cheese` and `DeliAndColdCuts` split the old
deli; `CannedGoods` leaves `Pantry` holding dry staples only; the old `Condiments` becomes three
aisles (`Sauces`, `OilsAndVinegar`, `Spices`); `Cereal` (Müsli, Haferflocken) and `Spreads`
(Aufstriche, Marmelade, Nutella) split what was a single breakfast bucket; `Snacks` (savoury) and
`Sweets` (confectionery) split the old `SnacksAndSweets`; `Baby` and `Pet` are separate aisles.

Update the file's header comment — the old "Food/HouseholdSupply … drives whether Cycle 3
offers the item for inventory promotion" note is stale (category is unconsumed; expiry drives
promotion). The new comment states: this is the "what kind / which aisle" facet, orthogonal to
`ExpiryProfile`; `Unknown = 0` is the safe default and the classification-failure fallback;
`Other` is a recognized non-shoppable item.

## 2. Infrastructure — classifier prompt + version

`Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs`:

- **Prompt.** Rewrite the `productCategory` block of `SystemPrompt` to describe all 25 values,
  one short line each with en/de examples (mirroring the existing style). The `expiryHandling`
  block and the `reasoning`-first ordering are untouched. The strict-output schema `enum`
  auto-derives from the enum names — no schema-skeleton edit.
- **Version.** Bump `Version => 2`.
- **Failure fallback → `Unknown`.** The two fallback sites that currently return
  `ProductCategory.Other` (empty/refused content at `OpenAiItemClassifier.cs:114`; schema-valid-
  but-semantically-inconsistent at `:131`) switch to `ProductCategory.Unknown`. Rationale: under the new taxonomy `Other` means
  "recognized but non-shoppable (a task)", while a *failed* classification is semantically
  "couldn't classify" = `Unknown`. (The expiry fallback stays `NonPerishable`.)

## 3. Re-classification rollout — no new code

Bumping `Version` 1 → 2 makes every existing `Product` stale (`ClassifierVersion 1 < 2`). The
existing `BackfillProductClassification` maintenance task re-classifies the whole catalog over
successive cold starts (capped per run), with lazy re-classification on item reference in
between. Nothing to build. Old rows keep carrying their version-1 `int` category until
overwritten; since nothing reads category, the transient state is invisible.

## 4. Cost estimate (the re-classify token budget)

Model: `gpt-5.4-mini` (`appsettings.json` → `Ai:Classifier:Model`).

| Per-call input | Current | After (25 values) |
|----------------|--------:|------------------:|
| System prompt | ~300 tok | **~1,020 tok** (`productCategory` block 4 → 25 bilingual lines) |
| Strict-output schema (counts as input) | ~130 tok | **~240 tok** (25 enum names) |
| User message (item name) | ~2–6 tok | ~2–6 tok |
| **Total input / call** | ~440 tok | **~1,270 tok** |

Per-call output: ~60 visible tokens (one-sentence `reasoning` + 3 JSON fields) plus a variable
model-reasoning-token count (logged via `OutputTokenDetails.ReasoningTokenCount`).

Two factors keep the bulk re-classify cheap:

1. **Prompt caching crosses its threshold.** The static prefix is now ~1,270 tokens —
   comfortably over OpenAI's 1,024-token caching minimum (the old prompt was *under* it). The
   system prompt + schema are byte-identical every call, so during a backfill burst nearly all
   input tokens are cache hits (~10× cheaper). The longer 25-value prompt is close to free in
   steady state.
2. **It is N = distinct product names, once.** One call per distinct `(household,
   normalizedName)` — not per list-item or per request. For the single `stage` client that is
   tens–low-hundreds of names total. Example: 300 names ≈ ~380k input tokens (mostly cached)
   + ~20k output, spread across cold starts. Steady-state per-new-name cost is unchanged from
   today (same one call, slightly longer prompt).

No dollar figure is quoted for `gpt-5.4-mini`; budget as
`≈ N_names × (~1.3k input + ~80 output) tokens`, most input cached after the first call.

## 5. No migration, no API, no frontend

- **No EF migration.** Category is stored as `int` (EF default); adding CLR enum labels changes
  no schema. `ProductConfiguration` is unchanged.
- **No `npm run api` / no frontend.** Category is in no response DTO and no generated client
  (verified — zero matches under `ClientApp/src/lib`).

## 6. Testing

- **Fixtures using removed values.** Update the `Frigorino.Test` fixtures that name
  `ProductCategory.Food` / `HouseholdSupply` — `ProductAggregateTests`, `PromoteSuggestionTests`,
  `ClassifyProductJobTests` — to a surviving value (e.g. `Produce`, `HouseholdAndCleaning`).
- **Integration stub + steps.** Update `StubItemClassifier` (maps test inputs → categories) and
  `ClassificationApiSteps` (asserts a category via `Enum.Parse<ProductCategory>`) to the new
  values.
- **Rollout trigger.** Ensure a test asserts that a version-1 `Product` is treated as stale and
  re-classified under version 2 (lock in the version bump). Confirm whether
  `ProductClassificationGapsTests` / `ClassifyProductJobTests` already cover this; add a case if
  not.
- **Fallback.** Add/extend a classifier test that a refusal / empty / inconsistent response now
  yields `ProductCategory.Unknown` (was `Other`).
- No JS test runner (unchanged).

## 7. Out of scope (the follow-up sorting spec)

The household `SortBlueprint` store, the blueprint editor UI (reusing `@dnd-kit` `SortableList`),
the per-list "Sort by category" apply action (a bulk re-rank of the unchecked section via the
fractional-index `Rank`), and any reading of `EffectiveCategory`. This spec only makes the
category meaningful; nothing consumes it yet.

## Impact / cost

1 enum rewrite · 1 prompt rewrite + `Version` bump · 2 fallback-constant changes · test fixture
updates. No EF migration, no API regeneration, no frontend, no new rollout code. Re-classify
token spend is small and self-throttling (see §4).

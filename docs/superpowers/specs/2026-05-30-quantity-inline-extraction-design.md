# Cycle 2.5 — Inline quantity extraction (lists) — design

- **Date:** 2026-05-30
- **Status:** Approved (design) — pending written-spec review
- **Branch:** off `stage` (Cycle 2 classifier already merged to `stage`)
- **Prerequisite:** Cycle 2 classification engine (`IItemClassifier`, Product catalog, `OnProductReferenced` trigger seam, `Classifier` config) — **present on `stage`**.
- **Supersedes:** the input-model, picker, and scope decisions of `2026-05-27-quantity-value-object-design.md`. That spec's `Quantity` value-object rationale (decisions 1–3, 8) still holds and is reused here; its picker-centric entry model (layout B, both entities) is replaced by inline extraction.

## Summary

Replace `ListItem`'s free-text `Quantity string?` with a structured **`Quantity` value object** (numeric `Value` + `QuantityUnit`), and make **natural inline typing the primary entry path**. A user types `"20 apples"` or `"1l milk"`; a cheap async LLM **extractor** pulls `{clean name, quantity}` from the raw text, writes them back to the item, then **chains** into the Cycle 2 classifier (now keyed on the clean name, so the Product catalog cache stays intact). A lightweight popover edits quantity manually and is the fallback when extraction is disabled, fails, or guesses wrong.

**Scope: lists only.** The migration touches `ListItem` only; `InventoryItem` keeps its free-text quantity until a later inventory cycle. The `Quantity` VO is built as shared domain with inventory as its eventual second consumer.

## Why two pipelines, two models

Quantity and classification are different *kinds* of fact, and the design keeps them cleanly separate (see `feedback_clean_domain_separation`):

| | Classification (category, expiry facets) | Quantity |
|---|---|---|
| Belongs to | the **product type** (`Product`) | **this list entry** (`ListItem`) |
| Lifetime | per product, **cached** (version-gated) | per entry, **never cached** |
| Model | powerful (real reasoning over facets) | cheap/fast (pull a number + unit) |
| Trigger | downstream of extraction | upstream, fires per qualifying add |

The cache constraint forces the ordering: the Product catalog key is the **clean name**, which is only known *after* the quantity prefix/suffix is removed. So extraction is logically **upstream** of classification — the cheap extractor produces the clean name as a byproduct, and classification keys on it. This preserves the Cycle 2 cache (otherwise `"20 apples"`, `"5 apples"`, `"apples"` would each cost a classifier call).

## Data flow

1. **Add.** `CreateItem` persists the `ListItem` with the raw text and no quantity, returns `201` immediately.
2. **Trigger.** The slice calls the quantity pipeline front door — this **replaces** today's direct `classificationTrigger.OnProductReferenced(...)` call in `CreateItem`/`UpdateItem`.
3. **Digit-gate (backend, position-agnostic).** Only if the raw text contains a digit *anywhere* do we spend the LLM. `"20 apples"` and `"apples 20"` both qualify; `"milk"`, `"call dentist"` do not and skip extraction entirely. This keeps the per-item LLM cost off the majority of plain items.
4. **Extract (cheap model, async via Cycle 1 channel queue).** `"20 apples"` → `{ CleanName: "apples", Quantity: {20, Piece} }`. The job **writes back** `ListItem.Text = "apples"` plus the two quantity columns. The LLM returns `CleanName` directly, so it handles quantity in any position *and* the "digit-is-part-of-the-name" cases (`"7up"`, `"WD-40"`, `"E45 cream"` → name kept, quantity null).
5. **Chain to classify (powerful model).** On extraction completion the job triggers classification on the clean name → Product catalog (cached). For no-digit text and for the disabled/failed paths, classification runs directly on the raw text — **exactly Cycle 2's current behavior**.
6. **UI sync.** Frontend renders the raw text optimistically, then **bounded single-item poll** via the existing `GetItem` endpoint (`refetchInterval`) until the quantity arrives or a ~4s timeout, then patches the cache and shows the chip. Poll is gated on the same "raw text has a digit" heuristic. No SignalR in v1 (designed-for upgrade later).

**Text-rewrite behavior (accepted):** the item typed as `"20 apples"` becomes displayed as `"apples"` + a `"20"` chip a beat later. The literal typed text is replaced by the extracted clean name — this is the essence of "extraction primary."

## Domain model (shared VO; lists consume now)

- **`QuantityUnit`** enum, string-serialized via the existing global `JsonStringEnumConverter`: `Gram, Kilogram, Milliliter, Liter, Piece, Pack, Can, Bottle, Bag`.
- **Unit metadata** (static, in the domain): each unit → `Dimension` (`Mass` / `Volume` / `Count`) + base-conversion factor (grams for mass, millilitres for volume; count units carry no factor). v1 *defines* this metadata but exposes **no conversion API** (follow-up).
- **`Quantity`** readonly record struct (`decimal Value`, `QuantityUnit Unit`):
  - `Quantity.Create(value, unit) → Result<Quantity>` — validates `Value > 0` and finite.
  - `Quantity.TryParse(string) → Quantity?` — deterministic best-effort parse ("2kg", "500 ml", "1,5 l"/"1.5 l" en/de separators, bare "3" → `{3, Piece}`, unit words → unit; junk → none). **Used by the migration backfill only**, never at live entry (never call an LLM inside a startup migration; deterministic parsing is free and synchronous).
  - Formatter → display string ("1 l", "2 bottles") for read projections / frontend.
- **`ListItem`** aggregate: drop `Quantity string?`; add `QuantityValue decimal?` + `QuantityUnit` (nullable enum). **Both-or-null invariant** (both set, or both null = no quantity) enforced in the aggregate. Drop `QuantityMaxLength` + its `HasMaxLength` config.

## Extraction port (vendor-agnostic, mirrors `IItemClassifier`)

- **`IQuantityExtractor`** (`Frigorino.Domain/Interfaces`): `Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct)` where `QuantityExtraction(string CleanName, Quantity? Quantity)`. **No `Version`** — results are not cached.
- **OpenAI adapter** behind it (`OpenAiQuantityExtractor`), reusing the `ChatClient` + strict Structured Outputs pattern from `OpenAiItemClassifier`, on a **cheap model**. Refusal / transient error → `Result.Fail` → item keeps raw text, no quantity (lossy, safe). On a non-quantity result → `CleanName = rawText`, `Quantity = null` (no rewrite).
- **Trigger seam** mirrors Cycle 2's `IProductClassificationTrigger`: a quantity-pipeline trigger with `Queueing` and `Null` implementations.
  - `Queueing` (extractor enabled): applies the **digit-gate**. Digit present → enqueue the extraction job, which on completion chains to the classification trigger with the **clean name**. No digit → skip extraction, delegate straight to the classification trigger on the raw text.
  - `Null` (extractor disabled): no extraction; delegates straight to the classification trigger on the **raw text** (preserves Cycle 2 behavior). Classification stays alive regardless of the extractor's enabled-state.
  - The slice makes a **single** call to the quantity-pipeline trigger; the extraction layer is the new front door that classification hangs off of.

## Configuration (shared key, per-feature model + enable)

Restructure Cycle 2's flat `Classifier` block into a shared-key shape (vendor-neutral naming):

```jsonc
"Ai": {
  "ApiKey": "",                                         // shared across all AI features
  "Classifier":        { "Enabled": false, "Model": "gpt-4.1-mini" },   // powerful
  "QuantityExtractor": { "Enabled": false, "Model": "gpt-4.1-nano" }    // cheap/fast
}
```

- DI registers one `ChatClient` per feature (different model, **shared** `ApiKey`); each feature's `Enabled` gates its own `Queueing` vs `Null` trigger independently.
- The `AddItemClassification` DI extension is updated to read `Ai:ApiKey` + `Ai:Classifier:*`; a new `AddQuantityExtraction` reads `Ai:ApiKey` + `Ai:QuantityExtractor:*`.
- **Future per-user feature toggles (designed-for, not built):** v1 uses these global flags. A later per-user/household flag layer can override at runtime inside the trigger without reshaping config.
- Secrets supplied via user-secrets / env / `appsettings.Development.json` as today; `appsettings.json` carries empty/disabled placeholders.

## Persistence & migration

- One EF migration on **`ListItem` only**: drop `Quantity string?`, add `QuantityValue decimal?` + `QuantityUnit` (nullable). `InventoryItem` untouched.
- **Backfill** before the string column is dropped, using `Quantity.TryParse`: parseable → value + unit, unparseable → null. **Deliberate data-loss tradeoff, including on `stage`** (real client data) — consistent with the approved quantity spec (decision 6).
- Migrations run automatically at startup via `context.Database.MigrateAsync()`.

## API / DTOs

- **`QuantityDto(decimal Value, QuantityUnit Unit)`** — atomic nested DTO, nullable on requests/responses, so value and unit can never be transmitted apart (the both-or-null invariant is expressed in the DTO shape).
- **`UpdateItemRequest`** gains `QuantityDto? Quantity` — null = preserve existing (drives the edit popover).
- **Update modes are distinct:** extraction (via the quantity-pipeline trigger) fires only when `Text` is present on create/update — a text change re-extracts on the new text (matching Cycle 2's existing `if (request.Text is not null)` guard). A **quantity-only update** (popover, `Text == null`) writes the structured value directly and does **not** extract, so a manual edit is never overwritten by a re-extraction.
- **`CreateItemRequest`** stays text-only — quantity comes from extraction or a post-create edit (YAGNI on create-time quantity entry).
- **`GetItem` / `GetItems`** responses (`ListItemResponse`) gain `QuantityDto? Quantity`, built inline in `ToProjection`: `QuantityValue == null ? null : new QuantityDto(QuantityValue.Value, QuantityUnit.Value)`.
- Aggregate signatures map `QuantityDto` ↔ domain `Quantity` in the thin slice; the extraction job writes the domain `Quantity` directly.
- Regenerate the TS client via `npm run api`.

## Frontend

- **Composer entry** = plain text field. The approved spec's composer `quantityFeature` is **removed from entry** (quantity is no longer typed structurally at add time).
- **Item row** renders the formatted quantity chip from the structured value; shows a spinner/skeleton in the chip slot while the bounded poll is in flight (digit present, quantity not yet returned).
- **Edit popover** (simple: number field + unit dropdown) → `UpdateItem` with `QuantityDto`. This is how you adjust a quantity, add one to an item that has none, or fix a wrong extraction. It is also the only quantity path when the extractor is disabled.
- **Bounded single-item poll** via a `getItem` query (`refetchInterval` fn that stops on quantity-present or timeout).
- testids / `data-*` hooks preserved; tests never assert on translated text (`feedback_test_assertions_no_translated_text`).

## Disabled / failure behavior

- **Extractor disabled** (`Ai:QuantityExtractor:Enabled=false` or no key): no extraction; classification runs on raw text (Cycle 2 behavior); quantity only via the popover. Graceful no-op, mirroring Cycle 2.
- **Extraction failure** (refusal / transient): item keeps raw text, no quantity; logged at the adapter boundary, not surfaced to the user.

## Testing

- **Domain unit tests:** `Quantity.Create` validation; `TryParse` across mass/volume/count, en/de decimals, and junk→none; formatter output; both-or-null invariant; aggregate add/update (incl. null-preserve-on-update).
- **Extractor adapter:** structured-output → `QuantityExtraction` mapping (mocked `ChatClient`); refusal/transient → `Result.Fail`; non-quantity → `CleanName == rawText`, `Quantity == null`.
- **Pipeline:** extraction writes clean name + quantity, then triggers classification on the **clean name**; disabled/no-digit → classification on raw text; digit-gate excludes non-digit text.
- **Migration backfill:** parseable free-text → columns; unparseable → null.
- **Integration (Reqnroll + Playwright + Postgres Testcontainers):** a keyword-based **stub extractor** (mirroring `StubItemClassifier`); POST `"20 apples"` → item eventually `Text == "apples"` with `{20, Piece}`, and product `"apples"` is classified. Assert via DB polling / testids.
- **Final gate:** full `dotnet test` on the solution + `docker build`; frontend lint + tsc + prettier.

## Out of scope (deferred)

- **Inventory** inline extraction + structured quantity (later inventory cycle; the VO is built shared so inventory is a drop-in second consumer).
- **SignalR** real-time push (poll first; upgrade without data-model change later).
- **Unit conversions**, duplicate-merge by name+unit, low-stock thresholds (the metadata is defined; no API exposed).
- **Fuzzy non-numeric extraction** ("a dozen eggs") — the extractor may handle it if the model does, but it's not a v1 requirement and the digit-gate is numeric.

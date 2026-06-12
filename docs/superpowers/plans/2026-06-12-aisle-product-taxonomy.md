# Aisle-level Product Taxonomy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the coarse `ProductCategory` enum (`Unknown`/`Other`/`Food`/`HouseholdSupply`) with a 23-aisle supermarket taxonomy (+ 2 sentinels), update the classifier prompt to emit it, bump the classifier version to trigger automatic re-classification, and switch the failure fallback to `Unknown`.

**Architecture:** Backend-only, no new behaviour consumer. `ProductCategory` is a flat enum stored as `int` (EF default — no migration). The strict-output JSON schema auto-derives its enum from `Enum.GetNames<ProductCategory>()`, so only the enum + the prompt prose change. Bumping `OpenAiItemClassifier.Version` makes every existing `Product` stale; the existing `BackfillProductClassification` maintenance task re-classifies the catalog over cold starts — no new rollout code. Category is read by no feature, no response DTO, and no generated TS client, so there is no API regeneration and no frontend change.

**Tech Stack:** .NET 10, EF Core (Postgres / InMemory for tests), xUnit + FakeItEasy, Reqnroll + Playwright + Testcontainers (integration), OpenAI SDK behind the `IItemClassifier` port.

**Spec:** `docs/superpowers/specs/2026-06-12-aisle-product-taxonomy-design.md`

---

## File Structure

**Production code (2 files):**
- `Application/Frigorino.Domain/Products/ProductCategory.cs` — redefine the enum + header comment (Task 1).
- `Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs` — prompt rewrite, `Version => 2`, fallback `Other → Unknown` (Task 2).

**Test code (5 files), all updated in Task 1 to keep the solution compiling & green:**
- `Application/Frigorino.Test/Domain/ProductAggregateTests.cs`
- `Application/Frigorino.Test/Features/PromoteSuggestionTests.cs`
- `Application/Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs`
- `Application/Frigorino.IntegrationTests/Infrastructure/StubItemClassifier.cs`
- `Application/Frigorino.IntegrationTests/Slices/Lists/Classification.Api.feature` and `Extraction.Api.feature`

**Deliberately unchanged:** `ProductConfiguration.cs` (still `int`, no migration), `ProductClassificationGaps.cs` + `ProductClassificationGapsTests.cs` (version-agnostic, already use a local `CurrentVersion = 2`), `Product.cs`, any DTO / generated client. No EF migration. No `npm run api`.

**Fixture value mapping** (removed value → surviving replacement, used consistently across all test edits):
- `Food` (always on "milk") → `DairyAndEggs`
- `HouseholdSupply` (always on "soap") → `HouseholdAndCleaning`
- `Other` → unchanged (survives as the non-shoppable sentinel)
- Stub catch-all (was `Food`) → `Pantry`

---

## Task 1: Redefine the enum and update all consumers (one atomic green commit)

Removing `Food`/`HouseholdSupply` breaks compilation everywhere they are referenced (tests + the integration stub only — never production logic). This task changes the enum and every consumer together so the solution compiles and all tests pass in a single commit. The compiler is the guide: after the enum change, `dotnet build` lists exactly the lines to fix.

**Files:**
- Modify: `Application/Frigorino.Domain/Products/ProductCategory.cs`
- Modify: `Application/Frigorino.Test/Domain/ProductAggregateTests.cs:10-11,22,50,53`
- Modify: `Application/Frigorino.Test/Features/PromoteSuggestionTests.cs:13-14`
- Modify: `Application/Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs:45-47,63`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/StubItemClassifier.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/Classification.Api.feature:10,16`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/Extraction.Api.feature:15`

- [ ] **Step 1: Redefine the enum**

Replace the entire contents of `Application/Frigorino.Domain/Products/ProductCategory.cs` with:

```csharp
namespace Frigorino.Domain.Products
{
    // The "what kind / which aisle" facet of a product, orthogonal to ExpiryProfile (how it
    // expires). Stored as an int column on Product (EF default — no migration when values change).
    // Two sentinels + 23 supermarket aisles:
    //   Unknown = 0 is default(ProductCategory) AND the classification-failure fallback (we could
    //     not classify it: nonsense / refusal / inconsistent model output).
    //   Other is a recognized item that is NOT a stocked grocery/household good (a task, a one-off).
    // The aisles exist so a future "sort a list by store walk-order" feature has a meaningful axis;
    // nothing reads this facet yet. The classifier's strict-output schema derives its enum from
    // Enum.GetNames<ProductCategory>(), so adding/removing a value here updates the schema with no
    // hand-edit — but the OpenAiItemClassifier system prompt must describe each value.
    public enum ProductCategory
    {
        Unknown = 0,
        Other = 1,
        Produce = 2,
        Bakery = 3,
        Meat = 4,
        Fish = 5,
        DairyAndEggs = 6,
        Cheese = 7,
        DeliAndColdCuts = 8,
        Frozen = 9,
        Pantry = 10,
        CannedGoods = 11,
        Sauces = 12,
        OilsAndVinegar = 13,
        Spices = 14,
        Cereal = 15,
        Spreads = 16,
        Snacks = 17,
        Sweets = 18,
        Beverages = 19,
        Alcohol = 20,
        HouseholdAndCleaning = 21,
        HealthAndBeauty = 22,
        Baby = 23,
        Pet = 24,
    }
}
```

- [ ] **Step 2: Build the solution to surface every broken reference**

Run: `dotnet build Application/Frigorino.sln`
Expected: FAIL. Compile errors `CS0117: 'ProductCategory' does not contain a definition for 'Food'` (and `'HouseholdSupply'`) at the test/stub lines listed below. This is the to-do list for the rest of this task.

- [ ] **Step 3: Fix `ProductAggregateTests.cs`**

Change the `AiClassification` helper (line 11) `ProductCategory.Food` → `ProductCategory.DairyAndEggs`:

```csharp
        private static ProductClassification AiClassification(int days) =>
            new(ProductCategory.DairyAndEggs, ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value);
```

Update the assertion in `Create_Valid_SetsColumnsAndVersion` (line 22):

```csharp
            Assert.Equal(ProductCategory.DairyAndEggs, product.ClassificationProductCategory);
```

In `ApplyClassification_OverwritesLayerAndVersion`, change the `HouseholdSupply` classification (line 50) and its assertion (line 53) to `HouseholdAndCleaning`:

```csharp
            product.ApplyClassification(
                new ProductClassification(ProductCategory.HouseholdAndCleaning, ExpiryProfile.NonPerishable),
                classifierVersion: 2);

            Assert.Equal(ProductCategory.HouseholdAndCleaning, product.ClassificationProductCategory);
```

- [ ] **Step 4: Fix `PromoteSuggestionTests.cs`**

Change the `ProductWith` helper (lines 13-14) `ProductCategory.Food` → `ProductCategory.DairyAndEggs`:

```csharp
        var classification = new ProductClassification(
            ProductCategory.DairyAndEggs, ExpiryProfile.Create(handling, shelfLifeDays).Value);
```

- [ ] **Step 5: Fix `ClassifyProductJobTests.cs`**

Change the `AiResult` helper (lines 46-47) `ProductCategory.Food` → `ProductCategory.DairyAndEggs`:

```csharp
        private static Result<ProductClassification> AiResult(int days) =>
            Result.Ok(new ProductClassification(
                ProductCategory.DairyAndEggs, ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, days).Value));
```

Update the assertion in `Run_NewName_ClassifiesAndInserts` (line 63):

```csharp
            Assert.Equal(ProductCategory.DairyAndEggs, product.ClassificationProductCategory);
```

(The `Run_StaleVersion_ReclassifiesAndUpdates` test uses `ProductCategory.Other`, which survives — leave it unchanged. It already proves a version-1 product is re-classified under a version-2 classifier, which is exactly the rollout trigger this spec relies on.)

- [ ] **Step 6: Fix `StubItemClassifier.cs`**

Replace the whole class body so the deterministic stub emits surviving categories. The catch-all becomes `Pantry`:

```csharp
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;

namespace Frigorino.IntegrationTests.Infrastructure;

// Deterministic, network-free classifier for integration tests:
//   "milk"/"milch"      → DairyAndEggs, AI-recommended 7-day shelf life
//   "soap"/"spülmittel" → HouseholdAndCleaning, non-perishable
//   "call"/"anruf"      → Other, non-perishable (a task, not a stockable product)
//   everything else     → Pantry, non-perishable
public sealed class StubItemClassifier : IItemClassifier
{
    public int Version => 1;

    public Task<Result<ProductClassification>> ClassifyAsync(string normalizedName, CancellationToken ct)
    {
        ProductClassification result;
        if (normalizedName.Contains("milk") || normalizedName.Contains("milch"))
        {
            result = new ProductClassification(
                ProductCategory.DairyAndEggs, ExpiryProfile.Create(ExpiryHandling.AiRecommendsShelfLife, 7).Value);
        }
        else if (normalizedName.Contains("soap") || normalizedName.Contains("spülmittel"))
        {
            result = new ProductClassification(ProductCategory.HouseholdAndCleaning, ExpiryProfile.NonPerishable);
        }
        else if (normalizedName.Contains("call") || normalizedName.Contains("anruf"))
        {
            result = new ProductClassification(ProductCategory.Other, ExpiryProfile.NonPerishable);
        }
        else
        {
            result = new ProductClassification(ProductCategory.Pantry, ExpiryProfile.NonPerishable);
        }

        return Task.FromResult(Result.Ok(result));
    }
}
```

- [ ] **Step 7: Fix the two `.feature` files to match the stub's new categories**

In `Application/Frigorino.IntegrationTests/Slices/Lists/Classification.Api.feature`:
- Line 10: `And the product "milk" is categorized as "Food"` → `... categorized as "DairyAndEggs"`
- Line 16: `And the product "salt" is categorized as "Food"` → `... categorized as "Pantry"`
- Line 21 (`"call dentist" ... "Other"`) is unchanged.

In `Application/Frigorino.IntegrationTests/Slices/Lists/Extraction.Api.feature`:
- Line 15: `And the product "milk" is categorized as "Food"` → `... categorized as "DairyAndEggs"`
- Line 20 (`"call dentist" ... "Other"`) is unchanged.

- [ ] **Step 8: Rebuild — expect a clean compile**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS (0 errors). If any `CS0117` remains, fix that line with the mapping in the File Structure section and rebuild.

- [ ] **Step 9: Run the unit-test project**

Run: `dotnet test Application/Frigorino.Test`
Expected: PASS (all green). These cover the domain/aggregate/job fixtures just edited.

- [ ] **Step 10: Commit**

```bash
git add Application/Frigorino.Domain/Products/ProductCategory.cs \
        Application/Frigorino.Test/Domain/ProductAggregateTests.cs \
        Application/Frigorino.Test/Features/PromoteSuggestionTests.cs \
        Application/Frigorino.Test/Infrastructure/ClassifyProductJobTests.cs \
        Application/Frigorino.IntegrationTests/Infrastructure/StubItemClassifier.cs \
        Application/Frigorino.IntegrationTests/Slices/Lists/Classification.Api.feature \
        Application/Frigorino.IntegrationTests/Slices/Lists/Extraction.Api.feature
git commit -m "feat(domain): replace ProductCategory with 23-aisle taxonomy"
```

---

## Task 2: Classifier prompt, version bump, and failure fallback

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs:17` (Version), `:54-58` (prompt block), `:107-114` and `:128-131` (fallbacks)

There is no unit test here: the OpenAI `ChatClient` is a concrete SDK type that is not faked anywhere in the codebase (there is no `OpenAiItemClassifierTests`), so this is a prompt/constant change verified by build + the existing job/gap tests staying green. The strict-output schema needs no edit — it derives its `productCategory.enum` from the enum names changed in Task 1.

- [ ] **Step 1: Bump the version (line 17)**

```csharp
        public int Version => 2;
```

- [ ] **Step 2: Rewrite the `productCategory` block of the system prompt**

Replace the five `productCategory` lines (currently lines 54-58: the `"Set 'productCategory' to exactly one of:\n"` header and the four `Unknown`/`Food`/`HouseholdSupply`/`Other` bullets) with the 25-value block below. **Leave everything else untouched** — the intro line, the `reasoning` instruction, the entire `expiryHandling` block, the `"For Other items, use Unknown..."` line, and the closing `"Respond only via the provided JSON schema."` line all stay exactly as they are.

```csharp
            "Set 'productCategory' to exactly one supermarket aisle — pick the single aisle where the item is normally shopped/stored:\n" +
            "- Unknown: you cannot classify it (nonsense text, placeholders, invalid characters, emotes).\n" +
            "- Other: a recognized thing that is NOT a stocked grocery/household good (e.g. a task like 'call dentist'/'Zahnarzt anrufen', or a one-off object).\n" +
            "- Produce: fresh fruit & vegetables (e.g. bananas/Bananen, lettuce/Salat, apples/Äpfel).\n" +
            "- Bakery: bread & baked goods (e.g. bread/Brot, rolls/Brötchen, croissants).\n" +
            "- Meat: fresh or packaged meat & poultry (e.g. chicken/Hähnchen, mince/Hackfleisch).\n" +
            "- Fish: fresh or packaged fish & seafood (e.g. salmon/Lachs, shrimp/Garnelen).\n" +
            "- DairyAndEggs: milk, butter, yogurt, eggs (e.g. milk/Milch, yogurt/Joghurt, eggs/Eier).\n" +
            "- Cheese: cheese of any kind (e.g. Gouda, cream cheese/Frischkäse, Parmesan).\n" +
            "- DeliAndColdCuts: chilled sausage, sliced cold cuts, deli counter (e.g. ham/Schinken, salami/Salami, Aufschnitt).\n" +
            "- Frozen: frozen foods (e.g. frozen pizza/Tiefkühlpizza, ice cream/Eis, frozen vegetables/TK-Gemüse).\n" +
            "- Pantry: dry shelf-stable staples (e.g. pasta/Nudeln, rice/Reis, flour/Mehl, sugar/Zucker).\n" +
            "- CannedGoods: canned or jarred goods (e.g. canned tomatoes/Dosentomaten, beans/Bohnen, corn/Mais).\n" +
            "- Sauces: sauces, dressings & pastes (e.g. ketchup/Ketchup, pasta sauce/Passata, mayonnaise/Mayonnaise, pesto/Pesto).\n" +
            "- OilsAndVinegar: cooking oils & vinegar (e.g. olive oil/Olivenöl, sunflower oil/Sonnenblumenöl, vinegar/Essig).\n" +
            "- Spices: spices, salt & baking aids (e.g. salt/Salz, pepper/Pfeffer, cinnamon/Zimt, baking powder/Backpulver).\n" +
            "- Cereal: breakfast cereal & oats (e.g. muesli/Müsli, oats/Haferflocken, cornflakes/Cornflakes).\n" +
            "- Spreads: sweet or savoury spreads (e.g. jam/Marmelade, Nutella, honey/Honig, peanut butter/Erdnussbutter).\n" +
            "- Snacks: savoury snacks (e.g. chips/Chips, pretzels/Brezeln, crackers/Cracker, nuts/Nüsse).\n" +
            "- Sweets: confectionery (e.g. chocolate/Schokolade, gummy bears/Gummibärchen, candy/Bonbons).\n" +
            "- Beverages: non-alcoholic drinks (e.g. water/Wasser, juice/Saft, soda/Limonade, coffee/Kaffee, tea/Tee).\n" +
            "- Alcohol: alcoholic drinks (e.g. beer/Bier, wine/Wein, spirits/Schnaps).\n" +
            "- HouseholdAndCleaning: cleaning & household supplies (e.g. dish soap/Spülmittel, paper towels/Küchenrolle, batteries/Batterien).\n" +
            "- HealthAndBeauty: personal care, health & cosmetics (e.g. toothpaste/Zahnpasta, shampoo/Shampoo, plasters/Pflaster).\n" +
            "- Baby: baby food & baby care (e.g. baby food/Babybrei, diapers/Windeln, baby wipes/Feuchttücher).\n" +
            "- Pet: pet supplies & pet food (e.g. dog food/Hundefutter, cat litter/Katzenstreu).\n" +
```

- [ ] **Step 3: Switch the two failure fallbacks from `Other` to `Unknown`**

There are two fallback sites returning `ProductCategory.Other`. A failed classification means "could not classify" = `Unknown`, not "recognized non-shoppable" = `Other`.

First site — empty/refused content (currently lines ~111-114). Update the log message and the return value:

```csharp
                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning(
                        "Classifier returned no usable content for '{Name}'; defaulting to Unknown/non-perishable.",
                        normalizedName);
                    return Result.Ok(new ProductClassification(ProductCategory.Unknown, ExpiryProfile.NonPerishable));
                }
```

Second site — schema-valid-but-semantically-inconsistent (currently lines ~128-132):

```csharp
                if (dto is null || profile.IsFailed)
                {
                    // Model produced a schema-valid-but-semantically-inconsistent combination; be safe.
                    return Result.Ok(new ProductClassification(ProductCategory.Unknown, ExpiryProfile.NonPerishable));
                }
```

- [ ] **Step 4: Build**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS (0 errors).

- [ ] **Step 5: Run the unit-test project (must still be green)**

Run: `dotnet test Application/Frigorino.Test`
Expected: PASS. Nothing here asserts the real classifier's `Version` or fallback, so all tests stay green.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs
git commit -m "feat(infra): aisle classifier prompt, version 2, Unknown fallback"
```

---

## Task 3: Full-solution verification

Per repo convention, the final gate runs the whole solution (unit + integration) and a Docker build. Integration tests use Postgres Testcontainers and need the Docker daemon.

**Files:** none (verification only).

- [ ] **Step 1: Confirm Docker is running**

Run: `docker info`
Expected: prints server info. If it errors with the daemon unreachable, stop and ask the user to start Docker Desktop (do not skip the integration run).

- [ ] **Step 2: Run the full solution test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS. Includes `Frigorino.Test` and `Frigorino.IntegrationTests`. The classification/extraction `.feature` scenarios now assert `DairyAndEggs` / `Pantry` / `Other` via the updated stub.

- [ ] **Step 3: Confirm no spurious OpenAPI / client drift**

Run: `git status --short`
Expected: clean (no changes). Category is in no DTO, so building the web project emits no `openapi.json` diff and there is nothing to regenerate. If `ClientApp/src/lib/openapi.json` shows a diff, investigate before proceeding — it should not change.

- [ ] **Step 4: Docker build (catches Dockerfile / publish drift)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: PASS. No project was added or removed, so this is a regression guard only.

---

## Self-Review notes (already reconciled)

- **Spec §1 (enum):** Task 1 Step 1 — 23 aisles + `Unknown`/`Other`, values `0..24`. ✓
- **Spec §2 (prompt + version + fallback):** Task 2 — prompt block (all 25 values), `Version => 2`, both fallbacks → `Unknown`. ✓
- **Spec §3 (rollout, no new code):** verified by `ClassifyProductJobTests.Run_StaleVersion_*` (kept) + `ProductClassificationGapsTests` (already version-2, unchanged). No code authored. ✓
- **Spec §5 (no migration / API / frontend):** no `ProductConfiguration` or migration edit; Task 3 Step 3 asserts no `openapi.json` drift. ✓
- **Spec §6 (testing):** fixtures (Task 1 Steps 3-5), stub + features (Steps 6-7), full-suite gate (Task 3). The "fallback now yields Unknown" check is by inspection — no `ChatClient` fake exists to drive it; flagged in Task 2. ✓
- **Type/name consistency:** every replacement uses the same mapping (`Food→DairyAndEggs`, `HouseholdSupply→HouseholdAndCleaning`, stub catch-all `→Pantry`, `Other` retained) across unit fixtures, the stub, and the `.feature` strings. ✓

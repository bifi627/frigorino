# Hybrid deterministic-then-LLM quantity extraction — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Parse unambiguous quantities (and skip obvious junk like URLs) deterministically and synchronously on item create/update, reserving the LLM for genuinely ambiguous input.

**Architecture:** A pure-domain router (`ItemTextRouter.Analyze`) classifies each item's text into one of four routes (SkipAi / Resolved / NeedsExtraction / ClassifyOnly). The create/update slices run it before `SaveChanges` — writing a deterministically-resolved quantity + clean name in the same DB write — then hand the analysis to a shrunken trigger (`OnItemRouted`) for async fan-out (enqueue LLM / classify / nothing). The router and parser are pure and unit-tested with no mocks; side-effects stay in Infrastructure, honouring the ArchUnitNET layer rules.

**Tech Stack:** .NET 10, FluentResults, EF Core, xUnit + FakeItEasy, `System.Text.RegularExpressions`.

**Spec:** `docs/superpowers/specs/2026-05-31-hybrid-quantity-extraction-design.md`

---

## File Structure

**Domain (`Frigorino.Domain`)**
- Modify `Quantities/Quantity.cs` — add pure `TryParse`.
- Create `Quantities/ItemTextRouter.cs` — `ItemTextRoute` enum, `ItemTextAnalysis` struct, `ItemTextRouter.Analyze`.
- Modify `Entities/List.cs` — `AddItem` gains optional `Quantity? quantity = null`.
- Modify `Interfaces/IQuantityExtractionTrigger.cs` — `OnItemEntered` → `OnItemRouted(..., ItemTextAnalysis)`.

**Infrastructure (`Frigorino.Infrastructure`)**
- Modify `Services/QuantityExtractionTriggers.cs` — both impls implement `OnItemRouted`, mapping route → side-effect.

**Features (`Frigorino.Features`)**
- Modify `Lists/Items/CreateItem.cs` — `Analyze` → `AddItem(cleanName, quantity)` → `OnItemRouted`.
- Modify `Lists/Items/UpdateItem.cs` — pre-analysis on the text-changed/no-explicit-quantity branch.

**Tests (`Frigorino.Test`)**
- Create `Domain/QuantityTryParseTests.cs`.
- Create `Domain/ItemTextRouterTests.cs`.
- Modify `Domain/ListAggregateTests.cs` — AddItem-with-quantity.
- Modify `Infrastructure/QuantityExtractionTriggerTests.cs` — rewrite for `OnItemRouted`.

> **Namespace note:** `ItemTextRoute`, `ItemTextAnalysis`, and `ItemTextRouter` live in `Frigorino.Domain.Quantities` alongside `Quantity` / `QuantityExtraction` — the cohesive set. `IQuantityExtractionTrigger` (in `Frigorino.Domain.Interfaces`) adds `using Frigorino.Domain.Quantities;`.

---

## Task 1: `Quantity.TryParse` (deterministic parser)

**Files:**
- Test: `Application/Frigorino.Test/Domain/QuantityTryParseTests.cs` (create)
- Modify: `Application/Frigorino.Domain/Quantities/Quantity.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Domain/QuantityTryParseTests.cs`:

```csharp
using Frigorino.Domain.Quantities;

namespace Frigorino.Test.Domain
{
    public class QuantityTryParseTests
    {
        // xUnit InlineData cannot carry decimal literals — compare via double cast.
        [Theory]
        [InlineData("2kg flour", "flour", 2.0, QuantityUnit.Kilogram)]
        [InlineData("500 ml milk", "milk", 500.0, QuantityUnit.Milliliter)]
        [InlineData("1,5 l juice", "juice", 1.5, QuantityUnit.Liter)]
        [InlineData("2 l milk", "milk", 2.0, QuantityUnit.Liter)]
        [InlineData("500g Mehl", "Mehl", 500.0, QuantityUnit.Gram)]
        [InlineData("1.5 kg flour", "flour", 1.5, QuantityUnit.Kilogram)]
        [InlineData("flour 2kg", "flour", 2.0, QuantityUnit.Kilogram)]
        [InlineData("milk 500ml", "milk", 500.0, QuantityUnit.Milliliter)]
        [InlineData("3 milk", "milk", 3.0, QuantityUnit.Piece)]
        [InlineData("2 lemons", "lemons", 2.0, QuantityUnit.Piece)]
        public void TryParse_ConfidentShapes_Resolves(
            string input, string expectedName, double expectedValue, QuantityUnit expectedUnit)
        {
            var ok = Quantity.TryParse(input, out var cleanName, out var quantity);

            Assert.True(ok);
            Assert.Equal(expectedName, cleanName);
            Assert.Equal((decimal)expectedValue, quantity.Value);
            Assert.Equal(expectedUnit, quantity.Unit);
        }

        [Theory]
        [InlineData("7up")]            // brand-digit glued to letters
        [InlineData("WD-40")]          // digit glued via hyphen
        [InlineData("E45 cream")]      // leading letter + glued digit
        [InlineData("Coca Cola 2")]    // trailing bare integer
        [InlineData("milk 2")]         // trailing bare integer
        [InlineData("1,5 milk")]       // bare decimal count (ambiguous)
        [InlineData("2kg")]            // number + unit, no product
        [InlineData("2 kg")]           // number + bare unit token, no product
        [InlineData("milk")]           // no digit at all
        [InlineData("")]               // empty
        public void TryParse_AmbiguousOrEmpty_ReturnsFalse(string input)
        {
            var ok = Quantity.TryParse(input, out _, out _);

            Assert.False(ok);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QuantityTryParseTests"`
Expected: FAIL — `Quantity` does not contain a definition for `TryParse`.

- [ ] **Step 3: Implement `TryParse`**

In `Application/Frigorino.Domain/Quantities/Quantity.cs`, add the `using` directives at the top and the method inside the `Quantity` struct (after `Create`):

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
```

```csharp
        // Deterministic, conservative parse of the unambiguous quantity shapes (Option A): a number
        // is a quantity ONLY when it is a standalone token glued to / followed by a known metric
        // unit, or the leading bare integer count. Everything else (brand-digits like "7up"/"WD-40",
        // trailing bare integers, mid-string, container words) returns false and is left to the LLM.
        // Patterns are tried in order; first confident match wins.
        private static readonly Regex LeadingUnit = new(
            @"^\s*(?<num>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|ml|l)\b\s*(?<name>.+?)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TrailingUnit = new(
            @"^\s*(?<name>.+?)\s+(?<num>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|ml|l)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LeadingBare = new(
            @"^\s*(?<num>\d+)\s+(?<name>.+?)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UnitOnly = new(
            @"^(kg|g|ml|l)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryParse(string text, out string cleanName, out Quantity quantity)
        {
            cleanName = string.Empty;
            quantity = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            foreach (var (regex, isBare) in new[]
                     {
                         (LeadingUnit, false),
                         (TrailingUnit, false),
                         (LeadingBare, true),
                     })
            {
                var match = regex.Match(text);
                if (!match.Success)
                {
                    continue;
                }

                var name = match.Groups["name"].Value.Trim();
                // Number-only-no-product: "2kg" (empty name) or "2 kg" (name is just a unit token).
                if (name.Length == 0 || (isBare && UnitOnly.IsMatch(name)))
                {
                    continue;
                }

                var numText = match.Groups["num"].Value.Replace(',', '.');
                if (!decimal.TryParse(numText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                var unit = isBare ? QuantityUnit.Piece : MapUnit(match.Groups["unit"].Value);
                var created = Quantity.Create(value, unit);
                if (created.IsFailed)
                {
                    continue;
                }

                cleanName = name;
                quantity = created.Value;
                return true;
            }

            return false;
        }

        private static QuantityUnit MapUnit(string unit) => unit.ToLowerInvariant() switch
        {
            "g" => QuantityUnit.Gram,
            "kg" => QuantityUnit.Kilogram,
            "ml" => QuantityUnit.Milliliter,
            "l" => QuantityUnit.Liter,
            _ => QuantityUnit.Piece,
        };
```

> Note: `Quantity.cs` already has `using FluentResults;`. If the compiler flags a duplicate, keep the existing one — only add `System.Globalization` and `System.Text.RegularExpressions`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QuantityTryParseTests"`
Expected: PASS (all theory rows green).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Quantities/Quantity.cs Application/Frigorino.Test/Domain/QuantityTryParseTests.cs
git commit -m "feat: deterministic Quantity.TryParse for unambiguous shapes"
```

---

## Task 2: `ItemTextRouter` (guard chain + disposition)

**Files:**
- Test: `Application/Frigorino.Test/Domain/ItemTextRouterTests.cs` (create)
- Create: `Application/Frigorino.Domain/Quantities/ItemTextRouter.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Domain/ItemTextRouterTests.cs`:

```csharp
using Frigorino.Domain.Quantities;

namespace Frigorino.Test.Domain
{
    public class ItemTextRouterTests
    {
        [Theory]
        [InlineData("https://example.com/recipe")]
        [InlineData("check www.shop.com later")]
        [InlineData("   ")]                         // empty after trim
        [InlineData("!!! ??? ...")]                 // punctuation-only
        public void Analyze_Junk_SkipsAi(string input)
        {
            var result = ItemTextRouter.Analyze(input);

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
            Assert.Null(result.Quantity);
        }

        [Fact]
        public void Analyze_TooLong_SkipsAi()
        {
            var longText = new string('x', 121);

            var result = ItemTextRouter.Analyze(longText);

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
        }

        [Fact]
        public void Analyze_TooManyWords_SkipsAi()
        {
            var manyWords = string.Join(' ', Enumerable.Repeat("buy", 16));

            var result = ItemTextRouter.Analyze(manyWords);

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
        }

        [Fact]
        public void Analyze_UrlWithDigits_SkipsBeforeDigitGate()
        {
            var result = ItemTextRouter.Analyze("https://shop.com/item/123");

            Assert.Equal(ItemTextRoute.SkipAi, result.Route);
        }

        [Fact]
        public void Analyze_ConfidentQuantity_Resolves()
        {
            var result = ItemTextRouter.Analyze("2kg flour");

            Assert.Equal(ItemTextRoute.Resolved, result.Route);
            Assert.Equal("flour", result.CleanName);
            Assert.NotNull(result.Quantity);
            Assert.Equal(2m, result.Quantity!.Value.Value);
            Assert.Equal(QuantityUnit.Kilogram, result.Quantity!.Value.Unit);
        }

        [Fact]
        public void Analyze_DigitNoConfidentParse_NeedsExtraction()
        {
            var result = ItemTextRouter.Analyze("7up");

            Assert.Equal(ItemTextRoute.NeedsExtraction, result.Route);
            Assert.Equal("7up", result.CleanName);
            Assert.Null(result.Quantity);
        }

        [Fact]
        public void Analyze_NoDigit_ClassifyOnly()
        {
            var result = ItemTextRouter.Analyze("milk");

            Assert.Equal(ItemTextRoute.ClassifyOnly, result.Route);
            Assert.Equal("milk", result.CleanName);
            Assert.Null(result.Quantity);
        }

        [Fact]
        public void Analyze_NonResolvedRoute_KeepsRawTextAsCleanName()
        {
            // The trigger keys off CleanName for every route; non-Resolved must echo raw text.
            // ("milk 2" is a trailing bare integer that TryParse rejects — leading bare integers
            // like "20 apples..." would instead Resolve, so they must not be used here.)
            var result = ItemTextRouter.Analyze("milk 2");

            Assert.Equal(ItemTextRoute.NeedsExtraction, result.Route);
            Assert.Equal("milk 2", result.CleanName);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ItemTextRouterTests"`
Expected: FAIL — `ItemTextRouter` / `ItemTextRoute` / `ItemTextAnalysis` do not exist.

- [ ] **Step 3: Implement the router**

Create `Application/Frigorino.Domain/Quantities/ItemTextRouter.cs`:

```csharp
using System.Linq;
using System.Text.RegularExpressions;

namespace Frigorino.Domain.Quantities
{
    // What to do with a new/edited list-item's text. SkipAi/NeedsExtraction/ClassifyOnly carry the
    // raw text as CleanName; only Resolved carries the deterministically stripped name + quantity.
    public enum ItemTextRoute
    {
        SkipAi,
        Resolved,
        NeedsExtraction,
        ClassifyOnly,
    }

    public readonly record struct ItemTextAnalysis(
        ItemTextRoute Route,
        string CleanName,
        Quantity? Quantity);

    // Pure front-door triage for the quantity/classification pipeline. Free to run, so the slices
    // call it unconditionally (extraction enabled or not). Guards are evaluated in priority order:
    //   0. skip junk (URL / empty / punctuation-only / over the length ceiling) — terminal,
    //   A. deterministic facet extraction (today: Quantity.TryParse),
    //   B. disposition of the ambiguous remainder (digit -> LLM, else classify).
    public static class ItemTextRouter
    {
        // Generous ceilings: well above any real product name, well below the 500-char ListItem.Text
        // cap — guards obvious nonsense (e.g. a 300-char paste) without dropping legit multi-word items.
        private const int MaxProductChars = 120;
        private const int MaxProductWords = 15;

        private static readonly Regex Url = new(@"https?://|www\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Digit = new(@"\d", RegexOptions.Compiled);
        private static readonly char[] WordSeparators = { ' ', '\t', '\n', '\r' };

        public static ItemTextAnalysis Analyze(string rawText)
        {
            var text = (rawText ?? string.Empty).Trim();

            // Phase 0: skip guards (terminal).
            if (text.Length == 0
                || !text.Any(char.IsLetterOrDigit)
                || Url.IsMatch(text)
                || text.Length > MaxProductChars
                || text.Split(WordSeparators, System.StringSplitOptions.RemoveEmptyEntries).Length > MaxProductWords)
            {
                return new ItemTextAnalysis(ItemTextRoute.SkipAi, rawText ?? string.Empty, null);
            }

            // Phase A: deterministic facet extraction (quantity is the only facet today).
            if (Quantity.TryParse(rawText!, out var cleanName, out var quantity))
            {
                return new ItemTextAnalysis(ItemTextRoute.Resolved, cleanName, quantity);
            }

            // Phase B: disposition of the ambiguous remainder.
            return Digit.IsMatch(rawText!)
                ? new ItemTextAnalysis(ItemTextRoute.NeedsExtraction, rawText!, null)
                : new ItemTextAnalysis(ItemTextRoute.ClassifyOnly, rawText!, null);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ItemTextRouterTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Quantities/ItemTextRouter.cs Application/Frigorino.Test/Domain/ItemTextRouterTests.cs
git commit -m "feat: ItemTextRouter guard chain (skip/resolve/extract/classify)"
```

---

## Task 3: `List.AddItem` accepts an optional quantity

**Files:**
- Test: `Application/Frigorino.Test/Domain/ListAggregateTests.cs` (modify — add test)
- Modify: `Application/Frigorino.Domain/Entities/List.cs:138-161`

- [ ] **Step 1: Write the failing test**

Append this test to the `ListAggregateTests` class in `Application/Frigorino.Test/Domain/ListAggregateTests.cs` (keep existing tests intact; uses the existing `HouseholdId` const and `List.Create` pattern already present in that file):

```csharp
        [Fact]
        public void AddItem_WithQuantity_SetsBothQuantityColumns()
        {
            var list = List.Create("Groceries", null, HouseholdId, "user-1").Value;
            var quantity = Quantity.Create(2m, QuantityUnit.Kilogram).Value;

            var result = list.AddItem("flour", quantity);

            Assert.True(result.IsSuccess);
            Assert.Equal("flour", result.Value.Text);
            Assert.Equal(2m, result.Value.QuantityValue);
            Assert.Equal(QuantityUnit.Kilogram, result.Value.QuantityUnit);
        }

        [Fact]
        public void AddItem_WithoutQuantity_LeavesQuantityNull()
        {
            var list = List.Create("Groceries", null, HouseholdId, "user-1").Value;

            var result = list.AddItem("milk");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.QuantityValue);
            Assert.Null(result.Value.QuantityUnit);
        }
```

> If `ListAggregateTests.cs` does not already have `using Frigorino.Domain.Quantities;`, add it to the file's using block.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateTests.AddItem_WithQuantity"`
Expected: FAIL — `AddItem` does not take 2 arguments.

- [ ] **Step 3: Implement the optional parameter**

In `Application/Frigorino.Domain/Entities/List.cs`, change the `AddItem` signature and the `QuantityValue` / `QuantityUnit` initializers:

```csharp
        public Result<ListItem> AddItem(string text, Quantity? quantity = null)
        {
            var errors = ValidateItemText(text, requireText: true);
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new ListItem
            {
                ListId = Id,
                Text = text.Trim(),
                QuantityValue = quantity?.Value,
                QuantityUnit = quantity?.Unit,
                Status = false,
                SortOrder = ComputeAppendSortOrder(targetStatus: false),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            ListItems.Add(item);
            return Result.Ok(item);
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateTests.AddItem"`
Expected: PASS (both new tests + the pre-existing `AddItem` tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/List.cs Application/Frigorino.Test/Domain/ListAggregateTests.cs
git commit -m "feat: List.AddItem accepts an optional resolved quantity"
```

---

## Task 4: Trigger `OnItemRouted` (interface + both impls)

**Files:**
- Modify: `Application/Frigorino.Domain/Interfaces/IQuantityExtractionTrigger.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/QuantityExtractionTriggers.cs`
- Test: `Application/Frigorino.Test/Infrastructure/QuantityExtractionTriggerTests.cs` (rewrite)

- [ ] **Step 1: Write the failing tests**

Replace the entire body of `Application/Frigorino.Test/Infrastructure/QuantityExtractionTriggerTests.cs` with:

```csharp
using FakeItEasy;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class QuantityExtractionTriggerTests
    {
        private static ItemTextAnalysis Resolved(string name) =>
            new(ItemTextRoute.Resolved, name, Quantity.Create(2m, QuantityUnit.Kilogram).Value);

        [Fact]
        public void Queueing_NeedsExtraction_EnqueuesAndDoesNotClassifyDirectly()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.NeedsExtraction, "20 apples", null));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [Fact]
        public void Queueing_Resolved_ClassifiesCleanNameAndDoesNotEnqueue()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemRouted(42, 7, 100, Resolved("flour"));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
            A.CallTo(() => classification.OnProductReferenced(42, "flour")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Queueing_ClassifyOnly_ClassifiesRawAndDoesNotEnqueue()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.ClassifyOnly, "milk", null));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
            A.CallTo(() => classification.OnProductReferenced(42, "milk")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Queueing_SkipAi_DoesNothing()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.SkipAi, "https://x.com", null));

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [Theory]
        [InlineData(ItemTextRoute.NeedsExtraction, "20 apples")] // extraction off -> classify raw instead
        [InlineData(ItemTextRoute.ClassifyOnly, "milk")]
        public void Null_NonSkipRoutes_ClassifyCleanName(ItemTextRoute route, string cleanName)
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(route, cleanName, null));

            A.CallTo(() => classification.OnProductReferenced(42, cleanName)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Null_Resolved_ClassifiesCleanName()
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemRouted(42, 7, 100, Resolved("flour"));

            A.CallTo(() => classification.OnProductReferenced(42, "flour")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Null_SkipAi_DoesNothing()
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemRouted(42, 7, 100, new ItemTextAnalysis(ItemTextRoute.SkipAi, "https://x.com", null));

            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QuantityExtractionTriggerTests"`
Expected: FAIL — `IQuantityExtractionTrigger` has no `OnItemRouted`.

- [ ] **Step 3: Update the interface**

Replace `Application/Frigorino.Domain/Interfaces/IQuantityExtractionTrigger.cs` with:

```csharp
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Interfaces
{
    // Single front door the list-item slices call after computing an ItemTextAnalysis. The enabled
    // implementation enqueues the extract job for NeedsExtraction (chaining to classification on the
    // clean name) and classifies directly for Resolved/ClassifyOnly; the disabled implementation
    // classifies the clean name for every non-skip route. SkipAi does nothing on either.
    public interface IQuantityExtractionTrigger
    {
        void OnItemRouted(int householdId, int listId, int itemId, ItemTextAnalysis analysis);
    }
}
```

- [ ] **Step 4: Update both trigger implementations**

Replace `Application/Frigorino.Infrastructure/Services/QuantityExtractionTriggers.cs` with:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    // Enabled path. Resolved/ClassifyOnly classify the clean name directly (no LLM). NeedsExtraction
    // enqueues the extract job, which re-extracts from the raw text and chains classification on its
    // own clean name. SkipAi (URL/junk) does nothing — no extraction, no classification.
    public class QueueingQuantityExtractionTrigger : IQuantityExtractionTrigger
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly IProductClassificationTrigger _classificationTrigger;

        public QueueingQuantityExtractionTrigger(
            IBackgroundTaskQueue queue, IProductClassificationTrigger classificationTrigger)
        {
            _queue = queue;
            _classificationTrigger = classificationTrigger;
        }

        public void OnItemRouted(int householdId, int listId, int itemId, ItemTextAnalysis analysis)
        {
            switch (analysis.Route)
            {
                case ItemTextRoute.NeedsExtraction:
                    _queue.TryEnqueue((sp, ct) =>
                        sp.GetRequiredService<IExtractQuantityJob>()
                          .Run(householdId, listId, itemId, analysis.CleanName, ct));
                    break;
                case ItemTextRoute.Resolved:
                case ItemTextRoute.ClassifyOnly:
                    _classificationTrigger.OnProductReferenced(householdId, analysis.CleanName);
                    break;
                case ItemTextRoute.SkipAi:
                default:
                    break;
            }
        }
    }

    // Disabled path: extraction is off. Every non-skip route classifies the clean name (for
    // NeedsExtraction the clean name equals the raw text — nothing was stripped). SkipAi does nothing.
    public class NullQuantityExtractionTrigger : IQuantityExtractionTrigger
    {
        private readonly IProductClassificationTrigger _classificationTrigger;

        public NullQuantityExtractionTrigger(IProductClassificationTrigger classificationTrigger)
        {
            _classificationTrigger = classificationTrigger;
        }

        public void OnItemRouted(int householdId, int listId, int itemId, ItemTextAnalysis analysis)
        {
            if (analysis.Route != ItemTextRoute.SkipAi)
            {
                _classificationTrigger.OnProductReferenced(householdId, analysis.CleanName);
            }
        }
    }
}
```

- [ ] **Step 5: Do NOT commit or test in isolation yet**

`Frigorino.Test` references `Frigorino.Features`, so the Test project cannot build until the slices (Tasks 5–6) are updated to call `OnItemRouted`. The interface rename in this task breaks `Frigorino.Features` compilation by design. **Proceed directly to Tasks 5 and 6, then run the tests and commit Tasks 4 + 5 + 6 as one compiling change (Task 6, Steps 4–5).** Do not leave a broken intermediate commit.

---

## Task 5: `CreateItem` slice wiring

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/CreateItem.cs`

- [ ] **Step 1: Update the handler**

In `Application/Frigorino.Features/Lists/Items/CreateItem.cs`, add `using Frigorino.Domain.Quantities;` to the using block, then replace the body from `var result = list.AddItem(...)` through the `OnItemEntered` call:

```csharp
            var analysis = ItemTextRouter.Analyze(request.Text);

            var result = list.AddItem(analysis.CleanName, analysis.Quantity);
            if (result.IsFailed)
            {
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            quantityTrigger.OnItemRouted(householdId, listId, result.Value.Id, analysis);
```

> Why this is correct: for every non-Resolved route `analysis.CleanName == request.Text` and `analysis.Quantity == null`, so `AddItem` behaves exactly as before; only the Resolved route writes the stripped name + quantity. The `201` response (built via `ListItemResponse.From`) now carries the resolved quantity directly. An empty/whitespace `request.Text` still fails `AddItem` validation (→ `ValidationProblem`) before the trigger runs, unchanged.

- [ ] **Step 2: Build the solution**

Run: `dotnet build Application/Frigorino.sln`
Expected: succeeds (once Task 6 is also applied — both slices reference the renamed trigger method). If building after only Task 5, `UpdateItem.cs` will still reference `OnItemEntered` and fail; apply Task 6 before building.

- [ ] **Step 3: Commit (together with Task 6)** — see Task 6.

---

## Task 6: `UpdateItem` slice wiring

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/UpdateItem.cs:53-84`

- [ ] **Step 1: Update the handler**

In `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`, replace the block from `Quantity? quantity = null;` (line 53) through the closing of the `OnItemEntered` `if` (line 84) with:

```csharp
            Quantity? quantity = null;
            if (request.Quantity is not null)
            {
                var parsed = Quantity.Create(request.Quantity.Value, request.Quantity.Unit);
                if (parsed.IsFailed)
                {
                    return parsed.ToValidationProblem();
                }
                quantity = parsed.Value;
            }

            // Run the deterministic router ONLY on a text change with no explicit quantity intent —
            // same condition as the legacy re-extraction guard (the edit composer always sends a
            // quantity or ClearQuantity, making the user authoritative). On a Resolved parse we write
            // the stripped name + quantity in this same save; SkipAi/NeedsExtraction/ClassifyOnly
            // leave the user's text as-typed and the existing quantity untouched.
            ItemTextAnalysis? analysis =
                request.Text is not null && request.Quantity is null && request.ClearQuantity != true
                    ? ItemTextRouter.Analyze(request.Text)
                    : null;

            var textToWrite = request.Text;
            if (analysis is { Route: ItemTextRoute.Resolved } resolved)
            {
                textToWrite = resolved.CleanName;
                quantity = resolved.Quantity;
            }

            var result = list.UpdateItem(itemId, textToWrite, quantity, request.ClearQuantity ?? false, request.Status);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                return result.ToValidationProblem();
            }

            await db.SaveChangesAsync(ct);

            if (analysis is ItemTextAnalysis routed)
            {
                quantityTrigger.OnItemRouted(householdId, listId, itemId, routed);
            }
```

> Why this is correct: when the router branch is inactive (explicit quantity, clear, or no text change) `analysis` is null → behaviour is identical to today (no extraction, no classification side-effect). When active and `Resolved`, `UpdateItem`'s non-null `quantity` parameter sets both columns and `textToWrite` is the stripped name — one save, no async. For `SkipAi`/`NeedsExtraction`/`ClassifyOnly`, `textToWrite` stays the raw text and `quantity` stays null (preserve), so the existing quantity is left untouched; the route's async side-effect (none / enqueue / classify) is dispatched via `OnItemRouted`.

- [ ] **Step 2: Build the solution**

Run: `dotnet build Application/Frigorino.sln`
Expected: BUILD SUCCEEDED — no remaining references to `OnItemEntered`.

- [ ] **Step 3: Confirm `OnItemEntered` is fully gone**

Run: `git grep -n "OnItemEntered" -- "Application/*.cs"`
Expected: no output (the old method name is removed everywhere except, harmlessly, the prior plan doc under `docs/`).

- [ ] **Step 4: Run the affected unit tests**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QuantityExtractionTriggerTests|FullyQualifiedName~ItemTextRouterTests|FullyQualifiedName~QuantityTryParseTests|FullyQualifiedName~ListAggregateTests"`
Expected: PASS.

- [ ] **Step 5: Commit Tasks 4 + 5 + 6 together**

These three tasks share the interface rename and must land in one compiling commit:

```bash
git add Application/Frigorino.Domain/Interfaces/IQuantityExtractionTrigger.cs \
        Application/Frigorino.Infrastructure/Services/QuantityExtractionTriggers.cs \
        Application/Frigorino.Test/Infrastructure/QuantityExtractionTriggerTests.cs \
        Application/Frigorino.Features/Lists/Items/CreateItem.cs \
        Application/Frigorino.Features/Lists/Items/UpdateItem.cs
git commit -m "feat: route item text through ItemTextRouter in create/update slices"
```

---

## Task 7: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Run the full solution test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: all `Frigorino.Test` + `Frigorino.IntegrationTests` pass. (If the inventory "undo delete in toast" integration test times out intermittently, re-run before suspecting a regression — it is a known flake.)

- [ ] **Step 2: Build the deployment image**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: image builds (no project/Dockerfile drift; SPA + backend compile). If the Docker daemon is unreachable, ask the user to start Docker Desktop rather than skipping.

- [ ] **Step 3: Commit any incidental fixes**

Only if Steps 1–2 surfaced fixes:

```bash
git add -A
git commit -m "fix: address verification findings for hybrid quantity extraction"
```

---

## Notes for the implementer

- **No DB migration.** This feature only changes *when* `QuantityValue` / `QuantityUnit` get written; the columns already exist. Do not add a migration.
- **No frontend change.** The SPA already renders `ListItemResponse.Quantity`; deterministic resolution simply means the value is present in the `201`/`200` response instead of arriving via a later poll.
- **No `npm run api` needed.** The wire contract (`ListItemResponse`, `QuantityDto`) is unchanged — `ItemTextAnalysis` and `ItemTextRoute` are internal domain types, never serialized.
- **`ExtractQuantityJob` is unchanged.** Its tests stay green as-is; the NeedsExtraction path still enqueues it exactly as before.
- **Brace style:** block `{ }` even for single-line conditions (matches the codebase).
```

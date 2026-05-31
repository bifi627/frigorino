# Inline Quantity Extraction (Lists) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `ListItem`'s free-text quantity with a structured `Quantity` value object, and make natural inline typing ("20 apples", "1l milk") the primary entry path — a cheap async LLM extractor strips the quantity into structured columns, then chains into the Cycle 2 classifier on the clean name.

**Architecture:** Two AI pipelines, two models, **chained**: a cheap `IQuantityExtractor` (new) runs first (digit-gated), writes the clean name + quantity onto the `ListItem`, then triggers the existing `IProductClassificationTrigger` on the clean name so the Product-catalog cache stays intact. Both run async on the Cycle 1 `IBackgroundTaskQueue`. The UI renders the raw text optimistically and bounded-polls the existing `GetItem` endpoint until the quantity lands. Lists only.

**Tech Stack:** .NET 10 vertical slices, EF Core (Postgres), FluentResults, OpenAI SDK v2.8.0 strict Structured Outputs, xUnit + FakeItEasy + EF InMemory, Reqnroll + Playwright + Postgres Testcontainers, React 19 + TanStack Query + MUI.

**Spec:** `docs/superpowers/specs/2026-05-30-quantity-inline-extraction-design.md`.

**Decisions locked at planning time (override the spec where they differ):**
- **No `Quantity.TryParse`, no migration backfill** — the migration drops the old free-text column and adds two nullable columns; existing quantities are lost (total data loss, accepted).
- **No `Dimension`/base-factor metadata** on `QuantityUnit` — YAGNI (no conversions, flat unit dropdown).
- **`QuantityUnit` serializes as an integer** on the wire (TS `number`), consistent with `HouseholdRole`/`ProductCategory`/`ExpiryHandling`. There is NO global `JsonStringEnumConverter`; do not add one. The LLM adapter uses a *local* `JsonStringEnumConverter` for its structured output only, exactly like `OpenAiItemClassifier`.
- **Two keyed `ChatClient`s** (`AiKeys.Classifier`, `AiKeys.Extractor`) sharing one API key, per-feature model — config restructured from `Classifier:*` to `Ai:*`.
- **Clearing a quantity is out of scope for v1** (popover sets/changes only; `null` on update still means "preserve").

> ⚠️ **Deploy note (do before/with shipping):** the config key rename `Classifier__*` → `Ai__*` means the Railway env vars on stage/prod must be renamed (`Classifier__ApiKey` → `Ai__ApiKey`, add `Ai__Classifier__Model` / `Ai__QuantityExtractor__Model` / `Ai__QuantityExtractor__Enabled`). Until renamed, classification falls back to disabled. Flag this to the user at finish time.

---

## Build order rationale

Backend tasks (1–7) keep `dotnet test` on the solution **green at every commit**. The TS client is regenerated once (Task 8) after all DTO changes land; the frontend does **not** compile between Task 8 and Task 11 — that is expected, frontend verification happens in Tasks 9–11. Integration test is Task 12; full verification gate is Task 13.

## File map

**New (backend):**
- `Frigorino.Domain/Quantities/QuantityUnit.cs`, `Quantity.cs`, `QuantityExtraction.cs`
- `Frigorino.Domain/Interfaces/IQuantityExtractor.cs`, `IExtractQuantityJob.cs`, `IQuantityExtractionTrigger.cs`
- `Frigorino.Infrastructure/Services/AiKeys.cs`, `OpenAiQuantityExtractor.cs`, `ExtractQuantityJob.cs`, `QuantityExtractionTriggers.cs`, `QuantityExtractionDependencyInjection.cs`
- `Frigorino.Features/Lists/Items/QuantityDto.cs`
- `Frigorino.Test/Domain/QuantityTests.cs`, `Frigorino.Test/Infrastructure/ExtractQuantityJobTests.cs`, `QuantityExtractionTriggerTests.cs`, `Frigorino.Test/Infrastructure/ListItemQuantityPersistenceTests.cs`
- `Frigorino.IntegrationTests/Infrastructure/StubQuantityExtractor.cs`, `Slices/Lists/Extraction.Api.feature`, `ExtractionApiSteps.cs`
- One EF migration via `dotnet ef migrations add`

**Modified (backend):** `ListItem.cs`, `List.cs`, `ListItemConfiguration.cs`, `ListItemResponse.cs`, `CreateItem.cs`, `UpdateItem.cs`, `OpenAiItemClassifier.cs` (keyed-services ctor), `ItemClassificationDependencyInjection.cs`, `appsettings.json`, `Program.cs`, `TestWebApplicationFactory.cs`, `ListAggregateItemTests.cs`.

**New/Modified (frontend):** see Tasks 8–11.

---

### Task 1: `Quantity` value object (domain)

**Files:**
- Create: `Application/Frigorino.Domain/Quantities/QuantityUnit.cs`
- Create: `Application/Frigorino.Domain/Quantities/Quantity.cs`
- Test: `Application/Frigorino.Test/Domain/QuantityTests.cs`

- [ ] **Step 1: Write the failing test**

`Application/Frigorino.Test/Domain/QuantityTests.cs`:

```csharp
using Frigorino.Domain.Quantities;

namespace Frigorino.Test.Domain
{
    public class QuantityTests
    {
        // xUnit InlineData cannot carry decimal literals — pass double and cast.
        [Theory]
        [InlineData(1.0)]
        [InlineData(0.5)]
        [InlineData(500.0)]
        public void Create_PositiveValue_Succeeds(double value)
        {
            var result = Quantity.Create((decimal)value, QuantityUnit.Liter);

            Assert.True(result.IsSuccess);
            Assert.Equal((decimal)value, result.Value.Value);
            Assert.Equal(QuantityUnit.Liter, result.Value.Unit);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Create_NonPositiveValue_FailsKeyedOnValue(double value)
        {
            var result = Quantity.Create((decimal)value, QuantityUnit.Piece);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(Quantity.Value), result.Errors[0].Metadata["Property"]);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QuantityTests"`
Expected: FAIL to compile (`Quantity` / `QuantityUnit` not defined).

- [ ] **Step 3: Write the implementation**

`Application/Frigorino.Domain/Quantities/QuantityUnit.cs`:

```csharp
namespace Frigorino.Domain.Quantities
{
    // Fixed grocery/pantry unit set. Stored as int (EF default) and serialized as int on the
    // wire (matches the existing enum convention; no JsonStringEnumConverter). Piece is the
    // default for a bare count.
    public enum QuantityUnit
    {
        Gram = 0,
        Kilogram = 1,
        Milliliter = 2,
        Liter = 3,
        Piece = 4,
        Pack = 5,
        Can = 6,
        Bottle = 7,
        Bag = 8,
    }
}
```

`Application/Frigorino.Domain/Quantities/Quantity.cs`:

```csharp
using FluentResults;

namespace Frigorino.Domain.Quantities
{
    // Pure domain value object: a quantity on a list item. Persisted as two flat nullable columns
    // on ListItem (QuantityValue + QuantityUnit), not an EF owned type — mirrors ExpiryProfile.
    // Both columns are set together or both null (the "no quantity" state); the List aggregate
    // enforces that invariant.
    public readonly record struct Quantity
    {
        public decimal Value { get; }
        public QuantityUnit Unit { get; }

        private Quantity(decimal value, QuantityUnit unit)
        {
            Value = value;
            Unit = unit;
        }

        // Invariant: Value must be strictly positive (decimal is always finite).
        public static Result<Quantity> Create(decimal value, QuantityUnit unit)
        {
            if (value <= 0)
            {
                return Result.Fail<Quantity>(
                    new Error("Quantity value must be greater than zero.")
                        .WithMetadata("Property", nameof(Value)));
            }

            return Result.Ok(new Quantity(value, unit));
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QuantityTests"`
Expected: PASS (5 cases).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Quantities Application/Frigorino.Test/Domain/QuantityTests.cs
git commit -m "feat: add Quantity value object and QuantityUnit enum"
```

---

### Task 2: AI ports (domain interfaces)

**Files:**
- Create: `Application/Frigorino.Domain/Quantities/QuantityExtraction.cs`
- Create: `Application/Frigorino.Domain/Interfaces/IQuantityExtractor.cs`
- Create: `Application/Frigorino.Domain/Interfaces/IExtractQuantityJob.cs`
- Create: `Application/Frigorino.Domain/Interfaces/IQuantityExtractionTrigger.cs`

No tests (interfaces + record). Verified by build.

- [ ] **Step 1: Create the result record**

`Application/Frigorino.Domain/Quantities/QuantityExtraction.cs`:

```csharp
namespace Frigorino.Domain.Quantities
{
    // Result of inline extraction: the product name with any quantity removed, plus the
    // structured quantity (null when the text carried none).
    public sealed record QuantityExtraction(string CleanName, Quantity? Quantity);
}
```

- [ ] **Step 2: Create the extractor port**

`Application/Frigorino.Domain/Interfaces/IQuantityExtractor.cs`:

```csharp
using FluentResults;
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Interfaces
{
    // The ONLY quantity-extraction AI abstraction. The OpenAI SDK never crosses this boundary.
    // Transient/API errors return Result.Fail (the job drops the work item — lossy by design);
    // a model refusal / no-quantity result is returned as Ok with the raw text as CleanName.
    public interface IQuantityExtractor
    {
        Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct);
    }
}
```

- [ ] **Step 3: Create the job + trigger ports**

`Application/Frigorino.Domain/Interfaces/IExtractQuantityJob.cs`:

```csharp
namespace Frigorino.Domain.Interfaces
{
    // Unit of work enqueued onto the background runner; resolved in a fresh DI scope.
    public interface IExtractQuantityJob
    {
        Task Run(int householdId, int listId, int itemId, string rawText, CancellationToken ct);
    }
}
```

`Application/Frigorino.Domain/Interfaces/IQuantityExtractionTrigger.cs`:

```csharp
namespace Frigorino.Domain.Interfaces
{
    // Called by the list-item slices after an item's text is entered or changed. The enabled
    // implementation digit-gates and enqueues the extract job (which chains to classification on
    // the clean name); the disabled implementation skips extraction and classifies the raw text.
    // This is the single front door the slices call — classification hangs off of it.
    public interface IQuantityExtractionTrigger
    {
        void OnItemEntered(int householdId, int listId, int itemId, string rawText);
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build Application/Frigorino.Domain`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Quantities/QuantityExtraction.cs Application/Frigorino.Domain/Interfaces/IQuantityExtractor.cs Application/Frigorino.Domain/Interfaces/IExtractQuantityJob.cs Application/Frigorino.Domain/Interfaces/IQuantityExtractionTrigger.cs
git commit -m "feat: add quantity extraction ports (IQuantityExtractor, job, trigger)"
```

---

### Task 3: `ListItem` quantity contract change (entity + aggregate + EF + DTO + slices + migration)

This is one atomic task because the column/signature change ripples through the compiler. The slices keep using the **existing** `IProductClassificationTrigger` (classify on raw text) here; extraction is wired in Task 7.

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/ListItem.cs`
- Modify: `Application/Frigorino.Domain/Entities/List.cs:137-205,343-370` (AddItem / UpdateItem / validation; add ApplyExtractedQuantity)
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs:23-24`
- Create: `Application/Frigorino.Features/Lists/Items/QuantityDto.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/CreateItem.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`
- Modify: `Application/Frigorino.Test/Domain/ListAggregateItemTests.cs`
- Create: `Application/Frigorino.Test/Infrastructure/ListItemQuantityPersistenceTests.cs`

- [ ] **Step 1: Update the `ListItem` entity**

Replace the `Quantity`/`QuantityMaxLength` members. New `Application/Frigorino.Domain/Entities/ListItem.cs`:

```csharp
using Frigorino.Domain.Quantities;

namespace Frigorino.Domain.Entities
{
    public class ListItem
    {
        // Source of truth for length constraints. Both the List aggregate methods and the
        // EF configuration (ListItemConfiguration) read from this so DB and aggregate agree.
        public const int TextMaxLength = 500;

        public int Id { get; set; }
        public int ListId { get; set; }
        public string Text { get; set; } = string.Empty;

        // Structured quantity: both columns set together, or both null (no quantity).
        // The both-or-null invariant is enforced by the List aggregate.
        public decimal? QuantityValue { get; set; }
        public QuantityUnit? QuantityUnit { get; set; }

        public bool Status { get; set; } = false; // false = unchecked, true = checked
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public List List { get; set; } = null!;
    }
}
```

- [ ] **Step 2: Update the `List` aggregate**

In `Application/Frigorino.Domain/Entities/List.cs`, add `using Frigorino.Domain.Quantities;` at the top. Replace the `AddItem`, `UpdateItem`, `ValidateItemFields`, and `NormaliseQuantity` members (lines ~137-205 and ~343-370) with:

```csharp
        public Result<ListItem> AddItem(string text)
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
                QuantityValue = null,
                QuantityUnit = null,
                Status = false,
                SortOrder = ComputeAppendSortOrder(targetStatus: false),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            ListItems.Add(item);
            return Result.Ok(item);
        }

        public Result<ListItem> UpdateItem(int itemId, string? text, Quantity? quantity, bool? status)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            // All three fields are "preserve on null", so an all-null payload is a guaranteed
            // no-op — reject it rather than returning 200 OK on garbage.
            if (text is null && quantity is null && status is null)
            {
                return Result.Fail<ListItem>(
                    new Error("Update request must set at least one field.")
                        .WithMetadata("Property", string.Empty));
            }

            var errors = ValidateItemText(text, requireText: text is not null);
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            if (status.HasValue && item.Status != status.Value)
            {
                item.SortOrder = ComputeAppendSortOrder(targetStatus: status.Value);
                item.Status = status.Value;
            }

            if (text is not null)
            {
                item.Text = text.Trim();
            }
            // quantity == null means "preserve"; setting it writes both columns. (No clear in v1.)
            if (quantity is not null)
            {
                item.QuantityValue = quantity.Value.Value;
                item.QuantityUnit = quantity.Value.Unit;
            }

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        // Applied by the quantity-extraction job: overwrite the item's text with the extracted
        // clean name and set (or clear) the structured quantity authoritatively.
        public Result<ListItem> ApplyExtractedQuantity(int itemId, string cleanName, Quantity? quantity)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            var errors = ValidateItemText(cleanName, requireText: true);
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            item.Text = cleanName.Trim();
            item.QuantityValue = quantity?.Value;
            item.QuantityUnit = quantity?.Unit;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }
```

And replace the old `ValidateItemFields`/`NormaliseQuantity` helpers with:

```csharp
        private static List<IError> ValidateItemText(string? text, bool requireText)
        {
            var errors = new System.Collections.Generic.List<IError>();
            if (requireText)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    errors.Add(new Error("Item text is required.")
                        .WithMetadata("Property", nameof(ListItem.Text)));
                }
                else if (text!.Trim().Length > ListItem.TextMaxLength)
                {
                    errors.Add(new Error($"Item text must be {ListItem.TextMaxLength} characters or fewer.")
                        .WithMetadata("Property", nameof(ListItem.Text)));
                }
            }
            return errors;
        }
```

- [ ] **Step 3: Update the EF configuration**

In `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs`, replace the `Quantity` property block (lines 23-24) with:

```csharp
            builder.Property(li => li.QuantityValue)
                .HasColumnType("numeric(12,3)");

            // QuantityUnit is a nullable enum — EF maps it to a nullable integer column.
            builder.Property(li => li.QuantityUnit);
```

- [ ] **Step 4: Add `QuantityDto` and update `ListItemResponse`**

`Application/Frigorino.Features/Lists/Items/QuantityDto.cs`:

```csharp
using Frigorino.Domain.Quantities;

namespace Frigorino.Features.Lists.Items
{
    // Atomic nested DTO — value and unit can never be transmitted apart. Nullable on the wire
    // (null = no quantity). QuantityUnit serializes as an integer (existing enum convention).
    public sealed record QuantityDto(decimal Value, QuantityUnit Unit);
}
```

Replace `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs`:

```csharp
using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Lists.Items
{
    public sealed record ListItemResponse(
        int Id,
        int ListId,
        string Text,
        QuantityDto? Quantity,
        bool Status,
        int SortOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static ListItemResponse From(ListItem item)
        {
            return new ListItemResponse(
                item.Id,
                item.ListId,
                item.Text,
                item.QuantityValue == null
                    ? null
                    : new QuantityDto(item.QuantityValue.Value, item.QuantityUnit!.Value),
                item.Status,
                item.SortOrder,
                item.CreatedAt,
                item.UpdatedAt);
        }

        public static readonly Expression<Func<ListItem, ListItemResponse>> ToProjection = i => new ListItemResponse(
            i.Id,
            i.ListId,
            i.Text,
            i.QuantityValue == null
                ? null
                : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.Status,
            i.SortOrder,
            i.CreatedAt,
            i.UpdatedAt);
    }
}
```

- [ ] **Step 5: Update the `CreateItem` slice (text-only request)**

In `Application/Frigorino.Features/Lists/Items/CreateItem.cs`: change the request record to `public sealed record CreateItemRequest(string Text);` and the call to `var result = list.AddItem(request.Text);`. Leave the `classificationTrigger.OnProductReferenced(householdId, request.Text);` line unchanged (extraction is wired in Task 7).

- [ ] **Step 6: Update the `UpdateItem` slice (QuantityDto request)**

In `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`: add `using Frigorino.Domain.Quantities;`, change the request record to `public sealed record UpdateItemRequest(string? Text, QuantityDto? Quantity, bool? Status);`, and replace the handler body's mapping + call (the `list.UpdateItem(...)` line) with:

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

            var result = list.UpdateItem(itemId, request.Text, quantity, request.Status);
```

Leave the existing `if (request.Text is not null) { classificationTrigger.OnProductReferenced(...); }` block unchanged (swapped in Task 7).

- [ ] **Step 7: Update `ListAggregateItemTests`**

In `Application/Frigorino.Test/Domain/ListAggregateItemTests.cs`, add `using Frigorino.Domain.Quantities;`. Apply these edits:

- `AddItem` calls drop the second arg: `list.AddItem("Milk")`, `list.AddItem("Eggs")`, `list.AddItem("Bread")`, `list.AddItem("   ")`, `list.AddItem(tooLong)`.
- **Delete** `AddItem_QuantityTooLong_FailsKeyedOnQuantity` and `AddItem_WhitespaceQuantity_NormalisedToNull` (the free-text quantity rules are gone).
- Replace `AddItem_TrimsTextAndQuantity` with:

```csharp
        [Fact]
        public void AddItem_TrimsText()
        {
            var list = NewList();

            var result = list.AddItem("  Milk  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Milk", result.Value.Text);
            Assert.Null(result.Value.QuantityValue);
            Assert.Null(result.Value.QuantityUnit);
        }
```

- All `UpdateItem(...)` calls: the quantity arg is now `Quantity?`. For the preserve cases pass `null` (e.g. `list.UpdateItem(item.Id, text: "Soy milk", quantity: null, status: null)`). Replace `AddSeed(list, "Milk", quantity: "1 L")` seeds and the `Assert.Equal("1 L", item.Quantity)` assertion in `UpdateItem_PartialUpdate_PreservesUnsetFields` with:

```csharp
        [Fact]
        public void UpdateItem_PartialUpdate_PreservesUnsetQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", quantity: Quantity.Create(1, QuantityUnit.Liter).Value);

            var result = list.UpdateItem(item.Id, text: "Soy milk", quantity: null, status: null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Soy milk", item.Text);
            Assert.Equal(1m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Liter, item.QuantityUnit);
        }

        [Fact]
        public void UpdateItem_SetsQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, text: null,
                quantity: Quantity.Create(2, QuantityUnit.Bottle).Value, status: null);

            Assert.True(result.IsSuccess);
            Assert.Equal(2m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Bottle, item.QuantityUnit);
        }
```

- Add `ApplyExtractedQuantity` tests:

```csharp
        [Fact]
        public void ApplyExtractedQuantity_RewritesTextAndSetsQuantity()
        {
            var list = NewList();
            var item = AddSeed(list, "20 apples");

            var result = list.ApplyExtractedQuantity(item.Id, "apples",
                Quantity.Create(20, QuantityUnit.Piece).Value);

            Assert.True(result.IsSuccess);
            Assert.Equal("apples", item.Text);
            Assert.Equal(20m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Piece, item.QuantityUnit);
        }

        [Fact]
        public void ApplyExtractedQuantity_NoQuantity_RewritesTextLeavesQuantityNull()
        {
            var list = NewList();
            var item = AddSeed(list, "milk");

            var result = list.ApplyExtractedQuantity(item.Id, "milk", quantity: null);

            Assert.True(result.IsSuccess);
            Assert.Equal("milk", item.Text);
            Assert.Null(item.QuantityValue);
            Assert.Null(item.QuantityUnit);
        }

        [Fact]
        public void ApplyExtractedQuantity_ItemNotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.ApplyExtractedQuantity(itemId: 999, cleanName: "apples", quantity: null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }
```

- Update the `AddSeed` helper signature to take a structured quantity:

```csharp
        private ListItem AddSeed(List list, string text, Quantity? quantity = null, bool status = false, int? sortOrder = null)
        {
            var item = new ListItem
            {
                Id = ++_nextItemId,
                ListId = list.Id,
                Text = text,
                QuantityValue = quantity?.Value,
                QuantityUnit = quantity?.Unit,
                Status = status,
                SortOrder = sortOrder ?? (status
                    ? SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap
                    : SortOrderCalculator.UncheckedMinRange + SortOrderCalculator.DefaultGap),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true,
            };
            list.ListItems.Add(item);
            return item;
        }
```

- [ ] **Step 8: Add a persistence round-trip test**

`Application/Frigorino.Test/Infrastructure/ListItemQuantityPersistenceTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class ListItemQuantityPersistenceTests
    {
        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task ListItem_RoundTripsStructuredQuantity()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem
                {
                    ListId = 1,
                    Text = "milk",
                    QuantityValue = 1.5m,
                    QuantityUnit = QuantityUnit.Liter,
                });
                await db.SaveChangesAsync();
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal(1.5m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Liter, item.QuantityUnit);
        }

        [Fact]
        public async Task ListItem_RoundTripsNullQuantity()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                db.ListItems.Add(new ListItem { ListId = 1, Text = "call dentist" });
                await db.SaveChangesAsync();
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Null(item.QuantityValue);
            Assert.Null(item.QuantityUnit);
        }
    }
}
```

- [ ] **Step 9: Generate the migration**

Run:
```bash
dotnet ef migrations add AddListItemQuantityColumns --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Open the generated `Application/Frigorino.Infrastructure/Migrations/*_AddListItemQuantityColumns.cs` and verify `Up` drops `Quantity` and adds the two columns (no data backfill). It should look like:

```csharp
            migrationBuilder.DropColumn(name: "Quantity", table: "ListItems");

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityValue", table: "ListItems",
                type: "numeric(12,3)", nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityUnit", table: "ListItems",
                type: "integer", nullable: true);
```

If the generated `Up` differs materially (e.g. it kept `Quantity`), the model/config is wrong — fix Steps 1/3 and regenerate (`dotnet ef migrations remove` first).

- [ ] **Step 10: Build + run the affected tests**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests|FullyQualifiedName~ListItemQuantityPersistenceTests"`
Expected: PASS. Then `dotnet build Application/Frigorino.sln` — Expected: Build succeeded (slices compile against the new DTOs).

- [ ] **Step 11: Commit**

```bash
git add Application/Frigorino.Domain Application/Frigorino.Infrastructure Application/Frigorino.Features Application/Frigorino.Test
git commit -m "feat: replace ListItem free-text quantity with structured Quantity columns"
```

---

### Task 4: Config restructure to `Ai:*` + keyed ChatClients

Moves the classifier config under a shared-key `Ai` section so the extractor can share the key with its own model. Keeps `dotnet test` green (classifier still works, now via a keyed ChatClient).

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/AiKeys.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs:90-94` (ctor → keyed client)
- Modify: `Application/Frigorino.Infrastructure/Services/ItemClassificationDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/appsettings.json:21-25`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs:30-31`

- [ ] **Step 1: Add the DI key constants**

`Application/Frigorino.Infrastructure/Services/AiKeys.cs`:

```csharp
namespace Frigorino.Infrastructure.Services
{
    // Keys for the two keyed ChatClient registrations. Both share one API key but use
    // per-feature models, so they must be distinguished in DI.
    public static class AiKeys
    {
        public const string Classifier = "ai-classifier";
        public const string Extractor = "ai-extractor";
    }
}
```

- [ ] **Step 2: Make the classifier resolve its keyed client**

In `Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs` add `using Microsoft.Extensions.DependencyInjection;` and change the constructor signature (line ~90) to:

```csharp
        public OpenAiItemClassifier(
            [FromKeyedServices(AiKeys.Classifier)] ChatClient client,
            ILogger<OpenAiItemClassifier> logger)
        {
            _client = client;
            _logger = logger;
        }
```

- [ ] **Step 3: Update the classifier DI extension to `Ai:*` + keyed client**

Replace the body of `AddItemClassification` in `Application/Frigorino.Infrastructure/Services/ItemClassificationDependencyInjection.cs`:

```csharp
            var enabled = configuration.GetValue<bool>("Ai:Classifier:Enabled");
            var apiKey = configuration["Ai:ApiKey"];
            var model = configuration["Ai:Classifier:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-5.4-mini";
            }

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddKeyedSingleton<ChatClient>(AiKeys.Classifier, new ChatClient(model: model, apiKey: apiKey));
                services.AddScoped<IItemClassifier, OpenAiItemClassifier>();
                services.AddScoped<IClassifyProductJob, ClassifyProductJob>();
                services.AddScoped<IProductClassificationTrigger, QueueingProductClassificationTrigger>();
            }
            else
            {
                services.AddScoped<IProductClassificationTrigger, NullProductClassificationTrigger>();
            }

            return services;
```

- [ ] **Step 4: Restructure `appsettings.json`**

Replace the `"Classifier": { ... }` block (lines 21-25) with:

```jsonc
  "Ai": {
    "ApiKey": "",
    "Classifier": { "Enabled": true, "Model": "gpt-5.4-mini" }, // powerful; gpt-5-mini/gpt-5.4-mini
    "QuantityExtractor": { "Enabled": true, "Model": "gpt-5.4-nano" } // cheap; gpt-5-nano/gpt-5.4-nano
  },
```

- [ ] **Step 5: Update the integration test factory config keys**

In `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`, replace lines 30-31:

```csharp
        builder.UseSetting("Ai:Classifier:Enabled", "true");
        builder.UseSetting("Ai:ApiKey", "integration-test-stub-key");
```

- [ ] **Step 6: Build + verify the classifier path still resolves**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ClassifyProductJobTests|FullyQualifiedName~ProductClassificationTriggerTests"`
Expected: PASS. Then `dotnet build Application/Frigorino.sln` — Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/AiKeys.cs Application/Frigorino.Infrastructure/Services/OpenAiItemClassifier.cs Application/Frigorino.Infrastructure/Services/ItemClassificationDependencyInjection.cs Application/Frigorino.Web/appsettings.json Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs
git commit -m "refactor: restructure AI config to shared Ai:* section with keyed ChatClients"
```

---

### Task 5: `OpenAiQuantityExtractor` adapter (vendor boundary)

Mirrors `OpenAiItemClassifier`. No unit test — `ChatClient` is a concrete SDK type that isn't mockable (same as the classifier), so it is covered by the integration stub in Task 12.

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/OpenAiQuantityExtractor.cs`

- [ ] **Step 1: Write the adapter**

`Application/Frigorino.Infrastructure/Services/OpenAiQuantityExtractor.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    // Vendor boundary. Cheap-model inline extraction of {clean name, quantity} from a user's raw
    // list-item text. Swapping vendor later = rewrite this one class behind IQuantityExtractor.
    public class OpenAiQuantityExtractor : IQuantityExtractor
    {
        // "reasoning" is FIRST (strict outputs generate fields in schema order → cheap CoT).
        // quantityUnit is always required (strict); when quantityValue is null we ignore the unit.
        // Unit enum names are interpolated from QuantityUnit so they can't drift.
        private static readonly BinaryData Schema = BinaryData.FromString($$"""
            {
                "type": "object",
                "properties": {
                    "reasoning": { "type": "string" },
                    "cleanName": { "type": "string" },
                    "quantityValue": { "type": ["number", "null"] },
                    "quantityUnit": {
                        "type": "string",
                        "enum": [{{string.Join(", ", Enum.GetNames<QuantityUnit>().Select(n => $"\"{n}\""))}}]
                    }
                },
                "required": ["reasoning", "cleanName", "quantityValue", "quantityUnit"],
                "additionalProperties": false
            }
            """);

        private static readonly string SystemPrompt =
            "You extract the product name and quantity a user wrote on a household list. Inputs may be English or German, and the quantity may come before or after the name, or be absent.\n" +
            "Set 'cleanName' to the item with any quantity/amount removed (e.g. '20 apples'/'apples 20' -> 'apples'; '1l milk' -> 'milk'; '500g Mehl' -> 'Mehl'; '2 bottles of beer' -> 'beer').\n" +
            "Set 'quantityValue' to the numeric amount, or null if there is none. A digit that is part of a brand/name is NOT a quantity (e.g. '7up' -> cleanName '7up', quantityValue null; 'WD-40' -> 'WD-40', null; 'E45 cream' -> 'E45 cream', null).\n" +
            "Set 'quantityUnit' to the best-fitting unit: Gram/Kilogram for weight, Milliliter/Liter for volume, Bottle/Can/Pack/Bag for containers, Piece for a bare count. When quantityValue is null, still pick Piece (it is ignored).\n" +
            "In 'reasoning', briefly justify your choice in one short English sentence regardless of input language.\n" +
            "Respond only via the provided JSON schema.";

        private static readonly SystemChatMessage SystemMessage = new(SystemPrompt);

        private static readonly ChatCompletionOptions Options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "quantity_extraction",
                jsonSchema: Schema,
                jsonSchemaIsStrict: true),
        };

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        private sealed record ExtractorResponse(
            string Reasoning, string CleanName, decimal? QuantityValue, QuantityUnit QuantityUnit);

        private readonly ChatClient _client;
        private readonly ILogger<OpenAiQuantityExtractor> _logger;

        public OpenAiQuantityExtractor(
            [FromKeyedServices(AiKeys.Extractor)] ChatClient client,
            ILogger<OpenAiQuantityExtractor> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct)
        {
            var messages = new ChatMessage[] { SystemMessage, new UserChatMessage(rawText) };

            try
            {
                var completion = await _client.CompleteChatAsync(messages, Options, ct);

                // Refusal / empty → no extraction; keep the raw text, no quantity.
                if (completion.Value.Content.Count == 0
                    || string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
                {
                    _logger.LogWarning("Extractor returned no usable content for '{Raw}'; keeping raw text.", rawText);
                    return Result.Ok(new QuantityExtraction(rawText, null));
                }

                var dto = JsonSerializer.Deserialize<ExtractorResponse>(completion.Value.Content[0].Text, JsonOptions);
                if (dto is null)
                {
                    return Result.Ok(new QuantityExtraction(rawText, null));
                }

                Quantity? quantity = null;
                if (dto.QuantityValue is decimal v && v > 0)
                {
                    var q = Quantity.Create(v, dto.QuantityUnit);
                    if (q.IsSuccess)
                    {
                        quantity = q.Value;
                    }
                }

                var cleanName = string.IsNullOrWhiteSpace(dto.CleanName) ? rawText : dto.CleanName.Trim();

                _logger.LogInformation(
                    "Extracted '{Raw}' -> name '{Name}', qty {Value}/{Unit}: {Reasoning}",
                    rawText, cleanName, dto.QuantityValue, dto.QuantityUnit, dto.Reasoning);

                return Result.Ok(new QuantityExtraction(cleanName, quantity));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Extractor call failed for '{Raw}'.", rawText);
                return Result.Fail<QuantityExtraction>("Extractor call failed.");
            }
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/OpenAiQuantityExtractor.cs
git commit -m "feat: add OpenAiQuantityExtractor adapter behind IQuantityExtractor"
```

---

### Task 6: `ExtractQuantityJob` (cache-aware write-back + chain to classify)

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/ExtractQuantityJob.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ExtractQuantityJobTests.cs`

- [ ] **Step 1: Write the failing tests**

`Application/Frigorino.Test/Infrastructure/ExtractQuantityJobTests.cs`:

```csharp
using FakeItEasy;
using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frigorino.Test.Infrastructure
{
    public class ExtractQuantityJobTests
    {
        private const int HouseholdId = 42;
        private const int ListId = 7;
        private const int ItemId = 100;

        private sealed class FakeExtractor : IQuantityExtractor
        {
            private readonly Result<QuantityExtraction> _result;
            public int Calls { get; private set; }
            public FakeExtractor(Result<QuantityExtraction> result) => _result = result;
            public Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct)
            {
                Calls++;
                return Task.FromResult(_result);
            }
        }

        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static async Task SeedListWithItem(string dbName, string text)
        {
            using var db = NewContext(dbName);
            db.Lists.Add(new List
            {
                Id = ListId,
                HouseholdId = HouseholdId,
                Name = "Groceries",
                CreatedByUserId = "u",
                IsActive = true,
                ListItems = { new ListItem { Id = ItemId, ListId = ListId, Text = text, IsActive = true } },
            });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task Run_WithQuantity_WritesBackAndTriggersClassificationOnCleanName()
        {
            var dbName = Guid.NewGuid().ToString();
            await SeedListWithItem(dbName, "20 apples");
            var extractor = new FakeExtractor(Result.Ok(
                new QuantityExtraction("apples", Quantity.Create(20, QuantityUnit.Piece).Value)));
            var classification = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractQuantityJob(db, extractor, classification, NullLogger<ExtractQuantityJob>.Instance);
                await job.Run(HouseholdId, ListId, ItemId, "20 apples", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal("apples", item.Text);
            Assert.Equal(20m, item.QuantityValue);
            Assert.Equal(QuantityUnit.Piece, item.QuantityUnit);
            A.CallTo(() => classification.OnProductReferenced(HouseholdId, "apples")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Run_NoQuantity_RewritesTextTriggersClassification()
        {
            var dbName = Guid.NewGuid().ToString();
            await SeedListWithItem(dbName, "7up");
            var extractor = new FakeExtractor(Result.Ok(new QuantityExtraction("7up", null)));
            var classification = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractQuantityJob(db, extractor, classification, NullLogger<ExtractQuantityJob>.Instance);
                await job.Run(HouseholdId, ListId, ItemId, "7up", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Null(item.QuantityValue);
            A.CallTo(() => classification.OnProductReferenced(HouseholdId, "7up")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Run_ExtractorFails_WritesNothingAndDoesNotClassify()
        {
            var dbName = Guid.NewGuid().ToString();
            await SeedListWithItem(dbName, "20 apples");
            var extractor = new FakeExtractor(Result.Fail<QuantityExtraction>("transient"));
            var classification = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractQuantityJob(db, extractor, classification, NullLogger<ExtractQuantityJob>.Instance);
                await job.Run(HouseholdId, ListId, ItemId, "20 apples", CancellationToken.None);
            }

            using var verify = NewContext(dbName);
            var item = await verify.ListItems.SingleAsync();
            Assert.Equal("20 apples", item.Text);
            Assert.Null(item.QuantityValue);
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Run_ItemMissing_NoOp()
        {
            var dbName = Guid.NewGuid().ToString();
            var extractor = new FakeExtractor(Result.Ok(new QuantityExtraction("x", null)));
            var classification = A.Fake<IProductClassificationTrigger>();

            using (var db = NewContext(dbName))
            {
                var job = new ExtractQuantityJob(db, extractor, classification, NullLogger<ExtractQuantityJob>.Instance);
                await job.Run(HouseholdId, ListId, ItemId, "x", CancellationToken.None);
            }

            Assert.Equal(0, extractor.Calls);
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExtractQuantityJobTests"`
Expected: FAIL to compile (`ExtractQuantityJob` not defined).

- [ ] **Step 3: Write the job**

`Application/Frigorino.Infrastructure/Services/ExtractQuantityJob.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Services
{
    // Runs in a fresh DI scope on the background runner. Lossy by design: any failure drops the
    // work item. On success it rewrites the item's text to the clean name + sets the structured
    // quantity, then chains to classification on the clean name (the catalog key — cache intact).
    public class ExtractQuantityJob : IExtractQuantityJob
    {
        private readonly ApplicationDbContext _db;
        private readonly IQuantityExtractor _extractor;
        private readonly IProductClassificationTrigger _classificationTrigger;
        private readonly ILogger<ExtractQuantityJob> _logger;

        public ExtractQuantityJob(
            ApplicationDbContext db,
            IQuantityExtractor extractor,
            IProductClassificationTrigger classificationTrigger,
            ILogger<ExtractQuantityJob> logger)
        {
            _db = db;
            _extractor = extractor;
            _classificationTrigger = classificationTrigger;
            _logger = logger;
        }

        public async Task Run(int householdId, int listId, int itemId, string rawText, CancellationToken ct)
        {
            // Load the aggregate so the write-back goes through the domain method. If the list/item
            // is gone (deleted between enqueue and run), no-op without calling the extractor.
            var list = await _db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            var item = list?.ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (list is null || item is null)
            {
                return;
            }

            var result = await _extractor.ExtractAsync(rawText, ct);
            if (result.IsFailed)
            {
                _logger.LogWarning(
                    "Quantity extraction failed for item {ItemId} (household {HouseholdId}); dropping.",
                    itemId, householdId);
                return;
            }

            var extraction = result.Value;
            var applied = list.ApplyExtractedQuantity(itemId, extraction.CleanName, extraction.Quantity);
            if (applied.IsFailed)
            {
                return;
            }

            await _db.SaveChangesAsync(ct);

            // Chain: classify on the clean name so the Product-catalog cache keys on "apples",
            // not "20 apples". Enabled trigger enqueues classify; disabled trigger is a no-op.
            _classificationTrigger.OnProductReferenced(householdId, extraction.CleanName);
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExtractQuantityJobTests"`
Expected: PASS (4 cases).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/ExtractQuantityJob.cs Application/Frigorino.Test/Infrastructure/ExtractQuantityJobTests.cs
git commit -m "feat: add ExtractQuantityJob (write-back + chain to classification)"
```

---

### Task 7: Triggers, DI wiring, and slice swap

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/QuantityExtractionTriggers.cs`
- Create: `Application/Frigorino.Infrastructure/Services/QuantityExtractionDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/Program.cs:60`
- Modify: `Application/Frigorino.Features/Lists/Items/CreateItem.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`
- Test: `Application/Frigorino.Test/Infrastructure/QuantityExtractionTriggerTests.cs`

- [ ] **Step 1: Write the failing trigger tests**

`Application/Frigorino.Test/Infrastructure/QuantityExtractionTriggerTests.cs`:

```csharp
using FakeItEasy;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class QuantityExtractionTriggerTests
    {
        [Fact]
        public void Queueing_DigitText_EnqueuesAndDoesNotClassifyDirectly()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemEntered(42, 7, 100, "20 apples");

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => classification.OnProductReferenced(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [Fact]
        public void Queueing_NoDigitText_ClassifiesRawAndDoesNotEnqueue()
        {
            var queue = A.Fake<IBackgroundTaskQueue>();
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new QueueingQuantityExtractionTrigger(queue, classification);

            trigger.OnItemEntered(42, 7, 100, "milk");

            A.CallTo(() => queue.TryEnqueue(A<Func<IServiceProvider, CancellationToken, Task>>._))
                .MustNotHaveHappened();
            A.CallTo(() => classification.OnProductReferenced(42, "milk")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Null_ClassifiesRawAndDoesNotEnqueue()
        {
            var classification = A.Fake<IProductClassificationTrigger>();
            var trigger = new NullQuantityExtractionTrigger(classification);

            trigger.OnItemEntered(42, 7, 100, "20 apples");

            A.CallTo(() => classification.OnProductReferenced(42, "20 apples")).MustHaveHappenedOnceExactly();
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QuantityExtractionTriggerTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Write the triggers**

`Application/Frigorino.Infrastructure/Services/QuantityExtractionTriggers.cs`:

```csharp
using System.Text.RegularExpressions;
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    // Enabled path. Digit-gate: only digit-bearing text pays the LLM. Digit present -> enqueue
    // extraction (which chains to classification on the clean name). No digit -> classify the raw
    // text directly (Cycle 2 behavior; nothing to extract).
    public class QueueingQuantityExtractionTrigger : IQuantityExtractionTrigger
    {
        private static readonly Regex Digit = new(@"\d", RegexOptions.Compiled);

        private readonly IBackgroundTaskQueue _queue;
        private readonly IProductClassificationTrigger _classificationTrigger;

        public QueueingQuantityExtractionTrigger(
            IBackgroundTaskQueue queue, IProductClassificationTrigger classificationTrigger)
        {
            _queue = queue;
            _classificationTrigger = classificationTrigger;
        }

        public void OnItemEntered(int householdId, int listId, int itemId, string rawText)
        {
            if (Digit.IsMatch(rawText))
            {
                _queue.TryEnqueue((sp, ct) =>
                    sp.GetRequiredService<IExtractQuantityJob>().Run(householdId, listId, itemId, rawText, ct));
            }
            else
            {
                _classificationTrigger.OnProductReferenced(householdId, rawText);
            }
        }
    }

    // Disabled path: extraction is off. Classification still runs on the raw text.
    public class NullQuantityExtractionTrigger : IQuantityExtractionTrigger
    {
        private readonly IProductClassificationTrigger _classificationTrigger;

        public NullQuantityExtractionTrigger(IProductClassificationTrigger classificationTrigger)
        {
            _classificationTrigger = classificationTrigger;
        }

        public void OnItemEntered(int householdId, int listId, int itemId, string rawText)
        {
            _classificationTrigger.OnProductReferenced(householdId, rawText);
        }
    }
}
```

- [ ] **Step 4: Write the DI extension**

`Application/Frigorino.Infrastructure/Services/QuantityExtractionDependencyInjection.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace Frigorino.Infrastructure.Services
{
    public static class QuantityExtractionDependencyInjection
    {
        // Must be called AFTER AddItemClassification — both trigger impls depend on the
        // IProductClassificationTrigger it registers.
        public static IServiceCollection AddQuantityExtraction(
            this IServiceCollection services, IConfiguration configuration)
        {
            var enabled = configuration.GetValue<bool>("Ai:QuantityExtractor:Enabled");
            var apiKey = configuration["Ai:ApiKey"];
            var model = configuration["Ai:QuantityExtractor:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-5.4-nano";
            }

            if (enabled && !string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddKeyedSingleton<ChatClient>(AiKeys.Extractor, new ChatClient(model: model, apiKey: apiKey));
                services.AddScoped<IQuantityExtractor, OpenAiQuantityExtractor>();
                // Job depends on IQuantityExtractor — registered only on the enabled path (same
                // ValidateOnBuild reasoning as ClassifyProductJob).
                services.AddScoped<IExtractQuantityJob, ExtractQuantityJob>();
                services.AddScoped<IQuantityExtractionTrigger, QueueingQuantityExtractionTrigger>();
            }
            else
            {
                services.AddScoped<IQuantityExtractionTrigger, NullQuantityExtractionTrigger>();
            }

            return services;
        }
    }
}
```

- [ ] **Step 5: Wire into `Program.cs`**

In `Application/Frigorino.Web/Program.cs`, after line 60 (`builder.Services.AddItemClassification(builder.Configuration);`) add:

```csharp
builder.Services.AddQuantityExtraction(builder.Configuration);
```

- [ ] **Step 6: Swap the slices to the quantity trigger**

In `Application/Frigorino.Features/Lists/Items/CreateItem.cs`: change the handler parameter `IProductClassificationTrigger classificationTrigger` to `IQuantityExtractionTrigger quantityTrigger`, and replace `classificationTrigger.OnProductReferenced(householdId, request.Text);` with:

```csharp
            quantityTrigger.OnItemEntered(householdId, listId, result.Value.Id, request.Text);
```

In `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`: change the parameter likewise and replace the trigger block with:

```csharp
            if (request.Text is not null)
            {
                quantityTrigger.OnItemEntered(householdId, listId, itemId, request.Text);
            }
```

(The `Domain.Interfaces` using is already present in both files.)

- [ ] **Step 7: Run tests + build**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~QuantityExtractionTriggerTests"`
Expected: PASS (3 cases). Then `dotnet build Application/Frigorino.sln` — Expected: Build succeeded (ValidateOnBuild passes — the extract job resolves on the enabled path only).

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/QuantityExtractionTriggers.cs Application/Frigorino.Infrastructure/Services/QuantityExtractionDependencyInjection.cs Application/Frigorino.Web/Program.cs Application/Frigorino.Features/Lists/Items/CreateItem.cs Application/Frigorino.Features/Lists/Items/UpdateItem.cs Application/Frigorino.Test/Infrastructure/QuantityExtractionTriggerTests.cs
git commit -m "feat: wire quantity-extraction triggers + DI + slice front door"
```

---

### Task 8: Regenerate the TypeScript client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/api/**` (committed)

> After this task the frontend will NOT type-check until Task 11 — `item.quantity` changes from `string` to `QuantityDto | null`. That is expected; frontend verification happens in Tasks 9–11.

- [ ] **Step 1: Regenerate**

From `Application/Frigorino.Web/ClientApp/`:
```bash
npm run api
```
This rebuilds the backend, emits `src/lib/openapi.json`, and regenerates `src/lib/api`.

- [ ] **Step 2: Confirm the new types**

Verify `src/lib/api/types.gen.ts` now contains a `QuantityDto` type (`{ value: number; unit: QuantityUnit }`), `QuantityUnit = number`, `CreateItemRequest = { text: string }` (no quantity), `UpdateItemRequest` with `quantity?: QuantityDto | null`, and `ListItemResponse` with `quantity: QuantityDto | null`.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib
git commit -m "chore: regenerate TS client for structured quantity DTOs"
```

---

### Task 9: Frontend — drop composer quantity entry, update item mutations

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListFooter.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useCreateListItem.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useUpdateListItem.ts`
- Modify: the list page that renders `ListFooter` (locate in Step 4)

> No JS test runner exists; verify with `npm run tsc` + `npm run lint` after Task 11. Each step compiles in isolation conceptually but the chain is verified at Task 11.

- [ ] **Step 1: Remove the quantity feature from `ListFooter`**

In `ListFooter.tsx`:
- Change the import to drop `quantityFeature`: `import { Composer, type Completion, type DuplicateResult } from "../../../../components/composer";`
- Change `const features = [quantityFeature] as const;` to `const features = [] as const;`
- Change the prop types to drop the `quantity` argument:

```typescript
    onAddItem: (data: string) => void;
    onUpdateItem: (data: string) => void;
```

- Replace `initialDraft` with text-only:

```typescript
        const initialDraft = useMemo(
            () => (editingItem ? { text: editingItem.text } : undefined),
            [editingItem],
        );
```

- Replace `handleComplete` body:

```typescript
        const handleComplete = useCallback(
            (r: Completion<typeof features>) => {
                if (r.mode === "edit") {
                    onUpdateItem(r.text);
                } else {
                    onAddItem(r.text);
                    onScrollToLastUnchecked();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLastUnchecked],
        );
```

- [ ] **Step 2: Update `useCreateListItem` (text-only body, no optimistic quantity)**

In `useCreateListItem.ts`, the optimistic item drops `quantity` (the field no longer exists on `CreateItemRequest`/the optimistic shape uses `quantity: null`):

```typescript
            const optimisticItem: ListItemResponse = {
                id: Date.now(),
                text: variables.body.text,
                quantity: null,
                status: false,
                sortOrder: lastUncheckedSortOrder + 1,
                listId: variables.path.listId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };
```

- [ ] **Step 3: Update `useUpdateListItem` optimistic merge for `QuantityDto`**

In `useUpdateListItem.ts`, the optimistic patches set `quantity` from `variables.body.quantity` (now `QuantityDto | null | undefined`); `?? item.quantity` preserves on undefined. Both the list-cache `map` and the detail-cache patch change `quantity: variables.body.quantity ?? item.quantity` — the existing code already reads `variables.body.quantity`, so only the *type* changes; no code change needed beyond what the regenerated types enforce. Confirm it still type-checks; if TS complains about `null` vs `undefined`, use `variables.body.quantity ?? item.quantity` unchanged (both are valid `QuantityDto | null`).

- [ ] **Step 4: Update the list page handlers**

Find the page that wires `ListFooter`:
```bash
cd Application/Frigorino.Web/ClientApp && npx --no-install grep -rl "onAddItem" src/features/lists || true
```
Use Grep for `ListFooter` usage under `src/features/lists/`. In that page:
- The `onAddItem`/`onUpdateItem` callbacks passed to `ListFooter` lose their `quantity` parameter. Update them to call `useCreateListItem().mutate({ path: {...}, body: { text } })` and `useUpdateListItem().mutate({ path: {...}, body: { text } })` (no `quantity` on create; update sends only `text`).
- Capture the created item id for the extraction poll wired in Task 11 — note a TODO marker here; Task 11 fills it.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists
git commit -m "feat: list composer entry is text-only (quantity via extraction/popover)"
```

---

### Task 10: Frontend — render structured quantity + edit popover

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/quantityFormat.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/QuantityEditPopover.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add the unit label keys**

In both translation files, add a `quantityUnits` block under the existing root object. `en/translation.json`:

```json
  "quantityUnits": {
    "0": "g", "1": "kg", "2": "ml", "3": "l",
    "4": "pc", "5": "pack", "6": "can", "7": "bottle", "8": "bag"
  },
```

`de/translation.json`:

```json
  "quantityUnits": {
    "0": "g", "1": "kg", "2": "ml", "3": "l",
    "4": "Stk", "5": "Pack", "6": "Dose", "7": "Flasche", "8": "Beutel"
  },
```

- [ ] **Step 2: Add the formatter + unit list**

`Application/Frigorino.Web/ClientApp/src/features/lists/items/quantityFormat.ts`:

```typescript
import type { TFunction } from "i18next";
import type { QuantityDto } from "../../../lib/api";

// QuantityUnit is emitted as `number` by the generated client. These mirror the backend enum
// order (Gram=0 .. Bag=8). The label text comes from i18n (quantityUnits.<n>).
export const QUANTITY_UNIT_VALUES = [0, 1, 2, 3, 4, 5, 6, 7, 8] as const;

export const unitLabel = (t: TFunction, unit: number): string =>
    t(`quantityUnits.${unit}`);

// Render a structured quantity like "1.5 l" / "2 bottle". Trailing zeros trimmed.
export const formatQuantity = (t: TFunction, q: QuantityDto): string => {
    const value = Number(q.value);
    const num = Number.isInteger(value) ? value.toString() : value.toString();
    return `${num} ${unitLabel(t, q.unit)}`;
};
```

- [ ] **Step 3: Add the edit popover**

`Application/Frigorino.Web/ClientApp/src/features/lists/items/components/QuantityEditPopover.tsx`:

```typescript
import {
    Box,
    Button,
    MenuItem,
    Popover,
    TextField,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { QuantityDto } from "../../../../lib/api";
import { QUANTITY_UNIT_VALUES, unitLabel } from "../quantityFormat";

interface Props {
    anchorEl: HTMLElement | null;
    current: QuantityDto | null;
    onClose: () => void;
    onSave: (quantity: QuantityDto) => void;
}

export function QuantityEditPopover({ anchorEl, current, onClose, onSave }: Props) {
    const { t } = useTranslation();
    const [value, setValue] = useState(current ? String(current.value) : "1");
    const [unit, setUnit] = useState<number>(current?.unit ?? 4); // default Piece

    const numeric = Number(value.replace(",", "."));
    const valid = Number.isFinite(numeric) && numeric > 0;

    return (
        <Popover
            open={Boolean(anchorEl)}
            anchorEl={anchorEl}
            onClose={onClose}
            anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
        >
            <Box
                sx={{ display: "flex", gap: 1, alignItems: "center", p: 1.5 }}
                data-testid="quantity-edit-popover"
            >
                <TextField
                    autoFocus
                    size="small"
                    type="text"
                    inputMode="decimal"
                    placeholder={t("common.quantity")}
                    value={value}
                    onChange={(e) => setValue(e.target.value)}
                    sx={{ width: 90 }}
                />
                <TextField
                    select
                    size="small"
                    value={unit}
                    onChange={(e) => setUnit(Number(e.target.value))}
                    sx={{ width: 110 }}
                >
                    {QUANTITY_UNIT_VALUES.map((u) => (
                        <MenuItem key={u} value={u}>
                            {unitLabel(t, u)}
                        </MenuItem>
                    ))}
                </TextField>
                <Button
                    variant="contained"
                    size="small"
                    disabled={!valid}
                    onClick={() => {
                        onSave({ value: numeric, unit });
                        onClose();
                    }}
                >
                    {t("common.save")}
                </Button>
            </Box>
        </Popover>
    );
}
```

- [ ] **Step 4: Render quantity + spinner + popover in `ListItemContent`**

In `ListItemContent.tsx`, add an `isExtracting?: boolean` prop and an `onQuantityChange?: (q: QuantityDto) => void` prop. Replace the `secondary` slot of `ListItemText` to render the formatted quantity (clickable chip → popover), a spinner while extracting, or an "add quantity" affordance. Concretely:

- Add imports: `import { CircularProgress, Chip } from "@mui/material";`, `import { useState } from "react";`, `import { formatQuantity } from "../quantityFormat";`, `import { QuantityEditPopover } from "./QuantityEditPopover";`, `import type { QuantityDto } from "../../../../lib/api";`
- Change `interface Props` to `{ item: ListItemResponse; isExtracting?: boolean; onQuantityChange?: (q: QuantityDto) => void }`.
- Inside the component add `const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);`
- Replace the `secondary={ item.quantity && (...) }` block with:

```tsx
            secondary={
                <Box sx={{ display: "inline-flex", alignItems: "center", gap: 0.5 }}>
                    {item.quantity ? (
                        <Chip
                            size="small"
                            variant="outlined"
                            data-testid={`list-item-quantity-${item.text}`}
                            label={formatQuantity(t, item.quantity)}
                            onClick={(e) => setAnchorEl(e.currentTarget)}
                            sx={{
                                height: 20,
                                textDecoration: item.status ? "line-through" : "none",
                            }}
                        />
                    ) : isExtracting ? (
                        <CircularProgress size={12} data-testid={`list-item-quantity-loading-${item.text}`} />
                    ) : (
                        <Chip
                            size="small"
                            variant="outlined"
                            label={t("common.quantity")}
                            onClick={(e) => setAnchorEl(e.currentTarget)}
                            sx={{ height: 20, opacity: 0.5 }}
                        />
                    )}
                    {onQuantityChange && (
                        <QuantityEditPopover
                            anchorEl={anchorEl}
                            current={item.quantity}
                            onClose={() => setAnchorEl(null)}
                            onSave={onQuantityChange}
                        />
                    )}
                </Box>
            }
```

(Keep the long-press copy handler on the primary text; `Box` is already imported.)

- [ ] **Step 5: Wire `onQuantityChange` from the row to `useUpdateListItem`**

In the component that renders `ListItemContent` for each row (the list item row — find via Grep for `ListItemContent` under `src/features/lists/`), pass:

```tsx
onQuantityChange={(q) =>
    updateItem.mutate({
        path: { householdId, listId, itemId: item.id },
        body: { quantity: q },
    })
}
```

where `updateItem = useUpdateListItem()`.

- [ ] **Step 6: Verify type-check + lint**

From `ClientApp/`: `npm run tsc` then `npm run lint`.
Expected: both pass (the `useUpdateListItem`/poll wiring from Task 11 may still be pending — if `isExtracting` is unused yet, leave it optional so this compiles).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src
git commit -m "feat: render structured quantity chip + inline edit popover"
```

---

### Task 11: Frontend — bounded single-item extraction poll

After a digit-bearing item is created, poll the new item via `GetItem` until its quantity lands or a timeout, patching the items cache; surface `isExtracting` to the row.

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useExtractionPoll.ts`
- Modify: the list page (created-item tracking) and the row (pass `isExtracting`)

- [ ] **Step 1: Write the poll hook**

`Application/Frigorino.Web/ClientApp/src/features/lists/items/useExtractionPoll.ts`:

```typescript
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import {
    getItemOptions,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";

const MAX_POLL_MS = 4000;
const INTERVAL_MS = 600;

// Polls a single just-created item until its quantity arrives (extraction completed) or the
// deadline passes, then patches the items-list cache. `enabled` should be set only when the
// entered text contained a digit (otherwise no extraction runs).
export const useExtractionPoll = (
    householdId: number,
    listId: number,
    itemId: number | null,
    enabled: boolean,
) => {
    const queryClient = useQueryClient();
    const startedAt = itemId ?? 0; // changes when a new item id arrives

    const query = useQuery({
        ...getItemOptions({ path: { householdId, listId, itemId: itemId ?? 0 } }),
        enabled: enabled && (itemId ?? 0) > 0,
        refetchInterval: (q) => {
            const data = q.state.data as ListItemResponse | undefined;
            if (data?.quantity) return false; // arrived
            if (Date.now() - (q.state.dataUpdatedAt || startedAt) > MAX_POLL_MS) return false;
            return INTERVAL_MS;
        },
        staleTime: 0,
        gcTime: 0,
    });

    useEffect(() => {
        const item = query.data;
        if (!item?.quantity) return;
        queryClient.setQueryData<ListItemResponse[]>(
            getItemsQueryKey({ path: { householdId, listId } }),
            (old) =>
                old?.map((i) =>
                    i.id === item.id ? { ...i, text: item.text, quantity: item.quantity } : i,
                ) ?? old,
        );
    }, [query.data, householdId, listId, queryClient]);

    const isExtracting =
        enabled && (itemId ?? 0) > 0 && !query.data?.quantity && query.isFetching;

    return { isExtracting, extractingItemId: itemId };
};
```

- [ ] **Step 2: Track the created item + digit gate in the list page**

In the list page, after `useCreateListItem().mutateAsync(...)` resolves (it returns the created `ListItemResponse`), store `{ id, hadDigit }`:

```tsx
const [pendingExtraction, setPendingExtraction] = useState<{ id: number; hadDigit: boolean } | null>(null);
// in onAddItem:
const created = await createItem.mutateAsync({ path: { householdId, listId }, body: { text } });
setPendingExtraction({ id: created.id, hadDigit: /\d/.test(text) });

const { isExtracting, extractingItemId } = useExtractionPoll(
    householdId,
    listId,
    pendingExtraction?.id ?? null,
    pendingExtraction?.hadDigit ?? false,
);
```

- [ ] **Step 3: Pass `isExtracting` to the matching row**

Where rows are rendered, pass `isExtracting={isExtracting && item.id === extractingItemId}` to `ListItemContent`.

- [ ] **Step 4: Verify**

From `ClientApp/`: `npm run tsc` && `npm run lint` && `npm run prettier` (write).
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src
git commit -m "feat: bounded single-item poll surfaces extracted quantity on the list"
```

---

### Task 12: Integration test — stub extractor + API scenarios

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Infrastructure/StubQuantityExtractor.cs`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/Extraction.Api.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/ExtractionApiSteps.cs`

- [ ] **Step 1: Write the deterministic stub extractor**

`Application/Frigorino.IntegrationTests/Infrastructure/StubQuantityExtractor.cs`:

```csharp
using System.Text.RegularExpressions;
using FluentResults;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;

namespace Frigorino.IntegrationTests.Infrastructure;

// Deterministic, network-free extractor for integration tests. Parses a leading
// "<number><optional g/kg/ml/l> <name>" token; otherwise returns the raw text with no quantity.
//   "20 apples" -> ("apples", {20, Piece})
//   "1l milk"   -> ("milk", {1, Liter})
public sealed class StubQuantityExtractor : IQuantityExtractor
{
    private static readonly Regex Pattern = new(
        @"^(?<num>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|ml|l)?\s+(?<name>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<Result<QuantityExtraction>> ExtractAsync(string rawText, CancellationToken ct)
    {
        var match = Pattern.Match(rawText.Trim());
        if (!match.Success)
        {
            return Task.FromResult(Result.Ok(new QuantityExtraction(rawText, (Quantity?)null)));
        }

        var value = decimal.Parse(match.Groups["num"].Value.Replace(',', '.'),
            System.Globalization.CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "kg" => QuantityUnit.Kilogram,
            "g" => QuantityUnit.Gram,
            "ml" => QuantityUnit.Milliliter,
            "l" => QuantityUnit.Liter,
            _ => QuantityUnit.Piece,
        };
        var quantity = Quantity.Create(value, unit).Value;
        return Task.FromResult(Result.Ok(
            new QuantityExtraction(match.Groups["name"].Value.Trim(), quantity)));
    }
}
```

- [ ] **Step 2: Enable + register the stub in the factory**

In `TestWebApplicationFactory.cs`, add to the `ConfigureWebHost` settings (next to the `Ai:Classifier:Enabled` line from Task 4):

```csharp
        builder.UseSetting("Ai:QuantityExtractor:Enabled", "true");
```

and in `ConfigureServices` (next to the classifier stub swap):

```csharp
            services.RemoveAll<IQuantityExtractor>();
            services.AddScoped<IQuantityExtractor, StubQuantityExtractor>();
```

- [ ] **Step 3: Write the feature**

`Application/Frigorino.IntegrationTests/Slices/Lists/Extraction.Api.feature`:

```gherkin
Feature: Inline Quantity Extraction API

  Background:
    Given I am logged in with an active household

  Scenario: Adding "20 apples" extracts the count and renames the item
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "20 apples" to "Weekly Groceries" via the API
    Then the list item eventually has text "apples" with quantity 20 unit 4

  Scenario: Adding "1l milk" extracts the volume and chains classification
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "1l milk" to "Weekly Groceries" via the API
    Then the list item eventually has text "milk" with quantity 1 unit 3
    And the product "milk" is categorized as "Food"

  Scenario: A non-digit task is not extracted but is still classified
    Given there is a list named "Weekly Groceries"
    When I POST an item with text "Call Dentist" to "Weekly Groceries" via the API
    Then the product "call dentist" is categorized as "Other"
```

- [ ] **Step 4: Write the steps**

`Application/Frigorino.IntegrationTests/Slices/Lists/ExtractionApiSteps.cs` (mirrors `ClassificationApiSteps`; reuses its `POST` and `categorized as` steps if they're in the same assembly — if Reqnroll reports a duplicate step binding, remove the duplicated `[When]`/`categorized` bindings here and rely on `ClassificationApiSteps`). Add the quantity polling step:

```csharp
using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ExtractionApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I POST an item with text {string} to {string} via the API")]
    public async Task WhenIPostAnItem(string text, string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryCreateListItemAsync(listId, text);
        Assert.Equal(201, ctx.LastApiResponse.Status);
    }

    [Then("the list item eventually has text {string} with quantity {int} unit {int}")]
    public async Task ThenItemHasQuantity(string expectedText, int value, int unit)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ListItems.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Text == expectedText
                    && i.List.HouseholdId == ctx.HouseholdId);
            if (item is not null && item.QuantityValue == value
                && item.QuantityUnit == (QuantityUnit)unit)
            {
                return;
            }
            await Task.Delay(100);
        }
        Assert.Fail($"Item '{expectedText}' with quantity {value}/{unit} did not appear in time.");
    }
}
```

> If `WhenIPostAnItem` collides with `ClassificationApiSteps.WhenIPostAnItemWithTextViaTheApi` (same step text), delete the one here and keep the existing binding. The `[Then] ... categorized as` step is already provided by `ClassificationApiSteps`.

- [ ] **Step 5: Run the new integration scenarios**

Ensure Docker Desktop is running (Testcontainers). Run:
```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Extraction"
```
Expected: 3 scenarios PASS. (If Docker is unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.IntegrationTests
git commit -m "test: API integration coverage for inline quantity extraction"
```

---

### Task 13: Final verification gate

- [ ] **Step 1: Full backend + integration suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: all green. (The known-flaky inventory "undo delete in toast" scenario may time out intermittently — re-run the inventory scenarios to confirm flake, not regression, per `project_flaky_undo_toast_it`.)

- [ ] **Step 2: Frontend verification**

From `ClientApp/`: `npm run lint` && `npm run tsc` && `npm run prettier`.
Expected: all pass.

- [ ] **Step 3: Docker build (catches Dockerfile/SPA/pipeline drift)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: build succeeds. (If the Docker daemon is unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 4: Manual smoke (optional, with real key)**

With `Ai:ApiKey` set and both features enabled, `dev-up`, add "2 bottles of beer" → item becomes "beer" + "2 bottle" chip a beat later; tap the chip → popover edits it; add "milk" → no quantity, classified in the catalog.

- [ ] **Step 5: Finish the branch**

Use **superpowers:finishing-a-development-branch**. Remind the user of the ⚠️ Railway env-var rename (`Classifier__*` → `Ai__*`) before stage/prod deploy.

---

## Self-review

**Spec coverage:** inline-extraction-primary (T9 removes composer entry; extraction writes back T6) ✓; two-pipeline/two-model chained (T6 chains to classification; T4 keyed clients) ✓; digit-gate (T7) ✓; text-rewrite (T3 `ApplyExtractedQuantity`) ✓; `IQuantityExtractor` port + OpenAI adapter (T2/T5) ✓; shared-key config + per-feature model (T4) ✓; bounded single-item poll, no SignalR (T11) ✓; simple edit popover (T10) ✓; lists-only migration, total data loss (T3) ✓; update modes distinct — quantity-only update doesn't extract (T7 update only triggers on `Text is not null`) ✓; disabled/failure no-ops (T6 fail path, T7 Null trigger) ✓.

**Deviations from spec (decided with user during planning):** no `Quantity.TryParse`, no backfill, no `Dimension` metadata, `QuantityUnit` integer-on-wire (not string). All recorded in the header.

**Type consistency:** `Quantity.Create(decimal, QuantityUnit) → Result<Quantity>`; `QuantityExtraction(string CleanName, Quantity? Quantity)`; `IQuantityExtractionTrigger.OnItemEntered(int, int, int, string)`; `IExtractQuantityJob.Run(int householdId, int listId, int itemId, string rawText, CT)`; `QuantityDto(decimal Value, QuantityUnit Unit)`; `ListItem.QuantityValue/QuantityUnit`; `AiKeys.Classifier/Extractor` — used consistently across tasks.

**Known soft spots for the implementer:** the exact list-page/row file paths (T9 Step 4, T10 Step 5, T11 Steps 2–3) require a Grep for `ListFooter`/`ListItemContent`/`useCreateListItem` under `src/features/lists/`; possible Reqnroll step-binding collision (T12 Step 4, flagged inline).


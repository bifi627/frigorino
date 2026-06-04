# Persist the Promote-to-Inventory Batch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the pending-promotion batch from device-scoped `localStorage` to the database (a per-`ListItem` promoted/skipped distinction), so every household member shares the same per-list promote batch.

**Architecture:** Three nullable columns on `ListItem` — `PromotionExpiryHandling`, `PromotionSuggestedExpiry`, `PromotionResolvedAt`. "Pending" is the pure column predicate `Status && PromotionExpiryHandling != null && PromotionResolvedAt == null`. The candidacy stamp is captured by the existing toggle slice (which already computes the suggestion); promote/skip stamp `PromotionResolvedAt` via aggregate methods. Reads: a count on `GetList` + a lazy detail slice. The existing promote-sheet UX is preserved exactly; only the data source changes.

**Tech Stack:** .NET 10 vertical slices, EF Core (Postgres), FluentResults, xUnit; React 19 + TanStack Query + hey-api generated client + Zustand.

**Spec:** `docs/superpowers/specs/2026-06-04-persist-promote-batch-design.md`

---

## File Structure

**Backend — modify:**
- `Application/Frigorino.Domain/Entities/ListItem.cs` — three promotion columns.
- `Application/Frigorino.Domain/Entities/List.cs` — `ToggleItemStatus` clears on uncheck; new `ApplyPromotionSuggestion`, `ResolvePromotion`.
- `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs` — column mappings + composite index.
- `Application/Frigorino.Features/Lists/ListResponse.cs` — `PendingPromotionCount`.
- `Application/Frigorino.Features/Lists/CreateList.cs`, `UpdateList.cs` — `From(...)` call sites.
- `Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs` — stamp candidacy in the same `SaveChanges`.
- `Application/Frigorino.Web/Program.cs` — register three new slices.

**Backend — create:**
- `Application/Frigorino.Features/Lists/Promote/GetPendingPromotions.cs` — detail read slice + response DTO.
- `Application/Frigorino.Features/Lists/Promote/PromoteListItems.cs` — batch promote write slice + request/response DTOs.
- `Application/Frigorino.Features/Lists/Promote/SkipPromotion.cs` — skip write slice + request DTO.
- `Application/Frigorino.Test/Domain/ListAggregatePromotionTests.cs` — aggregate unit tests.

**Frontend — create:**
- `ClientApp/src/features/lists/promote/usePendingPromotions.ts` — query hook.
- `ClientApp/src/features/lists/promote/usePromoteListItems.ts` — mutation hook.
- `ClientApp/src/features/lists/promote/useSkipPromotion.ts` — mutation hook.

**Frontend — modify:**
- `ClientApp/src/features/lists/promote/PromoteBar.tsx` — count from `useList`.
- `ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx` — server data + new mutations.
- `ClientApp/src/features/lists/items/useToggleListItemStatus.ts` — drop store side-effect, invalidate `getList`.

**Frontend — delete:**
- `ClientApp/src/features/lists/promote/promotableStore.ts`.

---

## Task 1: Domain — promotion columns + aggregate methods (TDD)

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/ListItem.cs`
- Modify: `Application/Frigorino.Domain/Entities/List.cs:373-387` (`ToggleItemStatus`)
- Test: `Application/Frigorino.Test/Domain/ListAggregatePromotionTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Domain/ListAggregatePromotionTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain
{
    // Unit tests for the List aggregate's promotion-to-inventory state transitions.
    // Pending = Status && PromotionExpiryHandling != null && PromotionResolvedAt == null.
    public class ListAggregatePromotionTests
    {
        [Fact]
        public void ApplyPromotionSuggestion_PerishableItem_StampsCandidacyAndClearsResolved()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true);
            item.PromotionResolvedAt = DateTime.UtcNow.AddDays(-1); // stale resolution

            var expiry = new DateOnly(2026, 6, 20);
            var result = list.ApplyPromotionSuggestion(
                item.Id, ExpiryHandling.AiRecommendsShelfLife, expiry);

            Assert.True(result.IsSuccess);
            Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, item.PromotionExpiryHandling);
            Assert.Equal(expiry, item.PromotionSuggestedExpiry);
            Assert.Null(item.PromotionResolvedAt);
        }

        [Fact]
        public void ApplyPromotionSuggestion_NonPerishable_LeavesCandidacyNull()
        {
            var list = NewList();
            var item = AddSeed(list, "Salt", status: true);

            var result = list.ApplyPromotionSuggestion(item.Id, handling: null, suggestedExpiry: null);

            Assert.True(result.IsSuccess);
            Assert.Null(item.PromotionExpiryHandling);
            Assert.Null(item.PromotionSuggestedExpiry);
            Assert.Null(item.PromotionResolvedAt);
        }

        [Fact]
        public void ApplyPromotionSuggestion_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.ApplyPromotionSuggestion(999, ExpiryHandling.UserEntersFromPackage, null);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void ToggleItemStatus_Uncheck_ClearsPromotionState()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true,
                sortOrder: SortOrderCalculator.CheckedMinRange + SortOrderCalculator.DefaultGap);
            item.PromotionExpiryHandling = ExpiryHandling.AiRecommendsShelfLife;
            item.PromotionSuggestedExpiry = new DateOnly(2026, 6, 20);
            item.PromotionResolvedAt = null;

            var result = list.ToggleItemStatus(item.Id); // checked -> unchecked

            Assert.True(result.IsSuccess);
            Assert.False(item.Status);
            Assert.Null(item.PromotionExpiryHandling);
            Assert.Null(item.PromotionSuggestedExpiry);
            Assert.Null(item.PromotionResolvedAt);
        }

        [Fact]
        public void ResolvePromotion_PendingItem_StampsResolvedAt()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true);
            item.PromotionExpiryHandling = ExpiryHandling.AiRecommendsShelfLife;

            var when = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
            var result = list.ResolvePromotion(item.Id, when);

            Assert.True(result.IsSuccess);
            Assert.Equal(when, item.PromotionResolvedAt);
        }

        [Fact]
        public void ResolvePromotion_AlreadyResolved_IsIdempotentNoOp()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", status: true);
            var first = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
            item.PromotionResolvedAt = first;

            var result = list.ResolvePromotion(item.Id, new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc));

            Assert.True(result.IsSuccess);
            Assert.Equal(first, item.PromotionResolvedAt); // unchanged — first writer wins
        }

        [Fact]
        public void ResolvePromotion_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.ResolvePromotion(999, DateTime.UtcNow);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        // ------- Helpers -------

        private const string CreatorId = "user-creator";
        private const int HouseholdId = 42;

        private static List NewList()
        {
            return new List
            {
                Id = 1,
                Name = "Groceries",
                HouseholdId = HouseholdId,
                CreatedByUserId = CreatorId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsActive = true,
            };
        }

        private int _nextItemId = 100;

        private ListItem AddSeed(List list, string text, bool status = false, int? sortOrder = null)
        {
            var item = new ListItem
            {
                Id = ++_nextItemId,
                ListId = list.Id,
                Text = text,
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
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregatePromotionTests"`
Expected: FAIL — `List` has no `ApplyPromotionSuggestion` / `ResolvePromotion`; `ListItem` has no promotion properties (compile errors).

- [ ] **Step 3: Add the columns to `ListItem`**

In `Application/Frigorino.Domain/Entities/ListItem.cs`, add the `Frigorino.Domain.Products` using and the three properties after the `IsActive` property (line 55), before the navigation properties:

```csharp
using Frigorino.Domain.Products;
```

```csharp
        // Promotion-to-inventory state (replaces the device-local localStorage batch). All null
        // for items never checked-while-perishable. Pending promotion =
        //   Status && PromotionExpiryHandling != null && PromotionResolvedAt == null.
        // Stamped/cleared exclusively by List aggregate methods (ToggleItemStatus,
        // ApplyPromotionSuggestion, ResolvePromotion).
        public ExpiryHandling? PromotionExpiryHandling { get; set; }
        public DateOnly? PromotionSuggestedExpiry { get; set; }
        public DateTime? PromotionResolvedAt { get; set; }
```

- [ ] **Step 4: Clear promotion state on uncheck in `ToggleItemStatus`**

In `Application/Frigorino.Domain/Entities/List.cs`, replace the body of `ToggleItemStatus` (lines 373-387) with:

```csharp
        public Result<ListItem> ToggleItemStatus(int itemId)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            var newStatus = !item.Status;
            item.SortOrder = ComputeAppendSortOrder(targetStatus: newStatus);
            item.Status = newStatus;

            // Unchecking retracts any promotion candidacy/resolution so a later re-check is a clean
            // re-evaluation — mirrors the old localStorage "uncheck removes from the batch" contract.
            if (!newStatus)
            {
                item.PromotionExpiryHandling = null;
                item.PromotionSuggestedExpiry = null;
                item.PromotionResolvedAt = null;
            }

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }
```

- [ ] **Step 5: Add `ApplyPromotionSuggestion` and `ResolvePromotion` to `List`**

In `Application/Frigorino.Domain/Entities/List.cs`, add the `Frigorino.Domain.Products` using at the top (alongside the existing usings), then insert these two methods immediately after `ToggleItemStatus` (after line 387):

```csharp
using Frigorino.Domain.Products;
```

```csharp
        // Stamps the promotion candidacy captured when the item was checked (perishable product →
        // handling + suggested expiry; non-perishable → both null) and resets resolution so a
        // freshly-checked item becomes pending again. The handler supplies the suggestion because
        // it derives from the Product catalog (a different aggregate the entity must not touch).
        public Result<ListItem> ApplyPromotionSuggestion(int itemId, ExpiryHandling? handling, DateOnly? suggestedExpiry)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            item.PromotionExpiryHandling = handling;
            item.PromotionSuggestedExpiry = suggestedExpiry;
            item.PromotionResolvedAt = null;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }

        // Marks a pending-promotion item as dealt with — promoted into inventory OR skipped (X /
        // Clear All). Idempotent: an already-resolved item is a no-op success, so two members racing
        // the same shared batch (Person A + Person B) don't error — first writer wins.
        public Result ResolvePromotion(int itemId, DateTime resolvedAt)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            if (item.PromotionResolvedAt is null)
            {
                item.PromotionResolvedAt = resolvedAt;
                item.UpdatedAt = resolvedAt;
            }
            return Result.Ok();
        }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregatePromotionTests"`
Expected: PASS (7 tests).

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.Domain/Entities/ListItem.cs Application/Frigorino.Domain/Entities/List.cs Application/Frigorino.Test/Domain/ListAggregatePromotionTests.cs
git commit -m "feat(domain): add ListItem promotion state + aggregate methods"
```

---

## Task 2: EF configuration + migration

**Files:**
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs`
- Create: migration under `Application/Frigorino.Infrastructure/Migrations/`

- [ ] **Step 1: Map the new columns + add the composite index**

In `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs`, add the property mappings after the `FileSizeBytes` property (line 42), before the `QuantityValue` block:

```csharp
            // Promotion-to-inventory state. PromotionExpiryHandling is a nullable enum → nullable
            // int column (matches the QuantityUnit convention below).
            builder.Property(li => li.PromotionExpiryHandling);
            builder.Property(li => li.PromotionSuggestedExpiry);
            builder.Property(li => li.PromotionResolvedAt);
```

Then add the index alongside the other `HasIndex` calls (after line 79):

```csharp
            // Supports the pending-promotion count (GetList) and the pending-promotions detail read.
            builder.HasIndex(li => new { li.ListId, li.Status, li.PromotionResolvedAt });
```

- [ ] **Step 2: Generate the migration**

Run:
```bash
dotnet ef migrations add AddListItemPromotionState --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: creates `Application/Frigorino.Infrastructure/Migrations/<timestamp>_AddListItemPromotionState.cs` adding three nullable columns (`PromotionExpiryHandling` int?, `PromotionSuggestedExpiry` date?, `PromotionResolvedAt` timestamptz?) and the index. No data backfill (existing rows default to null = not a candidate).

- [ ] **Step 3: Build to verify the migration compiles**

Run: `dotnet build Application/Frigorino.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs Application/Frigorino.Infrastructure/Migrations/
git commit -m "feat(infra): persist ListItem promotion columns + migration"
```

---

## Task 3: `PendingPromotionCount` on the list read

**Files:**
- Modify: `Application/Frigorino.Features/Lists/ListResponse.cs`
- Modify: `Application/Frigorino.Features/Lists/CreateList.cs:53`
- Modify: `Application/Frigorino.Features/Lists/UpdateList.cs:66-70`
- Test: `Application/Frigorino.Test/Features/ListResponsePendingPromotionTests.cs` (create)

- [ ] **Step 1: Write the failing projection test**

Create `Application/Frigorino.Test/Features/ListResponsePendingPromotionTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Features.Lists;

namespace Frigorino.Test.Features
{
    // Verifies the EF-translatable projection counts only checked, candidate, unresolved items.
    public class ListResponsePendingPromotionTests
    {
        [Fact]
        public void ToProjection_CountsOnlyCheckedCandidateUnresolved()
        {
            var list = new List
            {
                Id = 1,
                Name = "Groceries",
                HouseholdId = 42,
                CreatedByUserId = "u1",
                CreatedByUser = new User { ExternalId = "u1", Name = "U", Email = "u@e.com" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
            };
            // Pending: checked + candidate + unresolved.
            list.ListItems.Add(Item(1, status: true, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: null));
            // Not pending: resolved.
            list.ListItems.Add(Item(2, status: true, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: DateTime.UtcNow));
            // Not pending: not a candidate (handling null).
            list.ListItems.Add(Item(3, status: true, handling: null, resolvedAt: null));
            // Not pending: unchecked.
            list.ListItems.Add(Item(4, status: false, handling: ExpiryHandling.AiRecommendsShelfLife, resolvedAt: null));

            var projected = ListResponse.ToProjection.Compile()(list);

            Assert.Equal(1, projected.PendingPromotionCount);
        }

        private static ListItem Item(int id, bool status, ExpiryHandling? handling, DateTime? resolvedAt)
        {
            return new ListItem
            {
                Id = id,
                ListId = 1,
                Text = "x",
                Status = status,
                IsActive = true,
                PromotionExpiryHandling = handling,
                PromotionResolvedAt = resolvedAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListResponsePendingPromotionTests"`
Expected: FAIL — `ListResponse` has no `PendingPromotionCount` (compile error).

- [ ] **Step 3: Add the field to `ListResponse`**

In `Application/Frigorino.Features/Lists/ListResponse.cs`, add `int PendingPromotionCount` as the last positional parameter of the record (after `CheckedCount` on line 15):

```csharp
    public sealed record ListResponse(
        int Id,
        string Name,
        string? Description,
        int HouseholdId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        ListCreatorResponse CreatedByUser,
        int UncheckedCount,
        int CheckedCount,
        int PendingPromotionCount)
```

Update `From` (add the param + pass it through):

```csharp
        public static ListResponse From(List list, User creator, int uncheckedCount, int checkedCount, int pendingPromotionCount)
        {
            return new ListResponse(
                list.Id,
                list.Name,
                list.Description,
                list.HouseholdId,
                list.CreatedAt,
                list.UpdatedAt,
                new ListCreatorResponse(creator.ExternalId, creator.Name, creator.Email),
                uncheckedCount,
                checkedCount,
                pendingPromotionCount);
        }
```

Update `ToProjection` (add the pending count as the final argument):

```csharp
        public static readonly Expression<Func<List, ListResponse>> ToProjection = l => new ListResponse(
            l.Id,
            l.Name,
            l.Description,
            l.HouseholdId,
            l.CreatedAt,
            l.UpdatedAt,
            new ListCreatorResponse(l.CreatedByUser.ExternalId, l.CreatedByUser.Name, l.CreatedByUser.Email),
            l.ListItems.Count(i => i.IsActive && !i.Status),
            l.ListItems.Count(i => i.IsActive && i.Status),
            l.ListItems.Count(i => i.IsActive && i.Status && i.PromotionExpiryHandling != null && i.PromotionResolvedAt == null));
```

- [ ] **Step 4: Update the two `From` call sites**

In `Application/Frigorino.Features/Lists/CreateList.cs` line 53 (a freshly created list has zero pending):

```csharp
            var response = ListResponse.From(list, creator, uncheckedCount: 0, checkedCount: 0, pendingPromotionCount: 0);
```

In `Application/Frigorino.Features/Lists/UpdateList.cs` lines 66-70:

```csharp
            var response = ListResponse.From(
                list,
                list.CreatedByUser,
                list.ListItems.Count(i => i.IsActive && !i.Status),
                list.ListItems.Count(i => i.IsActive && i.Status),
                list.ListItems.Count(i => i.IsActive && i.Status && i.PromotionExpiryHandling != null && i.PromotionResolvedAt == null));
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListResponsePendingPromotionTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Lists/ListResponse.cs Application/Frigorino.Features/Lists/CreateList.cs Application/Frigorino.Features/Lists/UpdateList.cs Application/Frigorino.Test/Features/ListResponsePendingPromotionTests.cs
git commit -m "feat(lists): expose PendingPromotionCount on the list read"
```

---

## Task 4: Pending-promotions detail read slice

**Files:**
- Create: `Application/Frigorino.Features/Lists/Promote/GetPendingPromotions.cs`
- Modify: `Application/Frigorino.Web/Program.cs:315-322`

- [ ] **Step 1: Create the slice + response DTO**

Create `Application/Frigorino.Features/Lists/Promote/GetPendingPromotions.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Products;
using Frigorino.Features.Households;
using Frigorino.Features.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Promote
{
    // One pending-promotion candidate for the review sheet. Projected straight from the stored
    // promotion columns — the candidacy/suggestion was captured at check time, so no Product join.
    public sealed record PendingPromotionResponse(
        int ListItemId,
        string Text,
        QuantityDto? Quantity,
        ExpiryHandling ExpiryHandling,
        DateOnly? SuggestedExpiry);

    public static class GetPendingPromotionsEndpoint
    {
        public static IEndpointRouteBuilder MapGetPendingPromotions(this IEndpointRouteBuilder app)
        {
            app.MapGet("/{listId:int}/pending-promotions", Handle)
               .WithName("GetPendingPromotions")
               .Produces<List<PendingPromotionResponse>>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<List<PendingPromotionResponse>>, NotFound>> Handle(
            int householdId,
            int listId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var listExists = await db.Lists
                .AnyAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (!listExists)
            {
                return TypedResults.NotFound();
            }

            var pending = await db.ListItems
                .Where(i => i.ListId == listId
                            && i.IsActive
                            && i.Status
                            && i.PromotionExpiryHandling != null
                            && i.PromotionResolvedAt == null)
                .OrderBy(i => i.SortOrder)
                .Select(i => new PendingPromotionResponse(
                    i.Id,
                    i.Text,
                    i.QuantityValue == null
                        ? null
                        : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
                    i.PromotionExpiryHandling!.Value,
                    i.PromotionSuggestedExpiry))
                .ToListAsync(ct);

            return TypedResults.Ok(pending);
        }
    }
}
```

- [ ] **Step 2: Register the slice on the lists group**

In `Application/Frigorino.Web/Program.cs`, add the `Frigorino.Features.Lists.Promote` import region (the file uses fully-qualified extension calls — add the call alongside the other `lists.Map*` calls after line 322, line 322 is `lists.MapDeleteList();`):

```csharp
lists.MapGetPendingPromotions();
```

Ensure the using is present at the top of `Program.cs` (add if missing):

```csharp
using Frigorino.Features.Lists.Promote;
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded (also regenerates `ClientApp/src/lib/openapi.json`).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Lists/Promote/GetPendingPromotions.cs Application/Frigorino.Web/Program.cs
git commit -m "feat(lists): add pending-promotions detail read slice"
```

---

## Task 5: Promote batch write slice (cross-aggregate)

**Files:**
- Create: `Application/Frigorino.Features/Lists/Promote/PromoteListItems.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

This is the cross-aggregate slice (List + Inventory). `Households/Members/AddMember.cs` is the precedent.

- [ ] **Step 1: Create the slice + request/response DTOs**

Create `Application/Frigorino.Features/Lists/Promote/PromoteListItems.cs`:

```csharp
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Promote
{
    // One row from the promote sheet: a checked candidate the user selected, with the (possibly
    // edited) quantity/expiry to write into inventory.
    public sealed record PromoteEntry(int ListItemId, QuantityDto? Quantity, DateOnly? ExpiryDate);

    public sealed record PromoteListItemsRequest(int InventoryId, List<PromoteEntry> Items);

    public sealed record PromoteListItemsResponse(int PromotedCount);

    public static class PromoteListItemsEndpoint
    {
        public static IEndpointRouteBuilder MapPromoteListItems(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{listId:int}/promote", Handle)
               .WithName("PromoteListItems")
               .Produces<PromoteListItemsResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Ok<PromoteListItemsResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int listId,
            PromoteListItemsRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            if (request.Items is null || request.Items.Count == 0)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Items)] = ["At least one item is required."],
                });
            }

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            var inventory = await db.Inventories
                .Include(i => i.InventoryItems)
                .FirstOrDefaultAsync(i => i.Id == request.InventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var now = DateTime.UtcNow;
            var promoted = 0;
            foreach (var entry in request.Items)
            {
                var sourceItem = list.ListItems.FirstOrDefault(i => i.Id == entry.ListItemId && i.IsActive);
                if (sourceItem is null)
                {
                    return TypedResults.NotFound();
                }

                // Already resolved by a racing member → skip silently (idempotent batch).
                if (sourceItem.PromotionResolvedAt is not null)
                {
                    continue;
                }

                Quantity? quantity = null;
                if (entry.Quantity is not null)
                {
                    var parsed = Quantity.Create(entry.Quantity.Value, entry.Quantity.Unit);
                    if (parsed.IsFailed)
                    {
                        return parsed.ToValidationProblem();
                    }
                    quantity = parsed.Value;
                }

                var added = inventory.AddItem(sourceItem.Text, quantity, entry.ExpiryDate);
                if (added.IsFailed)
                {
                    return added.ToValidationProblem();
                }

                var resolved = list.ResolvePromotion(sourceItem.Id, now);
                if (resolved.IsFailed)
                {
                    // ResolvePromotion only fails EntityNotFound, already excluded above.
                    return TypedResults.NotFound();
                }
                promoted++;
            }

            await db.SaveChangesAsync(ct);

            return TypedResults.Ok(new PromoteListItemsResponse(promoted));
        }
    }
}
```

- [ ] **Step 2: Register the slice**

In `Application/Frigorino.Web/Program.cs`, after the `lists.MapGetPendingPromotions();` line:

```csharp
lists.MapPromoteListItems();
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Lists/Promote/PromoteListItems.cs Application/Frigorino.Web/Program.cs
git commit -m "feat(lists): add atomic batch promote-to-inventory slice"
```

---

## Task 6: Skip write slice (X / Clear All)

**Files:**
- Create: `Application/Frigorino.Features/Lists/Promote/SkipPromotion.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Create the slice + request DTO**

Create `Application/Frigorino.Features/Lists/Promote/SkipPromotion.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Promote
{
    // X (one id) or Clear All (all pending ids) — resolve without writing to inventory.
    public sealed record SkipPromotionRequest(List<int> ListItemIds);

    public static class SkipPromotionEndpoint
    {
        public static IEndpointRouteBuilder MapSkipPromotion(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{listId:int}/promote/skip", Handle)
               .WithName("SkipPromotion")
               .Produces(StatusCodes.Status204NoContent)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<NoContent, NotFound>> Handle(
            int householdId,
            int listId,
            SkipPromotionRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var list = await db.Lists
                .Include(l => l.ListItems)
                .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            var now = DateTime.UtcNow;
            foreach (var itemId in request.ListItemIds ?? new List<int>())
            {
                // No-op for ids not on the list or already resolved (idempotent); never 404 a skip.
                list.ResolvePromotion(itemId, now);
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
    }
}
```

Note: `ResolvePromotion` returns `EntityNotFoundError` for unknown ids, but the skip handler intentionally ignores its result — a stale id from a racing client must not fail the whole skip. Resolution is idempotent and harmless.

- [ ] **Step 2: Register the slice**

In `Application/Frigorino.Web/Program.cs`, after the `lists.MapPromoteListItems();` line:

```csharp
lists.MapSkipPromotion();
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Lists/Promote/SkipPromotion.cs Application/Frigorino.Web/Program.cs
git commit -m "feat(lists): add skip-promotion slice for X / clear-all"
```

---

## Task 7: Stamp candidacy inside the toggle slice

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs:47-77`

The product lookup currently runs *after* `SaveChangesAsync` (response-only). Move it before the save and stamp via `ApplyPromotionSuggestion`, so candidacy persists in the same unit of work. Uncheck clears via the aggregate (Task 1). The `Promote` field still rides the response unchanged.

- [ ] **Step 1: Rewrite the handler body**

In `Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs`, replace lines 47-77 (from `var result = list.ToggleItemStatus(itemId);` through the final `return TypedResults.Ok(response);`) with:

```csharp
            var result = list.ToggleItemStatus(itemId);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                throw new InvalidOperationException(
                    $"ToggleItemStatus cannot map error of type {first.GetType().Name}.");
            }

            // Only when the item is now checked DONE do we look up its product (one indexed point
            // lookup on the unique (HouseholdId, NormalizedName)) and capture a promote suggestion.
            // The suggestion is persisted on the item (shared, durable batch) AND returned in the
            // response. Un-checking clears promotion state in the aggregate (no lookup needed).
            PromoteSuggestion? suggestion = null;
            if (result.Value.Status)
            {
                var normalized = ProductName.Normalize(result.Value.Text);
                var product = await db.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p => p.HouseholdId == householdId && p.NormalizedName == normalized, ct);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                suggestion = PromoteSuggestion.For(product, today);

                list.ApplyPromotionSuggestion(
                    result.Value.Id, suggestion?.ExpiryHandling, suggestion?.SuggestedExpiry);
            }

            await db.SaveChangesAsync(ct);

            var response = ListItemResponse.From(result.Value) with { Promote = suggestion };
            return TypedResults.Ok(response);
```

- [ ] **Step 2: Build + run the toggle-related tests**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded.

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregate"`
Expected: PASS (existing toggle/sort-order tests + the new promotion tests).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs
git commit -m "feat(lists): persist promotion candidacy when toggling an item checked"
```

---

## Task 8: Regenerate the TypeScript API client

**Files:**
- Modify: `ClientApp/src/lib/api/**` (generated), `ClientApp/src/lib/openapi.json`

- [ ] **Step 1: Regenerate**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run api
```
Expected: rebuilds the backend, emits `openapi.json`, regenerates the client. New symbols appear in `src/lib/api/@tanstack/react-query.gen.ts`: `getPendingPromotionsOptions` / `getPendingPromotionsQueryKey`, `promoteListItemsMutation`, `skipPromotionMutation`; new types in `types.gen.ts`: `PendingPromotionResponse`, `PromoteListItemsRequest`, `PromoteEntry`, `SkipPromotionRequest`; `ListResponse` gains `pendingPromotionCount`.

- [ ] **Step 2: Type-check**

Run (from `ClientApp/`): `npm run tsc`
Expected: passes (existing code unaffected; `PromoteSuggestion` type unchanged).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/api Application/Frigorino.Web/ClientApp/src/lib/openapi.json
git commit -m "chore(api): regenerate client for promote-batch endpoints"
```

---

## Task 9: Frontend hooks (query + two mutations)

**Files:**
- Create: `ClientApp/src/features/lists/promote/usePendingPromotions.ts`
- Create: `ClientApp/src/features/lists/promote/usePromoteListItems.ts`
- Create: `ClientApp/src/features/lists/promote/useSkipPromotion.ts`

Follows the API hook conventions (`features/lists/useList.ts` query template, `features/lists/useDeleteList.ts` mutation template). Mutation hooks are arg-less; callers pass `{ path, body }`.

- [ ] **Step 1: Create the query hook**

Create `ClientApp/src/features/lists/promote/usePendingPromotions.ts`:

```typescript
import { useQuery } from "@tanstack/react-query";
import { getPendingPromotionsOptions } from "../../../lib/api/@tanstack/react-query.gen";

// Server-shared pending-promotion batch for a list. Replaces the device-local promotableStore.
// `enabled` lets the sheet fetch only when opened.
export const usePendingPromotions = (
    householdId: number,
    listId: number,
    enabled = true,
) =>
    useQuery({
        ...getPendingPromotionsOptions({ path: { householdId, listId } }),
        enabled: enabled && householdId > 0 && listId > 0,
        staleTime: 1000 * 30,
    });
```

- [ ] **Step 2: Create the promote mutation hook**

Create `ClientApp/src/features/lists/promote/usePromoteListItems.ts`:

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getInventoryItemsQueryKey,
    getListQueryKey,
    getPendingPromotionsQueryKey,
    promoteListItemsMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

// Atomic batch promote. Caller passes { path: { householdId, listId }, body: { inventoryId, items } }.
// Invalidates the pending batch, the list (for PendingPromotionCount), and the target inventory.
export const usePromoteListItems = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...promoteListItemsMutation(),
        onSuccess: (_data, variables) => {
            const { householdId, listId } = variables.path;
            queryClient.invalidateQueries({
                queryKey: getPendingPromotionsQueryKey({
                    path: { householdId, listId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getListQueryKey({ path: { householdId, listId } }),
            });
            queryClient.invalidateQueries({
                queryKey: getInventoryItemsQueryKey({
                    path: { householdId, inventoryId: variables.body.inventoryId },
                }),
            });
        },
    });
};
```

- [ ] **Step 3: Create the skip mutation hook**

Create `ClientApp/src/features/lists/promote/useSkipPromotion.ts`:

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getListQueryKey,
    getPendingPromotionsQueryKey,
    skipPromotionMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

// Resolve-as-skipped (X = one id, Clear All = all pending ids). Caller passes
// { path: { householdId, listId }, body: { listItemIds } }.
export const useSkipPromotion = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...skipPromotionMutation(),
        onSuccess: (_data, variables) => {
            const { householdId, listId } = variables.path;
            queryClient.invalidateQueries({
                queryKey: getPendingPromotionsQueryKey({
                    path: { householdId, listId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getListQueryKey({ path: { householdId, listId } }),
            });
        },
    });
};
```

- [ ] **Step 4: Type-check**

Run (from `ClientApp/`): `npm run tsc`
Expected: passes.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/promote/usePendingPromotions.ts Application/Frigorino.Web/ClientApp/src/features/lists/promote/usePromoteListItems.ts Application/Frigorino.Web/ClientApp/src/features/lists/promote/useSkipPromotion.ts
git commit -m "feat(web): add promote-batch query + mutation hooks"
```

---

## Task 10: PromoteBar — count from the list query

**Files:**
- Modify: `ClientApp/src/features/lists/promote/PromoteBar.tsx`

- [ ] **Step 1: Replace the store selector with the list query count**

In `ClientApp/src/features/lists/promote/PromoteBar.tsx`, replace the import of `usePromotableForList` (line 6) with the list hook, and derive the count from `pendingPromotionCount`:

Replace line 6:
```typescript
import { useList } from "../useList";
```

Replace the body's `entries`-based logic (lines 17-29) with:
```typescript
    const { t } = useTranslation();
    const { data: list } = useList(householdId, listId);
    const count = list?.pendingPromotionCount ?? 0;
    const [open, setOpen] = useState(false);

    useEffect(() => {
        if (count === 0) {
            setOpen(false);
        }
    }, [count]);

    if (count === 0) {
        return null;
    }
```

Update the two `entries.length` references in the JSX (the `data-count` attribute and the `t("promote.barReady", { count })` call) to use `count`:
```typescript
                data-count={count}
```
```typescript
                    {t("promote.barReady", { count })}
```

- [ ] **Step 2: Type-check**

Run (from `ClientApp/`): `npm run tsc`
Expected: passes.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteBar.tsx
git commit -m "feat(web): drive PromoteBar from server PendingPromotionCount"
```

---

## Task 11: PromoteReviewSheet + toggle hook — server data, delete localStorage store

**Files:**
- Modify: `ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx`
- Modify: `ClientApp/src/features/lists/items/useToggleListItemStatus.ts`
- Delete: `ClientApp/src/features/lists/promote/promotableStore.ts`

- [ ] **Step 1: Rewrite the sheet to read from the server and call the new mutations**

In `ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx`:

Replace the store import (lines 29-33) with the server hooks and the generated type:
```typescript
import { usePendingPromotions } from "./usePendingPromotions";
import { usePromoteListItems } from "./usePromoteListItems";
import { useSkipPromotion } from "./useSkipPromotion";
import type { PendingPromotionResponse } from "../../../lib/api/types.gen";
```

Replace the data/mutation wiring (lines 57-61) with:
```typescript
    const { t } = useTranslation();
    const { data: entries = [] } = usePendingPromotions(
        householdId,
        listId,
        open,
    );
    const promote = usePromoteListItems();
    const skip = useSkipPromotion();
```

The `PromotableEntry` row shape is replaced by `PendingPromotionResponse` (fields: `listItemId`, `text`, `quantity`, `expiryHandling`, `suggestedExpiry`). Update the drafts seeding (lines 75-85) to key on `listItemId`:
```typescript
    const [drafts, setDrafts] = useState<Record<number, RowDraft>>({});
    const seeded = useMemo(() => {
        const next: Record<number, RowDraft> = {};
        for (const e of entries) {
            next[e.listItemId] = drafts[e.listItemId] ?? {
                selected: true,
                expiry: e.suggestedExpiry ?? "",
                quantity: quantityToDraft(e.quantity ?? null),
            };
        }
        return next;
    }, [entries, drafts]);
```

Update `selectedCount`, `hasRowMissingDate`, `hasRowInvalidQuantity` to key on `e.listItemId` (replace each `e.itemId` with `e.listItemId`):
```typescript
    const selectedCount = entries.filter(
        (e) => seeded[e.listItemId]?.selected,
    ).length;

    const hasRowMissingDate = entries.some(
        (e) => seeded[e.listItemId]?.selected && !seeded[e.listItemId]?.expiry,
    );

    const hasRowInvalidQuantity = entries.some(
        (e) =>
            seeded[e.listItemId]?.selected &&
            !isDraftValid(seeded[e.listItemId].quantity),
    );
```

Replace `handleOmit` (X — single skip), `handleClearAll`, and `handleAdd` (lines 107-159):
```typescript
    const handleOmit = (itemId: number) => {
        skip.mutate({
            path: { householdId, listId },
            body: { listItemIds: [itemId] },
        });
        setDrafts((d) => {
            const next = { ...d };
            delete next[itemId];
            return next;
        });
    };

    const handleClearAll = () => {
        skip.mutate({
            path: { householdId, listId },
            body: { listItemIds: entries.map((e) => e.listItemId) },
        });
        setDrafts({});
        onClose();
    };

    const handleAdd = async () => {
        if (!targetId) return;
        const items = entries
            .filter((e) => seeded[e.listItemId]?.selected)
            .map((e) => {
                const draft = seeded[e.listItemId];
                return {
                    listItemId: e.listItemId,
                    quantity: draftToQuantity(draft.quantity),
                    expiryDate: draft.expiry || null,
                };
            });
        if (items.length === 0) return;
        try {
            const result = await promote.mutateAsync({
                path: { householdId, listId },
                body: { inventoryId: targetId, items },
            });
            if (result.promotedCount > 0) {
                toast.success(
                    t("promote.added", {
                        count: result.promotedCount,
                        inventory: targetName,
                    }),
                );
            }
            onClose();
        } catch {
            // Leave the batch intact on failure; the user can retry.
        }
    };
```

Update the `createItem.isPending` reference in the Add button's `disabled` prop (line 248) to `promote.isPending`. Update the `entries.map` render (lines 220-230) and the `PromoteRow` to use `entry.listItemId` for the key and `onChange`/`onOmit` wiring:
```typescript
                    {entries.map((entry) => (
                        <PromoteRow
                            key={entry.listItemId}
                            entry={entry}
                            draft={seeded[entry.listItemId]}
                            onChange={(patch) =>
                                updateDraft(entry.listItemId, patch)
                            }
                            onOmit={() => handleOmit(entry.listItemId)}
                        />
                    ))}
```

Update `PromoteRowProps` and `PromoteRow` (lines 266-301) to use `PendingPromotionResponse`:
```typescript
interface PromoteRowProps {
    entry: PendingPromotionResponse;
    draft: RowDraft;
    onChange: (patch: Partial<RowDraft>) => void;
    onOmit: () => void;
}

const PromoteRow = ({ entry, draft, onChange, onOmit }: PromoteRowProps) => {
    const { t } = useTranslation();
    const isRecommended = entry.expiryHandling === "AiRecommendsShelfLife";
```

Inside `PromoteRow`, replace the four `entry.name` references (the `data-testid` on the Box, the name `<Typography>`, and the two `data-testid` interpolations on the quantity inputs and the omit/expiry testids) with `entry.text`. The quantity block guard `entry.quantity &&` stays (now `entry.quantity` is the nullable `QuantityDto`).

- [ ] **Step 2: Drop the store side-effect from the toggle hook + invalidate the list**

In `ClientApp/src/features/lists/items/useToggleListItemStatus.ts`:

Remove the `usePromotableStore` import (line 9). Replace the whole `onSuccess` block (lines 67-85) — the store add/remove is gone; candidacy now lives server-side:
```typescript
        onSuccess: () => {
            // Promotion candidacy is stamped server-side by the toggle slice; the shared batch
            // count rides the list query. No client store to update anymore.
        },
```

Add a debounced `getList` invalidation in `onSettled` so the bar's `pendingPromotionCount` reconciles for the toggling member. Add the `getListQueryKey` import:
```typescript
import {
    getItemsQueryKey,
    getListQueryKey,
    toggleItemStatusMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
```
Replace `onSettled` (lines 86-98):
```typescript
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
            debouncedInvalidate(
                getListQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
        },
```

(If an empty `onSuccess` triggers an unused-variable lint, omit the `onSuccess` key entirely instead.)

- [ ] **Step 3: Delete the localStorage store**

Run:
```bash
git rm Application/Frigorino.Web/ClientApp/src/features/lists/promote/promotableStore.ts
```
Confirm nothing else imports it:
Run: `grep -rn "promotableStore" Application/Frigorino.Web/ClientApp/src`
Expected: no matches.

- [ ] **Step 4: Verify frontend (lint + tsc + prettier)**

Run (from `ClientApp/`):
```bash
npm run lint && npm run tsc && npm run prettier
```
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx Application/Frigorino.Web/ClientApp/src/features/lists/items/useToggleListItemStatus.ts
git commit -m "feat(web): server-shared promote sheet; remove localStorage batch"
```

---

## Task 12: Full verification + manual two-user check

**Files:** none (verification gate).

- [ ] **Step 1: Full backend + integration test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: all pass (Frigorino.Test + Frigorino.IntegrationTests). If a running dev backend locks `bin/Debug` DLLs, tear the stack down first (`/dev-down`).

- [ ] **Step 2: Frontend build**

Run (from `ClientApp/`): `npm run build`
Expected: `tsc -b && vite build` succeeds.

- [ ] **Step 3: Manual two-user verify (the core requirement)**

Bring up the dev stack (`/dev-up`). In two browser profiles signed into the same household (or one normal + one incognito):
- Person A checks off a perishable item → the PromoteBar count appears for Person A.
- Person B reloads/opens the same list → **PromoteBar shows the same count** (the whole point — shared state).
- Person B opens the sheet, deselects one item, picks an inventory, Promote → only selected items land in inventory; the deselected one stays pending for both.
- Person A reloads → sees the reduced pending count; Person B's earlier-skipped/X'd item is gone for both.
- Clear All on either side empties the batch for both.
- Uncheck a pending item on the list → it drops out of the batch; re-check → it returns as pending.

- [ ] **Step 4: Docker build (final drift gate)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: succeeds (catches Dockerfile/SPA/pipeline drift). If the Docker daemon is unreachable, ask the user to start Docker Desktop.

- [ ] **Step 5: Remove the shipped IDEAS.md entry**

Delete the "Persist the promote-to-inventory batch on the list (client feedback)" section from `IDEAS.md` (lines ~120-134) now that the work is implemented.

```bash
git add IDEAS.md
git commit -m "docs(ideas): drop shipped persist-promote-batch entry"
```

---

## Self-Review

**Spec coverage:**
- Data model (3 columns, pending predicate) → Task 1 + 2. ✓
- Candidacy lifecycle (capture at toggle, clear on uncheck, reset on recheck) → Task 1 (aggregate) + Task 7 (slice). ✓
- Reads (count on GetList + lazy detail slice) → Task 3 + Task 4. ✓
- Writes (atomic batch promote + skip, idempotent) → Task 5 + Task 6. ✓
- Frontend (delete store, count on bar, server-data sheet, toggle invalidation) → Tasks 8-11. ✓
- Preserved UX (checkbox default-on, X, Clear All, single target, close-does-nothing) → Task 11. ✓
- Cleanup task unchanged → no task (intentional, noted in spec). ✓
- Migration no-backfill → Task 2 Step 2. ✓
- Verification (full sln, npm, docker, two-user manual) → Task 12. ✓

**Placeholder scan:** No TBD/TODO; every code step shows full code; commands have expected output. ✓

**Type consistency:** `ApplyPromotionSuggestion(int, ExpiryHandling?, DateOnly?)`, `ResolvePromotion(int, DateTime)` used identically across Tasks 1/5/6/7. `PendingPromotionResponse` fields (`listItemId`/`text`/`quantity`/`expiryHandling`/`suggestedExpiry`) consistent across Tasks 4/9/11. `PromoteListItemsRequest { inventoryId, items: [{ listItemId, quantity, expiryDate }] }` / `promotedCount` consistent across Tasks 5/9/11. `SkipPromotionRequest { listItemIds }` consistent across Tasks 6/9/11. Generated hook/query-key names (`getPendingPromotionsOptions`, `getListQueryKey`, `promoteListItemsMutation`, `skipPromotionMutation`) consistent across Tasks 8-11. ✓

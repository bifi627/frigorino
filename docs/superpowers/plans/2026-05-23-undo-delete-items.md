# Undo on Item Delete (Snackbar with Revert) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user undo an accidental delete of a **list item** or **inventory item** via a Sonner "Undo" toast that restores the soft-deleted row in its original position.

**Architecture:** Delete already soft-deletes (`IsActive = false`) through aggregate methods (`List.RemoveItem`, `Inventory.RemoveItem`). We add the inverse — `RestoreItem` aggregate methods that flip `IsActive` back to `true` — exposed via two new vertical slices (`POST .../items/{itemId}/restore`). Because the row keeps its `SortOrder`/`CreatedAt`, restore returns the item to its exact prior position for free. The frontend fires a Sonner toast (already wired in `main.tsx`) from each delete hook's `onSuccess`; the toast's "Undo" action calls a new restore mutation hook, which invalidates the items query so the item reappears.

**Tech Stack:** .NET 8 minimal-API vertical slices + FluentResults + EF Core (Postgres); React 19 + TanStack Query + hey-api generated client + Sonner toasts + i18next. Tests: xUnit (aggregate units), Reqnroll + Playwright (API-level BDD **and** browser-driven UI BDD that drives the real SPA).

**Scope (locked with user):** List items + inventory items only. Lists, inventories, and households are explicitly **out of scope** for this plan (household restore in particular needs a different auth + membership-cascade design and is deferred to its own idea).

**Key facts that shaped this plan (verified against the code, 2026-05-23):**
- There is **no global EF query filter** on `IsActive`; every read slice filters explicitly. `.Include(l => l.ListItems)` therefore loads soft-deleted items too, so the restore handler can find the inactive row in the loaded aggregate. (`ApplicationDbContext.cs`)
- `RemoveItem(itemId)` finds `i.Id == itemId && i.IsActive`; `RestoreItem(itemId)` is its mirror: `i.Id == itemId && !i.IsActive`.
- Item operations carry **no role gate** by design — any active household member can delete/restore. The handler enforces membership via `db.FindActiveMembershipAsync(...)`; the aggregate method takes no `callerRole`. (See the header comment at `List.cs:130-135`.)
- The snackbar is **Sonner** (`sonner@^2.0.7`, `<Toaster />` in `main.tsx`), NOT MUI `Snackbar` or notistack. Sonner's `toast(msg, { action: { label, onClick }, duration })` is the confirmed undo API.
- `SortOrder` and `CreatedAt` live on the row and are untouched by delete, so restore preserves original position with no extra work.

---

## File Structure

**Backend — create:**
- `Application/Frigorino.Features/Lists/Items/RestoreItem.cs` — restore slice for list items.
- `Application/Frigorino.Features/Inventories/Items/RestoreInventoryItem.cs` — restore slice for inventory items.

**Backend — modify:**
- `Application/Frigorino.Domain/Entities/List.cs` — add `RestoreItem` aggregate method (after `RemoveItem`, ~line 219).
- `Application/Frigorino.Domain/Entities/Inventory.cs` — add `RestoreItem` aggregate method (after `RemoveItem`, ~line 209).
- `Application/Frigorino.Web/Program.cs` — wire `listItems.MapRestoreItem()` and `inventoryItems.MapRestoreInventoryItem()`.

**Backend — tests (create):**
- `Application/Frigorino.IntegrationTests/Shared/ToastSteps.cs` — shared browser step "I click undo in the delete toast" (used by both list and inventory UI scenarios).

**Backend — tests (modify):**
- `Application/Frigorino.Test/Domain/ListAggregateItemTests.cs` — `RestoreItem` unit tests.
- `Application/Frigorino.Test/Domain/InventoryAggregateItemTests.cs` — `RestoreItem` unit tests.
- `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` — `TryRestoreListItemAsync` / `TryRestoreInventoryItemAsync` helpers.
- `Application/Frigorino.IntegrationTests/Slices/Lists/ListItemApiSteps.cs` — restore step + "includes" assertion step.
- `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItemApiSteps.cs` — restore step + "includes" assertion step.
- `Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.Api.feature` — restore API scenario.
- `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItems.Api.feature` — restore API scenario.
- `Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.feature` — restore-via-undo UI scenario.
- `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItems.feature` — restore-via-undo UI scenario.

**Frontend — create:**
- `Application/Frigorino.Web/ClientApp/src/features/lists/items/useRestoreListItem.ts`
- `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useRestoreInventoryItem.ts`

**Frontend — modify:**
- `Application/Frigorino.Web/ClientApp/src/features/lists/items/useDeleteListItem.ts` — fire undo toast in `onSuccess`.
- `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useDeleteInventoryItem.ts` — fire undo toast in `onSuccess`.
- `Application/Frigorino.Web/ClientApp/src/main.tsx` — give the Sonner `<Toaster>` a deterministic `actionButton` class hook so the UI test can locate the Undo button without matching translated text.
- `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` — `common.itemDeleted`, `common.undo`.
- `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json` — same two keys.
- `Application/Frigorino.Web/ClientApp/src/lib/api/**` — regenerated by `npm run api` (committed).

---

## Task 1: `List.RestoreItem` aggregate method

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/List.cs` (add after `RemoveItem`, ~line 219)
- Test: `Application/Frigorino.Test/Domain/ListAggregateItemTests.cs` (add a `RestoreItem` region after the `RemoveItem` tests, ~line 293)

- [ ] **Step 1: Write the failing tests**

Add this region to `ListAggregateItemTests.cs` immediately after the `RemoveItem_AlreadyInactive_ReturnsEntityNotFound` test (after line 293), before the `// ------- ToggleItemStatus -------` region. Uses the existing `NewList()` and `AddSeed(...)` helpers in that file.

```csharp
        // ------- RestoreItem -------

        [Fact]
        public void RestoreItem_ReactivatesSoftDeletedItemAndStampsUpdatedAt()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            var before = item.UpdatedAt;

            var result = list.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.True(item.IsActive);
            Assert.Same(item, result.Value);
            Assert.True(item.UpdatedAt > before);
        }

        [Fact]
        public void RestoreItem_PreservesOriginalSortOrder()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk", sortOrder: 1_234_567);
            item.IsActive = false;

            var result = list.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(1_234_567, item.SortOrder);
        }

        [Fact]
        public void RestoreItem_NotFound_ReturnsEntityNotFound()
        {
            var list = NewList();

            var result = list.RestoreItem(itemId: 999);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RestoreItem_AlreadyActive_ReturnsEntityNotFound()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk"); // active by default

            var result = list.RestoreItem(item.Id);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests.RestoreItem"`
Expected: FAIL — compile error `'List' does not contain a definition for 'RestoreItem'`.

- [ ] **Step 3: Implement the aggregate method**

In `Application/Frigorino.Domain/Entities/List.cs`, add this method directly after `RemoveItem` (which ends at line 219, just before `ToggleItemStatus`):

```csharp
        public Result<ListItem> RestoreItem(int itemId)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && !i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"Soft-deleted list item {itemId} not found."));
            }

            item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests.RestoreItem"`
Expected: PASS (4 passed).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/List.cs Application/Frigorino.Test/Domain/ListAggregateItemTests.cs
git commit -m "feat: add List.RestoreItem aggregate method"
```

---

## Task 2: `Inventory.RestoreItem` aggregate method

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/Inventory.cs` (add after `RemoveItem`, ~line 209)
- Test: `Application/Frigorino.Test/Domain/InventoryAggregateItemTests.cs` (add a `RestoreItem` region after the `RemoveItem` tests, ~line 263)

- [ ] **Step 1: Write the failing tests**

Add this region to `InventoryAggregateItemTests.cs` immediately after `RemoveItem_AlreadyInactive_ReturnsEntityNotFound` (after line 263), before the `// ------- ReorderItem -------` region. Uses the existing `NewInventory()` and `AddSeed(...)` helpers (note: inventory `AddSeed` signature is `AddSeed(inventory, text, quantity, expiryDate, sortOrder)`).

```csharp
        // ------- RestoreItem -------

        [Fact]
        public void RestoreItem_ReactivatesSoftDeletedItemAndStampsUpdatedAt()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour");
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            var before = item.UpdatedAt;

            var result = inventory.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.True(item.IsActive);
            Assert.Same(item, result.Value);
            Assert.True(item.UpdatedAt > before);
        }

        [Fact]
        public void RestoreItem_PreservesOriginalSortOrder()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour", sortOrder: 1_234_567);
            item.IsActive = false;

            var result = inventory.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(1_234_567, item.SortOrder);
        }

        [Fact]
        public void RestoreItem_NotFound_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();

            var result = inventory.RestoreItem(itemId: 999);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RestoreItem_AlreadyActive_ReturnsEntityNotFound()
        {
            var inventory = NewInventory();
            var item = AddSeed(inventory, "Flour"); // active by default

            var result = inventory.RestoreItem(item.Id);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventoryAggregateItemTests.RestoreItem"`
Expected: FAIL — compile error `'Inventory' does not contain a definition for 'RestoreItem'`.

- [ ] **Step 3: Implement the aggregate method**

In `Application/Frigorino.Domain/Entities/Inventory.cs`, add this method directly after `RemoveItem` (which ends at line 209, just before the `ReorderItem` comment block):

```csharp
        public Result<InventoryItem> RestoreItem(int itemId)
        {
            var item = InventoryItems.FirstOrDefault(i => i.Id == itemId && !i.IsActive);
            if (item is null)
            {
                return Result.Fail<InventoryItem>(
                    new EntityNotFoundError($"Soft-deleted inventory item {itemId} not found."));
            }

            item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~InventoryAggregateItemTests.RestoreItem"`
Expected: PASS (4 passed).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Inventory.cs Application/Frigorino.Test/Domain/InventoryAggregateItemTests.cs
git commit -m "feat: add Inventory.RestoreItem aggregate method"
```

---

## Task 3: Restore slice for list items + wiring

**Files:**
- Create: `Application/Frigorino.Features/Lists/Items/RestoreItem.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (line 290-300 block, add to `listItems` group)

This slice mirrors `DeleteItem.cs` exactly, except: it's a `POST .../{itemId:int}/restore`, calls `list.RestoreItem(itemId)`, and returns the restored DTO (`Ok<ListItemResponse>`) instead of `NoContent`. `.Include(l => l.ListItems)` loads the soft-deleted row (no global query filter), so the aggregate can find it.

- [ ] **Step 1: Create the slice**

Create `Application/Frigorino.Features/Lists/Items/RestoreItem.cs`:

```csharp
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    public static class RestoreItemEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{itemId:int}/restore", Handle)
               .WithName("RestoreItem")
               .Produces<ListItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<ListItemResponse>, NotFound>> Handle(
            int householdId,
            int listId,
            int itemId,
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

            var result = list.RestoreItem(itemId);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                throw new InvalidOperationException(
                    $"RestoreItem cannot map error of type {first.GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ListItemResponse.From(result.Value));
        }
    }
}
```

- [ ] **Step 2: Wire the endpoint in `Program.cs`**

In `Application/Frigorino.Web/Program.cs`, add `listItems.MapRestoreItem();` to the `listItems` group. Place it right after `listItems.MapDeleteItem();` (line 297):

```csharp
listItems.MapGetItems();
listItems.MapGetItem();
listItems.MapCreateItem();
listItems.MapUpdateItem();
listItems.MapDeleteItem();
listItems.MapRestoreItem();
listItems.MapToggleItemStatus();
listItems.MapReorderItem();
listItems.MapCompactItems();
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded, 0 errors. (This also regenerates `ClientApp/src/lib/openapi.json` via the MSBuild OpenAPI target — that's expected; it'll be committed in Task 6.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/RestoreItem.cs Application/Frigorino.Web/Program.cs
git commit -m "feat: add restore endpoint for list items"
```

---

## Task 4: Restore slice for inventory items + wiring

**Files:**
- Create: `Application/Frigorino.Features/Inventories/Items/RestoreInventoryItem.cs`
- Modify: `Application/Frigorino.Web/Program.cs` (line 311-319 block, add to `inventoryItems` group)

- [ ] **Step 1: Create the slice**

Create `Application/Frigorino.Features/Inventories/Items/RestoreInventoryItem.cs`:

```csharp
using Frigorino.Domain.Errors;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories.Items
{
    public static class RestoreInventoryItemEndpoint
    {
        public static IEndpointRouteBuilder MapRestoreInventoryItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("/{itemId:int}/restore", Handle)
               .WithName("RestoreInventoryItem")
               .Produces<InventoryItemResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<InventoryItemResponse>, NotFound>> Handle(
            int householdId,
            int inventoryId,
            int itemId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var inventory = await db.Inventories
                .Include(i => i.InventoryItems)
                .FirstOrDefaultAsync(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive, ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var result = inventory.RestoreItem(itemId);
            if (result.IsFailed)
            {
                var first = result.Errors[0];
                if (first is EntityNotFoundError)
                {
                    return TypedResults.NotFound();
                }
                throw new InvalidOperationException(
                    $"RestoreInventoryItem cannot map error of type {first.GetType().Name}.");
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(InventoryItemResponse.From(result.Value));
        }
    }
}
```

- [ ] **Step 2: Wire the endpoint in `Program.cs`**

In `Application/Frigorino.Web/Program.cs`, add `inventoryItems.MapRestoreInventoryItem();` right after `inventoryItems.MapDeleteInventoryItem();` (line 317):

```csharp
inventoryItems.MapGetInventoryItems();
inventoryItems.MapCreateInventoryItem();
inventoryItems.MapUpdateInventoryItem();
inventoryItems.MapDeleteInventoryItem();
inventoryItems.MapRestoreInventoryItem();
inventoryItems.MapReorderInventoryItem();
inventoryItems.MapCompactInventoryItems();
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Web`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Inventories/Items/RestoreInventoryItem.cs Application/Frigorino.Web/Program.cs
git commit -m "feat: add restore endpoint for inventory items"
```

---

## Task 5: API BDD integration tests for both restore endpoints

This is the only automated coverage of the slice wiring + 404 mapping (the aggregate logic is covered by Tasks 1-2). It uses the raw Playwright `TestApiClient` (not the generated TS client), so it does not depend on Task 6.

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/ListItemApiSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItemApiSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.Api.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItems.Api.feature`

> **Docker note:** the integration tests use Postgres Testcontainers + Playwright. If `dotnet test` on the IntegrationTests project errors with a Docker-daemon-unreachable message, ask the user to start Docker Desktop rather than skipping the test.

- [ ] **Step 1: Add restore helpers to `TestApiClient`**

In `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`, add `TryRestoreListItemAsync` right after `TryDeleteListItemAsync` (line 146):

```csharp
    public Task<IAPIResponse> TryRestoreListItemAsync(int listId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}/restore",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }
```

And add `TryRestoreInventoryItemAsync` right after `TryDeleteInventoryItemAsync` (line 256):

```csharp
    public Task<IAPIResponse> TryRestoreInventoryItemAsync(int inventoryId, int itemId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/items/{itemId}/restore",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }
```

- [ ] **Step 2: Add the restore step + "includes" assertion to `ListItemApiSteps`**

In `Application/Frigorino.IntegrationTests/Slices/Lists/ListItemApiSteps.cs`, add these two binding methods (e.g. after the `WhenIDeleteTheItemViaTheApi` method, line 26, and the `omits` assertion, line 82). `ctx.GetListItemId(listName, itemText)` still resolves the id after a soft-delete because the id is captured at seed time.

```csharp
    [When("I POST restore for the item {string} in {string} via the API")]
    public async Task WhenIPostRestoreForTheItemViaTheApi(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        ctx.LastApiResponse = await api.TryRestoreListItemAsync(listId, itemId);
    }

    [Then("the API response when getting items of {string} includes {string}")]
    public async Task ThenTheApiResponseWhenGettingItemsIncludes(string listName, string itemText)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var items = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();
        Assert.Contains(itemText, items);
    }
```

- [ ] **Step 3: Add the restore scenario to `ListItems.Api.feature`**

Append this scenario to `Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.Api.feature`:

```gherkin
  Scenario: Restoring a deleted item via the API returns it to the list
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I DELETE the item "Milk" in "Weekly Groceries" via the API
    And I POST restore for the item "Milk" in "Weekly Groceries" via the API
    Then the API response status is 200
    And the API response when getting items of "Weekly Groceries" includes "Milk"
```

- [ ] **Step 4: Add the restore step + "includes" assertion to `InventoryItemApiSteps`**

Open `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItemApiSteps.cs` and add the inventory-equivalent binding methods. (Mirror the list version, using `ctx.InventoryIds[...]`, `ctx.GetInventoryItemId(...)`, and `api.TryGetInventoryItemsAsync(...)`. Confirm those exact member names by reading the existing delete/omits steps already in this file and copy their idiom — do not invent names.)

```csharp
    [When("I POST restore for the item {string} in {string} via the API")]
    public async Task WhenIPostRestoreForTheItemViaTheApi(string itemText, string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var itemId = ctx.GetInventoryItemId(inventoryName, itemText);
        ctx.LastApiResponse = await api.TryRestoreInventoryItemAsync(inventoryId, itemId);
    }

    [Then("the API response when getting items of {string} includes {string}")]
    public async Task ThenTheApiResponseWhenGettingItemsIncludes(string inventoryName, string itemText)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var response = await api.TryGetInventoryItemsAsync(inventoryId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var items = json!.Value.EnumerateArray()
            .Select(e => e.GetProperty("text").GetString())
            .ToArray();
        Assert.Contains(itemText, items);
    }
```

> **Step-name collision check:** Reqnroll binds step text globally across the assembly. If `InventoryItemApiSteps` already defines a `[When("I POST restore ...")]` or `[Then("... includes ...")]` with identical text, or if the inventory feature's `Given`/`When` phrasing for items differs from the list one, adjust the step text to be unique (e.g. include the word "inventory"). Read the existing inventory item steps + `InventoryItems.Api.feature` before adding, and reuse whatever delete/get-items step phrasing already exists there.

- [ ] **Step 5: Add the restore scenario to `InventoryItems.Api.feature`**

Append the inventory-equivalent scenario to `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItems.Api.feature`, matching the existing item-seeding + delete step phrasing already used in that file:

```gherkin
  Scenario: Restoring a deleted item via the API returns it to the inventory
    Given there is an inventory named "Pantry" with item "Flour"
    When I DELETE the item "Flour" in "Pantry" via the API
    And I POST restore for the item "Flour" in "Pantry" via the API
    Then the API response status is 200
    And the API response when getting items of "Pantry" includes "Flour"
```

- [ ] **Step 6: Run the integration tests**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Restoring"`
Expected: PASS (2 scenarios). If Docker isn't running, ask the user to start Docker Desktop.

- [ ] **Step 7: Commit**

```bash
git add Application/Frigorino.IntegrationTests
git commit -m "test: cover list/inventory item restore endpoints via API BDD"
```

---

## Task 6: Regenerate the frontend API client

The new slices added `RestoreItem` / `RestoreInventoryItem` operations to the OpenAPI spec. Regenerating produces `restoreItemMutation()` and `restoreInventoryItemMutation()` (named from the `.WithName(...)` operation ids, matching how `DeleteItem` → `deleteItemMutation`).

**Files:**
- Modify (generated, committed): `Application/Frigorino.Web/ClientApp/src/lib/openapi.json`, `Application/Frigorino.Web/ClientApp/src/lib/api/**`

- [ ] **Step 1: Regenerate**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run api`
(This rebuilds the backend to emit `openapi.json`, then regenerates the TS client. No backend boot/DB needed.)

- [ ] **Step 2: Verify the restore helpers were generated**

Run from `ClientApp/`: `npm run tsc`
Expected: no type errors.
Then confirm the new exports exist (used by Task 7):

Run: `grep -n "restoreItemMutation\|restoreInventoryItemMutation" src/lib/api/@tanstack/react-query.gen.ts`
Expected: both `export const restoreItemMutation` and `export const restoreInventoryItemMutation` are present.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib
git commit -m "chore: regenerate api client with item restore endpoints"
```

---

## Task 7: Restore mutation hooks

Arg-less mutation hooks per the project convention (callers pass `{ path }` to `mutate`). On success they invalidate the items query so the restored item reappears in its original `SortOrder` position. No optimistic re-insert: at undo time we only hold the path ids, and a single refetch is correct and simple.

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useRestoreListItem.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useRestoreInventoryItem.ts`

- [ ] **Step 1: Create `useRestoreListItem`**

Create `Application/Frigorino.Web/ClientApp/src/features/lists/items/useRestoreListItem.ts`:

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getItemsQueryKey,
    restoreItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreListItem = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreItemMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            });
        },
    });
};
```

- [ ] **Step 2: Create `useRestoreInventoryItem`**

Create `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useRestoreInventoryItem.ts`:

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getInventoryItemsQueryKey,
    restoreInventoryItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreInventoryItem = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreInventoryItemMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getInventoryItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        inventoryId: variables.path.inventoryId,
                    },
                }),
            });
        },
    });
};
```

- [ ] **Step 3: Type-check**

Run from `ClientApp/`: `npm run tsc`
Expected: no type errors. (If `variables.path` member names differ from the generated `RestoreItemData['path']` shape, fix to match the generated type — the path should be `{ householdId, listId, itemId }` for lists and `{ householdId, inventoryId, itemId }` for inventory.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/useRestoreListItem.ts Application/Frigorino.Web/ClientApp/src/features/inventories/items/useRestoreInventoryItem.ts
git commit -m "feat: add restore mutation hooks for list/inventory items"
```

---

## Task 8: i18n strings

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add English keys**

In `public/locales/en/translation.json`, the `common` block currently ends (line 68) with:

```json
        "textCopiedToClipboard": "Text copied to clipboard"
```

Change it to add the two new keys (note the added comma):

```json
        "textCopiedToClipboard": "Text copied to clipboard",
        "itemDeleted": "Item deleted",
        "undo": "Undo"
```

- [ ] **Step 2: Add German keys**

In `public/locales/de/translation.json`, the `common` block currently ends (line 68) with:

```json
        "textCopiedToClipboard": "Text in die Zwischenablage kopiert"
```

Change it to:

```json
        "textCopiedToClipboard": "Text in die Zwischenablage kopiert",
        "itemDeleted": "Element gelöscht",
        "undo": "Rückgängig"
```

- [ ] **Step 3: Validate JSON**

Run from `ClientApp/`: `npm run tsc` (cheap proxy that the build still parses; JSON is loaded at runtime so also confirm no trailing-comma error by eye).
Alternatively: `node -e "require('./public/locales/en/translation.json'); require('./public/locales/de/translation.json'); console.log('ok')"`
Expected: `ok`.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat: add itemDeleted/undo i18n strings"
```

---

## Task 9: Wire the undo toast into the delete hooks

Fire a Sonner toast from each delete hook's `onSuccess`. The toast's "Undo" action calls the restore hook with the same `variables.path` the delete used. The delete hook's existing optimistic `onMutate`/`onError`/`onSettled` logic stays untouched.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useDeleteListItem.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useDeleteInventoryItem.ts`

- [ ] **Step 1: Update `useDeleteListItem`**

Edit `Application/Frigorino.Web/ClientApp/src/features/lists/items/useDeleteListItem.ts`. Add three imports at the top:

```ts
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useRestoreListItem } from "./useRestoreListItem";
```

Add the two hook calls inside `useDeleteListItem`, right after the existing `debouncedInvalidate` line:

```ts
    const { t } = useTranslation();
    const restoreItem = useRestoreListItem();
```

Add an `onSuccess` callback to the `useMutation({...})` options (place it after `onError` and before `onSettled`):

```ts
        onSuccess: (_data, variables) => {
            toast(t("common.itemDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreItem.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
```

The full file should read:

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteItemMutation,
    getItemQueryKey,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";
import { useRestoreListItem } from "./useRestoreListItem";

export const useDeleteListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreItem = useRestoreListItem();

    return useMutation({
        ...deleteItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                },
            });

            await queryClient.cancelQueries({ queryKey });

            const previousItems =
                queryClient.getQueryData<ListItemResponse[]>(queryKey);

            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                return old.filter((item) => item.id !== variables.path.itemId);
            });

            queryClient.removeQueries({
                queryKey: getItemQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                        itemId: variables.path.itemId,
                    },
                }),
            });

            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getItemsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            listId: variables.path.listId,
                        },
                    }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("common.itemDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreItem.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
        },
    });
};
```

- [ ] **Step 2: Update `useDeleteInventoryItem`**

Edit `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useDeleteInventoryItem.ts` the same way. Full file:

```ts
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteInventoryItemMutation,
    getInventoryItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { InventoryItemResponse } from "../../../lib/api/types.gen";
import { useRestoreInventoryItem } from "./useRestoreInventoryItem";

export const useDeleteInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreItem = useRestoreInventoryItem();

    return useMutation({
        ...deleteInventoryItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getInventoryItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    inventoryId: variables.path.inventoryId,
                },
            });

            await queryClient.cancelQueries({ queryKey });

            const previousItems =
                queryClient.getQueryData<InventoryItemResponse[]>(queryKey);

            queryClient.setQueryData<InventoryItemResponse[]>(
                queryKey,
                (old) => {
                    if (!old) return old;
                    return old.filter(
                        (item) => item.id !== variables.path.itemId,
                    );
                },
            );

            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getInventoryItemsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            inventoryId: variables.path.inventoryId,
                        },
                    }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("common.itemDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreItem.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getInventoryItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        inventoryId: variables.path.inventoryId,
                    },
                }),
            );
        },
    });
};
```

- [ ] **Step 3: Lint + type-check**

Run from `ClientApp/`: `npm run tsc` then `npm run lint`
Expected: no errors. (If lint flags import order, run `npm run fix`.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/useDeleteListItem.ts Application/Frigorino.Web/ClientApp/src/features/inventories/items/useDeleteInventoryItem.ts
git commit -m "feat: show undo toast on list/inventory item delete"
```

---

## Task 10: Playwright UI BDD — restore via the Undo toast (end-to-end through the SPA)

Browser-driven Reqnroll + Playwright scenarios that drive the real SPA: open a list/inventory, delete an item via the row menu, click **Undo** in the Sonner toast, and assert the item returns. This is the only test that exercises the full chain (delete hook → toast → undo action → restore hook → restore endpoint → refetch → row reappears). It depends on the built SPA, so it must run after the frontend tasks (6-9).

**No translated text in assertions** (project rule): item text (`"Milk"`, `"Flour"`) is *user data*, fine to match; the toast's title/label are translated `t()` strings, so we never match them — instead we locate the Undo button by a deterministic class set on the Toaster.

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/main.tsx`
- Create: `Application/Frigorino.IntegrationTests/Shared/ToastSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItems.feature`

> **Docker note:** these run on Postgres Testcontainers + Playwright + a built SPA. If `dotnet test` errors with a Docker-daemon-unreachable message, ask the user to start Docker Desktop rather than skipping.

- [ ] **Step 1: Add a deterministic Undo-button class to the Toaster**

Sonner's `action: { label, onClick }` object renders a default-styled action button and applies any `classNames.actionButton` *in addition* to its defaults (this is not `unstyled`, so styling is unchanged). In `Application/Frigorino.Web/ClientApp/src/main.tsx`, change the `<Toaster />` (line 79) to:

```tsx
                <Toaster
                    toastOptions={{
                        classNames: { actionButton: "undo-action-button" },
                    }}
                />
```

This gives the UI test a stable, locale-independent selector (`[data-sonner-toast] .undo-action-button`).

- [ ] **Step 2: Create the shared "click undo" step**

Create `Application/Frigorino.IntegrationTests/Shared/ToastSteps.cs`. It needs no `using` directives — the project's `GlobalUsings.cs` already pulls in Reqnroll, `Microsoft.Playwright`, and xUnit. The response matcher (`/items/.../restore` POST 200) matches both the list and inventory restore routes, so this single step serves both features.

```csharp
namespace Frigorino.IntegrationTests.Shared;

[Binding]
public class ToastSteps(ScenarioContextHolder ctx)
{
    [When("I click undo in the delete toast")]
    public async Task WhenIClickUndoInTheDeleteToast()
    {
        // Subscribe before the click — the Undo action fires the restore POST. Wait for the
        // 200 so the follow-up "appears" assertion inspects post-server-confirm DOM rather than
        // the pre-refetch window. The matcher covers both list and inventory restore routes.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Url.EndsWith("/restore")
            && r.Request.Method == "POST"
            && r.Status == 200);
        await ctx.Page.Locator("[data-sonner-toast] .undo-action-button").ClickAsync();
        await responseTask;
    }
}
```

- [ ] **Step 3: Add the list UI scenario**

Append to `Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.feature`. Reuses existing steps `I open the list`, `I open the item menu for`, `I click delete from the item menu`, `{string} no longer appears in the list`, and `{string} appears in the list`:

```gherkin
  Scenario: Undo restores a deleted list item via the toast
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I open the list "Weekly Groceries"
    And I open the item menu for "Milk"
    And I click delete from the item menu
    Then "Milk" no longer appears in the list
    When I click undo in the delete toast
    Then "Milk" appears in the list
```

- [ ] **Step 4: Add the inventory UI scenario**

Append to `Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItems.feature`. Reuses existing steps `I open the inventory`, `I open the inventory item menu for`, `I click delete from the inventory item menu`, `{string} no longer appears in the inventory`, and `{string} appears in the inventory`:

```gherkin
  Scenario: Undo restores a deleted inventory item via the toast
    Given there is an inventory named "Pantry" with item "Flour"
    When I open the inventory "Pantry"
    And I open the inventory item menu for "Flour"
    And I click delete from the inventory item menu
    Then "Flour" no longer appears in the inventory
    When I click undo in the delete toast
    Then "Flour" appears in the inventory
```

- [ ] **Step 5: Run the two UI scenarios**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Undo"`
Expected: PASS (2 scenarios). If the run can't find a built SPA, the IntegrationTests `SpaBuildHelper` builds it automatically; if Docker isn't running, ask the user to start Docker Desktop.

> If the click flakes because the 5s toast auto-dismissed before the click: the optimistic delete + `Then ... no longer appears` resolves near-instantly, so the click lands well within the window — but if CI is slow, the fix is to confirm the toast is present first (`await Assertions.Expect(ctx.Page.Locator("[data-sonner-toast] .undo-action-button")).ToBeVisibleAsync();`) at the top of the step, NOT to widen the production toast duration.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/main.tsx Application/Frigorino.IntegrationTests/Shared/ToastSteps.cs Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.feature Application/Frigorino.IntegrationTests/Slices/Inventories/InventoryItems.feature
git commit -m "test: cover item restore via undo toast with Playwright UI BDD"
```

---

## Task 11: Full verification

- [ ] **Step 1: Run the whole backend solution test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: all green (xUnit Test project + IntegrationTests). This is the single command that covers unit + integration in one run; do not parallelize it with another `dotnet test` or with `npm run build` (shared Testcontainers ports / `ClientApp/build` dir). If Docker isn't running, ask the user to start Docker Desktop.

- [ ] **Step 2: Build the frontend**

Run from `ClientApp/`: `npm run build`
Expected: `tsc -b && vite build` succeeds.

- [ ] **Step 3: Manual verification in the running app**

The golden path (delete → undo → reappear) is now automated by Task 10; this step is a final visual sanity check plus the rapid-delete **stacking** edge case, which the BDD scenarios don't cover. Bring up the dev stack (use the `/dev-up` skill) and drive the SPA via Playwright MCP at `https://localhost:44375` (authenticated as `dev@frigorino.local`):

1. Open a list, add an item ("Milk").
2. Delete it via the item's "More" menu → Delete.
3. **Verify:** the item disappears immediately (optimistic) AND a toast "Item deleted" with an "Undo" button appears.
4. Click "Undo".
5. **Verify:** the item reappears in its original position (same spot in the unchecked section).
6. Delete it again, let the toast auto-dismiss (~5s), then **verify** the item stays gone (no resurrection).
7. Repeat steps 1-5 for an inventory item to confirm the inventory path.
8. (Edge) Delete two items rapidly → **verify** two stacked toasts appear and each "Undo" restores its own item.

If you cannot reach the running UI, say so explicitly rather than claiming success.

- [ ] **Step 4: Docker build sanity (drift guard)**

This feature adds no new projects and doesn't touch the Dockerfile, but a full image build is the cheapest guard against backend+SPA integration drift before Railway sees it.

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: build succeeds. If the Docker daemon is unreachable, ask the user to start Docker Desktop.

- [ ] **Step 5: Final commit (if `npm run fix` or regen changed anything)**

```bash
git add -A
git commit -m "chore: finalize undo-on-delete for items"
```

---

## Out of scope (deferred, by decision)

- **Lists / inventories (parent aggregates) restore.** Clean to add later (same pattern: `RestoreList`/`RestoreInventory` slice + aggregate method), but their delete flow navigates away to `/`, so the undo UX differs (toast must survive route change — Sonner does, but the call site moves to the confirm dialog).
- **Household restore.** Genuinely different: `Household.SoftDelete` cascades `IsActive=false` to all memberships including the deleter's, so the deleter can't pass the `FindActiveMembershipAsync` gate to restore it; and we can't reconstruct which memberships were active pre-delete. Needs its own auth model + cascade decision.
- **A "trash bin" view** for surfacing/restoring older soft-deletes beyond the toast window. The restore endpoints added here work indefinitely on any soft-deleted row, so a future trash view can reuse them as-is.

## Self-review notes
- **Spec coverage:** restore endpoints (Tasks 3-4), aggregate methods returning the restored DTO (Tasks 1-2), arg-less mutation hooks (Task 7), Sonner snackbar with Undo + ~5s dismiss + stacking (Task 9), i18n (Task 8), original-position preservation (verified via `SortOrder` tests + UI/manual steps). Test coverage now spans three layers: aggregate units (Tasks 1-2), API-level BDD on the slice wiring/404 mapping (Task 5), and browser-driven UI BDD on the full delete→undo→reappear chain (Task 10). Open questions resolved: SortOrder preserved (free, via the row); TTL indefinite (endpoint has no time gate); scope = items only.
- **Type consistency:** aggregate methods return `Result<ListItem>` / `Result<InventoryItem>`; slices return `Ok<ListItemResponse>` / `Ok<InventoryItemResponse>` built with the existing `.From(...)` factories; restore hooks named `useRestoreListItem` / `useRestoreInventoryItem`; generated ops `restoreItemMutation` / `restoreInventoryItemMutation` (verified-by-grep in Task 6 before use in Task 7). The UI step locates the Undo button via the `undo-action-button` class set on `<Toaster>` in Task 10 Step 1 — these two must match.
- **Assumptions to confirm during execution:** (a) the inventory `ScenarioContextHolder` accessor names in Task 5 Step 4 (`InventoryIds`, `GetInventoryItemId`) and the inventory feature's item-seeding step phrasing — read the existing inventory item steps first and reuse their exact idiom; (b) Sonner applies `classNames.actionButton` to the rendered action button (documented behavior) — if the `.undo-action-button` selector finds nothing at runtime, fall back to Sonner's `[data-sonner-toast] button[data-button]` attribute selector, which is also locale-independent.

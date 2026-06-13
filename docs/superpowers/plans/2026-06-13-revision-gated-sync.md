# Revision-Gated Collaborative Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a household member edits a list, inventory, or the expiry calendar another member is viewing, the viewer's screen updates within ~2s — without re-pulling the full payload every poll and without a persistent connection.

**Architecture:** Each syncable page polls a tiny `GET .../revision` endpoint every 2s (while focused). The endpoint returns an opaque change-token computed from a cheap indexed aggregate (`parentUpdatedAt.Ticks` + `MAX(item.UpdatedAt).Ticks` + active `COUNT`). The client compares the token to the last one it saw; on a change it invalidates the real data query (one full refetch) — unless a local mutation for that resource is in flight, in which case local edits win and the refetch is suppressed. No migration, no WebSocket, no new dependency.

**Tech Stack:** ASP.NET Core minimal-API vertical slices (`Frigorino.Features`), EF Core + Postgres, React 19 + TanStack Query (hey-api generated client), Reqnroll + Playwright integration tests.

**Spec:** `docs/superpowers/specs/2026-06-13-revision-gated-sync-design.md`

---

## Background the implementer must know

- **Vertical slices:** one file = one endpoint. A read slice is handler-only (no aggregate). Canonical references: `Application/Frigorino.Features/Lists/Items/GetItems.cs`, `Application/Frigorino.Features/Inventories/GetExpiryCalendar.cs`. Mirror their auth (`db.FindActiveMembershipAsync(...)` → 404), existence check, and `TypedResults`.
- **Routing groups** live in `Application/Frigorino.Web/Program.cs` (~lines 343–389). The `lists` group prefix is `/api/household/{householdId:int}/lists`; `inventories` is `/api/household/{householdId:int}/inventories`. Add the new endpoints to those groups, NOT the `listItems`/`inventoryItems` groups (the revision routes sit beside `GetList`/`GetInventory`/`GetExpiryCalendar`, not under `/items`).
- **Auto-timestamps:** `ApplicationDbContext.SaveChangesAsync` stamps `UpdatedAt` on every modified entity. Every mutation (add, edit, reorder via `Rank` change, soft-delete via `IsActive=false`) moves the token. The client never needs to compute anything — it only compares opaque strings.
- **TS client generation:** after backend endpoint/DTO changes, run `npm run api` from `Application/Frigorino.Web/ClientApp/` — it rebuilds the backend, emits `src/lib/openapi.json`, and regenerates `src/lib/api/`. The generated hook helpers are named from `.WithName(...)`: `WithName("GetListRevision")` → `getListRevisionOptions` / `getListRevisionQueryKey` / type `GetListRevisionResponse`.
- **No JS test runner** exists — frontend is verified via `lint`/`tsc`/`prettier` + manual `/dev-up` browser checks. Backend revision logic is covered by Reqnroll API integration tests.
- **Integration-test idiom:** `.feature` (Gherkin) + step bindings in `*Steps.cs` + HTTP helpers in `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`. Reqnroll creates one binding-class instance per scenario, so instance fields persist across that scenario's steps. Tests assert on JSON/testids, never translated text. Seeding goes through aggregate methods inside a DI scope (`ctx.Factory.Services.CreateScope()`) — see `ExpiryCalendarApiSteps.cs`.

## File Structure

**Backend (create):**
- `Application/Frigorino.Features/Sync/RevisionResponse.cs` — shared opaque-token DTO + `Compute(...)` factory. One responsibility: mint/shape the token.
- `Application/Frigorino.Features/Lists/GetListRevision.cs` — list revision slice.
- `Application/Frigorino.Features/Inventories/GetInventoryRevision.cs` — inventory revision slice.
- `Application/Frigorino.Features/Inventories/GetExpiryCalendarRevision.cs` — household calendar revision slice.

**Backend (modify):**
- `Application/Frigorino.Web/Program.cs` — register the three new slices.

**Tests (create):**
- `Application/Frigorino.IntegrationTests/Slices/Sync/Revision.Api.feature` — revision scenarios.
- `Application/Frigorino.IntegrationTests/Slices/Sync/RevisionApiSteps.cs` — capture/compare + DB-edit step bindings.

**Tests (modify):**
- `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` — add `TryGetListRevisionAsync`, `TryGetInventoryRevisionAsync`, `TryGetExpiryCalendarRevisionAsync`.

**Frontend (create):**
- `Application/Frigorino.Web/ClientApp/src/hooks/useRevisionInvalidation.ts` — shared core: compare token, gate on local mutation, advance baseline; plus `REVISION_QUERY_OPTIONS`.
- `Application/Frigorino.Web/ClientApp/src/features/lists/items/useListRevision.ts`
- `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useInventoryRevision.ts`
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/useCalendarRevision.ts`

**Frontend (modify) — one-line wiring each:**
- `.../features/lists/pages/ListViewPage.tsx`
- `.../features/inventories/pages/InventoryViewPage.tsx`
- `.../features/inventories/calendar/pages/ExpiryCalendarPage.tsx`

---

## Task 1: Shared `RevisionResponse` DTO

**Files:**
- Create: `Application/Frigorino.Features/Sync/RevisionResponse.cs`

- [ ] **Step 1: Create the DTO + token factory**

```csharp
namespace Frigorino.Features.Sync
{
    // Opaque change-detection token returned by the per-resource /revision endpoints. The client
    // treats `Rev` as a black box — it only compares it for equality between polls to decide whether
    // to refetch the real data query. Composed from the parent row's UpdatedAt (so a rename triggers
    // a refresh) plus the items' MAX(UpdatedAt) and active COUNT (so add / edit / reorder / soft-delete
    // all move it). Equality only — never parsed, never ordered.
    public sealed record RevisionResponse(string Rev)
    {
        // parentUpdatedAt is null for collection-level tokens (the calendar has no single parent row).
        // Empty item set → maxItemUpdatedAt null (encoded 0) and activeCount 0 → a stable token.
        public static RevisionResponse Compute(DateTime? parentUpdatedAt, DateTime? maxItemUpdatedAt, int activeCount)
        {
            var parentTicks = parentUpdatedAt?.Ticks ?? 0L;
            var maxTicks = maxItemUpdatedAt?.Ticks ?? 0L;
            return new RevisionResponse($"{parentTicks}.{maxTicks}.{activeCount}");
        }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Features`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Features/Sync/RevisionResponse.cs
git commit -m "feat: add shared RevisionResponse change-token DTO"
```

---

## Task 2: Integration-test scaffolding (HTTP helpers + capture/compare steps)

This task adds the reusable test infrastructure (helpers + steps). The `.feature` scenarios and the slices follow in Tasks 3–5 (TDD: each slice's scenarios fail until that slice exists).

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Create: `Application/Frigorino.IntegrationTests/Slices/Sync/RevisionApiSteps.cs`

- [ ] **Step 1: Add the three revision HTTP helpers to `TestApiClient`**

Add these methods inside the `TestApiClient` class (e.g. after `TryGetExpiryCalendarAsync`, ~line 285):

```csharp
    public Task<IAPIResponse> TryGetListRevisionAsync(int listId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/revision",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetInventoryRevisionAsync(int inventoryId, int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/{inventoryId}/revision",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }

    public Task<IAPIResponse> TryGetExpiryCalendarRevisionAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/calendar/revision",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }
```

- [ ] **Step 2: Create the capture/compare + DB-edit step bindings**

Create `Application/Frigorino.IntegrationTests/Slices/Sync/RevisionApiSteps.cs`:

```csharp
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Sync;

[Binding]
public class RevisionApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    // Reqnroll creates one instance of this binding class per scenario, so this list accumulates
    // the revisions captured within a single scenario.
    private readonly List<string> _revisions = new();

    [When("I capture the revision of list {string} via the API")]
    public async Task WhenICaptureTheRevisionOfList(string listName)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListRevisionAsync(listId);
        Xunit.Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        _revisions.Add(json.GetProperty("rev").GetString()!);
    }

    [When("I capture the revision of inventory {string} via the API")]
    public async Task WhenICaptureTheRevisionOfInventory(string inventoryName)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var response = await api.TryGetInventoryRevisionAsync(inventoryId);
        Xunit.Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        _revisions.Add(json.GetProperty("rev").GetString()!);
    }

    [When("I capture the expiry-calendar revision via the API")]
    public async Task WhenICaptureTheExpiryCalendarRevision()
    {
        var response = await api.TryGetExpiryCalendarRevisionAsync();
        Xunit.Assert.Equal(200, response.Status);
        var json = (await response.JsonAsync())!.Value;
        _revisions.Add(json.GetProperty("rev").GetString()!);
    }

    // Edits an item's text straight through the DbContext (bypassing the API) so a scenario can mutate
    // a NON-perishable inventory item — used to prove that edit does NOT move the calendar token.
    [When("I edit the text of item {string} in inventory {string} to {string} via the database")]
    public async Task WhenIEditTheTextOfItemViaTheDatabase(string itemText, string inventoryName, string newText)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inventoryId = ctx.InventoryIds[inventoryName];
        var item = await db.InventoryItems.FirstAsync(i => i.InventoryId == inventoryId && i.Text == itemText);
        item.Text = newText;
        await db.SaveChangesAsync();
    }

    [Then("the two captured revisions differ")]
    public void ThenTheTwoCapturedRevisionsDiffer()
    {
        Xunit.Assert.Equal(2, _revisions.Count);
        Xunit.Assert.NotEqual(_revisions[0], _revisions[1]);
    }

    [Then("the two captured revisions are equal")]
    public void ThenTheTwoCapturedRevisionsAreEqual()
    {
        Xunit.Assert.Equal(2, _revisions.Count);
        Xunit.Assert.Equal(_revisions[0], _revisions[1]);
    }
}
```

- [ ] **Step 3: Build the test project to verify it compiles**

Run: `dotnet build Application/Frigorino.IntegrationTests`
Expected: Build succeeded. (Steps reference helpers that now exist; `ctx.ListIds`/`ctx.InventoryIds`/`ctx.Factory` already exist — see `ExpiryCalendarApiSteps.cs`.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs Application/Frigorino.IntegrationTests/Slices/Sync/RevisionApiSteps.cs
git commit -m "test: add revision-endpoint HTTP helpers and capture/compare steps"
```

---

## Task 3: List revision slice (TDD)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Sync/Revision.Api.feature`
- Create: `Application/Frigorino.Features/Lists/GetListRevision.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Write the failing scenarios**

Create `Application/Frigorino.IntegrationTests/Slices/Sync/Revision.Api.feature` with the list section (inventory + calendar sections are appended in Tasks 4–5):

```gherkin
Feature: Resource revision tokens

  Background:
    Given I am logged in with an active household

  Scenario: A no-op read returns the same list revision token
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I capture the revision of list "Weekly Groceries" via the API
    And I capture the revision of list "Weekly Groceries" via the API
    Then the two captured revisions are equal

  Scenario: Adding an item changes the list revision token
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I capture the revision of list "Weekly Groceries" via the API
    And I POST an item "Bread" with comment "" to "Weekly Groceries" via the API
    And I capture the revision of list "Weekly Groceries" via the API
    Then the two captured revisions differ

  Scenario: Deleting an item changes the list revision token
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I capture the revision of list "Weekly Groceries" via the API
    And I DELETE the item "Milk" in "Weekly Groceries" via the API
    And I capture the revision of list "Weekly Groceries" via the API
    Then the two captured revisions differ

  Scenario: Renaming the list changes the list revision token
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I capture the revision of list "Weekly Groceries" via the API
    And I rename the list "Weekly Groceries" to "Groceries" via the database
    And I capture the revision of list "Weekly Groceries" via the API
    Then the two captured revisions differ

  Scenario: Non-member cannot read a list revision
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    And "bob" has created a list named "BobsList"
    When I GET the revision of list "BobsList" via the API
    Then the API response status is 404
```

Two new step bindings are needed for this section: the DB-rename `When` and the non-member `When`. Add them to `RevisionApiSteps.cs`:

```csharp
    [When("I rename the list {string} to {string} via the database")]
    public async Task WhenIRenameTheListViaTheDatabase(string listName, string newName)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var listId = ctx.ListIds[listName];
        var list = await db.Lists.FirstAsync(l => l.Id == listId);
        list.Name = newName;
        await db.SaveChangesAsync();
    }

    [When("I GET the revision of list {string} via the API")]
    public async Task WhenIGetTheRevisionOfList(string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryGetListRevisionAsync(listId);
    }
```

(The `POST an item ... with comment`, `DELETE the item`, `there is a list named ... with item`, `logged in as`, `existing household ... owned by`, `bob has created a list`, and `the API response status is 404` steps already exist — reused from `ListItemApiSteps.cs` / `ListSteps.cs` / `ApiResponseSteps.cs`.)

- [ ] **Step 2: Run the list scenarios to verify they fail**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Revision"`
Expected: FAIL — the capture step asserts `200` but gets `404` (no `/revision` endpoint yet). Confirm the failing count covers the 5 list scenarios (per `reference_reqnroll_filter_silent_skip`, verify scenarios actually ran — check the total, don't trust green).

- [ ] **Step 3: Implement the list revision slice**

Create `Application/Frigorino.Features/Lists/GetListRevision.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Sync;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists
{
    public static class GetListRevisionEndpoint
    {
        public static IEndpointRouteBuilder MapGetListRevision(this IEndpointRouteBuilder app)
        {
            app.MapGet("{listId:int}/revision", Handle)
               .WithName("GetListRevision")
               .Produces<RevisionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RevisionResponse>, NotFound>> Handle(
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

            var list = await db.Lists
                .Where(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive)
                .Select(l => new { l.UpdatedAt })
                .FirstOrDefaultAsync(ct);
            if (list is null)
            {
                return TypedResults.NotFound();
            }

            // Two cheap scalar aggregates over IX_ListItems_ListId_IsActive. Kept as two queries
            // (not a GroupBy projection) for reliable EF translation; both are sub-millisecond.
            var items = db.ListItems.Where(i => i.ListId == listId && i.IsActive);
            var maxUpdatedAt = await items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
            var count = await items.CountAsync(ct);

            return TypedResults.Ok(RevisionResponse.Compute(list.UpdatedAt, maxUpdatedAt, count));
        }
    }
}
```

- [ ] **Step 4: Register the slice in `Program.cs`**

In the `lists` group block (after `lists.MapGetList();`, ~line 348), add:

```csharp
lists.MapGetListRevision();
```

- [ ] **Step 5: Run the list scenarios to verify they pass**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Revision"`
Expected: PASS — the 5 list scenarios are green.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Lists/GetListRevision.cs Application/Frigorino.Web/Program.cs Application/Frigorino.IntegrationTests/Slices/Sync/Revision.Api.feature Application/Frigorino.IntegrationTests/Slices/Sync/RevisionApiSteps.cs
git commit -m "feat: add list revision endpoint"
```

---

## Task 4: Inventory revision slice (TDD)

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Sync/Revision.Api.feature`
- Create: `Application/Frigorino.Features/Inventories/GetInventoryRevision.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Append the failing inventory scenarios**

Append to `Revision.Api.feature` (the `an inventory ... has an item ... with no expiry` step exists in `ExpiryCalendarApiSteps.cs`):

```gherkin
  Scenario: A no-op read returns the same inventory revision token
    Given an inventory "Fridge" has an item "Cheese" with no expiry
    When I capture the revision of inventory "Fridge" via the API
    And I capture the revision of inventory "Fridge" via the API
    Then the two captured revisions are equal

  Scenario: Adding an item changes the inventory revision token
    Given an inventory "Fridge" has an item "Cheese" with no expiry
    When I capture the revision of inventory "Fridge" via the API
    And an inventory "Fridge" has an item "Butter" with no expiry
    And I capture the revision of inventory "Fridge" via the API
    Then the two captured revisions differ

  Scenario: Editing an item's text changes the inventory revision token
    Given an inventory "Fridge" has an item "Cheese" with no expiry
    When I capture the revision of inventory "Fridge" via the API
    And I edit the text of item "Cheese" in inventory "Fridge" to "Gouda" via the database
    And I capture the revision of inventory "Fridge" via the API
    Then the two captured revisions differ
```

- [ ] **Step 2: Run the inventory scenarios to verify they fail**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Revision"`
Expected: the 3 new inventory scenarios FAIL (capture step gets `404`). List scenarios still pass.

- [ ] **Step 3: Implement the inventory revision slice**

Create `Application/Frigorino.Features/Inventories/GetInventoryRevision.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Sync;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories
{
    public static class GetInventoryRevisionEndpoint
    {
        public static IEndpointRouteBuilder MapGetInventoryRevision(this IEndpointRouteBuilder app)
        {
            app.MapGet("{inventoryId:int}/revision", Handle)
               .WithName("GetInventoryRevision")
               .Produces<RevisionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RevisionResponse>, NotFound>> Handle(
            int householdId,
            int inventoryId,
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
                .Where(i => i.Id == inventoryId && i.HouseholdId == householdId && i.IsActive)
                .Select(i => new { i.UpdatedAt })
                .FirstOrDefaultAsync(ct);
            if (inventory is null)
            {
                return TypedResults.NotFound();
            }

            var items = db.InventoryItems.Where(i => i.InventoryId == inventoryId && i.IsActive);
            var maxUpdatedAt = await items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
            var count = await items.CountAsync(ct);

            return TypedResults.Ok(RevisionResponse.Compute(inventory.UpdatedAt, maxUpdatedAt, count));
        }
    }
}
```

- [ ] **Step 4: Register the slice in `Program.cs`**

In the `inventories` group block (after `inventories.MapGetInventory();`, ~line 377), add:

```csharp
inventories.MapGetInventoryRevision();
```

- [ ] **Step 5: Run the inventory scenarios to verify they pass**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Revision"`
Expected: PASS — all list + inventory scenarios green.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Inventories/GetInventoryRevision.cs Application/Frigorino.Web/Program.cs Application/Frigorino.IntegrationTests/Slices/Sync/Revision.Api.feature
git commit -m "feat: add inventory revision endpoint"
```

---

## Task 5: Calendar revision slice (TDD)

The calendar token is scoped to the **exact** calendar filter (`ExpiryDate != null`), so an edit to a non-perishable item must NOT move it — that over-trigger-avoidance is the key assertion here.

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Sync/Revision.Api.feature`
- Create: `Application/Frigorino.Features/Inventories/GetExpiryCalendarRevision.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Append the failing calendar scenarios**

Append to `Revision.Api.feature` (the `... has an item ... expiring in N days` step exists in `ExpiryCalendarApiSteps.cs`):

```gherkin
  Scenario: Adding a perishable item changes the calendar revision token
    Given an inventory "Fridge" has an item "Yogurt" expiring in 3 days
    When I capture the expiry-calendar revision via the API
    And an inventory "Fridge" has an item "Milk" expiring in 5 days
    And I capture the expiry-calendar revision via the API
    Then the two captured revisions differ

  Scenario: Editing a non-perishable item does not change the calendar revision token
    Given an inventory "Fridge" has an item "Yogurt" expiring in 3 days
    And an inventory "Fridge" has an item "Salt" with no expiry
    When I capture the expiry-calendar revision via the API
    And I edit the text of item "Salt" in inventory "Fridge" to "Sea Salt" via the database
    And I capture the expiry-calendar revision via the API
    Then the two captured revisions are equal

  Scenario: Non-member cannot read the calendar revision
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the expiry-calendar revision via the API
    Then the API response status is 404
```

Add the non-member calendar `When` to `RevisionApiSteps.cs`. This mirrors the existing list non-member pattern (`ListItemApiSteps` / `ExpiryCalendarApiSteps`): the `existing household "Other" owned by "bob"` step sets `ctx.HouseholdId` to bob's household, and the helper defaults to it — so alice (not a member) gets a 404:

```csharp
    [When("I GET the expiry-calendar revision via the API")]
    public async Task WhenIGetTheExpiryCalendarRevision()
    {
        ctx.LastApiResponse = await api.TryGetExpiryCalendarRevisionAsync();
    }
```

- [ ] **Step 2: Run the calendar scenarios to verify they fail**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Revision"`
Expected: the 3 new calendar scenarios FAIL (`404` from the missing endpoint). All prior scenarios still pass.

- [ ] **Step 3: Implement the calendar revision slice**

Create `Application/Frigorino.Features/Inventories/GetExpiryCalendarRevision.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Features.Sync;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories
{
    public static class GetExpiryCalendarRevisionEndpoint
    {
        public static IEndpointRouteBuilder MapGetExpiryCalendarRevision(this IEndpointRouteBuilder app)
        {
            // Literal "calendar/revision" — the int constraint on the sibling "{inventoryId:int}/revision"
            // route keeps "calendar" from colliding, same as the existing GetExpiryCalendar route.
            app.MapGet("calendar/revision", Handle)
               .WithName("GetExpiryCalendarRevision")
               .Produces<RevisionResponse>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<RevisionResponse>, NotFound>> Handle(
            int householdId,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // Scoped to the EXACT filter the calendar query uses — so a non-perishable item edit
            // (ExpiryDate == null) does not move the token. No parent row → parentUpdatedAt null.
            var items = db.InventoryItems.Where(i => i.IsActive
                && i.ExpiryDate != null
                && i.Inventory.IsActive
                && i.Inventory.HouseholdId == householdId);
            var maxUpdatedAt = await items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
            var count = await items.CountAsync(ct);

            return TypedResults.Ok(RevisionResponse.Compute(null, maxUpdatedAt, count));
        }
    }
}
```

- [ ] **Step 4: Register the slice in `Program.cs`**

In the `inventories` group block (after `inventories.MapGetExpiryCalendar();`, ~line 376), add:

```csharp
inventories.MapGetExpiryCalendarRevision();
```

- [ ] **Step 5: Run the full revision suite to verify it passes**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Revision"`
Expected: PASS — all list, inventory, and calendar scenarios green. Verify the total scenario count matches what you authored (per `reference_reqnroll_filter_silent_skip`).

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Features/Inventories/GetExpiryCalendarRevision.cs Application/Frigorino.Web/Program.cs Application/Frigorino.IntegrationTests/Slices/Sync/Revision.Api.feature Application/Frigorino.IntegrationTests/Slices/Sync/RevisionApiSteps.cs
git commit -m "feat: add expiry-calendar revision endpoint"
```

---

## Task 6: Regenerate the TypeScript API client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/openapi.json`, `Application/Frigorino.Web/ClientApp/src/lib/api/**`

- [ ] **Step 1: Regenerate the client**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run api`
Expected: rebuilds backend, emits `openapi.json`, regenerates `src/lib/api`. No manual edits.

- [ ] **Step 2: Verify the new generated helpers exist**

Run: `grep -c "getListRevisionOptions\|getInventoryRevisionOptions\|getExpiryCalendarRevisionOptions" Application/Frigorino.Web/ClientApp/src/lib/api/@tanstack/react-query.gen.ts`
Expected: `3` (one match per helper). Also confirm the response types exist:
Run: `grep -n "GetListRevisionResponse\|GetInventoryRevisionResponse\|GetExpiryCalendarRevisionResponse" Application/Frigorino.Web/ClientApp/src/lib/api/types.gen.ts`
Expected: each is an object type with a `rev: string` (or `string`) field.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/openapi.json Application/Frigorino.Web/ClientApp/src/lib/api
git commit -m "chore: regenerate API client with revision endpoints"
```

---

## Task 7: Shared `useRevisionInvalidation` core hook

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/hooks/useRevisionInvalidation.ts`

- [ ] **Step 1: Create the core hook + shared query options**

```ts
import { useQueryClient, type QueryKey } from "@tanstack/react-query";
import { useEffect, useRef } from "react";

// Shared focus-gating config for every /revision poll. Spread into the useQuery call alongside the
// generated getXRevisionOptions. 2s cadence (chosen with the user); polling stops entirely while the
// tab is backgrounded (keeps Railway's idle-sleep intact) and resumes instantly on focus.
export const REVISION_QUERY_OPTIONS = {
    refetchInterval: 2000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
    staleTime: 0,
} as const;

interface UseRevisionInvalidationParams {
    // The opaque token from the latest successful poll (undefined until the first poll resolves).
    rev: string | undefined;
    // The real data query to invalidate when the token moves (built via getXQueryKey).
    dataQueryKey: QueryKey;
    // True when a LOCAL mutation targeting this resource is in flight. Its own optimistic update plus
    // onSettled invalidation are authoritative, so we must NOT clobber it with a remote refetch.
    isLocalMutation: (variables: unknown) => boolean;
}

// Compares each incoming opaque revision token to the last one seen. On a change it invalidates the
// real data query (one full refetch) UNLESS a local mutation is in flight — in which case it skips the
// refetch but STILL advances the baseline, so the next tick doesn't re-detect the same change and fire
// a redundant fetch once the local mutation settles.
export const useRevisionInvalidation = ({
    rev,
    dataQueryKey,
    isLocalMutation,
}: UseRevisionInvalidationParams) => {
    const queryClient = useQueryClient();
    const lastRev = useRef<string | null>(null);
    // Keep the latest predicate without making it an effect dependency (it's recreated each render).
    const isLocalMutationRef = useRef(isLocalMutation);
    isLocalMutationRef.current = isLocalMutation;

    useEffect(() => {
        if (rev == null) {
            return;
        }
        // First successful poll: adopt as the baseline. The data query already holds fresh data from
        // its own mount fetch, so there is nothing to invalidate yet.
        if (lastRev.current === null) {
            lastRev.current = rev;
            return;
        }
        if (rev === lastRev.current) {
            return;
        }

        const localMutating =
            queryClient.isMutating({
                predicate: (m) => isLocalMutationRef.current(m.state.variables),
            }) > 0;
        if (!localMutating) {
            queryClient.invalidateQueries({ queryKey: dataQueryKey });
        }
        // Advance the baseline regardless of whether we invalidated — this is the fix that prevents a
        // redundant double-fetch once a local mutation settles.
        lastRev.current = rev;
        // dataQueryKey is a fresh array each render; when rev is unchanged the early-return above makes
        // the re-run a no-op, so it is safe to depend on.
    }, [rev, queryClient, dataQueryKey]);
};
```

- [ ] **Step 2: Type-check**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/hooks/useRevisionInvalidation.ts
git commit -m "feat: add useRevisionInvalidation core sync hook"
```

---

## Task 8: List sync — `useListRevision` + wire into `ListViewPage`

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useListRevision.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx`

- [ ] **Step 1: Create the list gate hook**

`Application/Frigorino.Web/ClientApp/src/features/lists/items/useListRevision.ts`:

```ts
import { useQuery } from "@tanstack/react-query";
import {
    getItemsQueryKey,
    getListRevisionOptions,
} from "../../../lib/api/@tanstack/react-query.gen";
import {
    REVISION_QUERY_OPTIONS,
    useRevisionInvalidation,
} from "../../../hooks/useRevisionInvalidation";

// Polls this list's opaque revision token every 2s (while focused) and invalidates the list-items
// query only when another user's change moves the token. Local edits win — an in-flight mutation on
// this list (its variables carry path.listId) suppresses the remote refetch for that tick.
export const useListRevision = (householdId: number, listId: number) => {
    const enabled = householdId > 0 && listId > 0;

    const { data } = useQuery({
        ...getListRevisionOptions({ path: { householdId, listId } }),
        ...REVISION_QUERY_OPTIONS,
        enabled,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getItemsQueryKey({ path: { householdId, listId } }),
        isLocalMutation: (variables) =>
            (variables as { path?: { listId?: number } } | undefined)?.path
                ?.listId === listId,
    });
};
```

- [ ] **Step 2: Wire it into `ListViewPage`**

Add the import near the other feature-hook imports (e.g. after the `useListItems` import, line 27):

```ts
import { useListRevision } from "../items/useListRevision";
```

Then call it right after the `useListItems(...)` call (~line 71, where `householdId` and `listIdNum` are in scope):

```ts
    useListRevision(householdId, listIdNum);
```

- [ ] **Step 3: Type-check + lint**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc && npm run lint`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/useListRevision.ts Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx
git commit -m "feat: live-sync list view via revision polling"
```

---

## Task 9: Inventory sync — `useInventoryRevision` + wire into `InventoryViewPage`

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/items/useInventoryRevision.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryViewPage.tsx`

- [ ] **Step 1: Create the inventory gate hook**

`Application/Frigorino.Web/ClientApp/src/features/inventories/items/useInventoryRevision.ts`:

```ts
import { useQuery } from "@tanstack/react-query";
import {
    getInventoryItemsQueryKey,
    getInventoryRevisionOptions,
} from "../../../lib/api/@tanstack/react-query.gen";
import {
    REVISION_QUERY_OPTIONS,
    useRevisionInvalidation,
} from "../../../hooks/useRevisionInvalidation";

// Polls this inventory's opaque revision token every 2s (while focused) and invalidates the
// inventory-items query only when another user's change moves the token. An in-flight mutation on this
// inventory (variables carry path.inventoryId) suppresses the remote refetch for that tick.
export const useInventoryRevision = (
    householdId: number,
    inventoryId: number,
) => {
    const enabled = householdId > 0 && inventoryId > 0;

    const { data } = useQuery({
        ...getInventoryRevisionOptions({ path: { householdId, inventoryId } }),
        ...REVISION_QUERY_OPTIONS,
        enabled,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getInventoryItemsQueryKey({
            path: { householdId, inventoryId },
        }),
        isLocalMutation: (variables) =>
            (variables as { path?: { inventoryId?: number } } | undefined)?.path
                ?.inventoryId === inventoryId,
    });
};
```

- [ ] **Step 2: Wire it into `InventoryViewPage`**

Add the import near the other feature-hook imports (e.g. after the `useInventoryItems` import, line 26):

```ts
import { useInventoryRevision } from "../items/useInventoryRevision";
```

Then call it right after the `useInventoryItems(...)` call (~line 80, where `householdId` and `inventoryId` are in scope):

```ts
    useInventoryRevision(householdId, inventoryId);
```

- [ ] **Step 3: Type-check + lint**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc && npm run lint`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/items/useInventoryRevision.ts Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoryViewPage.tsx
git commit -m "feat: live-sync inventory view via revision polling"
```

---

## Task 10: Calendar sync — `useCalendarRevision` + wire into `ExpiryCalendarPage`

Note the calendar hook lives one level shallower than the items hooks (`features/inventories/calendar/`), so its relative imports use **three** `../`, matching the sibling `useExpiryCalendar.ts`.

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/useCalendarRevision.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx`

- [ ] **Step 1: Create the calendar gate hook**

`Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/useCalendarRevision.ts`:

```ts
import { useQuery } from "@tanstack/react-query";
import {
    getExpiryCalendarQueryKey,
    getExpiryCalendarRevisionOptions,
} from "../../../lib/api/@tanstack/react-query.gen";
import {
    REVISION_QUERY_OPTIONS,
    useRevisionInvalidation,
} from "../../../hooks/useRevisionInvalidation";

// Polls the household-wide expiry-calendar revision token every 2s (while focused) and invalidates the
// calendar query only when another user's change moves the token. The calendar is household-wide, so
// suppress only on in-flight INVENTORY-item mutations (those carry path.inventoryId); list mutations
// don't affect the calendar.
export const useCalendarRevision = (householdId: number) => {
    const enabled = householdId > 0;

    const { data } = useQuery({
        ...getExpiryCalendarRevisionOptions({ path: { householdId } }),
        ...REVISION_QUERY_OPTIONS,
        enabled,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getExpiryCalendarQueryKey({ path: { householdId } }),
        isLocalMutation: (variables) =>
            (variables as { path?: { inventoryId?: number } } | undefined)?.path
                ?.inventoryId != null,
    });
};
```

- [ ] **Step 2: Wire it into `ExpiryCalendarPage`**

Add the import near the other calendar-hook imports (e.g. after the `useExpiryCalendar` import, line 33):

```ts
import { useCalendarRevision } from "../useCalendarRevision";
```

Then call it right after the `useExpiryCalendar(...)` call (the `householdId` is already derived at ~line 53):

```ts
    useCalendarRevision(householdId);
```

- [ ] **Step 3: Type-check + lint**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc && npm run lint`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/useCalendarRevision.ts Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx
git commit -m "feat: live-sync expiry calendar via revision polling"
```

---

## Task 11: Frontend formatting + full verification gate

**Files:** none (verification only)

- [ ] **Step 1: Prettier-write the frontend**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run prettier`
Expected: files formatted (the new hooks may be reformatted). If anything changed, commit it:

```bash
git add -A Application/Frigorino.Web/ClientApp/src
git commit -m "style: prettier-format revision sync hooks"
```

- [ ] **Step 2: Frontend lint + type-check (final)**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run lint && npm run tsc`
Expected: both clean.

- [ ] **Step 3: Full backend + integration test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: all tests pass (capture the pass/fail summary line — per `feedback_verify_exit_code`, don't trust a piped tail; read the totals). This re-runs the revision suite plus the rest of `Frigorino.Test` + `Frigorino.IntegrationTests`.

- [ ] **Step 4: Manual browser verification (two sessions)**

Bring up the stack (`/dev-up`) and run `npm run build` first (the integration harness / served SPA needs the build output; per `project_it_serves_clientapp_build`). Then, with two browser sessions on the same household (e.g. a normal tab + an incognito/Playwright-MCP session, or two Playwright contexts):

- Open the same list in both. Add/check/rename an item in session A → session B reflects it within ~2s without a manual refresh.
- Repeat on an inventory detail page.
- Repeat on the expiry calendar: add/edit an item with an expiry in session A → the calendar bar appears/moves in session B within ~2s. Edit a *non-perishable* item in A → the calendar in B does **not** refetch (watch the Network panel; no `calendar` GET fires).
- **Local-edits-win:** start a drag-reorder (or open an edit) in session A while session B concurrently changes the same list. Confirm A's in-progress edit is not clobbered mid-action (no flicker/revert); A reconciles after its own mutation settles.
- **Focus-gating:** background session B's tab → confirm `revision` polling stops in the Network panel; refocus → confirm it resumes and immediately catches up.

- [ ] **Step 5: Final confirmation**

Confirm the working tree is clean and all commits are present:

```bash
git status
git log --oneline stage..HEAD
```

Expected: clean tree; commits for the DTO, three endpoints, client regen, core hook, three sync wirings, and formatting.

---

## Notes for the implementer

- **Do not** add a covering index, backoff, SignalR, or presence — all explicitly out of scope (see spec). YAGNI.
- The token format is an implementation detail. Do not expose or parse it on the client — the frontend only ever compares opaque strings.
- If `npm run api` (Task 6) names a generated helper differently than assumed (it derives from `.WithName(...)`), use the actual generated name — grep `react-query.gen.ts` for `Revision` to find them, and update the three hook imports to match.
- If `dotnet test` reports the inventory/list/calendar item entities lack a public `Text`/`Name` setter (Task 2/3 DB-mutation steps), switch that step to the corresponding aggregate method (`inventory.AddItem` already used for seeds; for edits use the entity's update method) — but the entities expose public setters today, so the direct assignment should compile.

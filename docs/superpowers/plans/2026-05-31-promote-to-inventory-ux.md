# Promote-to-Inventory UX (Cycle 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a user checks off a perishable list item, passively offer to add it to an inventory via a sticky bar → review sheet, with AI-suggested expiry and pre-filled quantity, batch-persisted in the browser.

**Architecture:** The `ToggleItemStatus` slice attaches an optional `Promote` suggestion (looked up from the per-household `Product` catalog) to its response when an item is checked *done*. The React client accumulates those suggestions in a `localStorage`-persisted Zustand store; a sticky bar on the list page opens a review sheet that adds selected items to a chosen inventory via the existing `CreateInventoryItem` endpoint. No new write endpoint, no migration, no new package.

**Tech Stack:** .NET 10 vertical slices + EF Core (Postgres), FluentResults; React 19 + TanStack Query + Zustand + MUI; xUnit/FakeItEasy + Reqnroll/Playwright.

**Spec:** `docs/superpowers/specs/2026-05-31-promote-to-inventory-ux-design.md`

**Branch:** `feat/promote-to-inventory` (already created off `stage`; spec committed).

---

## File Structure

**Backend**
- Modify `Application/Frigorino.Domain/Products/ExpiryProfile.cs` — add `SuggestsInventoryTracking` predicate.
- Create `Application/Frigorino.Features/Lists/Items/PromoteSuggestion.cs` — wire DTO + `For(Product?, DateOnly)` factory.
- Modify `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs` — add `Promote` init property.
- Modify `Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs` — attach `Promote` on check-done.
- Test `Application/Frigorino.Test/Domain/ExpiryProfileTests.cs` (add or extend) and `Application/Frigorino.Test/Features/PromoteSuggestionTests.cs` (new).
- Integration `Application/Frigorino.IntegrationTests/Slices/Lists/Promote.Api.feature` + `PromoteApiSteps.cs`.

**Frontend** (`Application/Frigorino.Web/ClientApp/src/`)
- Create `features/lists/promote/promotableStore.ts` — persisted Zustand store + `usePromotableForList`.
- Modify `features/lists/items/useToggleListItemStatus.ts` — `onSuccess` pushes/removes store entries.
- Create `features/lists/promote/PromoteBar.tsx` — sticky bar + owns sheet open state.
- Create `features/lists/promote/PromoteReviewSheet.tsx` — review drawer.
- Modify `features/lists/pages/ListViewPage.tsx` — mount `PromoteBar` between header and list.
- Modify `public/locales/en/translation.json` and `public/locales/de/translation.json` — `promote.*` keys.

**E2E**
- Create `Application/Frigorino.IntegrationTests/Slices/Lists/Promote.feature` + `PromoteSteps.cs`.

---

## Task 1: Domain predicate `ExpiryProfile.SuggestsInventoryTracking`

**Files:**
- Modify: `Application/Frigorino.Domain/Products/ExpiryProfile.cs`
- Test: `Application/Frigorino.Test/Domain/ExpiryProfileTests.cs`

- [ ] **Step 1: Write the failing test**

Create or append to `Application/Frigorino.Test/Domain/ExpiryProfileTests.cs`:

```csharp
using Frigorino.Domain.Products;

namespace Frigorino.Test.Domain;

public class ExpiryProfileTests
{
    [Theory]
    [InlineData(ExpiryHandling.AiRecommendsShelfLife, 7, true)]
    [InlineData(ExpiryHandling.UserEntersFromPackage, null, true)]
    [InlineData(ExpiryHandling.NonPerishable, null, false)]
    [InlineData(ExpiryHandling.Unknown, null, false)]
    public void SuggestsInventoryTracking_is_true_only_for_perishable_handlings(
        ExpiryHandling handling, int? shelfLifeDays, bool expected)
    {
        var profile = ExpiryProfile.Create(handling, shelfLifeDays).Value;

        Assert.Equal(expected, profile.SuggestsInventoryTracking);
    }
}
```

> Note: if `ExpiryProfileTests.cs` already exists, add only the test method (keep the existing class), and skip the `using`/namespace/class scaffolding.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryProfileTests.SuggestsInventoryTracking"`
Expected: FAIL — `ExpiryProfile` does not contain a definition for `SuggestsInventoryTracking`.

- [ ] **Step 3: Write minimal implementation**

In `Application/Frigorino.Domain/Products/ExpiryProfile.cs`, add this property after `SuggestedExpiry`:

```csharp
        // Perishable handlings are the ones worth tracking in inventory (a date matters):
        // AI shelf life (we can suggest a date) or user-entered package date. NonPerishable /
        // Unknown do not surface a promote suggestion.
        public bool SuggestsInventoryTracking =>
            Handling is ExpiryHandling.AiRecommendsShelfLife or ExpiryHandling.UserEntersFromPackage;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryProfileTests.SuggestsInventoryTracking"`
Expected: PASS (4 cases).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Products/ExpiryProfile.cs Application/Frigorino.Test/Domain/ExpiryProfileTests.cs
git commit -m "feat: ExpiryProfile.SuggestsInventoryTracking predicate"
```

---

## Task 2: `PromoteSuggestion` DTO + factory + `ListItemResponse.Promote`

**Files:**
- Create: `Application/Frigorino.Features/Lists/Items/PromoteSuggestion.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs`
- Test: `Application/Frigorino.Test/Features/PromoteSuggestionTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Application/Frigorino.Test/Features/PromoteSuggestionTests.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;
using Frigorino.Features.Lists.Items;

namespace Frigorino.Test.Features;

public class PromoteSuggestionTests
{
    private static readonly DateOnly Today = new(2026, 5, 31);

    private static Product ProductWith(ExpiryHandling handling, int? shelfLifeDays)
    {
        var classification = new ProductClassification(
            ProductCategory.Food, ExpiryProfile.Create(handling, shelfLifeDays).Value);
        return Product.Create(1, "milk", classification, classifierVersion: 1).Value;
    }

    [Fact]
    public void For_null_product_returns_null()
    {
        Assert.Null(PromoteSuggestion.For(null, Today));
    }

    [Fact]
    public void For_ai_recommended_returns_handling_and_dated_suggestion()
    {
        var suggestion = PromoteSuggestion.For(
            ProductWith(ExpiryHandling.AiRecommendsShelfLife, 7), Today);

        Assert.NotNull(suggestion);
        Assert.Equal(ExpiryHandling.AiRecommendsShelfLife, suggestion!.ExpiryHandling);
        Assert.Equal(new DateOnly(2026, 6, 7), suggestion.SuggestedExpiry);
    }

    [Fact]
    public void For_user_enters_from_package_returns_handling_with_null_date()
    {
        var suggestion = PromoteSuggestion.For(
            ProductWith(ExpiryHandling.UserEntersFromPackage, null), Today);

        Assert.NotNull(suggestion);
        Assert.Equal(ExpiryHandling.UserEntersFromPackage, suggestion!.ExpiryHandling);
        Assert.Null(suggestion.SuggestedExpiry);
    }

    [Fact]
    public void For_non_perishable_returns_null()
    {
        Assert.Null(PromoteSuggestion.For(
            ProductWith(ExpiryHandling.NonPerishable, null), Today));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~PromoteSuggestionTests"`
Expected: FAIL — `PromoteSuggestion` type does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `Application/Frigorino.Features/Lists/Items/PromoteSuggestion.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Domain.Products;

namespace Frigorino.Features.Lists.Items
{
    // Optional promote-to-inventory hint attached to the toggle response when a list item is
    // checked DONE and its product (by normalized name) is a perishable. SuggestedExpiry is a
    // date for AiRecommendsShelfLife, null for UserEntersFromPackage (user reads the package).
    public sealed record PromoteSuggestion(ExpiryHandling ExpiryHandling, DateOnly? SuggestedExpiry)
    {
        // product == null  → not yet classified / no catalog row → no suggestion.
        // non-perishable    → no suggestion.
        public static PromoteSuggestion? For(Product? product, DateOnly today)
        {
            if (product is null)
            {
                return null;
            }

            var expiry = product.EffectiveExpiry;
            if (!expiry.SuggestsInventoryTracking)
            {
                return null;
            }

            return new PromoteSuggestion(expiry.Handling, expiry.SuggestedExpiry(today));
        }
    }
}
```

In `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs`, add an init-only property to the record body (do **not** add it to the positional constructor — that keeps `From` and the EF `ToProjection` untouched, both leaving it null). Insert immediately after the `From` method, before `ToProjection`:

```csharp
        // Promote-to-inventory hint, set only by the ToggleItemStatus slice via `with { Promote = ... }`.
        // Not part of the positional ctor: read/projection paths (From, ToProjection) leave it null.
        public PromoteSuggestion? Promote { get; init; }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~PromoteSuggestionTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/PromoteSuggestion.cs Application/Frigorino.Features/Lists/Items/ListItemResponse.cs Application/Frigorino.Test/Features/PromoteSuggestionTests.cs
git commit -m "feat: PromoteSuggestion DTO + factory and ListItemResponse.Promote"
```

---

## Task 3: Wire `ToggleItemStatus` to attach `Promote` on check-done

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs`
- Integration test: `Application/Frigorino.IntegrationTests/Slices/Lists/Promote.Api.feature` (new)
- Integration steps: `Application/Frigorino.IntegrationTests/Slices/Lists/PromoteApiSteps.cs` (new)

> Why an integration test (not a unit test): the slice `Handle` is a private static method; the promote *decision* is already unit-covered in Task 2. This test verifies the wiring + the EF point-lookup against real Postgres using the deterministic `StubItemClassifier` (`milk` → AI shelf life 7; everything non-`milk`/`soap`/`call` → non-perishable).

- [ ] **Step 1: Write the failing test (feature + steps)**

Create `Application/Frigorino.IntegrationTests/Slices/Lists/Promote.Api.feature`:

```gherkin
Feature: Promote suggestion on toggle (API)

  Background:
    Given I am logged in with an active household

  Scenario: Toggling a classified perishable item done returns a promote suggestion
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the product "milk" is in the catalog
    When I toggle item "Milk" in list "Weekly Groceries" via the API
    Then the toggle response has a promote suggestion with handling "AiRecommendsShelfLife"
    And the promote suggestion has a non-null suggested expiry

  Scenario: Toggling a non-perishable item done returns no promote suggestion
    Given there is a list named "Weekly Groceries" with item "Sugar"
    And the product "sugar" is in the catalog
    When I toggle item "Sugar" in list "Weekly Groceries" via the API
    Then the toggle response has no promote suggestion

  Scenario: Toggling an item back to unchecked returns no promote suggestion
    Given there is a list named "Weekly Groceries" with item "Milk"
    And the product "milk" is in the catalog
    When I toggle item "Milk" in list "Weekly Groceries" via the API
    And I toggle item "Milk" in list "Weekly Groceries" via the API
    Then the toggle response has no promote suggestion
```

Create `Application/Frigorino.IntegrationTests/Slices/Lists/PromoteApiSteps.cs`:

```csharp
using System.Text.Json;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class PromoteApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    private JsonElement _lastToggle;

    [Given("the product {string} is in the catalog")]
    public async Task GivenTheProductIsInTheCatalog(string normalizedName)
    {
        // Classification is fire-and-forget on item add; poll real Postgres until the row lands.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var exists = await db.Products.AsNoTracking().AnyAsync(p =>
                p.HouseholdId == ctx.HouseholdId && p.NormalizedName == normalizedName);
            if (exists)
            {
                return;
            }
            await Task.Delay(100);
        }
        throw new Exception($"Product '{normalizedName}' was not classified within 10s.");
    }

    [When("I toggle item {string} in list {string} via the API")]
    public async Task WhenIToggleItemViaTheApi(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        var response = await api.TryToggleListItemStatusAsync(listId, itemId);
        Assert.Equal(200, response.Status);
        _lastToggle = (await response.JsonAsync())!.Value;
    }

    [Then("the toggle response has a promote suggestion with handling {string}")]
    public void ThenToggleHasPromoteWithHandling(string handling)
    {
        Assert.True(_lastToggle.TryGetProperty("promote", out var promote)
            && promote.ValueKind == JsonValueKind.Object);
        Assert.Equal(handling, promote.GetProperty("expiryHandling").GetString());
    }

    [Then("the promote suggestion has a non-null suggested expiry")]
    public void ThenPromoteHasNonNullExpiry()
    {
        var promote = _lastToggle.GetProperty("promote");
        Assert.Equal(JsonValueKind.String, promote.GetProperty("suggestedExpiry").ValueKind);
    }

    [Then("the toggle response has no promote suggestion")]
    public void ThenToggleHasNoPromote()
    {
        var hasPromote = _lastToggle.TryGetProperty("promote", out var promote)
            && promote.ValueKind == JsonValueKind.Object;
        Assert.False(hasPromote);
    }
}
```

> The `Given there is a list named ... with item ...` step already exists (`ListItemSteps`), and it stores the item id via `ctx.SetListItemId` — read it with `ctx.GetListItemId`. Verify `ScenarioContextHolder` exposes a `GetListItemId(listName, itemText)` accessor (it is used as `SetListItemId` in `ListItemSteps`); if only `SetListItemId` exists, add the symmetric getter to `ScenarioContextHolder`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Promote"`
Expected: FAIL — first scenario fails at `the toggle response has a promote suggestion` because the slice doesn't emit `promote` yet (the field is absent / null).

> Requires Docker running (Postgres Testcontainers). If the daemon is unreachable, ask the user to start Docker Desktop.

- [ ] **Step 3: Write minimal implementation**

In `Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs`, replace the final block (currently `await db.SaveChangesAsync(ct); return TypedResults.Ok(ListItemResponse.From(result.Value));`) with:

```csharp
            await db.SaveChangesAsync(ct);

            var response = ListItemResponse.From(result.Value);

            // Only when the item is now checked DONE do we look up its product (one indexed point
            // lookup on the unique (HouseholdId, NormalizedName)) and attach a promote suggestion.
            // Un-checking, non-perishable, and not-yet-classified all yield Promote == null.
            if (result.Value.Status)
            {
                var normalized = ProductName.Normalize(result.Value.Text);
                var product = await db.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p => p.HouseholdId == householdId && p.NormalizedName == normalized, ct);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                response = response with { Promote = PromoteSuggestion.For(product, today) };
            }

            return TypedResults.Ok(response);
```

Add the missing using at the top of the file (alongside the existing `using Frigorino.Domain.Interfaces;`):

```csharp
using Frigorino.Domain.Products;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Promote"`
Expected: PASS (3 scenarios).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/ToggleItemStatus.cs Application/Frigorino.IntegrationTests/Slices/Lists/Promote.Api.feature Application/Frigorino.IntegrationTests/Slices/Lists/PromoteApiSteps.cs
git commit -m "feat: attach promote suggestion to toggle response on check-done"
```

---

## Task 4: Regenerate the frontend API client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/openapi.json`, `Application/Frigorino.Web/ClientApp/src/lib/api/**`

- [ ] **Step 1: Regenerate**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run api
```

Expected: rebuilds the backend, emits `openapi.json`, regenerates the TS client. No errors.

- [ ] **Step 2: Verify the new types exist**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
grep -n "PromoteSuggestion" src/lib/api/types.gen.ts
```

Expected: a `PromoteSuggestion` type with `expiryHandling` (string union including `'AiRecommendsShelfLife'` and `'UserEntersFromPackage'`) and `suggestedExpiry?: string | null`, and `ListItemResponse` gains `promote?: PromoteSuggestion | null`.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/openapi.json Application/Frigorino.Web/ClientApp/src/lib/api
git commit -m "chore: regenerate API client with promote suggestion"
```

---

## Task 5: i18n keys for the promote feature

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add the `promote` block (English)**

Add a top-level `"promote"` object to `en/translation.json` (place it alphabetically near other top-level feature keys; match the file's existing indentation):

```json
"promote": {
  "barReady_one": "{{count}} item ready for inventory",
  "barReady_other": "{{count}} items ready for inventory",
  "review": "Review",
  "sheetTitle": "Add to inventory",
  "sheetSubtitle": "Select items and a target. Add the rest to another inventory after.",
  "target": "To",
  "tagRecommended": "Recommended",
  "tagEnterDate": "Enter date",
  "quantity": "Quantity",
  "expiry": "Expiry date",
  "expiryPlaceholder": "tap to set",
  "omit": "Remove from list",
  "clearAll": "Clear all",
  "addCount_one": "Add {{count}} to {{inventory}}",
  "addCount_other": "Add {{count}} to {{inventory}}"
}
```

- [ ] **Step 2: Add the `promote` block (German)**

Add the matching `"promote"` object to `de/translation.json`:

```json
"promote": {
  "barReady_one": "{{count}} Artikel bereit fürs Inventar",
  "barReady_other": "{{count}} Artikel bereit fürs Inventar",
  "review": "Überprüfen",
  "sheetTitle": "Zum Inventar hinzufügen",
  "sheetSubtitle": "Artikel und Ziel auswählen. Den Rest danach zu einem anderen Inventar hinzufügen.",
  "target": "Nach",
  "tagRecommended": "Empfohlen",
  "tagEnterDate": "Datum eingeben",
  "quantity": "Menge",
  "expiry": "Ablaufdatum",
  "expiryPlaceholder": "zum Festlegen tippen",
  "omit": "Von der Liste entfernen",
  "clearAll": "Alle entfernen",
  "addCount_one": "{{count}} zu {{inventory}} hinzufügen",
  "addCount_other": "{{count}} zu {{inventory}} hinzufügen"
}
```

- [ ] **Step 3: Verify JSON parses**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
node -e "JSON.parse(require('fs').readFileSync('public/locales/en/translation.json','utf8'));JSON.parse(require('fs').readFileSync('public/locales/de/translation.json','utf8'));console.log('ok')"
```

Expected: prints `ok`.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat: i18n keys for promote-to-inventory"
```

---

## Task 6: Persisted Zustand batch store

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/promote/promotableStore.ts`

> No JS test runner exists in this repo; correctness for the store/components is enforced by `npm run tsc`/`lint`/`prettier` (Task 10) and the E2E test (Task 11).

- [ ] **Step 1: Create the store**

Create `Application/Frigorino.Web/ClientApp/src/features/lists/promote/promotableStore.ts`:

```ts
import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { useShallow } from "zustand/react/shallow";
import type { PromoteSuggestion, QuantityDto } from "../../../lib/api";

// One pending promote candidate. Persisted to localStorage so a mid-shop refresh doesn't lose
// the batch. Device-scoped by design — no DB row (see spec).
export interface PromotableEntry {
    itemId: number;
    listId: number;
    name: string;
    quantity: QuantityDto | null;
    expiryHandling: PromoteSuggestion["expiryHandling"];
    suggestedExpiry: string | null;
}

interface PromotableState {
    entries: PromotableEntry[];
    add: (entry: PromotableEntry) => void;
    remove: (itemId: number) => void;
    clearForList: (listId: number) => void;
}

export const usePromotableStore = create<PromotableState>()(
    persist(
        (set) => ({
            entries: [],
            // Replace any existing entry for the same item (re-toggle keeps the latest suggestion).
            add: (entry) =>
                set((s) => ({
                    entries: [
                        ...s.entries.filter((e) => e.itemId !== entry.itemId),
                        entry,
                    ],
                })),
            remove: (itemId) =>
                set((s) => ({
                    entries: s.entries.filter((e) => e.itemId !== itemId),
                })),
            clearForList: (listId) =>
                set((s) => ({
                    entries: s.entries.filter((e) => e.listId !== listId),
                })),
        }),
        {
            name: "frigorino.promote.batch",
            storage: createJSONStorage(() => localStorage),
        },
    ),
);

// Stable filtered selector for a list's pending entries (useShallow avoids re-render churn when
// an unrelated list's entries change).
export const usePromotableForList = (listId: number): PromotableEntry[] =>
    usePromotableStore(
        useShallow((s) => s.entries.filter((e) => e.listId === listId)),
    );
```

- [ ] **Step 2: Type-check**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run tsc
```

Expected: no errors. (If `PromoteSuggestion` is not exported from `../../../lib/api`, import it from `../../../lib/api/types.gen` instead — confirm the barrel export with `grep -n "PromoteSuggestion" src/lib/api/index.ts`.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/promote/promotableStore.ts
git commit -m "feat: persisted promotable batch store"
```

---

## Task 7: Toggle hook feeds the store

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useToggleListItemStatus.ts`

- [ ] **Step 1: Add the import**

At the top of `useToggleListItemStatus.ts`, add:

```ts
import { usePromotableStore } from "../promote/promotableStore";
```

- [ ] **Step 2: Add `onSuccess` (store-only, no invalidate)**

Insert an `onSuccess` handler into the `useMutation({ ... })` object, immediately before `onSettled`:

```ts
        onSuccess: (data) => {
            // Store-only side effect — NOT a query invalidate (see the onSettled note below).
            // The server attaches `promote` only when the item was checked DONE and its product
            // is a perishable; un-check / non-perishable / unclassified come back without it,
            // which retracts any pending entry for this item.
            const store = usePromotableStore.getState();
            if (data.promote) {
                store.add({
                    itemId: data.id,
                    listId: data.listId,
                    name: data.text,
                    quantity: data.quantity ?? null,
                    expiryHandling: data.promote.expiryHandling,
                    suggestedExpiry: data.promote.suggestedExpiry ?? null,
                });
            } else {
                store.remove(data.id);
            }
        },
```

- [ ] **Step 3: Type-check + lint**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run tsc && npm run lint
```

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/useToggleListItemStatus.ts
git commit -m "feat: toggle hook pushes promote suggestions into the batch store"
```

---

## Task 8: Sticky `PromoteBar` + mount in the list page

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteBar.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx`

> The `PromoteReviewSheet` it renders is built in Task 9. This task wires the bar with a placeholder-free sheet import; implement Task 9 before running the app, but each task still type-checks because Task 9's file is created here as a stub first, then filled. To keep tasks self-contained, this task creates the **full** `PromoteBar` and a **minimal** sheet stub; Task 9 replaces the stub body.

- [ ] **Step 1: Create a minimal sheet stub (so PromoteBar compiles)**

Create `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx`:

```tsx
interface PromoteReviewSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    listId: number;
}

// Stub — full implementation in Task 9.
export const PromoteReviewSheet = (_props: PromoteReviewSheetProps) => null;
```

- [ ] **Step 2: Create `PromoteBar.tsx`**

Create `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteBar.tsx`:

```tsx
import { Inventory2Outlined } from "@mui/icons-material";
import { Box, Button, Paper, Typography } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { usePromotableForList } from "./promotableStore";
import { PromoteReviewSheet } from "./PromoteReviewSheet";

interface PromoteBarProps {
    householdId: number;
    listId: number;
}

// Sits between the list header and the scrolling item list. Visible only while this list has
// pending promote candidates (perishables checked off but not yet added to inventory).
export const PromoteBar = ({ householdId, listId }: PromoteBarProps) => {
    const { t } = useTranslation();
    const entries = usePromotableForList(listId);
    const [open, setOpen] = useState(false);

    if (entries.length === 0) {
        return null;
    }

    return (
        <>
            <Paper
                elevation={0}
                data-testid="promote-bar"
                data-count={entries.length}
                sx={{
                    mx: 3,
                    mb: 1,
                    px: 1.5,
                    py: 1,
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                    bgcolor: "primary.main",
                    color: "primary.contrastText",
                }}
            >
                <Inventory2Outlined fontSize="small" />
                <Typography variant="body2" sx={{ flex: 1 }}>
                    {t("promote.barReady", { count: entries.length })}
                </Typography>
                <Button
                    size="small"
                    variant="contained"
                    color="secondary"
                    data-testid="promote-bar-review"
                    onClick={() => setOpen(true)}
                >
                    {t("promote.review")}
                </Button>
            </Paper>

            <PromoteReviewSheet
                open={open}
                onClose={() => setOpen(false)}
                householdId={householdId}
                listId={listId}
            />
        </>
    );
};
```

- [ ] **Step 3: Mount it in `ListViewPage.tsx`**

In `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx`, add the import near the other feature imports:

```ts
import { PromoteBar } from "../promote/PromoteBar";
```

Then in the returned JSX, insert `PromoteBar` between `</PageHeadActionBar>` (the self-closing `PageHeadActionBar`) and `<ListContainer ...>`:

```tsx
            <PageHeadActionBar
                title={list.name || t("lists.untitledList")}
                subtitle={list.description || undefined}
                directActions={directActions}
                menuActions={menuActions}
            />

            <PromoteBar householdId={householdId} listId={listIdNum} />

            <ListContainer
```

- [ ] **Step 4: Type-check + lint**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run tsc && npm run lint
```

Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteBar.tsx Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx
git commit -m "feat: sticky promote bar on the list page"
```

---

## Task 9: `PromoteReviewSheet` (rows, selection, picker, recommended hint, omit, clear, add)

**Files:**
- Modify (replace stub): `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx`

- [ ] **Step 1: Replace the stub with the full implementation**

Replace the entire contents of `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx` with:

```tsx
import { Close } from "@mui/icons-material";
import {
    Box,
    Button,
    Checkbox,
    Chip,
    Drawer,
    IconButton,
    MenuItem,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useHouseholdInventories } from "../../inventories/useHouseholdInventories";
import { useCreateInventoryItem } from "../../inventories/items/useCreateInventoryItem";
import { formatQuantity } from "../items/quantityFormat";
import { getExpiryInfo } from "../../../utils/dateUtils";
import {
    usePromotableForList,
    usePromotableStore,
    type PromotableEntry,
} from "./promotableStore";

interface PromoteReviewSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    listId: number;
}

// Per-row editable draft (quantity as the inventory's free-text string; expiry as YYYY-MM-DD).
interface RowDraft {
    selected: boolean;
    quantity: string;
    expiry: string;
}

export const PromoteReviewSheet = ({
    open,
    onClose,
    householdId,
    listId,
}: PromoteReviewSheetProps) => {
    const { t } = useTranslation();
    const entries = usePromotableForList(listId);
    const remove = usePromotableStore((s) => s.remove);
    const clearForList = usePromotableStore((s) => s.clearForList);
    const createItem = useCreateInventoryItem();

    const { data: inventories = [] } = useHouseholdInventories(
        householdId,
        householdId > 0,
    );

    const [inventoryId, setInventoryId] = useState<number | null>(null);
    // Effective target: explicit pick, else the newest inventory (GetInventories is newest-first).
    const targetId = inventoryId ?? inventories[0]?.id ?? null;
    const targetName =
        inventories.find((i) => i.id === targetId)?.name ?? "";

    // Drafts keyed by itemId; (re)seeded from the current entries.
    const [drafts, setDrafts] = useState<Record<number, RowDraft>>({});
    const seeded = useMemo(() => {
        const next: Record<number, RowDraft> = {};
        for (const e of entries) {
            next[e.itemId] = drafts[e.itemId] ?? {
                selected: true,
                quantity: e.quantity ? formatQuantity(t, e.quantity) : "",
                expiry: e.suggestedExpiry ?? "",
            };
        }
        return next;
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [entries, t]);

    const updateDraft = (itemId: number, patch: Partial<RowDraft>) =>
        setDrafts((d) => ({
            ...d,
            [itemId]: { ...seeded[itemId], ...patch },
        }));

    const selectedCount = entries.filter(
        (e) => seeded[e.itemId]?.selected,
    ).length;

    const handleOmit = (itemId: number) => {
        remove(itemId);
        setDrafts((d) => {
            const next = { ...d };
            delete next[itemId];
            return next;
        });
    };

    const handleClearAll = () => {
        clearForList(listId);
        setDrafts({});
        onClose();
    };

    const handleAdd = async () => {
        if (!targetId) return;
        const toAdd = entries.filter((e) => seeded[e.itemId]?.selected);
        for (const entry of toAdd) {
            const draft = seeded[entry.itemId];
            try {
                await createItem.mutateAsync({
                    path: { householdId, inventoryId: targetId },
                    body: {
                        text: entry.name,
                        quantity: draft.quantity || undefined,
                        expiryDate: draft.expiry || undefined,
                    },
                });
                remove(entry.itemId);
            } catch {
                // Leave the entry in the batch on failure; the user can retry.
            }
        }
        // Close once nothing is left for this list.
        if (usePromotableStore.getState().entries.every((e) => e.listId !== listId)) {
            onClose();
        }
    };

    return (
        <Drawer
            anchor="bottom"
            open={open}
            onClose={onClose}
            data-testid="promote-sheet"
            PaperProps={{ sx: { borderTopLeftRadius: 16, borderTopRightRadius: 16 } }}
        >
            <Box sx={{ p: 2, maxWidth: 600, mx: "auto", width: "100%" }}>
                <Stack
                    direction="row"
                    alignItems="center"
                    justifyContent="space-between"
                >
                    <Typography variant="h6">{t("promote.sheetTitle")}</Typography>
                    <IconButton onClick={onClose} size="small" aria-label="close">
                        <Close />
                    </IconButton>
                </Stack>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    {t("promote.sheetSubtitle")}
                </Typography>

                {inventories.length > 1 && (
                    <TextField
                        select
                        fullWidth
                        size="small"
                        label={t("promote.target")}
                        value={targetId ?? ""}
                        onChange={(e) => setInventoryId(Number(e.target.value))}
                        data-testid="promote-inventory-picker"
                        sx={{ mb: 2 }}
                    >
                        {inventories.map((inv) => (
                            <MenuItem key={inv.id} value={inv.id}>
                                {inv.name}
                            </MenuItem>
                        ))}
                    </TextField>
                )}

                <Stack spacing={1.5}>
                    {entries.map((entry) => (
                        <PromoteRow
                            key={entry.itemId}
                            entry={entry}
                            draft={seeded[entry.itemId]}
                            onChange={(patch) => updateDraft(entry.itemId, patch)}
                            onOmit={() => handleOmit(entry.itemId)}
                        />
                    ))}
                </Stack>

                <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                    <Button
                        fullWidth
                        color="inherit"
                        onClick={handleClearAll}
                        data-testid="promote-clear-all"
                    >
                        {t("promote.clearAll")}
                    </Button>
                    <Button
                        fullWidth
                        variant="contained"
                        disabled={selectedCount === 0 || !targetId}
                        onClick={handleAdd}
                        data-testid="promote-add-button"
                    >
                        {t("promote.addCount", {
                            count: selectedCount,
                            inventory: targetName,
                        })}
                    </Button>
                </Stack>
            </Box>
        </Drawer>
    );
};

interface PromoteRowProps {
    entry: PromotableEntry;
    draft: RowDraft;
    onChange: (patch: Partial<RowDraft>) => void;
    onOmit: () => void;
}

const PromoteRow = ({ entry, draft, onChange, onOmit }: PromoteRowProps) => {
    const { t } = useTranslation();
    const isRecommended = entry.expiryHandling === "AiRecommendsShelfLife";
    // Same readable hint the inventory list uses; pure fn of the field, so it updates live.
    const info = draft.expiry ? getExpiryInfo(draft.expiry, t) : null;

    return (
        <Box
            data-testid={`promote-row-${entry.name}`}
            sx={{ border: 1, borderColor: "divider", borderRadius: 2, p: 1.5 }}
        >
            <Stack direction="row" alignItems="center" spacing={1}>
                <Checkbox
                    edge="start"
                    checked={draft.selected}
                    onChange={(e) => onChange({ selected: e.target.checked })}
                    data-testid={`promote-row-select-${entry.name}`}
                />
                <Typography sx={{ flex: 1, fontWeight: 600 }}>
                    {entry.name}
                </Typography>
                <Chip
                    size="small"
                    label={
                        isRecommended
                            ? t("promote.tagRecommended")
                            : t("promote.tagEnterDate")
                    }
                    color={isRecommended ? "success" : "warning"}
                    variant="outlined"
                />
                <IconButton
                    size="small"
                    onClick={onOmit}
                    aria-label="omit"
                    data-testid={`promote-row-omit-${entry.name}`}
                >
                    <Close fontSize="small" />
                </IconButton>
            </Stack>
            <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                <TextField
                    size="small"
                    label={t("promote.quantity")}
                    value={draft.quantity}
                    onChange={(e) => onChange({ quantity: e.target.value })}
                    sx={{ width: 110 }}
                />
                <TextField
                    size="small"
                    type="date"
                    label={t("promote.expiry")}
                    value={draft.expiry}
                    onChange={(e) => onChange({ expiry: e.target.value })}
                    InputLabelProps={{ shrink: true }}
                    helperText={info?.humanReadable || " "}
                    sx={{ flex: 1 }}
                />
            </Stack>
        </Box>
    );
};
```

> `InventoryResponse` exposes `id` and `name` (used in `InventoriesPage`/`InventorySummaryCard`); `useCreateInventoryItem` body is `{ text, quantity?, expiryDate? }` (verified in the hook's optimistic update). If `npm run tsc` flags the import path for `useHouseholdInventories`, confirm it is the default export location `../../inventories/useHouseholdInventories`.

- [ ] **Step 2: Type-check + lint + format**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run tsc && npm run lint && npm run prettier
```

Expected: no errors; prettier writes formatting.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx
git commit -m "feat: promote review sheet with selection, picker, recommended hint, omit"
```

---

## Task 10: Frontend verification

**Files:** none (verification only).

- [ ] **Step 1: Full frontend gate**

Run from `Application/Frigorino.Web/ClientApp/`:

```bash
npm run tsc && npm run lint && npm run prettier:check
```

Expected: all pass. Fix any issues, then re-run.

- [ ] **Step 2: Commit any formatting fixups (if needed)**

```bash
git add -A && git commit -m "chore: frontend verification fixups" || echo "nothing to commit"
```

---

## Task 11: End-to-end SPA scenario (Reqnroll + Playwright)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/Promote.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Lists/PromoteSteps.cs`

> Reuses verified driver patterns from `ListItemSteps` (`ctx.Page`, `GetByTestId`, `WaitForResponseAsync`) and the catalog-poll from Task 3. The `Given there is a list ... with item ...`, `When I open the list ...`, and `When I toggle ... as done` steps already exist and seed the item via API (which fires classification). An inventory is created via API.

- [ ] **Step 1: Write the feature**

Create `Application/Frigorino.IntegrationTests/Slices/Lists/Promote.feature`:

```gherkin
Feature: Promote checked items to inventory (SPA)

  Background:
    Given I am logged in with an active household

  Scenario: Checking off a classified perishable offers it for inventory and adds it
    Given there is a list named "Weekly Groceries" with item "Milk"
    And there is an inventory named "Fridge"
    And the product "milk" is in the catalog
    When I open the list "Weekly Groceries"
    And I toggle "Milk" as done
    Then the promote bar shows 1 item
    When I open the promote review sheet
    And I add the selected promote items
    Then the inventory "Fridge" contains an item "Milk"
    And the promote bar is not visible

  Scenario: Omitting an item removes it from the batch without adding it
    Given there is a list named "Weekly Groceries" with item "Milk"
    And there is an inventory named "Fridge"
    And the product "milk" is in the catalog
    When I open the list "Weekly Groceries"
    And I toggle "Milk" as done
    And I open the promote review sheet
    And I omit "Milk" from the promote sheet
    Then the promote bar is not visible
```

- [ ] **Step 2: Write the steps**

Create `Application/Frigorino.IntegrationTests/Slices/Lists/PromoteSteps.cs`:

```csharp
using System.Text.Json;
using Microsoft.Playwright;

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class PromoteSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("there is an inventory named {string}")]
    public async Task GivenThereIsAnInventoryNamed(string name)
    {
        var inventoryId = await api.CreateInventoryAsync(name);
        ctx.InventoryIds[name] = inventoryId;
    }

    [Then("the promote bar shows {int} item")]
    [Then("the promote bar shows {int} items")]
    public async Task ThenThePromoteBarShows(int count)
    {
        var bar = ctx.Page.GetByTestId("promote-bar");
        await Assertions.Expect(bar).ToBeVisibleAsync();
        await Assertions.Expect(bar).ToHaveAttributeAsync("data-count", count.ToString());
    }

    [When("I open the promote review sheet")]
    public async Task WhenIOpenThePromoteReviewSheet()
    {
        await ctx.Page.GetByTestId("promote-bar-review").ClickAsync();
        await ctx.Page.GetByTestId("promote-sheet").WaitForAsync();
    }

    [When("I add the selected promote items")]
    public async Task WhenIAddTheSelectedPromoteItems()
    {
        // Wait for the inventory-item POST so the follow-up DB/API assertion sees a committed row.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/inventories/")
            && r.Url.EndsWith("/items")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("promote-add-button").ClickAsync();
        await responseTask;
    }

    [When("I omit {string} from the promote sheet")]
    public async Task WhenIOmitFromThePromoteSheet(string itemText)
    {
        await ctx.Page.GetByTestId($"promote-row-omit-{itemText}").ClickAsync();
    }

    [Then("the inventory {string} contains an item {string}")]
    public async Task ThenTheInventoryContainsAnItem(string inventoryName, string itemText)
    {
        var inventoryId = ctx.InventoryIds[inventoryName];
        var response = await api.TryGetInventoryItemsAsync(inventoryId);
        Assert.Equal(200, response.Status);
        var items = (await response.JsonAsync())!.Value;
        var texts = items.EnumerateArray()
            .Select(i => i.GetProperty("text").GetString())
            .ToList();
        Assert.Contains(itemText, texts);
    }

    [Then("the promote bar is not visible")]
    public async Task ThenThePromoteBarIsNotVisible()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("promote-bar"))
            .Not.ToBeVisibleAsync();
    }
}
```

> Verify `ScenarioContextHolder` has an `InventoryIds` dictionary (mirrors `ListIds`). If the inventory feature's existing steps already define `Given there is an inventory named {string}` (check `InventorySteps.cs`), delete that duplicate step from `PromoteSteps.cs` and reuse the existing one + its id-tracking. If `ScenarioContextHolder` lacks `InventoryIds`, add `public Dictionary<string, int> InventoryIds { get; } = new();`.

- [ ] **Step 3: Run the E2E scenarios**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Promote"`
Expected: PASS — the `Promote.Api` (Task 3) and `Promote` (SPA) scenarios all green.

> Requires Docker + a built SPA. The IntegrationTests harness builds/serves the SPA; if it serves from `wwwroot`/`build`, run `npm run build` in `ClientApp/` first (per repo testing setup). If the first SPA run flakes on the catalog timing, the `the product "milk" is in the catalog` step already gates on classification before the toggle.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Lists/Promote.feature Application/Frigorino.IntegrationTests/Slices/Lists/PromoteSteps.cs
git commit -m "test: E2E promote-to-inventory happy path and omit"
```

---

## Task 12: Full verification, Docker, and tracking cleanup

**Files:**
- Modify: `IDEAS.md` (trim the completed Cycle 3 sketch).

- [ ] **Step 1: Full solution tests**

Run: `dotnet test Application/Frigorino.sln`
Expected: all `Frigorino.Test` + `Frigorino.IntegrationTests` pass. (Capture `${PIPESTATUS[0]}` / read the pass-fail summary; don't trust a piped tail.)

- [ ] **Step 2: Frontend build**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run build`
Expected: `tsc -b && vite build` succeed.

- [ ] **Step 3: Docker build (catches Dockerfile/SPA/pipeline drift)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: image builds. (If the Docker daemon is unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 4: Trim the shipped cycle from `IDEAS.md`**

In `IDEAS.md`, update the "Promote checked list items into inventory" entry: remove the **Cycle 3** sketch block (it has shipped) and the now-completed bullets, keeping the entry's deferred future-doors note (storage/unit facets, override layer, learning-from-corrections, cross-list batching, localStorage TTL). If Cycles 1, 2, and 2.5 are also fully shipped and nothing actionable remains, remove the whole entry per the "delete tracking items when done" rule.

- [ ] **Step 5: Commit**

```bash
git add IDEAS.md
git commit -m "docs: retire shipped promote-to-inventory cycle from IDEAS"
```

- [ ] **Step 6: Finish the branch**

Use the `superpowers:finishing-a-development-branch` skill to decide merge/PR/cleanup (target: fast-forward into `stage` per the branch workflow).

---

## Self-Review

**Spec coverage:**
- Passive sticky bar → Task 8. Review sheet Option B + per-row selection → Task 9. Split-target add flow → Task 9 `handleAdd` (adds selected, keeps rest, closes when empty). Picker defaulting to newest, hidden for single inventory → Task 9. Perishables-only eligibility → Tasks 1–3. Promote embedded in toggle response, no new endpoint/migration → Tasks 2–3. localStorage persistence → Task 6. Omit vs deselect + Clear all + close-keeps-batch → Tasks 6 (`remove`/`clearForList`) + 9. Recommended human-readable date via `getExpiryInfo` → Task 9 `PromoteRow`. Quantity formatting reuse → Task 9 (`formatQuantity`). Reuse `CreateInventoryItem` → Task 9. Tests: domain predicate (Task 1), DTO factory (Task 2), API IT incl. un-check/non-perishable (Task 3), SPA happy + omit (Task 11). All spec testing items covered.

**Placeholder scan:** Task 8 intentionally ships a one-line stub for `PromoteReviewSheet`, fully replaced in Task 9 — not a placeholder in the delivered result; every code step shows real code.

**Type consistency:** `usePromotableStore`/`usePromotableForList`/`PromotableEntry` consistent across Tasks 6, 7, 9. `PromoteSuggestion(ExpiryHandling, DateOnly? SuggestedExpiry)` consistent across Tasks 2, 3, and the TS `promote.expiryHandling`/`promote.suggestedExpiry` in Tasks 7, 9. Store actions `add`/`remove`/`clearForList` used identically in Tasks 7 and 9. Testids (`promote-bar`, `promote-bar-review`, `promote-sheet`, `promote-add-button`, `promote-clear-all`, `promote-row-omit-{name}`) defined in Tasks 8–9 and consumed in Task 11.

**Correction vs spec:** `Product` has no `IsActive` column (cascade-deletes with the household), so the Task-3 lookup does not filter `IsActive` — the spec's "filtered IsActive" aside does not apply.

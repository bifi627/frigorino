# List Item Comments / Hints Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional free-text `Comment` ("hint") to list items, separate from the item name, settable at create and edit, viewable in the list without entering edit mode.

**Architecture:** One nullable `Comment` column on the flat `ListItem` table (source-of-truth `CommentMaxLength` const). Set/clear flows through the existing `List` aggregate methods (`AddItem`/`UpdateItem`) with `null = preserve`, empty/whitespace = clear-to-null semantics (mirroring `List.Update`'s `Description` handling — no `clearComment` flag). The existing `CreateItem`/`UpdateItem` slices and `ListItemResponse` carry the field; no new endpoint. Frontend adds a `commentComposerFeature` (mirroring `quantityComposerFeature`) to the list composer and a small tap-to-expand preview line in `ListItemContent`.

**Tech Stack:** .NET 10 vertical slices + EF Core (Postgres), FluentResults; React 19 + MUI + TanStack Query; xUnit (domain unit tests) + Reqnroll/Playwright (integration).

**Spec:** `docs/superpowers/specs/2026-06-03-list-item-comments-design.md`

---

## File Structure

**Backend (created/modified):**
- Modify `Application/Frigorino.Domain/Entities/ListItem.cs` — add `CommentMaxLength` const + `Comment` property.
- Modify `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs` — `HasMaxLength` for `Comment`.
- Create `Application/Frigorino.Infrastructure/Migrations/*_AddListItemComment.cs` (generated).
- Modify `Application/Frigorino.Domain/Entities/List.cs` — thread `comment` through `AddItem` + `UpdateItem`, add `ValidateComment`, extend the no-op guard.
- Modify `Application/Frigorino.Features/Lists/Items/CreateItem.cs` — `CreateItemRequest.Comment`, pass to `AddItem`.
- Modify `Application/Frigorino.Features/Lists/Items/UpdateItem.cs` — `UpdateItemRequest.Comment`, pass to `UpdateItem`.
- Modify `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs` — `Comment` in record, `From`, `ToProjection`.

**Tests:**
- Modify `Application/Frigorino.Test/Domain/ListAggregateItemTests.cs` — comment unit tests.
- Modify `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` — `comment` params on create/update helpers.
- Modify `Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.Api.feature` + `ListItemApiSteps.cs` — comment round-trip scenario.

**Frontend (created/modified):**
- `Application/Frigorino.Web/ClientApp/src/lib/api/**` — regenerated (`npm run api`).
- Modify `public/locales/{en,de}/translation.json` — i18n keys.
- Create `src/components/composer/features/commentComposerFeature.tsx`; export from `src/components/composer/index.ts`.
- Modify `src/features/lists/items/components/ListFooter.tsx` — wire comment feature into add + edit composers.
- Modify `src/features/lists/pages/ListViewPage.tsx` — thread comment into create/update mutation bodies.
- Modify `src/features/lists/items/useCreateListItem.ts` + `useUpdateListItem.ts` — optimistic comment.
- Modify `src/features/lists/items/components/ListItemContent.tsx` — tap-to-expand comment preview.

**Docs:**
- Modify `IDEAS.md` — remove the shipped "List item comments / hints" entry.

---

### Task 1: Add `Comment` column, EF config, and migration

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/ListItem.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs`
- Create: `Application/Frigorino.Infrastructure/Migrations/*_AddListItemComment.cs` (generated)

- [ ] **Step 1: Add the const + property to the entity**

In `ListItem.cs`, directly under the existing `TextMaxLength` const and the `Text` property, add:

```csharp
        // Source of truth for length constraints. Both the List aggregate methods and the
        // EF configuration (ListItemConfiguration) read from this so DB and aggregate agree.
        public const int TextMaxLength = 500;
        public const int CommentMaxLength = 500;
```

And add the property next to `Text` (a free-text hint, kept separate from the machine-parseable name):

```csharp
        public string Text { get; set; } = string.Empty;

        // Optional free-text hint ("the blue one", "ask the butcher"). Distinct from Text:
        // the name stays clean/parseable, the comment stays human prose. Never routed by
        // ItemTextRouter. null = no comment.
        public string? Comment { get; set; }
```

- [ ] **Step 2: Configure the column max length**

In `ListItemConfiguration.cs`, add after the `Text` property configuration block:

```csharp
            builder.Property(li => li.Text)
                .HasMaxLength(ListItem.TextMaxLength)
                .IsRequired();

            builder.Property(li => li.Comment)
                .HasMaxLength(ListItem.CommentMaxLength);
```

- [ ] **Step 3: Generate the migration**

Run:
```bash
dotnet ef migrations add AddListItemComment --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: a new migration file under `Application/Frigorino.Infrastructure/Migrations/` adding a nullable `Comment` column to `ListItems` (`AddColumn<string>(... nullable: true, maxLength: 500)`). Open it and confirm `Up` adds the column and `Down` drops it — no other changes.

- [ ] **Step 4: Build to verify**

Run:
```bash
dotnet build Application/Frigorino.sln
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/ListItem.cs Application/Frigorino.Infrastructure/EntityFramework/Configurations/ListItemConfiguration.cs Application/Frigorino.Infrastructure/Migrations
git commit -m "feat: add nullable Comment column to ListItem"
```

---

### Task 2: Thread `comment` through `List.AddItem` (TDD)

**Files:**
- Modify: `Application/Frigorino.Test/Domain/ListAggregateItemTests.cs`
- Modify: `Application/Frigorino.Domain/Entities/List.cs`

- [ ] **Step 1: Write the failing tests**

In `ListAggregateItemTests.cs`, add under the `// ------- AddItem -------` section:

```csharp
        [Fact]
        public void AddItem_SetsTrimmedComment()
        {
            var list = NewList();

            var result = list.AddItem("Milk", quantity: null, comment: "  the blue one  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("the blue one", result.Value.Comment);
        }

        [Fact]
        public void AddItem_WhitespaceComment_StoredAsNull()
        {
            var list = NewList();

            var result = list.AddItem("Milk", quantity: null, comment: "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddItem_NoComment_StoredAsNull()
        {
            var list = NewList();

            var result = list.AddItem("Milk");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddItem_CommentTooLong_FailsKeyedOnComment()
        {
            var list = NewList();
            var tooLong = new string('x', ListItem.CommentMaxLength + 1);

            var result = list.AddItem("Milk", quantity: null, comment: tooLong);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Comment), result.Errors[0].Metadata["Property"]);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests.AddItem_SetsTrimmedComment|FullyQualifiedName~ListAggregateItemTests.AddItem_CommentTooLong_FailsKeyedOnComment"
```
Expected: FAIL — `AddItem` has no `comment` parameter (compile error) / assertions fail.

- [ ] **Step 3: Implement — add the `comment` parameter, helper, and assignment**

In `List.cs`, change the `AddItem` signature to take a trailing optional `comment`, validate it, and assign:

```csharp
        public Result<ListItem> AddItem(string text, Quantity? quantity = null, string? comment = null)
        {
            var errors = ValidateItemText(text, requireText: true);
            errors.AddRange(ValidateComment(comment));
            if (errors.Count > 0)
            {
                return Result.Fail<ListItem>(errors);
            }

            var now = DateTime.UtcNow;
            var item = new ListItem
            {
                ListId = Id,
                Text = text.Trim(),
                Comment = NormalizeComment(comment),
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

Add these two private helpers next to `ValidateItemText`:

```csharp
        // empty/whitespace comment is normalized to null; otherwise trimmed.
        private static string? NormalizeComment(string? comment)
        {
            return string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        }

        private static List<IError> ValidateComment(string? comment)
        {
            var errors = new System.Collections.Generic.List<IError>();
            if (!string.IsNullOrWhiteSpace(comment) && comment.Trim().Length > ListItem.CommentMaxLength)
            {
                errors.Add(new Error($"Item comment must be {ListItem.CommentMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(ListItem.Comment)));
            }
            return errors;
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests.AddItem"
```
Expected: PASS (the new comment tests plus the existing `AddItem_*` tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/List.cs Application/Frigorino.Test/Domain/ListAggregateItemTests.cs
git commit -m "feat: accept comment in List.AddItem"
```

---

### Task 3: Thread `comment` through `List.UpdateItem` (TDD)

**Files:**
- Modify: `Application/Frigorino.Test/Domain/ListAggregateItemTests.cs`
- Modify: `Application/Frigorino.Domain/Entities/List.cs`

- [ ] **Step 1: Write the failing tests**

In `ListAggregateItemTests.cs`, add under the `// ------- UpdateItem -------` section:

```csharp
        [Fact]
        public void UpdateItem_SetsTrimmedComment()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: "  ask the butcher  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("ask the butcher", item.Comment);
        }

        [Fact]
        public void UpdateItem_NullComment_PreservesExistingComment()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.Comment = "the blue one";

            var result = list.UpdateItem(item.Id, text: "Soy milk", quantity: null, clearQuantity: false, status: null, comment: null);

            Assert.True(result.IsSuccess);
            Assert.Equal("the blue one", item.Comment);
        }

        [Fact]
        public void UpdateItem_EmptyComment_ClearsExistingComment()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            item.Comment = "the blue one";

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(item.Comment);
        }

        [Fact]
        public void UpdateItem_CommentOnly_IsNotTreatedAsNoOp()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: "for Anna's party");

            Assert.True(result.IsSuccess);
            Assert.Equal("for Anna's party", item.Comment);
        }

        [Fact]
        public void UpdateItem_CommentTooLong_FailsKeyedOnComment()
        {
            var list = NewList();
            var item = AddSeed(list, "Milk");
            var tooLong = new string('x', ListItem.CommentMaxLength + 1);

            var result = list.UpdateItem(item.Id, text: null, quantity: null, clearQuantity: false, status: null, comment: tooLong);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(ListItem.Comment), result.Errors[0].Metadata["Property"]);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests.UpdateItem_SetsTrimmedComment|FullyQualifiedName~ListAggregateItemTests.UpdateItem_CommentOnly_IsNotTreatedAsNoOp"
```
Expected: FAIL — `UpdateItem` has no `comment` parameter (compile error).

- [ ] **Step 3: Implement — add `comment`, extend the no-op guard, validate, assign**

In `List.cs`, change `UpdateItem` to add the trailing `comment` parameter. Update the no-op guard, add comment validation alongside text validation, and assign the comment with preserve/clear semantics:

```csharp
        public Result<ListItem> UpdateItem(int itemId, string? text, Quantity? quantity, bool clearQuantity, bool? status, string? comment = null)
        {
            var item = ListItems.FirstOrDefault(i => i.Id == itemId && i.IsActive);
            if (item is null)
            {
                return Result.Fail<ListItem>(
                    new EntityNotFoundError($"List item {itemId} not found."));
            }

            // text/quantity/status/comment are "preserve on null"; clearQuantity is the explicit
            // "remove the quantity" intent. With none of them set the payload is a guaranteed no-op —
            // reject it rather than returning 200 OK on garbage.
            if (text is null && quantity is null && !clearQuantity && status is null && comment is null)
            {
                return Result.Fail<ListItem>(
                    new Error("Update request must set at least one field.")
                        .WithMetadata("Property", string.Empty));
            }

            var errors = ValidateItemText(text, requireText: text is not null);
            errors.AddRange(ValidateComment(comment));
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
            // comment == null means "preserve"; an empty/whitespace string clears it; otherwise set.
            if (comment is not null)
            {
                item.Comment = NormalizeComment(comment);
            }
            // clearQuantity removes it; otherwise quantity == null means "preserve" and a non-null
            // quantity writes both columns. (clearQuantity wins over a stray quantity value.)
            if (clearQuantity)
            {
                item.QuantityValue = null;
                item.QuantityUnit = null;
            }
            else if (quantity is not null)
            {
                var q = quantity.Value;
                item.QuantityValue = q.Value;
                item.QuantityUnit = q.Unit;
            }

            item.UpdatedAt = DateTime.UtcNow;
            return Result.Ok(item);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests"
```
Expected: PASS (all `ListAggregateItemTests`, including the existing `UpdateItem_AllNullFields_FailsAsValidationError` which still holds because all five mutation fields are null).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/List.cs Application/Frigorino.Test/Domain/ListAggregateItemTests.cs
git commit -m "feat: accept comment in List.UpdateItem with preserve/clear semantics"
```

---

### Task 4: Carry `Comment` through the slices and response DTO

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/CreateItem.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/UpdateItem.cs`
- Modify: `Application/Frigorino.Features/Lists/Items/ListItemResponse.cs`

- [ ] **Step 1: Add `Comment` to `ListItemResponse` (record, `From`, `ToProjection`)**

In `ListItemResponse.cs`, add the field to the positional record (after `Text`):

```csharp
    public sealed record ListItemResponse(
        int Id,
        int ListId,
        string Text,
        string? Comment,
        QuantityDto? Quantity,
        bool Status,
        int SortOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool ExtractionPending = false)
```

Update `From` to pass `item.Comment`:

```csharp
        public static ListItemResponse From(ListItem item)
        {
            return new ListItemResponse(
                item.Id,
                item.ListId,
                item.Text,
                item.Comment,
                item.QuantityValue == null
                    ? null
                    : new QuantityDto(item.QuantityValue.Value, item.QuantityUnit!.Value),
                item.Status,
                item.SortOrder,
                item.CreatedAt,
                item.UpdatedAt);
        }
```

Update `ToProjection` to project `i.Comment`:

```csharp
        public static readonly Expression<Func<ListItem, ListItemResponse>> ToProjection = i => new ListItemResponse(
            i.Id,
            i.ListId,
            i.Text,
            i.Comment,
            i.QuantityValue == null
                ? null
                : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.Status,
            i.SortOrder,
            i.CreatedAt,
            i.UpdatedAt,
            false);
```

- [ ] **Step 2: Add `Comment` to `CreateItemRequest` and pass it to `AddItem`**

In `CreateItem.cs`, change the request record and the `AddItem` call:

```csharp
    public sealed record CreateItemRequest(string Text, string? Comment);
```

```csharp
            var analysis = ItemTextRouter.Analyze(request.Text);

            var result = list.AddItem(analysis.CleanName, analysis.Quantity, request.Comment);
```

(The router still analyzes `Text` only — the comment is passed straight through, never routed.)

- [ ] **Step 3: Add `Comment` to `UpdateItemRequest` and pass it to `UpdateItem`**

In `UpdateItem.cs`, change the request record:

```csharp
    public sealed record UpdateItemRequest(string? Text, QuantityDto? Quantity, bool? ClearQuantity, bool? Status, string? Comment);
```

And pass `request.Comment` as the trailing argument to the aggregate call:

```csharp
            var result = list.UpdateItem(itemId, textToWrite, quantity, request.ClearQuantity ?? false, request.Status, request.Comment);
```

(No change to the `textChangedWithoutQuantityIntent` router gate — comment never participates.)

- [ ] **Step 4: Build to verify**

Run:
```bash
dotnet build Application/Frigorino.sln
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/CreateItem.cs Application/Frigorino.Features/Lists/Items/UpdateItem.cs Application/Frigorino.Features/Lists/Items/ListItemResponse.cs
git commit -m "feat: carry comment through list item create/update slices and response"
```

---

### Task 5: API-level integration test for the comment round-trip

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/ListItems.Api.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/ListItemApiSteps.cs`

- [ ] **Step 1: Add `comment` to the test API client create/update helpers**

In `TestApiClient.cs`, add a trailing optional `comment` to `TryCreateListItemAsync` and include it in the POST body:

```csharp
    public Task<IAPIResponse> TryCreateListItemAsync(int listId, string? text, int? householdId = null, string? comment = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items",
            new APIRequestContextOptions
            {
                DataObject = new { text, comment },
                Headers = AuthHeaders,
            });
    }
```

And add a trailing optional `comment` to `TryUpdateListItemAsync`, including it in the PUT body:

```csharp
    public Task<IAPIResponse> TryUpdateListItemAsync(int listId, int itemId, string? text, string? quantity, bool? status, int? householdId = null, string? comment = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.PutAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/{itemId}",
            new APIRequestContextOptions
            {
                DataObject = new { text, quantity, status, comment },
                Headers = AuthHeaders,
            });
    }
```

- [ ] **Step 2: Add the scenario to the feature file**

Append to `ListItems.Api.feature`:

```gherkin
  Scenario: Creating an item with a comment round-trips through the API
    Given there is a list named "Weekly Groceries"
    When I POST an item "Milk" with comment "the blue one" to "Weekly Groceries" via the API
    Then the API response status is 201
    And the API item "Milk" in "Weekly Groceries" has comment "the blue one"

  Scenario: Clearing a comment via an empty-string update
    Given there is a list named "Weekly Groceries"
    When I POST an item "Milk" with comment "the blue one" to "Weekly Groceries" via the API
    And I PUT a comment "" onto "Milk" in "Weekly Groceries" via the API
    Then the API response status is 200
    And the API item "Milk" in "Weekly Groceries" has no comment
```

- [ ] **Step 3: Add the step definitions**

Append these steps to `ListItemApiSteps.cs` (inside the class):

```csharp
    [When("I POST an item {string} with comment {string} to {string} via the API")]
    public async Task WhenIPostAnItemWithCommentViaTheApi(string itemText, string comment, string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryCreateListItemAsync(listId, itemText, comment: comment);
    }

    [When("I PUT a comment {string} onto {string} in {string} via the API")]
    public async Task WhenIPutACommentOntoViaTheApi(string comment, string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, itemText);
        ctx.LastApiResponse = await api.TryUpdateListItemAsync(listId, itemId, text: null, quantity: null, status: null, comment: comment);
    }

    [Then("the API item {string} in {string} has comment {string}")]
    public async Task ThenTheApiItemHasComment(string itemText, string listName, string expectedComment)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var item = json!.Value.EnumerateArray()
            .First(e => e.GetProperty("text").GetString() == itemText);
        Assert.Equal(expectedComment, item.GetProperty("comment").GetString());
    }

    [Then("the API item {string} in {string} has no comment")]
    public async Task ThenTheApiItemHasNoComment(string itemText, string listName)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);

        var json = await response.JsonAsync();
        var item = json!.Value.EnumerateArray()
            .First(e => e.GetProperty("text").GetString() == itemText);
        var comment = item.GetProperty("comment");
        Assert.True(comment.ValueKind == System.Text.Json.JsonValueKind.Null);
    }
```

> Note: `GetListItemId` resolves item ids by text from a previously-fetched list — confirm the existing `Given there is a list named "X"` background seeds the household/list. The other scenarios in this file use the same `ctx.ListIds` / `GetListItemId` helpers, so this mirrors them.

- [ ] **Step 4: Run the integration test**

Requires Docker running (Postgres Testcontainers). Run:
```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~ListItemsAPI"
```
Expected: the two new scenarios PASS (alongside the existing List Items API scenarios). If the filter name does not match, run the full IT project: `dotnet test Application/Frigorino.IntegrationTests`.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests
git commit -m "test: API integration coverage for list item comments"
```

---

### Task 6: Regenerate the frontend API client

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/lib/api/**` (generated), `Application/Frigorino.Web/ClientApp/src/lib/openapi.json`

- [ ] **Step 1: Regenerate the client**

From `Application/Frigorino.Web/ClientApp/`:
```bash
npm run api
```
Expected: rebuilds the backend, emits `src/lib/openapi.json`, regenerates `src/lib/api/`. The generated `ListItemResponse` type now has `comment?: string | null`; `CreateItemRequest` has `comment?: string | null`; `UpdateItemRequest` has `comment?: string | null`.

- [ ] **Step 2: Verify the generated types include `comment`**

Run (from `ClientApp/`):
```bash
npm run tsc
```
Expected: type-check passes (no consumers broken yet; new optional field is additive).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib
git commit -m "chore: regenerate API client with item comment field"
```

---

### Task 7: Add i18n keys (EN/DE)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add the keys under the existing `lists` object**

In `en/translation.json`, add to the `lists` object:

```json
    "comment": "Comment",
    "addComment": "Add comment",
    "commentPlaceholder": "Add a hint…"
```

In `de/translation.json`, add to the `lists` object:

```json
    "comment": "Kommentar",
    "addComment": "Kommentar hinzufügen",
    "commentPlaceholder": "Hinweis hinzufügen…"
```

> If `lists.comment` / `lists.addComment` already exist, reuse them rather than duplicating. Place the keys next to other `lists.*` entries; keep the JSON valid (watch trailing commas).

- [ ] **Step 2: Verify JSON validity**

Run (from `ClientApp/`):
```bash
npm run tsc
```
Expected: passes (JSON imports parse). A malformed JSON would surface in the dev server / lint; if unsure, also run `npm run lint`.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales
git commit -m "feat: i18n keys for list item comments"
```

---

### Task 8: Create the `commentComposerFeature`

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/composer/features/commentComposerFeature.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/components/composer/index.ts`

- [ ] **Step 1: Create the feature**

Create `commentComposerFeature.tsx` (mirrors `quantityComposerFeature.tsx`; draft is a plain `string`):

```tsx
/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { Clear, StickyNote2 } from "@mui/icons-material";
import { Box, Chip, IconButton, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

// Client mirror of the backend ListItem.CommentMaxLength const (not exported to TS).
// Keep in sync if the backend cap changes.
export const COMMENT_MAX_LENGTH = 500;

export const isCommentValid = (value: string): boolean =>
    value.trim().length <= COMMENT_MAX_LENGTH;

const CommentToggle = ({ value, open, toggleOpen }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    return (
        <IconButton
            onClick={toggleOpen}
            aria-label={t("lists.addComment")}
            sx={{
                minWidth: 44,
                minHeight: 44,
                color: value.trim() || open ? "primary.main" : "inherit",
            }}
        >
            <StickyNote2 fontSize="small" />
        </IconButton>
    );
};

const CommentChip = ({ value, toggleOpen }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    return (
        <Chip
            clickable
            onClick={toggleOpen}
            aria-label={`${t("common.edit")} ${t("lists.comment")}`}
            size="small"
            icon={<StickyNote2 fontSize="small" />}
            label={value.trim()}
            sx={{ minHeight: 32, maxWidth: 220 }}
        />
    );
};

const CommentPanel = ({ value, setValue, disabled }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    const invalid = !isCommentValid(value);
    return (
        <Box
            sx={{ display: "flex", gap: 1, alignItems: "flex-start", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <TextField
                fullWidth
                multiline
                minRows={1}
                maxRows={4}
                variant="outlined"
                placeholder={t("lists.commentPlaceholder")}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                disabled={disabled}
                error={invalid}
                size="small"
                slotProps={{
                    htmlInput: {
                        "data-testid": "composer-comment",
                    },
                }}
            />
            <IconButton
                onClick={() => setValue("")}
                disabled={disabled || value.trim() === ""}
                title={t("common.clear")}
                aria-label={t("common.clear")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Clear fontSize="small" />
            </IconButton>
        </Box>
    );
};

export const commentComposerFeature = defineModifier({
    id: "comment",
    initial: "",
    isEmpty: (value) => value.trim() === "",
    isValid: isCommentValid,
    renderToggle: (slot) => <CommentToggle {...slot} />,
    renderPanel: (slot) => <CommentPanel {...slot} />,
    renderChip: (slot) => <CommentChip {...slot} />,
});
```

- [ ] **Step 2: Export it from the composer barrel**

In `src/components/composer/index.ts`, add after the `expiryFeature` export:

```ts
export { expiryFeature } from "./features/expiryFeature";
export {
    commentComposerFeature,
    isCommentValid,
    COMMENT_MAX_LENGTH,
} from "./features/commentComposerFeature";
```

- [ ] **Step 3: Verify type-check and lint**

Run (from `ClientApp/`):
```bash
npm run tsc && npm run lint
```
Expected: passes.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer
git commit -m "feat: add commentComposerFeature mirroring the quantity feature"
```

---

### Task 9: Wire the comment feature into the list composer

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListFooter.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx`

- [ ] **Step 1: Update `ListFooter` imports and feature sets**

In `ListFooter.tsx`, extend the composer import and feature constants. Change the import block:

```tsx
import {
    Composer,
    commentComposerFeature,
    draftToQuantity,
    formatQuantity,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
```

Replace the feature-set constants:

```tsx
// Lists add via free-text (extraction fills the quantity), so the add composer stays
// quantity-free — but a comment can be attached at add time. Manual quantity entry/correction
// happens in edit mode.
const EDIT_FEATURES = [quantityComposerFeature, commentComposerFeature] as const;
const ADD_FEATURES = [commentComposerFeature] as const;
```

- [ ] **Step 2: Update the prop signatures, initial draft, and completion handler**

Change the two callback prop types in `ListFooterProps`:

```tsx
    onAddItem: (data: string, comment: string | null) => void;
    onUpdateItem: (
        data: string,
        quantity: QuantityDto | null,
        comment: string | null,
    ) => void;
```

Change the `features` selection:

```tsx
        const features = editingItem ? EDIT_FEATURES : ADD_FEATURES;
```

Seed the comment in `initialDraft`:

```tsx
        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: {
                              quantity: quantityToDraft(editingItem.quantity),
                              comment: editingItem.comment ?? "",
                          },
                      }
                    : undefined,
            [editingItem],
        );
```

Update `handleComplete` to forward the comment in both modes (empty → null):

```tsx
        const handleComplete = useCallback(
            (r: Completion<typeof EDIT_FEATURES>) => {
                const comment = r.comment.trim() || null;
                if (r.mode === "edit") {
                    onUpdateItem(r.text, draftToQuantity(r.quantity), comment);
                } else {
                    onAddItem(r.text, comment);
                    onScrollToLastUnchecked();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLastUnchecked],
        );
```

> `handleComplete` is typed against `EDIT_FEATURES` (the superset that has both `quantity` and `comment`), matching the existing pattern. `r.comment` is present at runtime in both add and edit because both feature sets include `commentComposerFeature`.

- [ ] **Step 3: Update `ListViewPage` handlers to thread the comment**

In `ListViewPage.tsx`, update `handleAddItem` to accept and send a comment:

```tsx
    const handleAddItem = useCallback(
        async (data: string, comment: string | null) => {
            if (!householdId) return;
            try {
                const created = await createMutation.mutateAsync({
                    path: { householdId, listId: listIdNum },
                    body: { text: data, comment },
                });
                setPendingExtraction({
                    id: created.id,
                    extractionPending: created.extractionPending,
                });
            } catch {
                // createMutation.onError rolls back the optimistic item; nothing to do here.
            }
        },
        [createMutation, householdId, listIdNum],
    );
```

And `handleUpdateItem` to accept and send a comment:

```tsx
    const handleUpdateItem = useCallback(
        (data: string, quantity: QuantityDto | null, comment: string | null) => {
            if (editingItem?.id && householdId) {
                updateMutation.mutate({
                    path: {
                        householdId,
                        listId: listIdNum,
                        itemId: editingItem.id,
                    },
                    body: {
                        text: data,
                        quantity,
                        clearQuantity: quantity === null,
                        status: null,
                        comment,
                    },
                });
                setEditOpenQuantity(false);
                setEditingItem(null);
            }
        },
        [editingItem?.id, updateMutation, householdId, listIdNum],
    );
```

- [ ] **Step 4: Verify type-check and lint**

Run (from `ClientApp/`):
```bash
npm run tsc && npm run lint
```
Expected: passes.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListFooter.tsx Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx
git commit -m "feat: wire comment into list add/edit composer"
```

---

### Task 10: Optimistic comment in the create/update hooks

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useCreateListItem.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useUpdateListItem.ts`

- [ ] **Step 1: Add comment to the optimistic create item**

In `useCreateListItem.ts`, set `comment` on the `optimisticItem` (it currently hard-codes other fields):

```tsx
            const optimisticItem: ListItemResponse = {
                id: tempId,
                text: variables.body.text,
                comment: variables.body.comment ?? null,
                quantity: null,
                status: false,
                sortOrder: lastUncheckedSortOrder + 1,
                listId: variables.path.listId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
                extractionPending: false,
            };
```

- [ ] **Step 2: Apply comment in the optimistic update (list cache + detail cache)**

In `useUpdateListItem.ts`, in the `onMutate` list-cache `map`, add `comment` to the updated item object (alongside `text`/`quantity`):

```tsx
                    return old.map((item) =>
                        item.id === variables.path.itemId
                            ? {
                                  ...item,
                                  text: variables.body.text ?? item.text,
                                  quantity: variables.body.clearQuantity
                                      ? null
                                      : (variables.body.quantity ??
                                        item.quantity),
                                  comment:
                                      variables.body.comment == null
                                          ? item.comment
                                          : (variables.body.comment.trim() ||
                                            null),
                                  updatedAt: new Date().toISOString(),
                              }
                            : item,
                    );
```

And in the detail-cache `setQueryData`, add the same `comment` line:

```tsx
                queryClient.setQueryData<ListItemResponse>(detailKey, {
                    ...currentItem,
                    text: variables.body.text ?? currentItem.text,
                    quantity: variables.body.clearQuantity
                        ? null
                        : (variables.body.quantity ?? currentItem.quantity),
                    comment:
                        variables.body.comment == null
                            ? currentItem.comment
                            : (variables.body.comment.trim() || null),
                    updatedAt: new Date().toISOString(),
                });
```

> `== null` treats both `null` and `undefined` as "preserve" (matching the domain's `null = preserve`); empty/whitespace clears; otherwise sets the trimmed value.

- [ ] **Step 3: Verify type-check and lint**

Run (from `ClientApp/`):
```bash
npm run tsc && npm run lint
```
Expected: passes.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/useCreateListItem.ts Application/Frigorino.Web/ClientApp/src/features/lists/items/useUpdateListItem.ts
git commit -m "feat: optimistic comment in list item create/update hooks"
```

---

### Task 11: Show the comment as a tap-to-expand preview in the list

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx`

- [ ] **Step 1: Update imports and add expand state**

Change the top imports to add `useState` and the icon:

```tsx
import { Box, Link, ListItemText, Typography } from "@mui/material";
import { StickyNote2 } from "@mui/icons-material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import { useLongPress } from "../../../../hooks/useLongPress";
import type { ListItemResponse } from "../../../../lib/api";
```

Inside the `ListItemContent` component body, add state (after the `useLongPress` call):

```tsx
    const [commentExpanded, setCommentExpanded] = useState(false);
```

- [ ] **Step 2: Render the comment line in the `secondary` slot**

Replace the existing `secondary={ item.quantity ? (...) : null }` block with one that renders the quantity chip and/or the comment preview as a small column:

```tsx
            secondary={
                item.quantity || item.comment ? (
                    <Box
                        sx={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 0.25,
                        }}
                    >
                        {item.quantity ? (
                            <Box
                                sx={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: 0.5,
                                }}
                            >
                                <ItemQuantityChip
                                    quantity={item.quantity}
                                    onClick={onEditQuantity}
                                    testId={`list-item-quantity-${item.text}`}
                                />
                            </Box>
                        ) : null}
                        {item.comment ? (
                            <Box
                                component="span"
                                role="button"
                                tabIndex={0}
                                onClick={(e) => {
                                    e.stopPropagation();
                                    setCommentExpanded((v) => !v);
                                }}
                                data-testid={`list-item-comment-${item.id}`}
                                sx={{
                                    display: "inline-flex",
                                    alignItems: "flex-start",
                                    gap: 0.5,
                                    cursor: "pointer",
                                }}
                            >
                                <StickyNote2
                                    sx={{
                                        fontSize: "0.85rem",
                                        mt: "1px",
                                        color: "text.disabled",
                                    }}
                                />
                                <Typography
                                    variant="caption"
                                    color="text.secondary"
                                    sx={{
                                        fontSize: "0.7rem",
                                        ...(commentExpanded
                                            ? {
                                                  whiteSpace: "pre-wrap",
                                                  wordBreak: "break-word",
                                              }
                                            : {
                                                  display: "-webkit-box",
                                                  WebkitLineClamp: 1,
                                                  WebkitBoxOrient: "vertical",
                                                  overflow: "hidden",
                                              }),
                                    }}
                                >
                                    {item.comment}
                                </Typography>
                            </Box>
                        ) : null}
                    </Box>
                ) : null
            }
```

> The comment line's `onClick` calls `stopPropagation` so toggling expand does not bubble to the row. Exact font size / line-clamp count is adjustable polish per the spec.

- [ ] **Step 3: Verify type-check and lint**

Run (from `ClientApp/`):
```bash
npm run tsc && npm run lint
```
Expected: passes.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx
git commit -m "feat: show list item comment as tap-to-expand preview"
```

---

### Task 12: Full verification, manual UI check, and IDEAS.md cleanup

**Files:**
- Modify: `IDEAS.md`

- [ ] **Step 1: Run the prettier writer and frontend checks**

From `ClientApp/`:
```bash
npm run fix && npm run tsc && npm run lint
```
Expected: prettier writes any formatting, tsc and lint pass. Stage any prettier changes.

- [ ] **Step 2: Build the SPA (so the IT harness / dev stack pick up the new testids)**

From `ClientApp/`:
```bash
npm run build
```
Expected: `tsc -b && vite build` succeeds, outputs to `ClientApp/build`.

- [ ] **Step 3: Run the full solution test suite**

From repo root (Docker must be running for Testcontainers):
```bash
dotnet test Application/Frigorino.sln
```
Expected: all tests pass (unit + integration). If the undo-toast inventory IT flakes, re-run before suspecting a regression.

- [ ] **Step 4: Docker build (drift check)**

```bash
docker build -f Application/Dockerfile -t frigorino .
```
Expected: image builds (no project/Dockerfile drift; SPA build copies into `wwwroot`).

- [ ] **Step 5: Manual UI verification**

Bring up the dev stack (`/dev-up` skill) and verify in the browser: add an item with a comment via the composer note toggle; confirm the preview line shows under the name and a long comment expands on tap; edit an item to change and to clear its comment; reload and confirm persistence. (Verbatim-from-plan UI code can hide runtime/DOM bugs that static checks miss.)

- [ ] **Step 6: Remove the shipped idea from IDEAS.md**

Delete the entire `## List item comments / hints` section from `IDEAS.md` (the header through the end of that entry's `**Impact / cost:**` bullet), including the `---` separator that precedes it. This entry's work is now shipped.

Verify nothing else references it:
```bash
git diff IDEAS.md
```
Expected: only the comments/hints section removed; the file remains valid markdown.

- [ ] **Step 7: Commit**

```bash
git add IDEAS.md Application/Frigorino.Web/ClientApp
git commit -m "docs: remove shipped list item comments idea from IDEAS.md"
```

---

## Self-Review Notes

- **Spec coverage:** entity+const (Task 1), aggregate AddItem (2) / UpdateItem (3), slices+response (4), API client regen (6), composer feature (8), composer wiring (9), optimistic hooks (10), list display (11), i18n (7), domain tests (2,3), integration test (5), verification + IDEAS cleanup (12). All spec sections map to a task.
- **Type consistency:** `comment` is a trailing optional param on `AddItem`/`UpdateItem` (existing callers unaffected); `NormalizeComment`/`ValidateComment` defined once (Task 2), reused (Task 3); `ListItemResponse` positional order (`Id, ListId, Text, Comment, Quantity, …`) is identical in record/`From`/`ToProjection`; frontend preserve-semantics use `== null` consistently in both hooks; `commentComposerFeature` id `"comment"` matches `r.comment` and `values.comment`.
- **Known follow-ups (out of scope, per spec):** InventoryItem mirror + promote-to-inventory carry-over.

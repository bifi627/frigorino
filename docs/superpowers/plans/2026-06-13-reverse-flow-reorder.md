# Reverse Flow: Inventory Item → Shopping List (Re-order) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a one-tap "Add to list" action on an inventory item that creates a pre-filled list item (name + structured quantity) on a chosen shopping list, without touching the inventory item.

**Architecture:** Reuses the existing `CreateItem` list-item slice (one new optional `Quantity` field, no new endpoint, no migration). The frontend adds an "Add to list" entry to the existing shared per-item kebab menu (`SortableListItem`), wired only for inventory rows, which opens a bottom-sheet (`ReorderSheet`) modeled on the promote review sheet but reduced to a single item. The inventory item is never mutated.

**Tech Stack:** .NET 10 vertical slice + EF Core; React 19 + TanStack Query + MUI; hey-api generated client; xUnit + FakeItEasy; Reqnroll + Playwright integration tests.

**Spec:** `docs/superpowers/specs/2026-06-13-reverse-flow-reorder-design.md`

---

## File Structure

**Backend**
- Modify: `Application/Frigorino.Features/Lists/Items/CreateItem.cs` — optional `Quantity` on request; handler branch; `Handle` made `public` for unit testing.
- Create: `Application/Frigorino.Test/Features/CreateItemSliceTests.cs` — unit tests for both paths.

**Frontend**
- Create: `Application/Frigorino.Web/ClientApp/src/components/common/QuantityDraftFields.tsx` — shared value+unit quantity editor (extracted from the promote row).
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx` — `PromoteRow` consumes `QuantityDraftFields` (no behavior change).
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/reorder/ReorderSheet.tsx` — the single-item re-order sheet.
- Modify: `Application/Frigorino.Web/ClientApp/src/components/sortables/SortableListItem.tsx` — optional `onAddToList` menu entry.
- Modify: `Application/Frigorino.Web/ClientApp/src/components/sortables/SortableList.tsx` — thread `onAddToList` through.
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryContainer.tsx` — host re-order state + sheet, gate the action on having ≥1 list.
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` + `de/translation.json` — `reorder.*` keys.
- Modify: `Application/Frigorino.Web/ClientApp/src/types/i18next.d.ts` — `reorder` namespace.
- Regenerated: `Application/Frigorino.Web/ClientApp/src/lib/api/**` + `openapi.json` (via `npm run api`).

**Integration tests**
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/Reorder.feature` + `ReorderSteps.cs`.

**Docs**
- Modify: `IDEAS.md` — remove the completed "Reverse flow" tracking entry (finishing step).

---

## Task 1: Backend — structured quantity on `CreateItem`

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/CreateItem.cs`
- Create: `Application/Frigorino.Test/Features/CreateItemSliceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Features/CreateItemSliceTests.cs`:

```csharp
using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Lists.Items;
using Frigorino.Features.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Features
{
    public class CreateItemSliceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static ICurrentUserService UserNamed(string id)
        {
            var svc = A.Fake<ICurrentUserService>();
            A.CallTo(() => svc.UserId).Returns(id);
            return svc;
        }

        private static async Task<int> SeedListAsync(TestApplicationDbContext db, string userId, int householdId)
        {
            db.Households.Add(new Household { Id = householdId, Name = "HH", CreatedByUserId = userId });
            db.UserHouseholds.Add(new UserHousehold
            {
                UserId = userId, HouseholdId = householdId, Role = HouseholdRole.Member,
                IsActive = true, JoinedAt = DateTime.UtcNow,
            });
            var list = new List
            {
                Name = "Groceries", HouseholdId = householdId, CreatedByUserId = userId,
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            db.Lists.Add(list);
            await db.SaveChangesAsync();
            return list.Id;
        }

        [Fact]
        public async Task Post_WithStructuredQuantity_PersistsQuantityAndSkipsExtraction()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            var trigger = A.Fake<IQuantityExtractionTrigger>();

            var result = await CreateItemEndpoint.Handle(
                householdId: 1, listId,
                new CreateItemRequest("Milk", null, new QuantityDto(2m, QuantityUnit.Liter)),
                UserNamed("u1"), db, trigger, CancellationToken.None);

            var created = Assert.IsType<Created<ListItemResponse>>(result.Result);
            Assert.False(created.Value!.ExtractionPending);

            var row = await db.ListItems.SingleAsync();
            Assert.Equal("Milk", row.Text);
            Assert.True(row.Quantity.HasValue);
            Assert.Equal(2m, row.Quantity!.Value.Value);
            Assert.Equal(QuantityUnit.Liter, row.Quantity!.Value.Unit);

            A.CallTo(() => trigger.OnItemRouted(
                A<int>._, A<int>._, A<int>._, A<ItemTextAnalysis>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_WithoutQuantity_RoutesThroughExtraction()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            var trigger = A.Fake<IQuantityExtractionTrigger>();

            var result = await CreateItemEndpoint.Handle(
                householdId: 1, listId,
                new CreateItemRequest("2 apples", null),
                UserNamed("u1"), db, trigger, CancellationToken.None);

            Assert.IsType<Created<ListItemResponse>>(result.Result);
            var row = await db.ListItems.SingleAsync();
            Assert.False(row.Quantity.HasValue);
            A.CallTo(() => trigger.OnItemRouted(
                A<int>._, A<int>._, A<int>._, A<ItemTextAnalysis>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Post_WithQuantity_NonMember_ReturnsNotFound()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            var trigger = A.Fake<IQuantityExtractionTrigger>();

            var result = await CreateItemEndpoint.Handle(
                householdId: 1, listId,
                new CreateItemRequest("Milk", null, new QuantityDto(2m, QuantityUnit.Liter)),
                UserNamed("intruder"), db, trigger, CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
            Assert.Empty(await db.ListItems.ToListAsync());
        }
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail to compile**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CreateItemSliceTests"`
Expected: BUILD FAILURE — `CreateItemRequest` has no 3-arg constructor and `CreateItemEndpoint.Handle` is not accessible (`private`).

- [ ] **Step 3: Modify `CreateItem.cs`**

Replace the entire contents of `Application/Frigorino.Features/Lists/Items/CreateItem.cs` with:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Items;
using Frigorino.Features.Quantities;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Lists.Items
{
    // Quantity is optional. When supplied (the inventory → list re-order path), it is written
    // directly and text routing / async extraction is skipped. When null, the text is routed
    // exactly as before and the quantity (if any) is filled in by the async extraction job.
    public sealed record CreateItemRequest(string Text, string? Comment, QuantityDto? Quantity = null);

    public static class CreateItemEndpoint
    {
        public static IEndpointRouteBuilder MapCreateItem(this IEndpointRouteBuilder app)
        {
            app.MapPost("", Handle)
               .WithName("CreateItem")
               .Produces<ListItemResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .ProducesValidationProblem();
            return app;
        }

        public static async Task<Results<Created<ListItemResponse>, NotFound, ValidationProblem>> Handle(
            int householdId,
            int listId,
            CreateItemRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            IQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            // AddItem mints a Rank by appending after the last unchecked item; a concurrent append
            // can collide on the partial unique index. RankRetry reloads fresh state and re-mints.
            async Task<CreateOutcome> SaveItemAsync(string name, Quantity? quantity, bool extractionPending)
            {
                return await RankRetry.SaveWithRetryAsync(async () =>
                {
                    db.ChangeTracker.Clear();

                    var list = await db.Lists
                        .Include(l => l.ListItems)
                        .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                    if (list is null)
                    {
                        return new CreateOutcome(null, NotFound: true, Problem: null);
                    }

                    var result = list.AddItem(name, quantity, request.Comment);
                    if (result.IsFailed)
                    {
                        return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                    }

                    await db.SaveChangesAsync(ct);

                    var resp = ListItemResponse.From(result.Value) with
                    {
                        ExtractionPending = extractionPending,
                    };
                    return new CreateOutcome(resp, NotFound: false, Problem: null);
                });
            }

            // Re-order path: a structured quantity was supplied (carried over from an inventory item).
            // Write it directly and skip routing / extraction — there is nothing to extract.
            if (request.Quantity is not null)
            {
                var parsed = Quantity.Create(request.Quantity.Value, request.Quantity.Unit);
                if (parsed.IsFailed)
                {
                    return parsed.ToValidationProblem();
                }

                var directOutcome = await SaveItemAsync(request.Text, parsed.Value, extractionPending: false);
                if (directOutcome.NotFound)
                {
                    return TypedResults.NotFound();
                }
                if (directOutcome.Problem is not null)
                {
                    return directOutcome.Problem;
                }

                var directResponse = directOutcome.Response!;
                return TypedResults.Created(
                    $"/api/household/{householdId}/lists/{listId}/items/{directResponse.Id}",
                    directResponse);
            }

            var analysis = ItemTextRouter.Analyze(request.Text);

            // The item is created with no quantity; if the text needs extraction the async LLM job
            // fills in the quantity (and strips the name) afterwards.
            var outcome = await SaveItemAsync(
                analysis.CleanName,
                quantity: null,
                extractionPending: analysis.Route == ItemTextRoute.NeedsExtraction);

            if (outcome.NotFound)
            {
                return TypedResults.NotFound();
            }
            if (outcome.Problem is not null)
            {
                return outcome.Problem;
            }

            var response = outcome.Response!;
            // Tell the client whether an async extraction was enqueued so its poll keys off this signal.
            quantityTrigger.OnItemRouted(householdId, listId, response.Id, analysis);

            return TypedResults.Created(
                $"/api/household/{householdId}/lists/{listId}/items/{response.Id}",
                response);
        }

        private sealed record CreateOutcome(ListItemResponse? Response, bool NotFound, ValidationProblem? Problem);
    }
}
```

> Note on the test's quantity asserts: `ListItem.Quantity` is `Quantity?` (a nullable `readonly record struct`). `row.Quantity!.Value` yields the `Quantity` struct (via `Nullable<T>.Value`); its `.Value` is the decimal and `.Unit` the unit. If the property turns out to expose differently, adapt the two assert lines — the behavior under test (quantity persisted, no extraction call) is unchanged.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CreateItemSliceTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/CreateItem.cs Application/Frigorino.Test/Features/CreateItemSliceTests.cs
git commit -m "feat(lists): accept optional structured quantity on CreateItem (re-order path)"
```

---

## Task 2: Regenerate the API client

**Files:** Regenerated under `Application/Frigorino.Web/ClientApp/src/lib/api/**` + `openapi.json`.

- [ ] **Step 1: Regenerate**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run api`
Expected: rebuilds the backend, emits `openapi.json`, regenerates the TS client. No errors.

- [ ] **Step 2: Verify the generated request type carries `quantity`**

Run: `grep -n "CreateItemRequest" Application/Frigorino.Web/ClientApp/src/lib/api/types.gen.ts`
Expected: the `CreateItemRequest` type now includes an optional `quantity` (a nullable `QuantityDto`-shaped object).

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/api Application/Frigorino.Web/ClientApp/src/lib/openapi.json
git commit -m "chore(api): regenerate client for CreateItem quantity field"
```

---

## Task 3: Extract `QuantityDraftFields` shared editor

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/components/common/QuantityDraftFields.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx`

- [ ] **Step 1: Create the shared component**

Create `Application/Frigorino.Web/ClientApp/src/components/common/QuantityDraftFields.tsx`:

```tsx
import { MenuItem, Stack, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import {
    isDraftValid,
    QUANTITY_UNIT_VALUES,
    unitLabel,
    type QuantityDraft,
} from "../composer";
import type { QuantityUnit } from "../../lib/api";

interface QuantityDraftFieldsProps {
    draft: QuantityDraft;
    onChange: (draft: QuantityDraft) => void;
    /** Full testid for the value input (placed on the htmlInput). */
    valueTestId: string;
    /** Full testid for the unit Select (placed on the FormControl). */
    unitTestId: string;
}

// Value + unit pair for editing a QuantityDraft. Shared by the promote review sheet and the
// inventory → list re-order sheet so both edit quantity identically.
export const QuantityDraftFields = ({
    draft,
    onChange,
    valueTestId,
    unitTestId,
}: QuantityDraftFieldsProps) => {
    const { t } = useTranslation();
    return (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <TextField
                size="small"
                type="text"
                label={t("common.quantity")}
                placeholder={t("common.quantity")}
                value={draft.value}
                onChange={(e) => onChange({ ...draft, value: e.target.value })}
                error={!isDraftValid(draft)}
                slotProps={{
                    inputLabel: { shrink: true },
                    htmlInput: {
                        inputMode: "decimal",
                        "data-testid": valueTestId,
                    },
                }}
                sx={{ width: 90 }}
            />
            <TextField
                select
                size="small"
                label={t("common.unit")}
                value={draft.unit}
                onChange={(e) =>
                    onChange({ ...draft, unit: e.target.value as QuantityUnit })
                }
                data-testid={unitTestId}
                slotProps={{ inputLabel: { shrink: true } }}
                sx={{ flex: 1, minWidth: 120 }}
            >
                {QUANTITY_UNIT_VALUES.map((u) => (
                    <MenuItem key={u} value={u}>
                        {unitLabel(t, u)}
                    </MenuItem>
                ))}
            </TextField>
        </Stack>
    );
};
```

- [ ] **Step 2: Refactor `PromoteRow` to consume it**

In `Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx`, replace the inline value+unit `Stack` block inside `PromoteRow` (the `<Stack direction="row" ...>` containing the quantity value `TextField` and the unit `TextField select`, currently roughly lines 359-411) with:

```tsx
                <QuantityDraftFields
                    draft={draft.quantity}
                    onChange={(quantity) => onChange({ quantity })}
                    valueTestId={`promote-row-quantity-value-${entry.text}`}
                    unitTestId={`promote-row-quantity-unit-${entry.text}`}
                />
```

Add the import near the other relative imports:

```tsx
import { QuantityDraftFields } from "../../../components/common/QuantityDraftFields";
```

Then remove now-unused imports from this file: `QUANTITY_UNIT_VALUES`, `unitLabel`, and `type QuantityUnit` (the `MenuItem` import is still used by the inventory-target picker; keep it). Keep `draftToQuantity`, `isDraftValid`, `quantityToDraft`, `type QuantityDraft` — the sheet still uses them.

- [ ] **Step 3: Type-check and lint**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc && npm run lint`
Expected: no errors (no unused-import errors; the promote testids are unchanged).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/common/QuantityDraftFields.tsx Application/Frigorino.Web/ClientApp/src/features/lists/promote/PromoteReviewSheet.tsx
git commit -m "refactor(web): extract shared QuantityDraftFields from promote row"
```

---

## Task 4: i18n keys

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/src/types/i18next.d.ts`

- [ ] **Step 1: Add the `reorder` block to English**

In `public/locales/en/translation.json`, add a new top-level `"reorder"` object (place it next to the existing `"promote"` block; remember to add a trailing comma to the preceding block):

```json
    "reorder": {
        "addToList": "Add to list",
        "sheetTitle": "Add to a list",
        "name": "Item",
        "targetList": "List",
        "add": "Add to {{list}}",
        "added": "Added {{name}} to {{list}}"
    },
```

- [ ] **Step 2: Add the `reorder` block to German**

In `public/locales/de/translation.json`, add the matching block next to `"promote"`:

```json
    "reorder": {
        "addToList": "Zur Liste hinzufügen",
        "sheetTitle": "Zu einer Liste hinzufügen",
        "name": "Artikel",
        "targetList": "Liste",
        "add": "Zu {{list}} hinzufügen",
        "added": "{{name}} zu {{list}} hinzugefügt"
    },
```

- [ ] **Step 3: Add the namespace to the i18next type**

In `src/types/i18next.d.ts`, add one line inside the `translation: { ... }` block (next to `promote`):

```ts
                reorder: Record<string, string>;
```

- [ ] **Step 4: Type-check**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json Application/Frigorino.Web/ClientApp/src/types/i18next.d.ts
git commit -m "feat(web): add reorder i18n keys (en/de)"
```

---

## Task 5: Add the "Add to list" entry to the shared item menu

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/sortables/SortableListItem.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/components/sortables/SortableList.tsx`

- [ ] **Step 1: Add the optional callback + menu item to `SortableListItem`**

In `SortableListItem.tsx`:

Update the icon import line (add `AddShoppingCart`):

```tsx
import { AddShoppingCart, Delete, Edit, MoreVert } from "@mui/icons-material";
```

Add an optional prop to `SortableListItemProps<T>` (after `onDelete`):

```tsx
    /** When provided, shows an "Add to list" menu entry (used for inventory rows). */
    onAddToList?: (item: T) => void;
```

Destructure it in the component signature (add `onAddToList,` next to `onDelete,`).

Add a handler next to `handleDelete`:

```tsx
    const handleAddToList = useCallback(() => {
        onAddToList?.(item);
        handleMenuClose();
    }, [onAddToList, item, handleMenuClose]);
```

In the `<Menu>`, add this as the first child (before the Edit `MenuItem`):

```tsx
                        {onAddToList && (
                            <MenuItem
                                onClick={handleAddToList}
                                data-testid="add-to-list-button"
                            >
                                <AddShoppingCart
                                    fontSize="small"
                                    sx={{ mr: 1 }}
                                />
                                {t("reorder.addToList")}
                            </MenuItem>
                        )}
```

- [ ] **Step 2: Thread `onAddToList` through `SortableList`**

In `SortableList.tsx`:

Add to `SortableListProps<T>` (after `onDelete`):

```tsx
    /** Optional per-item action; forwarded to each row's menu. */
    onAddToList?: (item: T) => void;
```

Destructure `onAddToList,` in the component signature (next to `onDelete,`).

Add `onAddToList={onAddToList}` as a prop on **both** `<SortableListItem ... />` usages (the unchecked-section map and the checked-section map).

- [ ] **Step 3: Type-check and lint**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc && npm run lint`
Expected: no errors. List rows are unaffected (no caller passes `onAddToList` yet).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/sortables/SortableListItem.tsx Application/Frigorino.Web/ClientApp/src/components/sortables/SortableList.tsx
git commit -m "feat(web): optional Add-to-list entry in shared item menu"
```

---

## Task 6: The re-order sheet

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/reorder/ReorderSheet.tsx`

- [ ] **Step 1: Create `ReorderSheet`**

Create `Application/Frigorino.Web/ClientApp/src/features/inventories/reorder/ReorderSheet.tsx`:

```tsx
import { Close } from "@mui/icons-material";
import {
    Box,
    Button,
    Drawer,
    IconButton,
    MenuItem,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { QuantityDraftFields } from "../../../components/common/QuantityDraftFields";
import {
    draftToQuantity,
    EMPTY_QUANTITY_DRAFT,
    isDraftValid,
    quantityToDraft,
    type QuantityDraft,
} from "../../../components/composer";
import type { InventoryItemResponse } from "../../../lib/api";
import { useCreateListItem } from "../../lists/items/useCreateListItem";
import { useHouseholdLists } from "../../lists/useHouseholdLists";

interface ReorderSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    item: InventoryItemResponse | null;
}

// Single-item mirror of the promote review sheet, retargeted at a shopping list. Pre-fills the
// inventory item's name + structured quantity; confirming creates a list item via the existing
// CreateItem write. The inventory item is never touched.
export const ReorderSheet = ({
    open,
    onClose,
    householdId,
    item,
}: ReorderSheetProps) => {
    const { t } = useTranslation();
    const { data: lists = [] } = useHouseholdLists(householdId, householdId > 0);
    const createItem = useCreateListItem();

    const [name, setName] = useState("");
    const [draft, setDraft] = useState<QuantityDraft>(EMPTY_QUANTITY_DRAFT);
    const [listId, setListId] = useState<number | null>(null);

    // Re-seed the form each time the sheet opens for a (possibly different) item.
    useEffect(() => {
        if (open && item) {
            setName(item.text ?? "");
            setDraft(quantityToDraft(item.quantity ?? null));
            setListId(null);
        }
    }, [open, item]);

    // Effective target: explicit pick, else the newest list (GetLists is newest-first).
    const targetId = listId ?? lists[0]?.id ?? null;
    const targetName = lists.find((l) => l.id === targetId)?.name ?? "";

    const trimmedName = name.trim();
    const canSubmit =
        trimmedName.length > 0 &&
        targetId !== null &&
        isDraftValid(draft) &&
        !createItem.isPending;

    const handleConfirm = async () => {
        if (targetId === null || trimmedName.length === 0) {
            return;
        }
        try {
            await createItem.mutateAsync({
                path: { householdId, listId: targetId },
                body: { text: trimmedName, quantity: draftToQuantity(draft) },
            });
            toast.success(
                t("reorder.added", { name: trimmedName, list: targetName }),
            );
            onClose();
        } catch {
            // Leave the sheet open on failure so the user can retry.
        }
    };

    return (
        <Drawer
            anchor="bottom"
            open={open}
            onClose={onClose}
            data-testid="reorder-sheet"
            slotProps={{
                paper: {
                    sx: { borderTopLeftRadius: 16, borderTopRightRadius: 16 },
                },
            }}
        >
            <Box sx={{ p: 2, maxWidth: 600, mx: "auto", width: "100%" }}>
                <Stack
                    direction="row"
                    sx={{
                        alignItems: "center",
                        justifyContent: "space-between",
                    }}
                >
                    <Typography variant="h6">
                        {t("reorder.sheetTitle")}
                    </Typography>
                    <IconButton
                        onClick={onClose}
                        size="small"
                        aria-label="close"
                    >
                        <Close />
                    </IconButton>
                </Stack>

                <Stack spacing={2} sx={{ mt: 2 }}>
                    <TextField
                        fullWidth
                        size="small"
                        label={t("reorder.name")}
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                        slotProps={{
                            inputLabel: { shrink: true },
                            htmlInput: { "data-testid": "reorder-name-input" },
                        }}
                    />

                    <QuantityDraftFields
                        draft={draft}
                        onChange={setDraft}
                        valueTestId="reorder-quantity-value"
                        unitTestId="reorder-quantity-unit"
                    />

                    {lists.length > 1 && (
                        <TextField
                            select
                            fullWidth
                            size="small"
                            label={t("reorder.targetList")}
                            value={targetId ?? ""}
                            onChange={(e) => setListId(Number(e.target.value))}
                            data-testid="reorder-list-picker"
                        >
                            {lists.map((l) => (
                                <MenuItem key={l.id} value={l.id}>
                                    {l.name}
                                </MenuItem>
                            ))}
                        </TextField>
                    )}

                    <Button
                        fullWidth
                        variant="contained"
                        disabled={!canSubmit}
                        onClick={handleConfirm}
                        data-testid="reorder-confirm-button"
                    >
                        {t("reorder.add", { list: targetName })}
                    </Button>
                </Stack>
            </Box>
        </Drawer>
    );
};
```

- [ ] **Step 2: Type-check and lint**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc && npm run lint`
Expected: no errors. (`body.quantity` is accepted because Task 2 regenerated `CreateItemRequest` with the optional `quantity` field.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/reorder/ReorderSheet.tsx
git commit -m "feat(web): inventory re-order sheet"
```

---

## Task 7: Wire the sheet into the inventory list

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryContainer.tsx`

- [ ] **Step 1: Add state, list gating, the menu callback, and the sheet**

In `InventoryContainer.tsx`:

Add imports:

```tsx
import { useState } from "react";
import { useHouseholdLists } from "../../../lists/useHouseholdLists";
import { ReorderSheet } from "../../reorder/ReorderSheet";
```

Inside the component body (after the existing hooks like `useReorderInventoryItem`), add:

```tsx
        const [reorderItem, setReorderItem] =
            useState<InventoryItemResponse | null>(null);
        const { data: lists = [] } = useHouseholdLists(
            householdId,
            householdId > 0,
        );
        // Re-order needs a destination; hide the action entirely when the household has no list.
        const canReorder = lists.length > 0;
```

On the `<SortableList ... />` element, add the prop (only wired when a list exists):

```tsx
                        onAddToList={
                            canReorder
                                ? (item) => setReorderItem(item)
                                : undefined
                        }
```

Wrap the returned `<Container>` in a fragment and render the sheet as a sibling. Change the `return (` / `<Container ...>` … `</Container>` / `);` structure to:

```tsx
        return (
            <>
                <Container
                    ref={ref}
                    maxWidth="sm"
                    sx={{
                        flex: 1,
                        overflow: "auto",
                        px: featureContentPx,
                        py: 0,
                        minHeight: 0,
                    }}
                >
                    {/* existing showNoMatches ? (...) : (<SortableList .../>) block, unchanged
                        except for the new onAddToList prop added above */}
                </Container>
                <ReorderSheet
                    open={reorderItem !== null}
                    onClose={() => setReorderItem(null)}
                    householdId={householdId}
                    item={reorderItem}
                />
            </>
        );
```

(Keep the existing `showNoMatches` conditional and `<SortableList>` exactly as they are — only add the `onAddToList` prop and the wrapping fragment + `<ReorderSheet>`.)

- [ ] **Step 2: Type-check and lint**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run tsc && npm run lint`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryContainer.tsx
git commit -m "feat(web): wire re-order sheet into inventory item list"
```

---

## Task 8: Integration test (SPA end-to-end)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/Reorder.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/ReorderSteps.cs`

> Reuses existing step bindings: `there is a list named {string} with item {string}` and `there is an inventory named {string} with item {string} and quantity {string}` (set up data + `ctx.ListIds` / `ctx.InventoryIds`), `I open the inventory {string}` (navigation), `I open the inventory item menu for {string}` (`InventoryItemSteps`), and `the inventory {string} contains an item {string}` (`PromoteSteps`).

- [ ] **Step 1: Build the SPA so new testids are served**

The integration harness serves the SPA from `ClientApp/build` (not live source). Run from `Application/Frigorino.Web/ClientApp/`: `npm run build`
Expected: build succeeds, outputs to `ClientApp/build`.

- [ ] **Step 2: Write the feature**

Create `Application/Frigorino.IntegrationTests/Slices/Inventories/Reorder.feature`:

```gherkin
Feature: Re-order an inventory item back to a shopping list (SPA)

  Background:
    Given I am logged in with an active household

  Scenario: Adding an inventory item to the only list creates a list item and leaves the inventory untouched
    Given there is a list named "Weekly Groceries" with item "Bread"
    And there is an inventory named "Fridge" with item "Milk" and quantity "2 l"
    When I open the inventory "Fridge"
    And I open the inventory item menu for "Milk"
    And I click add to list from the inventory item menu
    And I confirm the re-order
    Then the list "Weekly Groceries" contains an item "Milk"
    And the inventory "Fridge" contains an item "Milk"
```

- [ ] **Step 3: Write the new step bindings**

Create `Application/Frigorino.IntegrationTests/Slices/Inventories/ReorderSteps.cs`:

```csharp
namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class ReorderSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [When("I click add to list from the inventory item menu")]
    public async Task WhenIClickAddToListFromTheInventoryItemMenu()
    {
        await ctx.Page.GetByTestId("add-to-list-button").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("reorder-sheet")).ToBeVisibleAsync();
    }

    [When("I confirm the re-order")]
    public async Task WhenIConfirmTheReorder()
    {
        // The list-item create returns 201; wait for it so the assertion reads post-commit state.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.EndsWith("/items")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("reorder-confirm-button").ClickAsync();
        await responseTask;
    }

    [Then("the list {string} contains an item {string}")]
    public async Task ThenTheListContainsAnItem(string listName, string itemText)
    {
        var listId = ctx.ListIds[listName];
        var response = await api.TryGetListItemsAsync(listId);
        Assert.Equal(200, response.Status);
        var items = (await response.JsonAsync())!.Value;
        var texts = items.EnumerateArray()
            .Select(i => i.GetProperty("text").GetString())
            .ToList();
        Assert.Contains(itemText, texts);
    }
}
```

> If `ctx.ListIds` or `api.TryGetListItemsAsync` are named differently in this project, mirror the names used by the existing list integration steps (the inventory equivalents are `ctx.InventoryIds` and `api.TryGetInventoryItemsAsync`, used in `PromoteSteps.cs`). The `the list ... contains an item` assertion is a direct mirror of `PromoteSteps`' `the inventory ... contains an item`. If a `the list {string} contains an item {string}` binding already exists elsewhere, reuse it and drop this one to avoid a duplicate-step-definition error.

- [ ] **Step 4: Run the integration test**

Run: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~Reorder"`
Expected: the scenario passes (Docker must be running for Testcontainers; if the daemon is unreachable, start Docker Desktop).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Inventories/Reorder.feature Application/Frigorino.IntegrationTests/Slices/Inventories/ReorderSteps.cs
git commit -m "test(it): inventory item re-order to shopping list"
```

---

## Task 9: Full verification + remove tracking entry

**Files:**
- Modify: `IDEAS.md`

- [ ] **Step 1: Remove the completed IDEAS entry**

In `IDEAS.md`, delete the entire `## Reverse flow: inventory item → add to shopping list (re-order)` section (the heading and all its bullet content, through the trailing `---` separator that closes the section). This is the finishing step now that the work ships.

- [ ] **Step 2: Frontend final verification**

Run from `Application/Frigorino.Web/ClientApp/`: `npm run lint && npm run tsc && npm run prettier`
Expected: lint clean, type-check clean, prettier writes/formats with no remaining diffs to fix.

- [ ] **Step 3: Backend + integration full verification**

Run: `dotnet test Application/Frigorino.sln`
Expected: all tests pass (unit + integration). Docker daemon must be running for the integration suite.

- [ ] **Step 4: Docker build (catch Dockerfile / SPA / pipeline drift)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add IDEAS.md
git commit -m "docs: remove shipped reverse-flow re-order idea"
```

---

## Self-Review

**Spec coverage:**
- No-trigger / no-lifecycle-coupling standalone action → Tasks 5–7 (menu entry + sheet, inventory item never mutated; IT Step asserts the inventory still contains the item). ✓
- Single item, not batch → `ReorderSheet` takes one `item`. ✓
- Entry point = existing per-item kebab, inventory-only → Task 5 (`onAddToList` optional; list rows don't pass it). ✓
- Reuse promote sheet UI, simplified (editable name, quantity-draft editor, list picker, no expiry/checkboxes) → Tasks 3 + 6. ✓
- Structured quantity transfer (not via text) → Task 1 (`CreateItem` optional `Quantity`, direct write, extraction skipped) + Task 6 (`draftToQuantity`). ✓
- Target = newest list, picker hidden when only one → Task 6 (`lists[0]`, `lists.length > 1` guard). ✓
- Reuse `CreateItem` slice + `useCreateListItem`, no new endpoint/migration → Tasks 1, 2, 6. ✓
- Zero lists → hide the entry → Task 7 (`canReorder`). ✓
- Tests: backend unit + integration → Tasks 1, 8. ✓
- Out-of-scope items (low-stock, auto-decrement, batch, classifier suggestion, persisted default-list pref) → none implemented. ✓

**Placeholder scan:** No TBD/TODO; every code step has full code. The two flex notes (Quantity property access in Task 1; `ListIds`/`TryGetListItemsAsync` naming in Task 8) are explicit fallbacks, not missing content.

**Type consistency:** `onAddToList` (camelCase, identical in `SortableListItem`, `SortableList`, `InventoryContainer`); testids `add-to-list-button`, `reorder-sheet`, `reorder-name-input`, `reorder-quantity-value`, `reorder-quantity-unit`, `reorder-list-picker`, `reorder-confirm-button` consistent between frontend tasks and IT step bindings; `QuantityDraftFields` prop names (`draft`, `onChange`, `valueTestId`, `unitTestId`) match both call sites; `reorder.*` i18n keys (`addToList`, `sheetTitle`, `name`, `targetList`, `add`, `added`) match usage in Tasks 5/6.

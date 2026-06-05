# Calendar Inline Edit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the user select an inventory item directly on the expiry calendar and edit it in place via a bottom action bar that expands into the existing composer — no navigation away from the calendar.

**Architecture:** A new `CalendarItemActionBar` component renders a slim bottom bar (item name · date · Edit) when a bar is selected, and expands to host the shared `Composer` when Edit is tapped. `ExpiryCalendarPage` unifies tap handling (every bar selects + highlights; the old compact-vs-wide split for tap behavior is removed), owns `selectedId` + `editing` state, and on save reuses `useUpdateInventoryItem` (which already invalidates the inventory-items query) plus a local invalidation of the expiry-calendar query so the bar updates immediately. The old `CalendarItemDetailsSheet` is deleted.

**Tech Stack:** React 19 + TypeScript, MUI, TanStack Query, FullCalendar, hey-api generated client. Tests: Reqnroll + Playwright integration tests (no JS unit runner).

**Spec:** `docs/superpowers/specs/2026-06-05-calendar-inline-edit-design.md`

---

## File Structure

- **Create** `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarItemActionBar.tsx` — the bottom action bar (minimal + editing modes). Owns the composer wiring; receives the selected item + callbacks from the page.
- **Modify** `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx` — replace `detailItem`/`CalendarItemDetailsSheet` with `editing` state + `CalendarItemActionBar`; unify tap behavior; add save handler + expiry-calendar invalidation; guard the clear-on-empty handler while editing; add `data-selected` to the compact event branch.
- **Delete** `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarItemDetailsSheet.tsx` — its informational role is absorbed by the action bar.
- **Modify** `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature` — rewrite the details-sheet scenario into an action-bar/edit scenario.
- **Modify** `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs` — replace the details-sheet steps with action-bar + edit steps.

i18n keys reused (all already exist in `public/locales/{en,de}/translation.json`): `common.edit`, `common.cancel`, `common.update`. No new keys.

---

## Task 1: Write the failing integration scenarios + steps

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs`

This is the red test. It references testids (`calendar-item-action-bar`, `calendar-action-bar-title`, `calendar-action-bar-edit`, `calendar-action-bar-composer`) that Tasks 2–3 create, plus existing composer testids (`autocomplete-input-textfield`, `autocomplete-input-submit-button`).

- [ ] **Step 1: Rewrite the `.feature` scenarios**

Replace the entire contents of `ExpiryCalendar.feature` with:

```gherkin
Feature: Expiry Calendar

  Background:
    Given I am logged in with an active household

  Scenario: User opens the calendar from the inventories header and focuses an item
    # 10 days out keeps the bar wide enough to render the inline label + date stamp.
    Given an inventory "Fridge" has an item "Milk" expiring in 10 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Milk"
    When I select the calendar item "Milk"
    Then the calendar item "Milk" is focused
    And the calendar action bar shows "Milk"

  Scenario: Tapping a short-span item also opens the action bar
    Given an inventory "Fridge" has an item "Yogurt" expiring in 2 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Yogurt"
    When I select the calendar item "Yogurt"
    Then the calendar action bar shows "Yogurt"

  Scenario: User edits an item from the calendar and stays selected
    Given an inventory "Fridge" has an item "Milk" expiring in 10 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Milk"
    When I select the calendar item "Milk"
    And I tap edit in the calendar action bar
    Then the calendar action bar is in edit mode
    When I change the item text to "Bread" and save
    Then the calendar shows the item "Bread"
    And the calendar action bar shows "Bread"
    And the calendar item "Bread" is focused

  Scenario: Filtering a level hides matching items and persists across reload
    Given an inventory "Fridge" has an item "Milk" expiring in 2 days
    And an inventory "Fridge" has an item "Rice" expiring in 40 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Rice"
    When I turn off the "fresh" level filter
    Then the calendar does not show the item "Rice"
    When I reload the calendar page
    Then the calendar does not show the item "Rice"
    And the calendar shows the item "Milk"
```

- [ ] **Step 2: Replace the details-sheet step with action-bar + edit steps**

In `ExpiryCalendarSteps.cs`, delete the `ThenTheItemDetailsSheetShows` method (the `[Then("the item details sheet shows {string}")]` binding) and add these four methods inside the class:

```csharp
    [Then("the calendar action bar shows {string}")]
    public async Task ThenTheCalendarActionBarShows(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-item-action-bar"))
            .ToBeVisibleAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-action-bar-title"))
            .ToHaveTextAsync(itemText);
    }

    [When("I tap edit in the calendar action bar")]
    public async Task WhenITapEditInTheCalendarActionBar()
    {
        await ctx.Page.GetByTestId("calendar-action-bar-edit").ClickAsync();
    }

    [Then("the calendar action bar is in edit mode")]
    public async Task ThenTheCalendarActionBarIsInEditMode()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-action-bar-composer"))
            .ToBeVisibleAsync();
    }

    [When("I change the item text to {string} and save")]
    public async Task WhenIChangeTheItemTextToAndSave(string newText)
    {
        var input = ctx.Page
            .GetByTestId("calendar-action-bar-composer")
            .GetByTestId("autocomplete-input-textfield")
            .Locator("input");
        await input.FillAsync(newText);
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
        // The composer collapses on save; wait for it to disappear before asserting.
        await Assertions.Expect(ctx.Page.GetByTestId("calendar-action-bar-composer"))
            .Not.ToBeVisibleAsync();
    }
```

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs
git commit -m "test: calendar inline-edit integration scenarios (red)"
```

---

## Task 2: Create the CalendarItemActionBar component

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarItemActionBar.tsx`

- [ ] **Step 1: Write the component**

```tsx
import { Edit } from "@mui/icons-material";
import { Box, Button, Paper, Slide, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import {
    Composer,
    draftToQuantity,
    expiryFeature,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
} from "../../../../components/composer";
import type {
    ExpiryCalendarItemResponse,
    QuantityDto,
} from "../../../../lib/api";
import { formatLocalDate } from "../../../../utils/dateUtils";

const features = [quantityComposerFeature, expiryFeature] as const;

interface CalendarItemActionBarProps {
    // The selected item, or null when nothing is selected (bar slides out).
    item: ExpiryCalendarItemResponse | null;
    editing: boolean;
    onEdit: () => void;
    onCancelEdit: () => void;
    onSave: (
        text: string,
        quantity: QuantityDto | null,
        expiryDate: string | null,
    ) => void;
    isSaving: boolean;
}

// Bottom action bar for the expiry calendar. Slim by default (name · inventory · date · Edit);
// expands in place to host the shared Composer when Edit is tapped. Non-modal (a fixed Paper,
// not a Drawer) so the calendar grid stays tappable for switching/clearing the selection.
export const CalendarItemActionBar = ({
    item,
    editing,
    onEdit,
    onCancelEdit,
    onSave,
    isSaving,
}: CalendarItemActionBarProps) => {
    const { t } = useTranslation();

    const initialDraft = item
        ? {
              text: item.text,
              values: {
                  quantity: quantityToDraft(item.quantity),
                  expiry: item.expiryDate ?? null,
              },
          }
        : undefined;

    const handleComplete = (r: Completion<typeof features>) => {
        if (r.kind !== "text") {
            return;
        }
        const quantity = draftToQuantity(r.quantity);
        onSave(r.text, quantity, r.expiry ?? null);
    };

    return (
        <Slide direction="up" in={Boolean(item)} mountOnEnter unmountOnExit>
            <Paper
                elevation={8}
                data-testid="calendar-item-action-bar"
                // Clicks inside the bar must not bubble to the page's clear-on-empty handler.
                onClick={(e) => e.stopPropagation()}
                sx={{
                    position: "fixed",
                    left: 0,
                    right: 0,
                    bottom: 0,
                    zIndex: (theme) => theme.zIndex.drawer,
                    borderTopLeftRadius: 16,
                    borderTopRightRadius: 16,
                    px: 2,
                    py: 1.5,
                    maxWidth: 600,
                    mx: "auto",
                }}
            >
                {item && !editing && (
                    <Stack
                        direction="row"
                        spacing={1}
                        sx={{ alignItems: "center" }}
                    >
                        <Box sx={{ minWidth: 0, flex: 1 }}>
                            <Typography
                                variant="subtitle1"
                                noWrap
                                data-testid="calendar-action-bar-title"
                            >
                                {item.text}
                            </Typography>
                            <Typography
                                variant="body2"
                                color="text.secondary"
                                noWrap
                            >
                                {item.inventoryName} ·{" "}
                                {formatLocalDate(item.expiryDate)}
                            </Typography>
                        </Box>
                        <Button
                            variant="contained"
                            size="small"
                            startIcon={<Edit />}
                            onClick={onEdit}
                            data-testid="calendar-action-bar-edit"
                        >
                            {t("common.edit")}
                        </Button>
                    </Stack>
                )}
                {item && editing && (
                    <Box data-testid="calendar-action-bar-composer">
                        <Composer
                            key={item.id}
                            features={features}
                            disabled={isSaving}
                            editing={{ active: true, onCancel: onCancelEdit }}
                            initialDraft={initialDraft}
                            onComplete={handleComplete}
                        />
                    </Box>
                )}
            </Paper>
        </Slide>
    );
};
```

- [ ] **Step 2: Type-check**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc`
Expected: PASS (no errors). The component is not yet imported anywhere, which is fine.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarItemActionBar.tsx
git commit -m "feat: calendar item action bar component"
```

---

## Task 3: Rewire ExpiryCalendarPage to use the action bar

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx`

- [ ] **Step 1: Swap imports**

Remove the `CalendarItemDetailsSheet` import block (the `import { CalendarItemDetailsSheet, type CalendarItemDetail } from "../components/CalendarItemDetailsSheet";` lines) and add the new imports. The import section should include:

```tsx
import { useQueryClient } from "@tanstack/react-query";
import { getExpiryCalendarQueryKey } from "../../../../lib/api/@tanstack/react-query.gen";
import type { QuantityDto } from "../../../../lib/api";
import { useUpdateInventoryItem } from "../../items/useUpdateInventoryItem";
import { CalendarItemActionBar } from "../components/CalendarItemActionBar";
```

Keep the existing `useExpiryCalendar`, `buildExpiryEvents`, etc. imports.

- [ ] **Step 2: Replace selection/detail state with selection + editing state**

Remove the `detailItem` state declaration:

```tsx
    // Details for a tapped compact (narrow) bar. null = sheet closed.
    const [detailItem, setDetailItem] = useState<CalendarItemDetail | null>(
        null,
    );
```

Replace it with editing state, and add the query client + mutation + selected-item lookup right after the existing `selectedId`/`settingsOpen` state:

```tsx
    // Edit mode for the selected item. Selection (selectedId) can be active without editing.
    const [editing, setEditing] = useState(false);

    const queryClient = useQueryClient();
    const updateMutation = useUpdateInventoryItem();

    // The full selected item (incl. quantity, which isn't in the FullCalendar event props).
    const selectedItem =
        items?.find((item) => item.id === selectedId) ?? null;
```

(`items` is already destructured from `useExpiryCalendar`. Leave `selectedId` and `settingsOpen` as they are.)

- [ ] **Step 3: Add the save handler**

Add this handler alongside the other handlers in the component body (e.g. just above `handleEventClick`):

```tsx
    const handleSave = async (
        text: string,
        quantity: QuantityDto | null,
        expiryDate: string | null,
    ) => {
        if (!selectedItem) {
            return;
        }
        await updateMutation.mutateAsync({
            path: {
                householdId,
                inventoryId: selectedItem.inventoryId,
                itemId: selectedItem.id,
            },
            // Mirrors InventoryViewPage.handleUpdateItem: text always sent; clearQuantity on
            // an empty quantity; expiryDate is write-through (null clears).
            body: {
                text,
                quantity,
                clearQuantity: quantity === null,
                expiryDate,
            },
        });
        // The shared hook invalidates the inventory-items query. The calendar reads a separate
        // query, so invalidate it here too — an expiry change then moves the bar immediately.
        await queryClient.invalidateQueries({
            queryKey: getExpiryCalendarQueryKey({ path: { householdId } }),
        });
        // Leave edit mode but keep the item selected/highlighted.
        setEditing(false);
    };
```

- [ ] **Step 4: Unify the event-click handler**

Replace the entire existing `handleEventClick` (the version with the `props.compact` → `setDetailItem` branch) with:

```tsx
    const handleEventClick = (info: EventClickArg) => {
        // Stop the wrapper's clear-on-empty handler from firing for this same click.
        info.jsEvent.stopPropagation();
        const props = info.event.extendedProps as ExpiryEventProps;
        // Every bar (compact or wide) now selects + highlights. Switching items leaves edit mode.
        setEditing(false);
        setSelectedId((prev) => (prev === props.itemId ? null : props.itemId));
    };
```

- [ ] **Step 5: Guard the clear-on-empty handler and add the compact test hook**

In the calendar `<Box className="expiry-calendar" ...>`, change the `onClick` so tapping the grid does NOT clear selection while editing (the user must Save or Cancel):

```tsx
                        onClick={() => {
                            if (!editing) {
                                setSelectedId(null);
                            }
                        }}
```

In `renderEventContent`, the compact branch currently renders name-only without the `data-selected` hook. Add `data-selected` to it so compact bars are testable/consistent. Change the compact `<Box>` opening to:

```tsx
            return (
                <Box
                    data-testid={`cal-event-${arg.event.title}`}
                    data-selected={
                        selectedId === props.itemId ? "true" : "false"
                    }
                    sx={{
                        overflow: "hidden",
                        whiteSpace: "nowrap",
                        textOverflow: "ellipsis",
                        fontSize: "0.7rem",
                        fontWeight: 600,
                        px: 0.25,
                    }}
                >
                    {arg.event.title}
                </Box>
            );
```

- [ ] **Step 6: Replace the sheet render with the action bar**

At the bottom of the returned JSX, remove the `<CalendarItemDetailsSheet ... />` element and replace it with:

```tsx
            <CalendarItemActionBar
                item={selectedItem}
                editing={editing}
                onEdit={() => setEditing(true)}
                onCancelEdit={() => setEditing(false)}
                onSave={handleSave}
                isSaving={updateMutation.isPending}
            />
```

(Leave `<CalendarSettingsSheet ... />` as is.)

- [ ] **Step 7: Type-check + lint**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc` then `npm run lint`
Expected: both PASS. If tsc reports `detailItem`/`setDetailItem`/`CalendarItemDetail` still referenced, you missed a removal in Step 2/6 — remove those references.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx
git commit -m "feat: select + inline-edit items on the expiry calendar"
```

---

## Task 4: Delete the obsolete details sheet

**Files:**
- Delete: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarItemDetailsSheet.tsx`

- [ ] **Step 1: Confirm there are no remaining references**

Run (from repo root): `grep -rn "CalendarItemDetailsSheet\|CalendarItemDetail\b" Application/Frigorino.Web/ClientApp/src`
Expected: no matches (the page was rewired in Task 3). If anything matches, fix it before deleting.

- [ ] **Step 2: Delete the file**

```bash
git rm Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/components/CalendarItemDetailsSheet.tsx
```

- [ ] **Step 3: Type-check**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git commit -m "ref: remove calendar item details sheet (replaced by action bar)"
```

---

## Task 5: Build, verify green, and manually verify

**Files:** none (verification only).

- [ ] **Step 1: Frontend quality gate**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run lint` then `npm run tsc` then `npm run prettier`
Expected: all PASS / clean. Commit any prettier reformatting:

```bash
git add -A && git commit -m "style: prettier" || echo "nothing to format"
```

- [ ] **Step 2: Build the SPA (integration harness serves ClientApp/build)**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run build`
Expected: PASS — `tsc -b && vite build` completes, output in `ClientApp/build`. New testids only appear in the integration run after this build.

- [ ] **Step 3: Run the full solution tests (Test + IntegrationTests)**

Requires Docker Desktop running (Testcontainers + Playwright). If `docker` is unreachable, ask the user to start Docker Desktop rather than skipping.

Run (from repo root): `dotnet test Application/Frigorino.sln`
Expected: PASS, including the three calendar scenarios from Task 1. Verify the pass/fail summary line directly — do not trust a piped tail's exit code.

If a scenario fails on the text-edit step, the likely cause is the composer text-input selector; confirm the input resolves via `calendar-action-bar-composer` → `autocomplete-input-textfield` → `input`.

- [ ] **Step 4: Manual browser verify (the net for runtime bugs static checks miss)**

Bring up the dev stack and drive the calendar on a phone viewport (the spec is mobile-first). Use the `/dev-up` skill, then Playwright MCP pointed at the printed SPA URL (authenticated as `dev@frigorino.local`):
1. Seed/keep an inventory item with an expiry, open `/inventories/calendar`.
2. Tap a bar → the action bar slides up showing the name + date + Edit.
3. Tap Edit → composer expands, seeded with the current text/quantity/expiry; the mobile keyboard opens on the text field.
4. Change the expiry date, Save → composer collapses, the item stays selected/highlighted, and the bar moves to the new date.
5. Tap empty grid space → action bar slides out, selection cleared.
6. Confirm no console errors.

Tear down with `/dev-down` only if the user asks.

- [ ] **Step 5: Update the spike learnings doc's open question**

The spike doc listed "what a click does" as open question #3. Append a one-line resolution note to `docs/superpowers/specs/2026-06-04-inventory-calendar-spike-learnings.md` under that question: `**Resolved (2026-06-05):** select → edit in place via a bottom action bar. See 2026-06-05-calendar-inline-edit-design.md.`

```bash
git add docs/superpowers/specs/2026-06-04-inventory-calendar-spike-learnings.md
git commit -m "docs: resolve calendar click open-question"
```

---

## Self-Review notes

- **Spec coverage:** interaction states (Task 3 handlers + Task 2 modes), minimal bar contents (Task 2), Edit→composer expand (Task 2), Save stays-selected (Task 3 `handleSave` sets `editing=false`, keeps `selectedId`), Cancel stays-selected (Task 2 `onCancelEdit` → page `setEditing(false)`), tap-empty clears (Task 3 Step 5, guarded while editing), removal of `CalendarItemDetailsSheet` (Task 4), dual invalidation / no stale data (Task 3 `handleSave` + reused hook), i18n via `t()` (Task 2), integration test on testids only (Task 1). All covered.
- **Type consistency:** `onSave(text, quantity, expiryDate)` signature matches between `CalendarItemActionBar` (Task 2) and the page `handleSave` (Task 3). `getExpiryCalendarQueryKey({ path: { householdId } })` matches the generated export. Update body shape matches `UpdateInventoryItemRequest` (`text`, `quantity`, `clearQuantity`, `expiryDate`) as used in `InventoryViewPage`.
- **No placeholders:** every code step shows full content.
```

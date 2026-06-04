# Inventory Expiry Calendar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A household-wide month calendar where each inventory item with an expiry renders as a multi-day "cook-by" bar spanning `[expiry − X … expiry]`, with tap-to-focus to keep a cramped view readable.

**Architecture:** One new read slice (`GetExpiryCalendar`) aggregates expiring items across all of the active household's inventories. The frontend adds a FullCalendar `dayGridMonth` page reached from a calendar icon in the Inventories header. The cook-by window `X` is a fixed client constant; no domain change.

**Tech Stack:** .NET 10 vertical slice + EF Core projection; React 19 + TanStack Router/Query + MUI; FullCalendar v6 (`@fullcalendar/react` + `@fullcalendar/daygrid`); Reqnroll + Playwright integration tests.

**Spec:** `docs/superpowers/specs/2026-06-04-inventory-calendar-design.md`. **Spike reference:** `docs/superpowers/specs/2026-06-04-inventory-calendar-spike-learnings.md` (patterns only — spike code is gone by design; do not reconstruct it).

**Conventions to honor (from CLAUDE.md / project memory):**
- C# braces: always block style `{}`, even single-line.
- Slices: read = handler-only inline EF projection; membership guard via `db.FindActiveMembershipAsync`.
- Frontend hooks: spread generated `getXOptions` / `xMutation`; never hand-write `queryFn`/`queryKey`.
- i18n: all copy via `t()`; tests assert on testids / `data-*`, never translated text.
- npm: caret-minor pins (`^1.2.3`); use `npm install` only when adding deps.
- Use package.json scripts (`npm run lint` / `tsc` / `prettier` / `api` / `routes:gen`), never raw `npx`.
- Integration harness serves `ClientApp/build` — run `npm run build` after React edits before running UI integration tests.
- All shell commands run from the worktree root: `C:/Repositories/frigorino/.claude/worktrees/feat-inventory-calendar`.

---

## File Structure

**Backend (create):**
- `Application/Frigorino.Features/Inventories/ExpiryCalendarItemResponse.cs` — response DTO + EF projection.
- `Application/Frigorino.Features/Inventories/GetExpiryCalendar.cs` — the read slice.

**Backend (modify):**
- `Application/Frigorino.Web/Program.cs` — register `inventories.MapGetExpiryCalendar()`.

**Backend tests (create):**
- `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.Api.feature`
- `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarApiSteps.cs`
- `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature` (UI)
- `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs` (UI)

**Backend tests (modify):**
- `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs` — add `TryGetExpiryCalendarAsync`.

**Frontend (create):**
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/useExpiryCalendar.ts`
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/expiryCalendarEvents.ts`
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/expiryCalendar.css`
- `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx`
- `Application/Frigorino.Web/ClientApp/src/routes/inventories/calendar.tsx`

**Frontend (modify):**
- `Application/Frigorino.Web/ClientApp/package.json` — FullCalendar deps.
- `Application/Frigorino.Web/ClientApp/src/utils/dateUtils.ts` — `CALENDAR_WINDOW_DAYS`.
- `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoriesPage.tsx` — header calendar icon.
- `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json` + `public/locales/de/translation.json` — `inventory.calendar.*`.
- `Application/Frigorino.Web/ClientApp/src/lib/api/*` + `src/lib/openapi.json` + `src/routeTree.gen.ts` — regenerated (committed).

**Cleanup:**
- `IDEAS.md` — remove the completed "Inventory expiry calendar view" entry.

---

## Task 1: Add FullCalendar dependencies

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/package.json`

- [ ] **Step 1: Install the three FullCalendar v6 packages**

Run (from `Application/Frigorino.Web/ClientApp/`):

```bash
npm install @fullcalendar/core @fullcalendar/react @fullcalendar/daygrid
```

Expected: three `@fullcalendar/*` entries added to `dependencies` in `package.json` with caret ranges (e.g. `^6.1.x`), and `package-lock.json` updated. No `@fullcalendar/interaction` — `eventClick` works on daygrid without it.

- [ ] **Step 2: Verify the caret-minor pin**

Read `package.json` and confirm each new dep is `^6.x.y` (caret, not `~`, not pinned exact). If npm wrote anything else, edit to `^`.

- [ ] **Step 3: Verify install + type resolution**

Run (from `ClientApp/`):

```bash
npm run tsc
```

Expected: PASS (exit 0). No new type errors — FullCalendar ships its own types.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/package.json Application/Frigorino.Web/ClientApp/package-lock.json
git commit -m "build(client): add FullCalendar v6 (react + daygrid) for the expiry calendar"
```

---

## Task 2: Backend — GetExpiryCalendar read slice (TDD via API integration test)

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.Api.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarApiSteps.cs`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Create: `Application/Frigorino.Features/Inventories/ExpiryCalendarItemResponse.cs`
- Create: `Application/Frigorino.Features/Inventories/GetExpiryCalendar.cs`
- Modify: `Application/Frigorino.Web/Program.cs`

- [ ] **Step 1: Add the API client helper**

In `TestApiClient.cs`, add this method next to `TryGetInventoriesAsync` (after the `TryDeleteInventoryAsync` method, around line 262):

```csharp
    public Task<IAPIResponse> TryGetExpiryCalendarAsync(int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        return ctx.BrowserContext.APIRequest.GetAsync(
            $"/api/household/{targetHouseholdId}/inventories/calendar",
            new APIRequestContextOptions { Headers = AuthHeaders });
    }
```

- [ ] **Step 2: Write the failing API feature**

Create `ExpiryCalendar.Api.feature`:

```gherkin
Feature: Expiry Calendar API

  Background:
    Given I am logged in with an active household

  Scenario: Calendar returns items with an expiry across all inventories
    Given an inventory "Fridge" has an item "Milk" expiring in 2 days
    And an inventory "Pantry" has an item "Rice" expiring in 40 days
    And an inventory "Fridge" has an item "Salt" with no expiry
    When I GET the expiry calendar via the API
    Then the API response status is 200
    And the API expiry calendar contains "Milk"
    And the API expiry calendar contains "Rice"
    And the API expiry calendar does not contain "Salt"

  Scenario: Non-member cannot read the expiry calendar
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the expiry calendar via the API
    Then the API response status is 404
```

- [ ] **Step 3: Write the step bindings**

Create `ExpiryCalendarApiSteps.cs`:

```csharp
using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class ExpiryCalendarApiSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("an inventory {string} has an item {string} expiring in {int} days")]
    public async Task GivenAnInventoryHasAnItemExpiringInDays(string inventoryName, string itemText, int days)
    {
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(days);
        await SeedItemAsync(inventoryName, itemText, expiry);
    }

    [Given("an inventory {string} has an item {string} with no expiry")]
    public async Task GivenAnInventoryHasAnItemWithNoExpiry(string inventoryName, string itemText)
    {
        await SeedItemAsync(inventoryName, itemText, null);
    }

    [When("I GET the expiry calendar via the API")]
    public async Task WhenIGetTheExpiryCalendarViaTheApi()
    {
        ctx.LastApiResponse = await api.TryGetExpiryCalendarAsync();
    }

    [Then("the API expiry calendar contains {string}")]
    public async Task ThenTheApiExpiryCalendarContains(string text)
    {
        var body = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        var found = body.EnumerateArray().Any(e => e.GetProperty("text").GetString() == text);
        Xunit.Assert.True(found, $"Expected the expiry calendar to contain '{text}'.");
    }

    [Then("the API expiry calendar does not contain {string}")]
    public async Task ThenTheApiExpiryCalendarDoesNotContain(string text)
    {
        var body = (await ctx.LastApiResponse!.JsonAsync())!.Value;
        var found = body.EnumerateArray().Any(e => e.GetProperty("text").GetString() == text);
        Xunit.Assert.False(found, $"Expected the expiry calendar NOT to contain '{text}'.");
    }

    private async Task SeedItemAsync(string inventoryName, string itemText, DateOnly? expiry)
    {
        using var scope = ctx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!ctx.InventoryIds.TryGetValue(inventoryName, out var inventoryId))
        {
            var creation = Inventory.Create(inventoryName, null, ctx.HouseholdId, ctx.UserContext.UserId);
            if (creation.IsFailed)
            {
                throw new InvalidOperationException(
                    $"Seed failed for inventory '{inventoryName}': {string.Join(", ", creation.Errors.Select(e => e.Message))}");
            }

            db.Inventories.Add(creation.Value);
            await db.SaveChangesAsync();
            inventoryId = creation.Value.Id;
            ctx.InventoryIds[inventoryName] = inventoryId;
        }

        db.InventoryItems.Add(new InventoryItem
        {
            InventoryId = inventoryId,
            Text = itemText,
            ExpiryDate = expiry,
            SortOrder = 0,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run (from repo root):

```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~ExpiryCalendar" 2>&1 | tail -30
```

Expected: FAIL — the "returns items" scenario fails because `GET …/inventories/calendar` is unregistered (returns 404, so "status is 200" fails). (Docker Desktop must be running for Testcontainers; if it errors with daemon-unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 5: Create the response DTO**

Create `ExpiryCalendarItemResponse.cs`:

```csharp
using System.Linq.Expressions;
using Frigorino.Domain.Entities;
using Frigorino.Features.Quantities;

namespace Frigorino.Features.Inventories
{
    // Flat per-bar payload for the household-wide cook-by calendar. One row per active inventory
    // item that has an expiry date; ExpiryDate is non-null by construction (the slice filters out
    // items without one). InventoryName drives the per-bar "which inventory" cue.
    public sealed record ExpiryCalendarItemResponse(
        int Id,
        int InventoryId,
        string InventoryName,
        string Text,
        QuantityDto? Quantity,
        DateOnly ExpiryDate)
    {
        // EF-translatable projection used by the read slice. Reads InventoryName through the
        // InventoryItem.Inventory navigation; stays inline (no method calls) so EF can translate it.
        public static readonly Expression<Func<InventoryItem, ExpiryCalendarItemResponse>> ToProjection = i => new ExpiryCalendarItemResponse(
            i.Id,
            i.InventoryId,
            i.Inventory.Name,
            i.Text,
            i.QuantityValue == null
                ? null
                : new QuantityDto(i.QuantityValue.Value, i.QuantityUnit!.Value),
            i.ExpiryDate!.Value);
    }
}
```

- [ ] **Step 6: Create the slice**

Create `GetExpiryCalendar.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Inventories
{
    public static class GetExpiryCalendarEndpoint
    {
        public static IEndpointRouteBuilder MapGetExpiryCalendar(this IEndpointRouteBuilder app)
        {
            // Collection-level view over ALL of the household's inventories — note the literal
            // "calendar" segment, NOT "{inventoryId}/calendar". The int route constraint on the
            // sibling "{inventoryId:int}" routes keeps "calendar" from colliding with them.
            app.MapGet("calendar", Handle)
               .WithName("GetExpiryCalendar")
               .Produces<ExpiryCalendarItemResponse[]>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status404NotFound);
            return app;
        }

        private static async Task<Results<Ok<ExpiryCalendarItemResponse[]>, NotFound>> Handle(
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

            var response = await db.InventoryItems
                .Where(i => i.IsActive
                    && i.ExpiryDate != null
                    && i.Inventory.IsActive
                    && i.Inventory.HouseholdId == householdId)
                .OrderBy(i => i.ExpiryDate)
                .Select(ExpiryCalendarItemResponse.ToProjection)
                .ToArrayAsync(ct);

            return TypedResults.Ok(response);
        }
    }
}
```

- [ ] **Step 7: Register the slice**

In `Program.cs`, find the inventories group registration (around line 363) and add the new line after `inventories.MapGetInventories();`:

```csharp
inventories.MapGetInventories();
inventories.MapGetExpiryCalendar();
```

- [ ] **Step 8: Run the test to verify it passes**

Run (from repo root):

```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~ExpiryCalendar" 2>&1 | tail -30
```

Expected: the two API scenarios PASS. (The UI feature file does not exist yet — `~ExpiryCalendar` currently matches only the API scenarios.) Confirm via the "Passed!" summary line, not just a zero exit on a piped command.

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Features/Inventories/ExpiryCalendarItemResponse.cs \
        Application/Frigorino.Features/Inventories/GetExpiryCalendar.cs \
        Application/Frigorino.Web/Program.cs \
        Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.Api.feature \
        Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarApiSteps.cs \
        Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs
git commit -m "feat(api): household-wide expiry calendar read slice"
```

---

## Task 3: Regenerate the TypeScript API client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/openapi.json`, `src/lib/api/**`

- [ ] **Step 1: Regenerate the client**

Run (from `Application/Frigorino.Web/ClientApp/`):

```bash
npm run api
```

Expected: rebuilds the backend, re-emits `openapi.json`, regenerates `src/lib/api/`. New symbols appear: `getExpiryCalendarOptions` / `getExpiryCalendarQueryKey` in `src/lib/api/@tanstack/react-query.gen.ts`, and an `ExpiryCalendarItemResponse` type in `src/lib/api/types.gen.ts`.

- [ ] **Step 2: Verify the generated symbols exist**

Run (from `ClientApp/`):

```bash
grep -r "getExpiryCalendarOptions" src/lib/api/ && grep -r "ExpiryCalendarItemResponse" src/lib/api/types.gen.ts
```

Expected: both grep commands print matches.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/openapi.json Application/Frigorino.Web/ClientApp/src/lib/api
git commit -m "chore(client): regenerate API client for expiry calendar endpoint"
```

---

## Task 4: Window constant + event mapping helper

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/utils/dateUtils.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/expiryCalendarEvents.ts`

- [ ] **Step 1: Add the window constant**

In `dateUtils.ts`, add directly after the `EXPIRY_THRESHOLDS` block (after line 19):

```typescript
// Cook-by planning window for the expiry calendar: how many days BEFORE an item's expiry
// its bar begins. Deliberately decoupled from EXPIRY_THRESHOLDS so the bar length (planning
// horizon) and the urgency color bands can be tuned independently.
export const CALENDAR_WINDOW_DAYS = 7;
```

- [ ] **Step 2: Create the event mapping helper**

Create `expiryCalendarEvents.ts`:

```typescript
import type { ExpiryCalendarItemResponse } from "../../../lib/api";
import {
    CALENDAR_WINDOW_DAYS,
    getExpiryLevel,
    parseLocalDate,
    type ExpiryLevel,
} from "../../../utils/dateUtils";

export interface ExpiryEventProps {
    itemId: number;
    inventoryId: number;
    inventoryName: string;
    expiryDate: string;
    activeToday: boolean;
}

export interface ExpiryCalendarEvent {
    id: string;
    title: string;
    start: string; // YYYY-MM-DD, inclusive
    end: string; // YYYY-MM-DD, EXCLUSIVE (FullCalendar all-day convention)
    allDay: true;
    backgroundColor: string;
    borderColor: string;
    extendedProps: ExpiryEventProps;
}

const MS_PER_DAY = 1000 * 60 * 60 * 24;

function toIsoDate(date: Date): string {
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function addDays(date: Date, days: number): Date {
    const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    copy.setDate(copy.getDate() + days);
    return copy;
}

function wholeDayDiff(from: Date, to: Date): number {
    return Math.round((to.getTime() - from.getTime()) / MS_PER_DAY);
}

// Maps inventory items (each with an expiry) to all-day cook-by bars spanning
// [expiry - CALENDAR_WINDOW_DAYS, expiry]. FullCalendar's all-day `end` is EXCLUSIVE, so
// end = expiry + 1 makes the bar cover the expiry day itself. `levelColor` is injected by the
// page so the bar color stays in sync with the MUI theme palette. `todayIso` is passed in
// (not read from the clock here) so the mapping is pure and deterministic.
export function buildExpiryEvents(
    items: ExpiryCalendarItemResponse[],
    todayIso: string,
    levelColor: (level: ExpiryLevel) => string,
): ExpiryCalendarEvent[] {
    const today = parseLocalDate(todayIso);
    return items.map((item) => {
        const expiry = parseLocalDate(item.expiryDate);
        const level = getExpiryLevel(wholeDayDiff(today, expiry));
        const color = levelColor(level);
        const windowStart = addDays(expiry, -CALENDAR_WINDOW_DAYS);
        const activeToday =
            wholeDayDiff(windowStart, today) >= 0 &&
            wholeDayDiff(today, expiry) >= 0;
        return {
            id: String(item.id),
            title: item.text,
            start: toIsoDate(windowStart),
            end: toIsoDate(addDays(expiry, 1)),
            allDay: true,
            backgroundColor: color,
            borderColor: color,
            extendedProps: {
                itemId: item.id,
                inventoryId: item.inventoryId,
                inventoryName: item.inventoryName,
                expiryDate: item.expiryDate,
                activeToday,
            },
        };
    });
}
```

- [ ] **Step 3: Type-check**

Run (from `ClientApp/`):

```bash
npm run tsc
```

Expected: PASS. (`ExpiryCalendarItemResponse` resolves from the Task 3 regeneration; `getExpiryLevel`, `parseLocalDate`, `ExpiryLevel`, `CALENDAR_WINDOW_DAYS` all resolve from `dateUtils`.)

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/utils/dateUtils.ts \
        Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/expiryCalendarEvents.ts
git commit -m "feat(client): expiry calendar event mapping + window constant"
```

---

## Task 5: Query hook

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/useExpiryCalendar.ts`

- [ ] **Step 1: Write the hook**

Create `useExpiryCalendar.ts` (mirrors `useInventoryItems.ts`):

```typescript
import { useQuery } from "@tanstack/react-query";
import { getExpiryCalendarOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useExpiryCalendar = (householdId: number, enabled = true) =>
    useQuery({
        ...getExpiryCalendarOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 30,
    });
```

- [ ] **Step 2: Type-check**

Run (from `ClientApp/`):

```bash
npm run tsc
```

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/useExpiryCalendar.ts
git commit -m "feat(client): useExpiryCalendar query hook"
```

---

## Task 6: Calendar page, styles, i18n, and route

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/expiryCalendar.css`
- Create: `Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/routes/inventories/calendar.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add i18n keys (English)**

In `public/locales/en/translation.json`, inside the existing top-level `"inventory"` object, add a `"calendar"` sub-object:

```json
        "calendar": {
            "title": "Expiry calendar",
            "empty": "No items with an expiry date yet.",
            "failedToLoad": "Failed to load the calendar."
        }
```

(Add a comma after the preceding key/value so the JSON stays valid.)

- [ ] **Step 2: Add i18n keys (German)**

In `public/locales/de/translation.json`, inside the existing top-level `"inventory"` object, add:

```json
        "calendar": {
            "title": "Ablaufkalender",
            "empty": "Noch keine Artikel mit Ablaufdatum.",
            "failedToLoad": "Kalender konnte nicht geladen werden."
        }
```

- [ ] **Step 3: Create the stylesheet**

Create `expiryCalendar.css`:

```css
/* Scoped to the page wrapper so FullCalendar's auto-injected base CSS is only adjusted here.
   The --fc-* variables are bridged to the MUI theme inline on the wrapper (see the page sx). */
.expiry-calendar .fc-daygrid-event-harness {
    margin-bottom: 2px; /* vertical gap so overlapping bars read as distinct rows */
}

.expiry-calendar .fc-event {
    border-radius: 6px;
    cursor: pointer;
    padding: 0 2px;
}

/* Focus-select: the tapped item stays bright with a white inset ring; the rest dim. */
.expiry-calendar .cal-selected {
    opacity: 1;
    box-shadow: inset 0 0 0 2px #fff;
}

.expiry-calendar .cal-dimmed {
    opacity: 0.3;
}

/* Cook-by window active today. */
.expiry-calendar .cal-active {
    font-weight: 700;
}

/* Toolbar chrome nudged toward MUI (no shouty uppercase / heavy shadow). */
.expiry-calendar .fc .fc-button {
    text-transform: none;
    box-shadow: none;
}
```

- [ ] **Step 4: Create the page component**

Create `pages/ExpiryCalendarPage.tsx`:

```tsx
import dayGridPlugin from "@fullcalendar/daygrid";
import FullCalendar from "@fullcalendar/react";
import type { EventClickArg, EventContentArg } from "@fullcalendar/core";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    Typography,
    useTheme,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { PageHeadActionBar } from "../../../../components/shared/PageHeadActionBar";
import { useCurrentHousehold } from "../../../me/activeHousehold/useCurrentHousehold";
import { pageContainerSx } from "../../../../theme";
import {
    formatLocalDate,
    todayIsoDate,
    type ExpiryLevel,
} from "../../../../utils/dateUtils";
import {
    buildExpiryEvents,
    type ExpiryEventProps,
} from "../expiryCalendarEvents";
import "../expiryCalendar.css";

export const ExpiryCalendarPage = () => {
    const theme = useTheme();
    const navigate = useNavigate();
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const { data: items, isLoading, error } = useExpiryCalendar(
        householdId,
        householdId > 0,
    );

    // Single-select focus: the tapped item is highlighted, the rest dim. null = nothing selected.
    const [selectedId, setSelectedId] = useState<number | null>(null);

    const levelColor = useMemo(() => {
        return (level: ExpiryLevel): string => {
            if (level === "expired" || level === "critical") {
                return theme.palette.error.main;
            }
            if (level === "soon") {
                return theme.palette.warning.main;
            }
            return theme.palette.success.main;
        };
    }, [theme]);

    const events = useMemo(
        () => buildExpiryEvents(items ?? [], todayIsoDate(), levelColor),
        [items, levelColor],
    );

    // Classes drive the focus-select visuals (see expiryCalendar.css).
    const eventClassNames = (arg: EventContentArg): string[] => {
        const props = arg.event.extendedProps as ExpiryEventProps;
        const classes: string[] = [];
        if (props.activeToday) {
            classes.push("cal-active");
        }
        if (selectedId !== null) {
            classes.push(
                selectedId === props.itemId ? "cal-selected" : "cal-dimmed",
            );
        }
        return classes;
    };

    // Stamp the expiry date on the bar tail (isEnd) and a continuation marker on wrapped
    // segments (!isStart) so a long Mon->Sun span is identifiable on any week-row it touches.
    // data-selected is the test hook for the focus-select assertion.
    const renderEventContent = (arg: EventContentArg) => {
        const props = arg.event.extendedProps as ExpiryEventProps;
        const isSelected = selectedId === props.itemId;
        return (
            <Box
                data-testid={`cal-event-${arg.event.title}`}
                data-selected={isSelected ? "true" : "false"}
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 0.5,
                    overflow: "hidden",
                    whiteSpace: "nowrap",
                    fontSize: "0.7rem",
                    px: 0.25,
                }}
            >
                {!arg.isStart && <span>↩</span>}
                <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>
                    {arg.event.title}
                </span>
                <span style={{ opacity: 0.8 }}>· {props.inventoryName}</span>
                {arg.isEnd && (
                    <span style={{ marginLeft: "auto", fontWeight: 600 }}>
                        {formatLocalDate(props.expiryDate)}
                    </span>
                )}
            </Box>
        );
    };

    const handleEventClick = (info: EventClickArg) => {
        // Stop the wrapper's clear-on-empty handler from firing for this same click.
        info.jsEvent.stopPropagation();
        const props = info.event.extendedProps as ExpiryEventProps;
        setSelectedId((prev) => (prev === props.itemId ? null : props.itemId));
    };

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("inventory.selectHouseholdToViewInventories")}
                    <Button
                        onClick={() => navigate({ to: "/" })}
                        sx={{ mt: 1, display: "block" }}
                    >
                        {t("common.goBackToDashboard")}
                    </Button>
                </Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={t("inventory.calendar.title")}
                section="inventory"
                directActions={[]}
                menuActions={[]}
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
                {isLoading && (
                    <Box
                        sx={{
                            display: "flex",
                            justifyContent: "center",
                            py: 4,
                        }}
                    >
                        <CircularProgress />
                    </Box>
                )}
                {error && (
                    <Alert severity="error" sx={{ mb: 3 }}>
                        {t("inventory.calendar.failedToLoad")}
                    </Alert>
                )}
                {!isLoading && !error && events.length === 0 && (
                    <Typography
                        variant="body2"
                        sx={{ color: "text.secondary", textAlign: "center", py: 4 }}
                        data-testid="calendar-empty"
                    >
                        {t("inventory.calendar.empty")}
                    </Typography>
                )}
                {!isLoading && !error && events.length > 0 && (
                    <Box
                        className="expiry-calendar"
                        data-testid="expiry-calendar"
                        onClick={() => setSelectedId(null)}
                        sx={{
                            "--fc-border-color": theme.palette.divider,
                            "--fc-page-bg-color": theme.palette.background.paper,
                            "--fc-neutral-bg-color": theme.palette.action.hover,
                            "--fc-today-bg-color": theme.palette.action.selected,
                            "& .fc": { fontSize: "0.8rem" },
                        }}
                    >
                        <FullCalendar
                            plugins={[dayGridPlugin]}
                            initialView="dayGridMonth"
                            height="auto"
                            firstDay={1}
                            dayMaxEvents={false}
                            dayMaxEventRows={false}
                            headerToolbar={{
                                left: "prev,next today",
                                center: "title",
                                right: "",
                            }}
                            events={events}
                            eventContent={renderEventContent}
                            eventClassNames={eventClassNames}
                            eventClick={handleEventClick}
                        />
                    </Box>
                )}
            </Container>
        </>
    );
};
```

- [ ] **Step 5: Add the hook import**

The page references `useExpiryCalendar` — add its import at the top of `ExpiryCalendarPage.tsx` with the other relative imports:

```tsx
import { useExpiryCalendar } from "../useExpiryCalendar";
```

- [ ] **Step 6: Create the route shell**

Create `src/routes/inventories/calendar.tsx` (mirrors `routes/inventories/create.tsx`):

```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { ExpiryCalendarPage } from "../../features/inventories/calendar/pages/ExpiryCalendarPage";

export const Route = createFileRoute("/inventories/calendar")({
    beforeLoad: requireAuth,
    component: ExpiryCalendarPage,
});
```

- [ ] **Step 7: Regenerate the route tree**

Run (from `ClientApp/`):

```bash
npm run routes:gen
```

Expected: `src/routeTree.gen.ts` updated with the `/inventories/calendar` route.

- [ ] **Step 8: Type-check + lint**

Run (from `ClientApp/`):

```bash
npm run tsc && npm run lint
```

Expected: both PASS. If lint flags the `useExpiryCalendar` import ordering, run `npm run fix` and re-check.

- [ ] **Step 9: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/calendar/ \
        Application/Frigorino.Web/ClientApp/src/routes/inventories/calendar.tsx \
        Application/Frigorino.Web/ClientApp/src/routeTree.gen.ts \
        Application/Frigorino.Web/ClientApp/public/locales/en/translation.json \
        Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(client): expiry calendar page with focus-select"
```

---

## Task 7: Entry point — calendar icon in the Inventories header

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoriesPage.tsx`

- [ ] **Step 1: Import the calendar icon**

Change the icon import at the top of `InventoriesPage.tsx` (line 1) from:

```tsx
import { Add } from "@mui/icons-material";
```

to:

```tsx
import { Add, CalendarMonth } from "@mui/icons-material";
```

- [ ] **Step 2: Add the navigate handler**

After `handleCreateInventory` (line 43), add:

```tsx
    const handleOpenCalendar = () => navigate({ to: "/inventories/calendar" });
```

- [ ] **Step 3: Add the header action**

Replace the `directActions` prop on `PageHeadActionBar` (line 93) with:

```tsx
                directActions={[
                    { icon: <Add />, onClick: handleCreateInventory },
                    {
                        icon: <CalendarMonth />,
                        onClick: handleOpenCalendar,
                        testId: "inventories-calendar-button",
                    },
                ]}
```

- [ ] **Step 4: Type-check + lint**

Run (from `ClientApp/`):

```bash
npm run tsc && npm run lint
```

Expected: both PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/inventories/pages/InventoriesPage.tsx
git commit -m "feat(client): open the expiry calendar from the inventories header"
```

---

## Task 8: UI integration test

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs`

- [ ] **Step 1: Build the SPA so the harness serves the new page + testids**

Run (from `ClientApp/`):

```bash
npm run build
```

Expected: PASS — outputs to `ClientApp/build`. (The integration harness serves `ClientApp/build`, not live source.)

- [ ] **Step 2: Write the UI feature**

Create `ExpiryCalendar.feature`:

```gherkin
Feature: Expiry Calendar

  Background:
    Given I am logged in with an active household

  Scenario: User opens the calendar from the inventories header and focuses an item
    Given an inventory "Fridge" has an item "Milk" expiring in 2 days
    When I open the inventories overview
    And I open the expiry calendar from the header
    Then the calendar shows the item "Milk"
    When I select the calendar item "Milk"
    Then the calendar item "Milk" is focused
```

- [ ] **Step 3: Write the UI step bindings**

Create `ExpiryCalendarSteps.cs` (the `Given … expiring in … days` step is reused from `ExpiryCalendarApiSteps` — Reqnroll shares bindings across the assembly):

```csharp
namespace Frigorino.IntegrationTests.Slices.Inventories;

[Binding]
public class ExpiryCalendarSteps(ScenarioContextHolder ctx)
{
    [When("I open the inventories overview")]
    public async Task WhenIOpenTheInventoriesOverview()
    {
        await ctx.Page.GotoAsync("/inventories", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    [When("I open the expiry calendar from the header")]
    public async Task WhenIOpenTheExpiryCalendarFromTheHeader()
    {
        await ctx.Page.GetByTestId("inventories-calendar-button").ClickAsync();
        await ctx.Page.WaitForURLAsync("**/inventories/calendar");
    }

    [Then("the calendar shows the item {string}")]
    public async Task ThenTheCalendarShowsTheItem(string itemText)
    {
        // A bar can wrap into multiple week-row segments (same testid each); assert the first.
        await Assertions.Expect(ctx.Page.GetByTestId($"cal-event-{itemText}").First)
            .ToBeVisibleAsync();
    }

    [When("I select the calendar item {string}")]
    public async Task WhenISelectTheCalendarItem(string itemText)
    {
        await ctx.Page.GetByTestId($"cal-event-{itemText}").First.ClickAsync();
    }

    [Then("the calendar item {string} is focused")]
    public async Task ThenTheCalendarItemIsFocused(string itemText)
    {
        await Assertions.Expect(
                ctx.Page.GetByTestId($"cal-event-{itemText}").First)
            .ToHaveAttributeAsync("data-selected", "true");
    }
}
```

- [ ] **Step 4: Run the UI test to verify it passes**

Run (from repo root):

```bash
dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~ExpiryCalendar" 2>&1 | tail -30
```

Expected: all ExpiryCalendar scenarios (2 API + 1 UI) PASS. Confirm via the "Passed!" summary line. (Docker Desktop must be running.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendar.feature \
        Application/Frigorino.IntegrationTests/Slices/Inventories/ExpiryCalendarSteps.cs
git commit -m "test(it): expiry calendar UI scenario (open from header + focus-select)"
```

---

## Task 9: Final verification, cleanup, and manual check

**Files:**
- Modify: `IDEAS.md`

- [ ] **Step 1: Remove the completed IDEAS.md entry**

Open `IDEAS.md`, delete the entire "## Inventory expiry calendar view (cook-by windows)" section (the heading and its bullet list). Leave the spike-learnings and design/plan docs in place (they are durable references).

- [ ] **Step 2: Full backend + integration test run**

Run (from repo root):

```bash
dotnet test Application/Frigorino.sln 2>&1 | tail -25
```

Expected: PASS — `Frigorino.Test` + `Frigorino.IntegrationTests`. Confirm via the "Passed!" summary, not a piped exit code.

- [ ] **Step 3: Frontend verification gate**

Run (from `ClientApp/`):

```bash
npm run lint && npm run tsc && npm run prettier:check
```

Expected: all PASS. If `prettier:check` fails, run `npm run prettier` and re-commit.

- [ ] **Step 4: Docker build (drift gate)**

Run (from repo root):

```bash
docker build -f Application/Dockerfile -t frigorino .
```

Expected: PASS. (No project was added, so no Dockerfile edit is needed — this just confirms the SPA build with the new dep still publishes cleanly. If the Docker daemon is unreachable, ask the user to start Docker Desktop.)

- [ ] **Step 5: Manual browser verify (catches the runtime-only class of bug)**

Bring up the dev stack (`/dev-up` skill) and, at a 390×844 phone viewport: sign in → dashboard → Inventory card chevron → `/inventories` → tap the calendar icon. Confirm: cook-by bars render colored by urgency; the expiry date shows on each bar's tail; tapping a bar highlights it and dims the rest; tapping it again or tapping empty space clears the focus. Seed at least two items whose windows overlap to confirm rows grow (no "+N" overflow). Tear down with `/dev-down` only if the user asks.

- [ ] **Step 6: Commit the cleanup**

```bash
git add IDEAS.md
git commit -m "docs: drop shipped expiry-calendar idea from IDEAS.md"
```

- [ ] **Step 7: Finish the branch**

Use the `superpowers:finishing-a-development-branch` skill to choose how to integrate `feat/inventory-calendar` (the project promotes via `stage`, per project memory).

---

## Self-Review

**Spec coverage:**
- Household-wide scope → Task 2 slice queries all active inventories in the household. ✓
- Dedicated read slice (vs client aggregation) → Task 2. ✓
- Placement under inventories, named for the view → `GetExpiryCalendar` on the `inventories` group at `inventories/calendar`. ✓
- Fixed configurable window `X`, no domain change → `CALENDAR_WINDOW_DAYS` in `dateUtils.ts` (Task 4); no domain files touched. ✓
- `start = expiry − X`, `end = expiry + 1` exclusive, local date parse → `buildExpiryEvents` (Task 4). ✓
- Color via `getExpiryLevel` bands as raw hex from theme → `levelColor` + event `backgroundColor/borderColor` (Tasks 4/6). ✓
- `eventContent` tail date + `↩` continuation + inventory cue → Task 6. ✓
- Active-today highlight via `eventClassNames` → `cal-active` (Tasks 4/6). ✓
- Single-select focus, tap-again/empty clears, no dialog/edit/action → `selectedId` + `cal-selected`/`cal-dimmed` + wrapper clear (Task 6). ✓
- Growing rows (no overflow) → `dayMaxEvents={false}` + `dayMaxEventRows={false}` (Task 6). ✓
- Entry: calendar icon in Inventories header → Task 7. ✓
- i18n via `t()` → Task 6 keys. ✓
- Testids + UI integration test (assert on `data-*`, never translated text) → Tasks 6/8. ✓
- MUI theming of FC chrome → `expiryCalendar.css` + `--fc-*` bridge (Task 6). ✓
- Verification (full sln test + lint/tsc/prettier + docker + manual) → Task 9. ✓
- Out-of-scope items (per-category/per-item X, detail/edit/quick-action, re-order, multi-select, dashboard shortcut) → not implemented. ✓

**Placeholder scan:** No TBD/TODO; every code step shows full content. ✓

**Type consistency:** `ExpiryCalendarItemResponse` (C# DTO ↔ generated TS type) used consistently; `ExpiryEventProps`/`ExpiryCalendarEvent` defined in Task 4 and consumed in Task 6; `buildExpiryEvents(items, todayIso, levelColor)` signature matches its call site; `getExpiryCalendarOptions` (Task 3 output) used in Task 5; testid `inventories-calendar-button` defined in Task 7 and used in Task 8; testid `cal-event-{title}` + `data-selected` defined in Task 6 and asserted in Task 8. ✓

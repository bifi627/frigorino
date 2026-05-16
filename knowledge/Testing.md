# Testing

Working notes on how the test suites are layered and where each style of test belongs. Trust this document over older notes — testing conventions have shifted alongside the slice migration.

## The two test projects

| Project | Runner | What it tests | Speed |
|---|---|---|---|
| `Frigorino.Test` | xUnit + FakeItEasy + EF InMemory | Domain aggregates (pure logic, no DbContext when possible), ArchUnit layer rules | sub-ms per test, full project < 5s |
| `Frigorino.IntegrationTests` | Reqnroll (BDD) + Playwright + Postgres Testcontainers | End-to-end slices — real HTTP, real Postgres, real browser. Each scenario gets a fresh DB. | 5–15s per scenario; suite ~minutes |

These are two different jobs, not two tiers of the same job. They answer different questions and should not duplicate each other.

## Unit vs integration: which question are you answering?

| Question | Test type |
|---|---|
| "Does this aggregate method enforce its invariant?" | **Unit** (`Frigorino.Test/Domain/*Tests.cs`) |
| "Does my validation matrix cover every branch?" | **Unit** — enumerate the matrix here |
| "Does the slice handler map the right `Result` error to the right HTTP status?" | Unit OR integration (one representative case integration; the rest unit if you have a handler-level test) |
| "Does the user actually see the change after submitting the form?" | **Integration UI** (Playwright via Reqnroll) |
| "Does the multi-user / role-policy gate hold end-to-end?" | **Integration API** (Reqnroll + `TestApiClient`) |
| "Does Postgres+EF persist what the aggregate produced?" | **Integration API** — InMemory doesn't catch this |
| "Does the request pipeline (auth → middleware → handler) wire correctly?" | **Integration** — the unit doesn't include the pipeline |

**Don't duplicate.** If `ListAggregateItemTests` already enumerates 12 branches of `AddItem`, the integration suite hits ONE happy path through `POST /items` — not all 12. The cost of an integration scenario (Postgres roundtrip + browser context) is wasted enumerating branches the aggregate test already covers.

**Don't unit-test what only the pipeline reveals.** Auth, session middleware, EF projection differences, OpenAPI serialization — those need integration coverage. A passing aggregate unit test guarantees nothing about whether the slice actually returns 403 instead of 404 for the unauthorized case.

## Integration: API vs Playwright

Both run through the same Reqnroll harness and share `ScenarioContextHolder`, but they answer different things.

| Use **Playwright** (UI) when... | Use **API** (via `TestApiClient`) when... |
|---|---|
| The assertion is "the user sees X" | The assertion is "the server returns X" |
| You're verifying a real user flow end-to-end (form → mutation → updated DOM) | You're verifying a backend invariant or role-policy gate |
| You need to exercise frontend wiring (TanStack Query cache, optimistic updates, navigation) | You need to bypass UI guards (HTML5 `required`, autocomplete sanitization) to hit a slice's validation branch directly |
| You're testing browser-only concerns (i18n, routing, focus management) | You're testing sort-order, aggregate state transitions, or anything the optimistic UI might shadow |

### Why the split matters: a real failure we hit

The audit suggested `Scenario: Toggling an item back to unchecked moves it below other unchecked items`. Written as a UI test, it failed: `expected {Bread, Milk}, got {Milk, Bread}`.

The backend was correct. `List.ToggleItemStatus` calls `ComputeAppendSortOrder` which appends to the bottom. The bug was in the test: `useToggleListItemStatus`'s optimistic update **only flips `status` and leaves `sortOrder` alone**, so the DOM briefly shows the stale order until the debounced refetch arrives. The UI assertion was racing against the debounce.

The lesson: **never UI-test a backend invariant the optimistic layer might shadow.** Move it to an API scenario. The UI test, if any, asserts "the row eventually shows in the new section", not "the row is at position N".

This is the most common "false negative dressed as a real failure" pattern in this codebase. If a Playwright scenario keeps flaking on order or count, ask: "is the optimistic update doing the same math as the server?" If no, the test belongs at the API layer.

### Feature-file layout

Heavy features split UI from API into sibling files:

```
Slices/Lists/
  Lists.feature           # UI: create / rename / delete
  Lists.Api.feature       # API: validation, role policy, 403/404
  ListItems.feature       # UI: add / toggle / remove / drag-reorder
  ListItems.Api.feature   # API: validation, compact, reorder math, toggle placement
```

Step bindings split the same way (`ListSteps.cs` / `ListApiSteps.cs` / `ListItemSteps.cs` / `ListItemApiSteps.cs`). Reqnroll discovers bindings globally so the split is for readability, not for binding scope.

Rule of thumb: when a single feature file goes above ~8 scenarios, split. Either UI/API as above, or by sub-theme via Gherkin `Rule:` blocks inside one file. Don't split into a file per scenario.

## What makes a valuable integration scenario

Bias toward scenarios that pin invariants which are easy to mis-implement:

- **Multi-user / role-policy negatives** — every slice migration is one careful condition away from regressing 403. These are the highest catch-rate-per-minute tests in the suite.
- **Aggregate invariant edges** — last-owner protection, sort-order section boundaries, soft-delete read filters.
- **Auth-boundary 404-vs-403** — the audit-boundary rule is "non-member sees 404; non-authorized member sees 403". Easy to swap.
- **Optimistic UI rollback paths** — a Playwright scenario with `Page.RouteAsync` intercepting the mutation to force a 4xx, asserting the UI reverts.

Skip:
- Pure read-after-write smoke ("I create X then navigate to overview and X is there") when another scenario already proves the read path works transitively.
- Tests that only exercise the framework ("I navigate to /x then I see /x").
- Tests asserting on translated text via `GetByText` — these break the moment i18n changes. Use testids or `data-*` attributes.
- Tests enumerating aggregate-method branches the unit suite already covers.

## Step-binding patterns that prevented flakes

A few patterns earned their keep during the slice migrations. Use these by default rather than discovering them under flake pressure.

**Always await mutation responses before the next step.** Back-to-back UI mutations race. The pattern:

```csharp
var responseTask = ctx.Page.WaitForResponseAsync(r =>
    r.Url.EndsWith("/items") && r.Request.Method == "POST" && r.Status == 201);
await ctx.Page.GetByTestId("submit-button").ClickAsync();
await responseTask;
```

Subscribe **before** the click so the listener is in place when the request fires.

**`DispatchEventAsync` over `ClickAsync` for dnd-kit-wrapped elements.** dnd-kit's sortable container adds ancestor aria attributes that Playwright's actionability check reads as "element is not enabled". `DispatchEventAsync(selector, "click")` bypasses the check by firing a synthetic click. `LocatorClickOptions { Force = true }` is the alternative when you need the full click flow.

**Retrying Playwright assertions over snapshot `IsVisibleAsync()`.** `Assertions.Expect(locator).ToBeVisibleAsync()` retries until the timeout; `IsVisibleAsync()` returns the value at that instant. Snapshot reads race against React re-renders.

**`Locator.Count` + a `ToHaveCountAsync` guard before a for-loop over `Nth(i)`.** Reading N attributes in a loop is one-shot; if the DOM is mid-rerender, the count is wrong. Wait for the count to stabilize first.

**Scope selectors to sections, not whole pages.** `GetByText("Flour").First` will match a placeholder, a tooltip, or an autocomplete suggestion. `page.Locator("[data-section='unchecked-items']").GetByText("Flour")` won't.

**Convert relative-time/seed values to absolute.** Seeded user ids derived from `ctx.DatabaseName` suffix prevent cross-scenario `Users` row collisions when multiple scenarios reuse the same alias.

## When to add a testid

Bare components from `@mui/material` don't get testids — none of MUI's `TextField` / `Button` / `IconButton` ships with one. Add `data-testid` whenever:

- You're writing a Playwright step that needs a stable selector.
- The role-based selector (`GetByRole(AriaRole.Textbox)`) would match more than one element on the page.
- The element's text comes from `t()` — translated text is off-limits for assertions.
- An action lives inside a shared component (page header, list-item menu, sortable item) where the per-instance label is needed in the testid: `data-testid={`drag-handle-item-${item.text}`}`.

When a shared component takes an action prop (like `HeadNavigationAction`), add an optional `testId?` field on the type and plumb it to a `data-testid` attribute on the rendered element. Don't tag the IconButton with the action's `text` — text is translated.

## Tools that are missing on purpose

- **No frontend test runner.** Jest/Vitest aren't configured. Component-level concerns are covered by Playwright integration tests through the real UI. If you find yourself wanting to unit-test a React hook, ask first whether an integration scenario covers it — usually yes.
- **No browser-launching unit tests.** Tests in `Frigorino.Test` never spin up a browser. If you need a browser, the test goes in `Frigorino.IntegrationTests`.
- **No mocked database in integration tests.** Postgres Testcontainers all the way down. InMemory provider hides query-translation and constraint bugs that real Postgres would catch.

## Verification cheat sheet

Common commands when adding tests:

```powershell
# Run a specific feature
dotnet test Application/Frigorino.IntegrationTests/... --filter "FullyQualifiedName~ListItemsApiFeature"

# Run aggregate unit tests for a feature area
dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ListAggregateItemTests"

# Frontend type + lint after adding testids
cd Application/Frigorino.Web/ClientApp && npm run tsc && npm run lint
```

When the combined `FullyQualifiedName~Slices.Lists|FullyQualifiedName~Slices.Inventories` run flakes with "Navigation interrupted by about:blank" or `**/lists/*/view` timeouts, the per-feature filter is the diagnostic: if each feature passes individually, the failure is a known parallelism teardown race, not a real regression.

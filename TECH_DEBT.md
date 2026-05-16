# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:
- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

## Drop the static `_checkedConnections` cache in `InitialConnectionMiddleware`

- **Where:** `Application/Frigorino.Web/Middlewares/InitialConnectMiddleware.cs:13`, with the test-side workaround in `Application/Frigorino.IntegrationTests/Shared/NavigationSteps.cs:9-11`.
- **Why deferred:** Replacing the unsafe `HashSet` with `ConcurrentDictionary` + a Postgres upsert was the surgical fix to stop integration-test flakes; removing the cache entirely is a behavior change and was out of scope for that PR.
- **Plan:** Remove the dictionary and run the upsert on every authenticated request. The upsert is idempotent and sub-millisecond on a hot connection. Then drop the per-scenario user-id-suffix workaround in `NavigationSteps.GivenIAmLoggedInAs` so future scenarios don't have to remember it.
- **Risk if left:** A new integration scenario that forgets to derive a unique user ID from the DB name will reuse a cached `userId`, skip the upsert against the fresh database, and fail with FK violations from a missing `Users` row. Easy to introduce, hard to debug.

## Move the `IntegrationTest` env branch out of `Program.cs`

- **Where:** `Application/Frigorino.Web/Program.cs:21-23`.
- **Why deferred:** The env-string check was the smallest change that let `TestWebApplicationFactory` register `TestAuthHandler` without Firebase trying to validate placeholder configuration. Refactoring DI ordering for clean replacement would have grown the slice PR.
- **Plan:** Register `AddFirebaseAuth` unconditionally in `Program.cs`, then have `TestWebApplicationFactory.ConfigureWebHost` *replace* the registered authentication scheme via `services.AddAuthentication(TestAuthHandler.SchemeName).AddScheme<...>(...)` after removing/overriding the Firebase entries. Pair with a `Debug.Assert(env.IsEnvironment("IntegrationTest"))` inside `TestAuthHandler.HandleAuthenticateAsync` so the handler refuses to run outside test.
- **Risk if left:** Test concerns leak into production startup. Each new test-only branch in `Program.cs` adds another env-string literal that has to stay in sync. Higher chance of an accidental misconfig where Firebase init silently no-ops in a real environment.

## Make local DB spin-up trivial

- **Where:** `Application/Frigorino.Web/ClientApp/package.json:15` (the misleadingly-named `sql` script), repo root (no docker-compose).
- **Why deferred:** Running the backend locally currently requires a Postgres connection string in `dotnet user-secrets`. Today's `npm run sql` script only starts **pgAdmin** (`dpage/pgadmin4`), not Postgres itself — so a fresh clone has no one-command way to bring up a working DB. We've been getting by with personal Railway/cloud connection strings, but that breaks any contributor (or AI agent) that doesn't have one.
- **Plan:** Add a `docker-compose.yml` at the repo root (or `Application/`) that spins up Postgres 16 + pgAdmin together, with default credentials matching what `appsettings.Development.json` could point at out of the box. Replace `npm run sql` with `npm run db` (or move it to a root-level script) that runs `docker compose up -d`. Update `appsettings.Development.json` (committed) to default to the local container's connection string, so user-secrets is only needed for cloud overrides.
- **Risk if left:** New contributors / agents can't start the backend without manual setup. Day-to-day this means swagger regen, migration testing, and integration smoke runs all require the original developer's machine.

## `useToggleListItemStatus` optimistic update doesn't recompute `sortOrder`

- **Where:** `Application/Frigorino.Web/ClientApp/src/hooks/useListItemQueries.ts:349-360` (the `onMutate` for the toggle hook).
- **Why deferred:** Surfaced while writing `Scenario: Toggling an item back to unchecked moves it below other unchecked items`. The backend (`List.ToggleItemStatus` → `ComputeAppendSortOrder`) is correct — toggling unchecks back to the bottom of the unchecked section. The optimistic update only flips `status` and leaves `sortOrder` untouched, so the UI shows the item in its previous slot until the debounced `onSettled` refetch arrives. Cosmetic-only flicker, and reproducing it under real users (not back-to-back automated toggles) was inconsistent — so we moved the assertion to the API feature instead of fixing the UI in the same change.
- **Plan:** Mirror the server's `ComputeAppendSortOrder` in `onMutate` the same way `useReorderListItem`'s optimistic update mirrors `List.ReorderItem`'s midpoint math. For unchecked→checked: `firstCheckedSortOrder - DEFAULT_GAP` (or `CHECKED_MIN + DEFAULT_GAP` if section empty). For checked→unchecked: `lastUncheckedSortOrder + DEFAULT_GAP` (or `UNCHECKED_MIN + DEFAULT_GAP` if section empty). Once fixed, the API-level `Toggling an item back to unchecked places it below other unchecked items` scenario can be duplicated as a UI scenario without flake.
- **Risk if left:** Users see a briefly-stale order on toggle, especially when un-checking. Subtle; easy to mistake for a backend bug. Also blocks adding UI-level reorder-on-toggle scenarios.

## Per-item action menu trapped inside dnd-kit's sortable container

- **Where:** `Application/Frigorino.Web/ClientApp/src/components/sortables/SortableListItem.tsx` — the `IconButton` + `Menu` sit inside `<SortableItem>`, whose root `<Box>` carries dnd-kit's `useSortable` attributes/listeners.
- **Why deferred:** Surfaced when writing the "User removes an item via the row menu" Playwright scenario — `ClickAsync` failed with `element is not enabled` because dnd-kit's ancestor aria attributes confuse Playwright's actionability check. Workaround in `ListItemSteps.cs:WhenIOpenTheItemMenuFor` / `WhenIClickDeleteFromTheItemMenu` uses `LocatorClickOptions { Force = true }`. Restructuring the SortableListItem DOM was out of scope for the test PR.
- **Plan:** Either (a) lift the action `<IconButton>` + `<Menu>` out of the `<SortableItem>` wrapper into a sibling slot in the parent `<SortableList>` row, so the menu lives outside the draggable ancestor; or (b) scope the dnd-kit listeners to a drag-handle child via `dragHandle="custom"` + `renderDragHandle` instead of spreading them on the root `<Box>` in the `none` branch. Option (b) is the smaller change. Drop `Force = true` from both step bindings once the workaround is no longer needed.
- **Risk if left:** Every new per-row interaction Playwright scenario inherits the `Force = true` workaround. Force-clicks skip Playwright's actionability checks, which is the protection against testing genuinely-broken UI (e.g. a button that really is disabled). The pattern is a smell that masks real regressions over time.

## `ScenarioContextHolder.ListItemIds` keyed by item text alone

- **Where:** `Application/Frigorino.IntegrationTests/Infrastructure/ScenarioContextHolder.cs:12` — `Dictionary<string, int> ListItemIds`.
- **Why deferred:** Today no scenario creates the same item text in two different lists within one scenario, so the collision is theoretical. Surfaced during the test-suite audit, not during a real failure.
- **Plan:** Change to `Dictionary<(string list, string text), int>` and update the two writes (`ListItemSteps.GivenThereIsAListNamedWithItem`, `ListItemSteps.GivenTheListAlsoHasItem`) plus the readers in `ListItemApiSteps.cs` to thread the list name. `ListIds` already keys by name and has the same risk — if/when scenarios start using two households per scenario, key it by `(householdName, listName)` too.
- **Risk if left:** First time a contributor writes a cross-list scenario that shares an item name, the id silently overwrites and the test fails on an unrelated `DELETE /items/{wrong-id}` 404 with no hint that the dictionary was the cause.

## Harden `SpaBuildHelper` against concurrency and opaque failures

- **Where:** `Application/Frigorino.IntegrationTests/Infrastructure/SpaBuildHelper.cs`.
- **Why deferred:** The env-var skip gate (`FRIGORINO_SKIP_SPA_BUILD=1`) is in place and used by `.github/workflows/ci.yml`, which is enough for the current CI lane. The remaining two hardenings only matter once we have parallel runs or hit a real build failure.
- **Plan:**
  - Add a coarse cross-process lock around the `npm run build` invocation (e.g. a file lock in `ClientApp/.spa-build.lock`) so parallel test runs in the same workspace don't race.
  - Surface stdout when the build fails (currently only stderr is captured) so root cause is visible without re-running.
- **Risk if left:** Concurrent local runs in the same workspace can corrupt `ClientApp/build`; opaque failures when `npm run build` errors mid-run.

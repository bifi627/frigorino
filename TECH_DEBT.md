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

## Add negative tests + domain unit tests for `Household.Create`

- **Where:** `Application/Frigorino.Domain/Entities/Household.cs:21` (factory), `Application/Frigorino.Features/Households/CreateHousehold.cs` (endpoint), `Application/Frigorino.Test/` (no coverage), `Application/Frigorino.IntegrationTests/Slices/Households/HouseholdSetup.feature` (only happy path).
- **Why deferred:** The vertical-slice migration prioritized parity with the old controller plus one happy-path scenario.
- **Plan:**
  - **Unit tests in `Frigorino.Test`:** cover `Household.Create` for empty/whitespace name, missing owner id, description trimming (whitespace-only → null), owner-membership seeding (one `UserHousehold` row with `Role = Owner`, `IsActive = true`).
  - **Reqnroll scenario:** add a "User submits an empty household name" flow that POSTs an empty `name` (via `TestApiClient` rather than the form, to bypass HTML5 validation) and asserts a 400 with a `name` validation error. This exercises `Result<T>.ToValidationProblem()` end-to-end.
- **Risk if left:** The whole point of moving to `Result<T>` is validation without exceptions, and that branch is currently unexercised. Regressions in `ResultExtensions.ToValidationProblem` (e.g. wrong metadata key, missing grouping) would ship undetected.

## Make `SpaBuildHelper` CI-friendly

- **Where:** `Application/Frigorino.IntegrationTests/Infrastructure/SpaBuildHelper.cs`.
- **Why deferred:** The current implementation was good enough for local runs and CI lanes that don't pre-build the SPA.
- **Plan:**
  - Gate the build by an env var: skip when `FRIGORINO_SKIP_SPA_BUILD=1` is set, so CI lanes that already produce `ClientApp/build` in a dedicated step can short-circuit.
  - Add a coarse cross-process lock around the `npm run build` invocation (e.g. a file lock in `ClientApp/.spa-build.lock`) so parallel test runs in the same workspace don't race.
  - Surface stdout when the build fails (currently only stderr is captured) so root cause is visible without re-running.
- **Risk if left:** Slow cold-start for any new CI lane; concurrent local runs in the same workspace can corrupt `ClientApp/build`; opaque failures when `npm run build` errors mid-run.

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

## Harden `SpaBuildHelper` against concurrency and opaque failures

- **Where:** `Application/Frigorino.IntegrationTests/Infrastructure/SpaBuildHelper.cs`.
- **Why deferred:** The env-var skip gate (`FRIGORINO_SKIP_SPA_BUILD=1`) is in place and used by `.github/workflows/ci.yml`, which is enough for the current CI lane. The remaining two hardenings only matter once we have parallel runs or hit a real build failure.
- **Plan:**
  - Add a coarse cross-process lock around the `npm run build` invocation (e.g. a file lock in `ClientApp/.spa-build.lock`) so parallel test runs in the same workspace don't race.
  - Surface stdout when the build fails (currently only stderr is captured) so root cause is visible without re-running.
- **Risk if left:** Concurrent local runs in the same workspace can corrupt `ClientApp/build`; opaque failures when `npm run build` errors mid-run.

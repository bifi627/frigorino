# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:

- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

- **@tanstack/react-router held at 1.128.8** — bumping to 1.170.7 breaks the household switcher.
- **Where:** `Application/Frigorino.Web/ClientApp/package.json` (`@tanstack/react-router`, `@tanstack/react-router-devtools`, `@tanstack/router-plugin`). Failing scenarios: `Application/Frigorino.IntegrationTests/Slices/CurrentHousehold/SwitchHousehold.feature` (3 scenarios) + 1 scenario in the active-household-persistence feature.
- **Why deferred:** the @tanstack/react-query 5.100.11 bump is the user-visible win; investigating a 42-minor router changelog was out of scope for the dependency-sweep PR.
- **Plan:** bisect router versions between 1.128.8 and 1.170.7 (start at 1.150, 1.140) against `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~SwitchHousehold"` to pin the breaking version. Failure signature: Playwright times out waiting for `GetByTestId("household-switcher-toggle")` after the post-create redirect to `/`, i.e. `useUserHouseholds` (in `HouseholdSwitcher.tsx`) stays `isLoading` and the toggle never renders. Likely culprits in that delta: loader-invalidation behavior on `navigate({ to: "/" })`, or router-driven QueryClient mounts. Once root-caused, bump all three router packages together (`react-router`, `react-router-devtools`, `router-plugin` versions must be aligned with the runtime).
- **Risk if left:** we miss security/perf fixes in 42+ minor releases; the longer we wait, the larger the bisect and the harder the eventual upgrade.

- **@hey-api/openapi-ts held at 0.97.2** — newer version blocked by `.npmrc` min-release-age=7 quarantine at the time of the dep-sweep.
- **Where:** `Application/Frigorino.Web/ClientApp/package.json` (`devDependencies`). 0.97.2 published 2026-05-18; eligible to bump from 2026-05-25.
- **Why deferred:** the dep-sweep wave that would have included it ran before the 7-day quarantine elapsed. Trying to bump with `--min-release-age=0` was rejected per policy (the override is reserved for CVE patches).
- **Plan:** check `npm view @hey-api/openapi-ts versions --json` for the latest after 2026-05-25; bump the `^x.y.z` base in `package.json` (it's a `^0.x` package, so any minor is technically a breaking change in semver); `npm install`; regenerate the client with `npm run api` from `ClientApp/`; sanity-check the generated TanStack-Query helpers under `src/lib/api/@tanstack/react-query.gen.ts` for shape changes. Verify with the full sln test (162 unit + 58 IT) — generated client diffs surface in IT.
- **Risk if left:** missing fixes/improvements in the codegen; the generated client under `src/lib/api/` drifts further from upstream patterns.

- **Railway Postgres 16 → 18 upgrade** — schedule a side-by-side dump/restore; no in-place path exists on Railway.
- **Where:** Railway Postgres service (managed; no repo change needed). Connection wiring: `ConnectionStrings__Database` env var on the `Frigorino.Web` Railway service, currently `${{Postgres.DATABASE_URL}}`. Researched 2026-05-23.
- **Why deferred:** PG16 is supported through Nov 2028; no immediate security/perf driver. Requires brief write downtime + a rollback window with the old service paused — wants explicit scheduling, not folded into an unrelated change. Stage cutover first per [[project_branch_workflow]].
- **Plan:** Side-by-side migration. (1) Add new service from Railway's [Deploy PostgreSQL 18](https://railway.com/deploy/postgresql-18-1) template; confirm volume size ≥ PG16's. (2) Scale `Frigorino.Web` to 0 replicas to quiesce API writes + Hangfire enqueues. (3) Run one-shot [railway-postgres-migration](https://railway.com/deploy/railway-postgres-migration) with `SOURCE_DATABASE_URL=${{Postgres.DATABASE_URL}}` + `TARGET_DATABASE_URL=${{Postgres18.DATABASE_URL}}` — streams `pg_dump --no-owner --no-privileges | psql`, no intermediate files (fine at current scale; alternative [postgres-migrator](https://railway.com/deploy/postgres-migrator) supports parallel `pg_restore` + row-count validation but its `PG_VERSION` build arg ships 15/16/17 client tools only — fork needed for v18). (4) Verify on PG18: row counts on `Users`/`Households`/`Lists`/`ListItems`/`Inventories`/`InventoryItems`/`UserHouseholds`/`__EFMigrationsHistory` and `SELECT schemaname, count(*) FROM pg_tables WHERE schemaname IN ('public','hangfire') GROUP BY 1`. (5) Repoint `ConnectionStrings__Database` to `${{Postgres18.DATABASE_URL}}`; Railway auto-redeploys; `MigrateAsync()` runs as no-op since `__EFMigrationsHistory` came over. (6) Smoke test golden flows. (7) Keep PG16 paused ≥24h for rollback (flip env back), then delete. PG18 breaking changes audited against codebase — none apply (no triggers / partitions / FTS / pg_trgm / raw COPY / MD5 auth; Npgsql 8 speaks SCRAM). Reference docs for the next session: [Railway PG overview](https://docs.railway.com/databases/postgresql), [PG18 release notes E.4](https://www.postgresql.org/docs/18/release-18.html), [PG18 pg_upgrade](https://www.postgresql.org/docs/current/pgupgrade.html), [Upgrading a Cluster 18.6](https://www.postgresql.org/docs/current/upgrading.html), [Station Q&A confirming no in-place path](https://station.railway.com/questions/upgrade-railway-postgres-v15-to-v16-5200d6dc), [PG18 release announcement](https://www.postgresql.org/about/news/postgresql-18-released-3142/).
- **Risk if left:** missing PG18 wins (native `uuidv7()`, async I/O, statistics-preserving `pg_upgrade` that would smooth the *next* bump). Drift accumulates — when PG16 EOL nears or PG19 ships, the jump grows and more breaking changes need re-auditing.

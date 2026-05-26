# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:

- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

- **@hey-api/openapi-ts held at 0.97.2** — newer version blocked by `.npmrc` min-release-age=7 quarantine at the time of the dep-sweep.
- **Where:** `Application/Frigorino.Web/ClientApp/package.json` (`devDependencies`). 0.97.2 published 2026-05-18; eligible to bump from 2026-05-25.
- **Why deferred:** the dep-sweep wave that would have included it ran before the 7-day quarantine elapsed. Trying to bump with `--min-release-age=0` was rejected per policy (the override is reserved for CVE patches).
- **Plan:** check `npm view @hey-api/openapi-ts versions --json` for the latest after 2026-05-25; bump the `^x.y.z` base in `package.json` (it's a `^0.x` package, so any minor is technically a breaking change in semver); `npm install`; regenerate the client with `npm run api` from `ClientApp/`; sanity-check the generated TanStack-Query helpers under `src/lib/api/@tanstack/react-query.gen.ts` for shape changes. Verify with the full sln test (162 unit + 58 IT) — generated client diffs surface in IT.
- **Risk if left:** missing fixes/improvements in the codegen; the generated client under `src/lib/api/` drifts further from upstream patterns.

- **`<div>`-in-`<p>` hydration warning on inventory items with an expiry date** — invalid DOM nesting in the item display.
- **Where:** `Application/Frigorino.Web/ClientApp/src/features/inventories/items/components/InventoryItemContent.tsx:73` — a `<Box>` (renders `<div>`) is passed as `ListItemText`'s `secondary` prop, which MUI wraps in a `<p>` (`Typography variant="body2"`). React 19 logs `In HTML, <div> cannot be a descendant of <p>. This will cause a hydration error.` Surfaces for every inventory item that renders the quantity/expiry secondary row.
- **Why deferred:** spotted during manual verification of the composer-input-redesign branch, but it lives in the item *display* component (not the composer/footer that branch touched), so fixing it there was out of scope. The same class of bug in the composer's autocomplete option *was* fixed on that branch (`ComposerTextField.tsx`).
- **Plan:** give `ListItemText` a `slotProps={{ secondary: { component: "div" } }}` (MUI v9) so the secondary wrapper is a `<div>`, or pass a plain `<span>`/`component="div"` element as `secondary` instead of a `<Box>`. Confirm in-browser that the console no longer logs the nesting error when an item has quantity/expiry.
- **Risk if left:** CSR-only today so it renders (browsers auto-close the `<p>`), but it's invalid HTML, spams the dev console, and would become a real hydration mismatch if any SSR/prerender is ever introduced.

- **Expiry-date off-by-one in negative/positive UTC offsets** — local↔UTC round-trip in the composer's expiry feature.
- **Where:** `Application/Frigorino.Web/ClientApp/src/components/composer/features/expiryFeature.tsx` — `formatForInput` uses `date.toISOString().split("T")[0]` (UTC) while `setToday` sets `new Date()` (local) and `handleChange` parses `new Date("YYYY-MM-DD")` (UTC midnight). Carried over verbatim from the old `DateInputPanel`, not introduced by the redesign.
- **Why deferred:** pre-existing behavior; the redesign preserved it to avoid scope creep, and the dev/test timezone didn't expose it.
- **Plan:** format/parse the date in local time consistently — e.g. build the input string from `getFullYear()/getMonth()/getDate()` (zero-padded) instead of `toISOString()`, and parse `YYYY-MM-DD` into a local `Date(y, m-1, d)`. Add a quick check in a non-UTC timezone (set `TZ`/browser tz to e.g. `Pacific/Auckland`) that "today" round-trips to the same calendar day on save.
- **Risk if left:** users in non-UTC timezones can see the expiry input show, and persist, a day before/after the one they picked.

- **Railway Postgres 16 → 18 upgrade** — schedule a side-by-side dump/restore; no in-place path exists on Railway.
- **Where:** Railway Postgres service (managed; no repo change needed). Connection wiring: `ConnectionStrings__Database` env var on the `Frigorino.Web` Railway service, currently `${{Postgres.DATABASE_URL}}`. Researched 2026-05-23.
- **Why deferred:** PG16 is supported through Nov 2028; no immediate security/perf driver. Requires brief write downtime + a rollback window with the old service paused — wants explicit scheduling, not folded into an unrelated change. Stage cutover first per [[project_branch_workflow]].
- **Plan:** Side-by-side migration. (1) Add new service from Railway's [Deploy PostgreSQL 18](https://railway.com/deploy/postgresql-18-1) template; confirm volume size ≥ PG16's. (2) Scale `Frigorino.Web` to 0 replicas to quiesce API writes. (3) Run one-shot [railway-postgres-migration](https://railway.com/deploy/railway-postgres-migration) with `SOURCE_DATABASE_URL=${{Postgres.DATABASE_URL}}` + `TARGET_DATABASE_URL=${{Postgres18.DATABASE_URL}}` — streams `pg_dump --no-owner --no-privileges | psql`, no intermediate files (fine at current scale; alternative [postgres-migrator](https://railway.com/deploy/postgres-migrator) supports parallel `pg_restore` + row-count validation but its `PG_VERSION` build arg ships 15/16/17 client tools only — fork needed for v18). (4) Verify on PG18: row counts on `Users`/`Households`/`Lists`/`ListItems`/`Inventories`/`InventoryItems`/`UserHouseholds`/`__EFMigrationsHistory` and `SELECT schemaname, count(*) FROM pg_tables WHERE schemaname IN ('public','hangfire') GROUP BY 1`. (5) Repoint `ConnectionStrings__Database` to `${{Postgres18.DATABASE_URL}}`; Railway auto-redeploys; `MigrateAsync()` runs as no-op since `__EFMigrationsHistory` came over. (6) Smoke test golden flows. (7) Keep PG16 paused ≥24h for rollback (flip env back), then delete. PG18 breaking changes audited against codebase — none apply (no triggers / partitions / FTS / pg_trgm / raw COPY / MD5 auth; Npgsql 8 speaks SCRAM). Reference docs for the next session: [Railway PG overview](https://docs.railway.com/databases/postgresql), [PG18 release notes E.4](https://www.postgresql.org/docs/18/release-18.html), [PG18 pg_upgrade](https://www.postgresql.org/docs/current/pgupgrade.html), [Upgrading a Cluster 18.6](https://www.postgresql.org/docs/current/upgrading.html), [Station Q&A confirming no in-place path](https://station.railway.com/questions/upgrade-railway-postgres-v15-to-v16-5200d6dc), [PG18 release announcement](https://www.postgresql.org/about/news/postgresql-18-released-3142/).
- **Risk if left:** missing PG18 wins (native `uuidv7()`, async I/O, statistics-preserving `pg_upgrade` that would smooth the *next* bump). Drift accumulates — when PG16 EOL nears or PG19 ships, the jump grows and more breaking changes need re-auditing.

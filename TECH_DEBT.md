# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:

- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

## - **Railway Postgres 16 → 18 upgrade** — schedule a side-by-side dump/restore; no in-place path exists on Railway.
- **Where:** Railway Postgres service (managed; no repo change needed). Connection wiring: `ConnectionStrings__Database` env var on the `Frigorino.Web` Railway service, currently `${{Postgres.DATABASE_URL}}`. Researched 2026-05-23.
- **Why deferred:** PG16 is supported through Nov 2028; no immediate security/perf driver. Requires brief write downtime + a rollback window with the old service paused — wants explicit scheduling, not folded into an unrelated change. Stage cutover first per [[project_branch_workflow]].
- **Plan:** Side-by-side migration. (1) Add new service from Railway's [Deploy PostgreSQL 18](https://railway.com/deploy/postgresql-18-1) template; confirm volume size ≥ PG16's. (2) Scale `Frigorino.Web` to 0 replicas to quiesce API writes. (3) Run one-shot [railway-postgres-migration](https://railway.com/deploy/railway-postgres-migration) with `SOURCE_DATABASE_URL=${{Postgres.DATABASE_URL}}` + `TARGET_DATABASE_URL=${{Postgres18.DATABASE_URL}}` — streams `pg_dump --no-owner --no-privileges | psql`, no intermediate files (fine at current scale; alternative [postgres-migrator](https://railway.com/deploy/postgres-migrator) supports parallel `pg_restore` + row-count validation but its `PG_VERSION` build arg ships 15/16/17 client tools only — fork needed for v18). (4) Verify on PG18: row counts on `Users`/`Households`/`Lists`/`ListItems`/`Inventories`/`InventoryItems`/`UserHouseholds`/`__EFMigrationsHistory` and `SELECT schemaname, count(*) FROM pg_tables WHERE schemaname IN ('public','hangfire') GROUP BY 1`. (5) Repoint `ConnectionStrings__Database` to `${{Postgres18.DATABASE_URL}}`; Railway auto-redeploys; `MigrateAsync()` runs as no-op since `__EFMigrationsHistory` came over. (6) Smoke test golden flows. (7) Keep PG16 paused ≥24h for rollback (flip env back), then delete. PG18 breaking changes audited against codebase — none apply (no triggers / partitions / FTS / pg_trgm / raw COPY / MD5 auth; Npgsql 8 speaks SCRAM). Reference docs for the next session: [Railway PG overview](https://docs.railway.com/databases/postgresql), [PG18 release notes E.4](https://www.postgresql.org/docs/18/release-18.html), [PG18 pg_upgrade](https://www.postgresql.org/docs/current/pgupgrade.html), [Upgrading a Cluster 18.6](https://www.postgresql.org/docs/current/upgrading.html), [Station Q&A confirming no in-place path](https://station.railway.com/questions/upgrade-railway-postgres-v15-to-v16-5200d6dc), [PG18 release announcement](https://www.postgresql.org/about/news/postgresql-18-released-3142/).
- **Risk if left:** missing PG18 wins (native `uuidv7()`, async I/O, statistics-preserving `pg_upgrade` that would smooth the *next* bump). Drift accumulates — when PG16 EOL nears or PG19 ships, the jump grows and more breaking changes need re-auditing.

## - **List-level extraction poll instead of single-item poll** — removes the temp-id reconciliation and the "only the last add polls" gap in one move.
- **Where:** `Application/Frigorino.Web/ClientApp/src/features/lists/items/useExtractionPoll.ts`, `useCreateListItem.ts` (the `tempId = Date.now()` → real-id swap in `onSuccess`), `features/lists/pages/ListViewPage.tsx` (`pendingExtraction` is a single slot; rapid successive adds overwrite it so earlier rows show stale raw text until a debounced refetch). Surfaced in the 6-hat review (Black + Green hats).
- **Why deferred:** the single-item poll + optimistic temp-id reconciliation works correctly for the common one-at-a-time add; the multi-add gap is acknowledged "v1" behavior, not a regression.
- **Plan:** drop the per-item `getItem` poll; instead `refetchInterval` the existing `getItems` list query for a bounded window after any digit-bearing add, comparing each row's `quantity` to detect arrival. The indicator is already rendered per-row from list data. This makes `pendingExtraction` a set (or just "poll the list while any add is in flight"), and deletes the `tempId` swap entirely. Trade-off: refetches the whole (small) list vs one item.
- **Risk if left:** after rapid multi-item adds, earlier extracted quantities don't reflect until the ~1s debounced invalidation (which keeps resetting if the user keeps typing); the temp-id `Date.now()` is also a latent duplicate-key risk on same-millisecond adds.

## - **Quantity processing-pulse color hardcoded instead of theme-sourced.**
- **Where:** `Application/Frigorino.Web/ClientApp/src/components/sortables/SortableListItem.tsx` (`processingPulse` keyframe uses literal `rgba(25, 118, 210, …)` while the static border uses `borderColor: "primary.main"`). Surfaced in the 6-hat review (Black/Red minor). Also consider general theming and styling approachs in the app, how can we improve?
- **Why deferred:** cosmetic; matches the existing orange edit-pulse which also hardcodes its color, so it's consistent with current code.
- **Plan:** read the primary color from the theme (e.g. `alpha(theme.palette.primary.main, x)` in an `sx` callback) for both the border and the pulse so a theme palette change can't make them diverge. Aligns with `knowledge/Frontend_Styling.md` "theme owns the palette."
- **Risk if left:** if `theme.ts` primary changes, the pulse glow and the solid border drift to different blues.

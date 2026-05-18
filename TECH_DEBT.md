# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:
- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

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

## Audit + split the main JS chunk

- **Where:** `Application/Frigorino.Web/ClientApp/vite.config.ts` (no `build.rollupOptions.output.manualChunks` configured today). Main output `build/assets/index-*.js` is ~951 kB / gzip ~302 kB after Faro landed; Vite emits the "chunk larger than 500 kB" warning on every build.
- **Why deferred:** Surfaced after the Faro Web SDK + `TracingInstrumentation` added ~170 kB to the main chunk during the observability rollout. The warning has been there longer than Faro — Faro just made it louder. A proper split needs a deliberate look at what's actually in the bundle, not a one-off `manualChunks` guess while wrapping up an unrelated PR.
- **Plan:**
  - Run `npx vite-bundle-visualizer` (or `rollup-plugin-visualizer`) once and capture the treemap. Expected heavy hitters: `@mui/material` + `@mui/icons-material`, `firebase/*`, `@grafana/faro-*`, `@tanstack/react-*`, `@dnd-kit/*`.
  - Pick a split axis:
    - **Vendor split** via `manualChunks: { mui: [...], firebase: [...], faro: [...], tanstack: [...] }` — predictable; caches well across our own releases since vendor chunks rarely change.
    - **Route-level split** — TanStack Router has `autoCodeSplitting: true` in `vite.config.ts`; verify per-route chunks actually exist for the `_protected` tree and aren't getting hoisted into the entry.
  - Faro specifically is a strong dynamic-import candidate: nothing in the auth or render path depends on it, and `initObservability()` could become `void import("./common/observability").then(m => m.initObservability())` after first paint without losing data (Faro buffers internally before init).
- **Risk if left:** First-paint cost for new visitors stays inflated, especially on mobile (~302 kB gzip ≈ multiple seconds on slow 3G before any JS runs). PWA service worker caches it after the first visit so returning users are fine — but the wake-from-Railway-sleep + cold-cache combination is the worst case, which is exactly when a UAT client first opens the app.

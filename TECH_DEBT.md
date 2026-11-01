# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:

- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

- **Recipe URL import: two `SaveChangesAsync` aren't transactional** — a mid-import DB failure can leave a phantom empty recipe.
- **Where:** `Application/Frigorino.Features/Recipes/ImportRecipe.cs` (the two `await db.SaveChangesAsync(ct)` calls — first persists recipe+section for real FK ids, second persists items+link).
- **Why deferred:** Surfaced by the final whole-branch review of the URL-import MVP; user chose to ship the baseline and iterate. Genuinely rare (needs a transient DB failure in the window between the two saves; the second save can't fail on validation — all data is pre-validated by the aggregate).
- **Plan:** Wrap both saves in `await using var tx = await db.Database.BeginTransactionAsync(ct); … await tx.CommitAsync(ct);`. The two-phase save is structurally required (item/link FKs need the recipe+section ids), so keep both saves — just make them atomic. Add an IT that forces the second save to fail and asserts no recipe row remains.
- **Risk if left:** On a transient DB error between the saves the endpoint returns 500 and leaves a recipe with its default section but no items/link. Low blast radius (a deletable empty recipe), but `stage` hosts a real client.

- **npm audit: 4 low `deepmerge` advisories via the PWA build chain** — `vite-plugin-pwa` → `workbox-build` → `@rollup/plugin-node-resolve` → `deepmerge` (GHSA-gcqx-w48m-w2h4, prototype poisoning in `mergeObject()`).
- **Where:** `Application/Frigorino.Web/ClientApp/package.json` (`vite-plugin-pwa`), transitive only.
- **Why deferred:** Advisory covers `deepmerge >=4.2.0` (no patched release). Latest `workbox-build` is 7.4.1 and `vite-plugin-pwa` 2.0.0 still depends on it. `npm audit fix --force` "fixes" it by downgrading to `vite-plugin-pwa@0.2.1` — not acceptable. Build-time only: the chain runs inside `vite build`, never in the shipped bundle or the push-only SW.
- **Plan:** Re-run `npm audit` on each deps-bump wave; clears once `workbox-build` (or `@rollup/plugin-node-resolve`) drops/patches `deepmerge`. Alternative if it lingers: our SW is push-only with `injectionPoint: undefined`, so `vite-plugin-pwa` mostly emits the manifest + compiles `sw.ts` — replacing it with a static `manifest.webmanifest` + a small Vite build entry for `sw.ts` would drop workbox entirely.
- **Risk if left:** Effectively none at runtime; noise in `npm audit` output, and a malicious build-config input would be needed to exploit it.

- **FullCalendar 6 → 7 migration deferred** — held on `@fullcalendar/*` `^6.1.x` (v6 still published under the `legacy` dist-tag).
- **Where:** `Application/Frigorino.Web/ClientApp/package.json` (`@fullcalendar/core`, `daygrid`, `react`), `src/features/inventories/calendar/pages/ExpiryCalendarPage.tsx` (only consumer), `src/features/inventories/calendar/expiryCalendar.css`.
- **Why deferred:** v7 is a re-theming migration, not a bump, and brings nothing we need. Skipped in the 2026-11 deps-bump wave.
- **Plan:** Per the official v6→v7 upgrade guides (JS + CSS):
  - Drop `@fullcalendar/core` + `@fullcalendar/daygrid`; bump `@fullcalendar/react` to 7.x; add the new `temporal-polyfill` peer dep.
  - Imports: `dayGridPlugin` from `@fullcalendar/react/daygrid`; `EventClickArg`/`EventContentArg` → `*Info` types from `@fullcalendar/react`; re-check the `de` locale import path.
  - CSS is no longer bundled: add a theme plugin (`themes/classic`) + import `skeleton.css`, `theme.css`, `palette.css`.
  - Re-bridge the MUI dark-theme vars on the wrapper `sx`: `--fc-border-color` → `--fc-classic-border`, `--fc-today-bg-color` → `--fc-classic-today` (check replacements for `--fc-page-bg-color` / `--fc-neutral-bg-color`).
  - `eventClassNames` (array) → `eventClass` (string); `eventContent` now renders into the inner container (dot handling via `listItemEventBeforeClass`).
  - Re-verify every selector in `expiryCalendar.css` (`.fc-daygrid-event-harness`, `.fc-event`, `.fc-button`) against v7 markup.
  - ITs (`ExpiryCalendarSteps.cs`) use testids only → catch behavior, not visuals; needs a manual light/dark visual check of the calendar.
- **Risk if left:** None short-term; v6 drifts toward unmaintained over time (watch for React 20 peer support / security fixes landing only in 7.x).

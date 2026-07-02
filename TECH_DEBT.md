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

- **Debounced-invalidation singleton: any unmount cancels ALL pending invalidations** — each consumer's cleanup calls `queryDebouncer.clear()` on the shared module-level instance.
- **Where:** `Application/Frigorino.Web/ClientApp/src/hooks/useDebouncedInvalidation.ts:56-60` (unmount effect), `:30-33` (`clear()`), `:37` (singleton).
- **Why deferred:** Found in the 2026-07-01 read-only audit (read-only session, no fixes applied). Timing-dependent and self-healing (next interaction or staleTime refetches).
- **Plan:** Delete the unmount `useEffect` and the now-unused `clear()` method. Pending invalidations belong to mutations that already committed; `invalidateQueries` on the app-lifetime queryClient is safe after any component unmounts. No per-instance tracking needed.
- **Risk if left:** Check off a list item → another hook consumer unmounts (sheet closes, StrictMode dev double-mount) within the ~1s debounce window → the reconciling refetch is silently dropped; UI shows stale optimistic rank/quantity/promote state until the next interaction.

- **Dead `/signalr` query-string bearer-token handler** — `OnMessageReceived` accepts the JWT from `?access_token=` for `/signalr` paths; no SignalR exists in the repo.
- **Where:** `Application/Frigorino.Infrastructure/Auth/FirebaseAuth.cs:43-56`.
- **Why deferred:** Found in the 2026-07-01 read-only audit. Unreachable today, not an auth bypass (token still fully validated).
- **Plan:** Delete the whole `OnMessageReceived` handler, keep `OnTokenValidated`.
- **Risk if left:** Tokens-in-URLs leak into logs/Referer if a `/signalr` route ever appears; loaded trap for whoever adds one.

- **Legacy `AuthController` writes `User` rows outside every domain rule** — `/api/Auth/me` lazily creates `new User { Name = "New User" }` bypassing the factory + `UserSync`; `/api/Auth/profile` sets `Name` with zero validation. SPA never calls either.
- **Where:** `Application/Frigorino.Web/Controllers/AuthController.cs:38-44,67`.
- **Why deferred:** Found in the 2026-07-01 read-only audit; deletion changes the OpenAPI spec so it needs a client regen, not a drive-by edit.
- **Plan:** Delete `AuthController.cs` (grep confirms no SPA consumer outside generated code), run `npm run api` to regenerate the committed client. Demo/WeatherForecast scaffold stays.
- **Risk if left:** Live unvalidated write surface duplicating `UserSync`; self-scoped so no IDOR, but violates the writes-go-through-the-domain invariant.

- **`DeleteInactiveItems`: SortBlueprint omission is deliberate but undocumented** — soft-deleted blueprint rows are tombstones (`GetBlueprints.cs:39` re-seeds the default only when NO row exists); nothing says so at the purge site.
- **Where:** `Application/Frigorino.Infrastructure/Tasks/DeleteInactiveItems.cs` (top of `Run`).
- **Why deferred:** Doc-only gap found in the 2026-07-01 read-only audit.
- **Plan:** One comment above the first `ExecuteDeleteAsync`: deliberately no SortBlueprint clause — tombstones prevent default re-seed; do not "complete the contract".
- **Risk if left:** A future maintainer completing the per-entity purge contract (a saved convention) purges the tombstones and resurrects deleted default blueprints.

- **Recipe-import SSRF guard: two reserved IPv4 ranges + no port restriction** — `IsPublicIpAddress` checks only exact `0.0.0.0`, not `0.0.0.0/8`, and allows `240.0.0.0/4`; public hosts reachable on any port.
- **Where:** `Application/Frigorino.Infrastructure/Services/RecipeImportUrl.cs:43-67`.
- **Why deferred:** Marginal hardening found in the 2026-07-01 read-only audit — the guard is otherwise strong (per-hop DNS revalidation, private/CGNAT/metadata/ULA blocks, redirect cap, size/time caps); these ranges aren't routable to internal infra.
- **Plan:** In the IPv4 branch add `if (b[0] == 0 || b[0] >= 240) return false;`; optionally restrict destination ports to 80/443 where the import URL is validated.
- **Risk if left:** Port-scanning of already-public IPs via import URLs; no internal reach.

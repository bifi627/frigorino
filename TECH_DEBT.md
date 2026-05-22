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

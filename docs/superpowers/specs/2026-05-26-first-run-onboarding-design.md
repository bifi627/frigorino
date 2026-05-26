# First-run onboarding: dedicated "create your first household" page

**Date:** 2026-05-26
**Status:** Design approved, ready for planning

## Problem

A user who signs in for the first time has zero households. Today they land at `/`
(the `WelcomePage` dashboard), which renders a "No Household" switcher and disabled
lists/inventories — an awkward empty state full of meaningless navigation. New users
need a single, obvious next step ("create a household"). This becomes load-bearing
once real users are onboarded beyond the dev/stage tester pool.

## Corrections to the original IDEAS sketch

The IDEAS.md sketch assumed infrastructure that does not exist. Verified against the code:

- **There is no `_protected.tsx` layout route.** Auth is enforced per-route via
  `requireAuth` in each route's `beforeLoad` (`src/common/authGuard.ts`). Routes are
  flat under `src/routes/`.
- **Active household is server-side state**, not a Zustand store. `useCurrentHousehold`
  reads it from the backend; `useSetCurrentHousehold` persists it. There is no client
  store to "set the active household" in.
- **The submit flow already exists.** `CreateHouseholdForm` already does
  create → set-active → navigate to `/`. Onboarding reuses it as-is.

## Decisions

1. **Guard scope: the entry point `/` only.** A brand-new user always lands at `/`
   after login, so guarding the dashboard route covers the real first-run path with a
   single-file change. Deep-links to `/lists` etc. with zero households are left as
   today's graceful empty state (rare, degrades no worse than current behavior).
2. **Dedicated `/onboarding` route**, reusing `CreateHouseholdForm`. `/household/create`
   stays unchanged for adding additional households later.
3. **Skip is allowed.** A "Skip for now" action lets the user into the empty dashboard.
   Because the app is unusable with zero households, "skip into the app" would otherwise
   bounce straight back via the guard — so the skip is **remembered for the session**
   and the guard honors it.

## Design

### 1. The guard (`src/routes/index.tsx`)

The check lives in the `Index` component, matching the file's existing pattern
(it already branches on auth-loading in the component, not in a loader).

```
auth loading            -> spinner (existing)
households loading      -> spinner
loaded & empty & !skip  -> <Navigate to="/onboarding" />
otherwise               -> <WelcomePage />
```

- Uses `useUserHouseholds()` for the empty signal (`[]` after auth resolves).
- Reads the skip flag (see §3).
- No `beforeLoad` data fetching, no new layout route, no other route files touched.

*Alternative considered and rejected:* a `beforeLoad` + `queryClient.ensureQueryData`
guard avoids a brief loading spinner but introduces loader-style fetching the codebase
does not use elsewhere. Not worth it for v1.

### 2. The `/onboarding` route + page

- **`src/routes/onboarding.tsx`** — thin shell: `createFileRoute("/onboarding")`,
  `beforeLoad: requireAuth`, imports the page component. (`routeTree.gen.ts`
  regenerates automatically.)
- **`src/features/households/pages/OnboardingPage.tsx`** — new component:
  - **Self-guard:** if `useUserHouseholds()` returns a non-empty list, `<Navigate to="/" />`
    (so an existing user can't get stuck on a dead onboarding page).
  - Friendly welcome heading + subtext (i18n, §5).
  - Reuses `<CreateHouseholdForm />` unchanged — it already creates the household, sets
    it active, and navigates to `/`. Once a household exists, the `/` guard no longer
    redirects, so the user lands on the populated dashboard.
  - A **"Skip for now"** button (`data-testid="onboarding-skip-button"`) that sets the
    skip flag and navigates to `/`.

### 3. Skip persistence

- Stored in **`sessionStorage`**, key `frigorino-onboarding-skipped`. Survives reloads
  within the tab, clears when the tab/session closes — matching "remember for the
  session." (`WelcomePage` already uses web storage for UI state, so this is consistent.)
- A tiny helper (e.g. `src/features/households/onboardingSkip.ts`) exposes
  `getOnboardingSkipped()` / `setOnboardingSkipped()` so the guard and the page share
  one source of truth.
- Once the user has a household the flag is moot (guard never fires). No cleanup needed.

### 4. Navigation bar suppression (`src/routes/__root.tsx`)

The root renders `<Navigation />` for any authenticated route. Extend the condition so
the bar is hidden on `/onboarding`, keeping it a clean single-purpose view:

```
showNavigation = (isAuthenticated || pathname.startsWith("/auth"))
                 && !pathname.startsWith("/onboarding")
```

### 5. Copy + i18n

New `onboarding` namespace in `public/locales/{en,de}/translation.json`:
- `onboarding.title` — welcome heading
- `onboarding.subtitle` — short friendly subtext ("Create your first household to get
  started"). **Silent on invites for v1** — does not promise an invite-acceptance flow
  that doesn't exist yet.
- `onboarding.skip` — "Skip for now"

The form's own labels/button reuse the existing `household.*` keys. Tests never assert
on translated text.

### 6. Edge cases

- **Removed from last household mid-session:** user previously had households (no skip
  flag set), now has zero. Next navigation to `/` → guard redirects to `/onboarding`.
  Matches the sketch's "flow re-engages."
- **Existing user opens `/onboarding` directly:** self-guard redirects to `/`.
- **Skip then reload:** flag persists in `sessionStorage`, dashboard stays, no bounce.

## Testing

No backend changes → no .NET unit tests. There is no frontend unit runner.

**Regression hedge: one Playwright/Reqnroll integration test** under
`Application/Frigorino.IntegrationTests/Slices/Onboarding/`
(`Onboarding.feature` + `OnboardingSteps.cs`). Each scenario boots a fresh DB with
**zero households**, so the onboarding path is reached with no seeding. Reuse existing
shared steps (`I am logged in as`, `I navigate to`, `I fill in the household name`,
`I submit the household form`, `I am redirected to`, `I reload the page`); add a new
`I skip onboarding` step (clicks `onboarding-skip-button`). All locators by
`data-testid`, all assertions via retrying `Assertions.Expect(...)`.

Scenarios:
1. **Zero-household user is redirected to onboarding** — log in, navigate to `/`,
   assert redirected to `/onboarding`.
2. **Creating the first household lands on the dashboard** — navigate to `/onboarding`,
   fill name, submit, assert redirected to `/`.
3. **Skipping enters the empty dashboard without bouncing back** — navigate to
   `/onboarding`, skip, assert at `/`; reload, assert still at `/` (not `/onboarding`).

Manual verification via the dev stack + Playwright MCP for the visual/UX pass
(welcome copy renders, no nav bar on onboarding).

## Scope / cost

- `routes/index.tsx` guard (~10 LOC), `routes/onboarding.tsx` (~8 LOC),
  `OnboardingPage.tsx` (~50 LOC), `onboardingSkip.ts` helper (~6 LOC),
  `__root.tsx` one-line change, i18n keys (en + de).
- One integration feature + step file.
- No backend changes. Matches the IDEAS half-day estimate.

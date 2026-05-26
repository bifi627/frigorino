# First-run Onboarding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Send a freshly-signed-in user with zero households to a dedicated `/onboarding` page (with a session-remembered "Skip for now") instead of the broken empty dashboard.

**Architecture:** A guard in the existing `/` route component (`routes/index.tsx`) redirects to `/onboarding` when `useUserHouseholds()` resolves empty and the session has not skipped. `/onboarding` is a new route reusing the existing `CreateHouseholdForm` (which already does create → set-active → navigate `/`) wrapped in welcome copy plus a skip button. The skip flag lives in `sessionStorage`. The root layout hides the nav bar on `/onboarding`. One Reqnroll/Playwright integration test hedges against regression. No backend changes.

**Tech Stack:** React 19, TanStack Router (file-based) + TanStack Query, MUI, i18next (en + de), Reqnroll + Playwright + Postgres Testcontainers for the e2e test.

---

## Context for the implementer

- The frontend lives under `Application/Frigorino.Web/ClientApp/`. Run all `npm` commands from there.
- TanStack Router is **file-based**. Adding `src/routes/onboarding.tsx` makes the `@tanstack/router-plugin` regenerate `src/routes/routeTree.gen.ts` during `npm run dev`/`npm run build` — **never edit that file by hand**. `tsc` will not know about `to: "/onboarding"` until the tree is regenerated, so the route task runs `npm run build` (which regenerates the tree) before later tasks reference the route.
- `useUserHouseholds(enabled = true)` (`src/features/households/useUserHouseholds.ts`) returns a TanStack Query result whose `data` is the array of the user's households. Empty array = zero households.
- `CreateHouseholdForm` (`src/features/households/components/CreateHouseholdForm.tsx`) already: creates the household, calls `useSetCurrentHousehold`, then `navigate({ to: "/" })`. Reuse it unchanged. Its submit button already has `data-testid="household-create-submit-button"`.
- Integration tests boot a **fresh DB with zero households per scenario**, so the onboarding path is reached with no seeding. Locate elements by `data-testid` only; assert with retrying `Assertions.Expect(...)`. Reusable shared steps already exist: `I am logged in as`, `I navigate to`, `I fill in the household name`, `I submit the household form`, `I am redirected to`, `I reload the page`, `the active household should be`.

## File structure

- Create: `src/features/households/onboardingSkip.ts` — session skip flag (get/set).
- Create: `src/features/households/pages/OnboardingPage.tsx` — the onboarding view.
- Create: `src/routes/onboarding.tsx` — thin route shell (`requireAuth` + page).
- Modify: `src/routes/index.tsx` — zero-household guard.
- Modify: `src/routes/__root.tsx` — hide nav bar on `/onboarding`.
- Modify: `public/locales/en/translation.json`, `public/locales/de/translation.json` — `onboarding` copy.
- Auto-regenerated: `src/routes/routeTree.gen.ts` (commit it; do not edit by hand).
- Create: `Application/Frigorino.IntegrationTests/Slices/Onboarding/Onboarding.feature` and `OnboardingSteps.cs` — regression test.

---

## Task 1: Add onboarding i18n copy (en + de)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add the `onboarding` block to the English file**

In `public/locales/en/translation.json`, insert a new top-level `onboarding` object immediately before the `"household": {` block. Replace:

```json
    "household": {
        "title": "Household",
```

with:

```json
    "onboarding": {
        "title": "Welcome to Frigorino",
        "subtitle": "Create your first household to get started.",
        "skip": "Skip for now"
    },
    "household": {
        "title": "Household",
```

- [ ] **Step 2: Add the `onboarding` block to the German file**

In `public/locales/de/translation.json`, insert the same key before the `"household": {` block. Replace:

```json
    "household": {
        "title": "Haushalt",
```

with:

```json
    "onboarding": {
        "title": "Willkommen bei Frigorino",
        "subtitle": "Erstellen Sie Ihren ersten Haushalt, um loszulegen.",
        "skip": "Vorerst überspringen"
    },
    "household": {
        "title": "Haushalt",
```

- [ ] **Step 3: Verify both files are valid JSON**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
node -e "JSON.parse(require('fs').readFileSync('public/locales/en/translation.json','utf8')); JSON.parse(require('fs').readFileSync('public/locales/de/translation.json','utf8')); console.log('OK')"
```
Expected: prints `OK` (no parse error).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat: add onboarding i18n strings (en + de)"
```

---

## Task 2: Write the failing integration test

This is the regression hedge. Written first so we can watch it go red, then green.

**Files:**
- Create: `Application/Frigorino.IntegrationTests/Slices/Onboarding/Onboarding.feature`
- Create: `Application/Frigorino.IntegrationTests/Slices/Onboarding/OnboardingSteps.cs`

- [ ] **Step 1: Write the feature file**

Create `Application/Frigorino.IntegrationTests/Slices/Onboarding/Onboarding.feature`:

```gherkin
Feature: First-run onboarding

  Scenario: A user with no households is sent to onboarding
    Given I am logged in as "newcomer"
    When I navigate to "/"
    Then I am redirected to "/onboarding"

  Scenario: Creating the first household from onboarding opens the dashboard
    Given I am logged in as "newcomer"
    When I navigate to "/onboarding"
    And I fill in the household name "My Home"
    And I submit the household form
    Then I am redirected to "/"
    And the active household should be "My Home"

  Scenario: Skipping onboarding enters the dashboard without bouncing back
    Given I am logged in as "newcomer"
    When I navigate to "/onboarding"
    And I skip onboarding
    Then I am redirected to "/"
    When I reload the page
    Then the onboarding page is not shown
```

- [ ] **Step 2: Write the two new step definitions**

Create `Application/Frigorino.IntegrationTests/Slices/Onboarding/OnboardingSteps.cs`. (Reqnroll bindings are global, so `I am logged in as`, `I navigate to`, `I fill in the household name`, `I submit the household form`, `I am redirected to`, `I reload the page`, and `the active household should be` are reused from the existing step classes — only the two onboarding-specific steps are new. `Reqnroll` and `Microsoft.Playwright` are covered by `GlobalUsings.cs`, so no extra `using` directives are needed.)

```csharp
namespace Frigorino.IntegrationTests.Slices.Onboarding;

[Binding]
public class OnboardingSteps(ScenarioContextHolder ctx)
{
    [When("I skip onboarding")]
    public async Task WhenISkipOnboarding()
    {
        await ctx.Page.GetByTestId("onboarding-skip-button").ClickAsync();
        await ctx.Page.WaitForURLAsync("**/");
    }

    [Then("the onboarding page is not shown")]
    public async Task ThenTheOnboardingPageIsNotShown()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("onboarding-skip-button"))
            .Not.ToBeVisibleAsync();
    }
}
```

- [ ] **Step 3: Build the SPA so the test runs against current assets, then run the test to watch it FAIL**

Build the SPA (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run build
```
Expected: build succeeds (the `/onboarding` route does not exist yet, so the served app has no onboarding behavior).

Run the test (from repo root). Confirm the docker daemon is running first; if Testcontainers reports the daemon is unreachable, ask the user to start Docker Desktop.
```bash
dotnet test Application/Frigorino.IntegrationTests/ --filter "FullyQualifiedName~Onboarding" 2>&1 | tee /tmp/onboarding-red.log; echo "EXIT=${PIPESTATUS[0]}"
```
Expected: `EXIT=1` (FAIL). Scenario 1 fails because navigating to `/` with zero households renders the dashboard, never `/onboarding`, so `Then I am redirected to "/onboarding"` times out.

- [ ] **Step 4: Commit the failing test**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Onboarding/Onboarding.feature Application/Frigorino.IntegrationTests/Slices/Onboarding/OnboardingSteps.cs
git commit -m "test: add failing first-run onboarding integration test"
```

---

## Task 3: Skip helper, onboarding page, and route

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/households/onboardingSkip.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/households/pages/OnboardingPage.tsx`
- Create: `Application/Frigorino.Web/ClientApp/src/routes/onboarding.tsx`
- Auto-modified: `Application/Frigorino.Web/ClientApp/src/routes/routeTree.gen.ts`

- [ ] **Step 1: Create the session skip helper**

Create `src/features/households/onboardingSkip.ts`:

```ts
const ONBOARDING_SKIPPED_KEY = "frigorino-onboarding-skipped";

export const getOnboardingSkipped = (): boolean => {
    try {
        return sessionStorage.getItem(ONBOARDING_SKIPPED_KEY) === "true";
    } catch {
        return false;
    }
};

export const setOnboardingSkipped = (): void => {
    try {
        sessionStorage.setItem(ONBOARDING_SKIPPED_KEY, "true");
    } catch {
        // sessionStorage unavailable (e.g. privacy mode) — skip is best-effort.
    }
};
```

- [ ] **Step 2: Create the onboarding page**

Create `src/features/households/pages/OnboardingPage.tsx`:

```tsx
import { Box, Button, Container, Typography } from "@mui/material";
import { Navigate, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { CreateHouseholdForm } from "../components/CreateHouseholdForm";
import { setOnboardingSkipped } from "../onboardingSkip";
import { useUserHouseholds } from "../useUserHouseholds";
import { pageContainerSx } from "../../../theme";

export function OnboardingPage() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: households, isLoading } = useUserHouseholds();

    // An existing user who already has a household has no business here.
    if (!isLoading && (households?.length ?? 0) > 0) {
        return <Navigate to="/" />;
    }

    const handleSkip = () => {
        setOnboardingSkipped();
        navigate({ to: "/" });
    };

    return (
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Box sx={{ mb: { xs: 3, sm: 4 }, textAlign: "center" }}>
                <Typography
                    variant="h4"
                    component="h1"
                    sx={{ fontWeight: 700, mb: 1 }}
                >
                    {t("onboarding.title")}
                </Typography>
                <Typography variant="body1" color="text.secondary">
                    {t("onboarding.subtitle")}
                </Typography>
            </Box>

            <CreateHouseholdForm />

            <Box sx={{ mt: 2, textAlign: "center" }}>
                <Button
                    data-testid="onboarding-skip-button"
                    variant="text"
                    onClick={handleSkip}
                >
                    {t("onboarding.skip")}
                </Button>
            </Box>
        </Container>
    );
}
```

- [ ] **Step 3: Create the route shell**

Create `src/routes/onboarding.tsx`:

```tsx
import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../common/authGuard";
import { OnboardingPage } from "../features/households/pages/OnboardingPage";

export const Route = createFileRoute("/onboarding")({
    beforeLoad: requireAuth,
    component: OnboardingPage,
});
```

- [ ] **Step 4: Regenerate the route tree, format, lint, and type-check**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run build && npm run fix && npm run lint
```
Expected: `npm run build` succeeds and regenerates `src/routes/routeTree.gen.ts` so it includes the `/onboarding` route; `npm run fix` formats; `npm run lint` reports no errors. (`build` runs `tsc -b` so this also type-checks.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/households/onboardingSkip.ts Application/Frigorino.Web/ClientApp/src/features/households/pages/OnboardingPage.tsx Application/Frigorino.Web/ClientApp/src/routes/onboarding.tsx Application/Frigorino.Web/ClientApp/src/routes/routeTree.gen.ts
git commit -m "feat: add /onboarding page with create-household form and skip"
```

---

## Task 4: Add the zero-household guard on `/`

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/routes/index.tsx`

- [ ] **Step 1: Replace the contents of `src/routes/index.tsx`**

Replace the whole file with:

```tsx
import { Box, CircularProgress } from "@mui/material";
import { createFileRoute, Navigate } from "@tanstack/react-router";
import { useAuthStore } from "../common/authProvider";
import { WelcomePage } from "../components/dashboard/WelcomePage";
import { LandingPage } from "../components/landing/LandingPage";
import { getOnboardingSkipped } from "../features/households/onboardingSkip";
import { useUserHouseholds } from "../features/households/useUserHouseholds";
import { useAuth } from "../hooks/useAuth";

export const Route = createFileRoute("/")({
    component: Index,
});

function FullPageSpinner() {
    return (
        <Box
            sx={{
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                minHeight: "100vh",
            }}
        >
            <CircularProgress size={40} />
        </Box>
    );
}

function Index() {
    const { isAuthenticated } = useAuth();
    const { loading } = useAuthStore();
    const { data: households, isLoading: householdsLoading } =
        useUserHouseholds(isAuthenticated);

    // Wait for auth to resolve before deciding anything.
    if (loading) {
        return <FullPageSpinner />;
    }

    if (!isAuthenticated) {
        return <LandingPage />;
    }

    // Authenticated: wait for the households list, then route first-run users.
    if (householdsLoading) {
        return <FullPageSpinner />;
    }

    if ((households?.length ?? 0) === 0 && !getOnboardingSkipped()) {
        return <Navigate to="/onboarding" />;
    }

    return <WelcomePage />;
}
```

- [ ] **Step 2: Format, lint, and type-check**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run fix && npm run lint && npm run tsc
```
Expected: no errors. (`to: "/onboarding"` resolves because Task 3 regenerated the route tree.)

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/routes/index.tsx
git commit -m "feat: redirect zero-household users to onboarding from dashboard"
```

---

## Task 5: Hide the nav bar on `/onboarding`

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/routes/__root.tsx:26-28`

- [ ] **Step 1: Update the `showNavigation` condition**

In `src/routes/__root.tsx`, replace:

```tsx
    // Hide navigation on landing page for non-authenticated users
    const showNavigation =
        isAuthenticated || location.pathname.startsWith("/auth");
```

with:

```tsx
    // Hide navigation on the landing page (unauthenticated) and on the
    // single-purpose onboarding page (no households yet, nothing to navigate to).
    const showNavigation =
        (isAuthenticated || location.pathname.startsWith("/auth")) &&
        !location.pathname.startsWith("/onboarding");
```

- [ ] **Step 2: Format, lint, and type-check**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run fix && npm run lint && npm run tsc
```
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/routes/__root.tsx
git commit -m "feat: hide nav bar on the onboarding page"
```

---

## Task 6: Turn the integration test green + full verification

**Files:** none changed (verification only).

- [ ] **Step 1: Rebuild the SPA with the implemented onboarding flow**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run build
```
Expected: build succeeds.

- [ ] **Step 2: Run the onboarding integration test to watch it PASS**

Run (from repo root; ensure Docker Desktop is running):
```bash
dotnet test Application/Frigorino.IntegrationTests/ --filter "FullyQualifiedName~Onboarding" 2>&1 | tee /tmp/onboarding-green.log; echo "EXIT=${PIPESTATUS[0]}"
```
Expected: `EXIT=0` and all 3 scenarios pass.

- [ ] **Step 3: Final frontend verification gate**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run lint && npm run tsc
```
Expected: no errors. (Prettier formatting was applied via `npm run fix` in the implementation tasks; CI runs `prettier:check`.)

- [ ] **Step 4: Manual UX pass (dev stack + browser)**

Use the `/dev-up` skill, then drive the SPA (Playwright MCP or a browser) as the seeded `dev@frigorino.local` user — note this user already has households, so verify the existing-user path is unaffected (`/onboarding` self-redirects to `/`). To exercise the first-run path, confirm via the integration test (which starts from zero households) is the authoritative check; for a live check, observe that:
  - The dashboard still loads normally for a user with households.
  - Visiting `/onboarding` directly as a user with households redirects to `/`.
  - The nav bar is absent on `/onboarding` and present elsewhere.

  Tear down with `/dev-down` when finished.

- [ ] **Step 5: No commit needed** (verification only). If `npm run fix` reformatted anything in Step 3 of earlier tasks that was missed, stage and commit it with `chore: formatting`.

---

## Self-review notes (author)

- **Spec coverage:** guard on `/` (Task 4), `/onboarding` route + reused form (Task 3), skip remembered for session (Task 3 helper + Task 4 guard read), nav-bar suppression (Task 5), i18n en+de silent on invites (Task 1), self-guard for existing users on `/onboarding` (Task 3), integration test for the three behaviors (Tasks 2 + 6). Removed-from-last-household edge case is covered by the same `/` guard (no skip flag set ⇒ redirect) — no extra code, exercised implicitly.
- **No placeholders:** every code/command step is concrete.
- **Type/name consistency:** `getOnboardingSkipped` / `setOnboardingSkipped`, `ONBOARDING_SKIPPED_KEY`, `OnboardingPage`, `onboarding-skip-button`, `onboarding.{title,subtitle,skip}` used consistently across tasks.

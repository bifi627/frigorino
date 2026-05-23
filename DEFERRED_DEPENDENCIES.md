# Deferred dependency updates

Snapshot of the 16 updates Dependabot proposed on 2026-05-22 (PRs #18–#33) that were closed unmerged when we tightened the policy. After the new config (`/dependabot.yml`) lands, Dependabot will only open patch + minor PRs — every entry below was either a major bump or part of a group containing one. Re-evaluate manually; bump in `package.json` / `*.csproj` / workflow YAML and let CI verify.

## Status (2026-05-23)

Patches + safe minors shipped on `fix/update-dependencies` (waves 1–3). MUI v9 shipped on `feat/upgrade-mui` (2026-05-23). Remaining list below is the still-deferred set:
- **Backend NuGet**: none.
- **Frontend npm**: none (MUI v9 applied). The @tanstack/react-router 1.170.7 minor is held back due to a runtime regression — tracked in `TECH_DEBT.md`.

## Backend — NuGet (`/Application`)

_All deferred backend bumps have been applied or dropped._

> Applied: all Microsoft.* 10.0.7 → 10.0.8 patches, FirebaseAdmin 3.3.0 → 3.5.0, Microsoft.Playwright 1.59.0 → 1.60.0, Microsoft.VisualStudio.Azure.Containers.Tools.Targets 1.22.1 → 1.23.0, FakeItEasy 8.3.0 → 9.0.1, coverlet.collector 6.0.0 → 10.0.1, Microsoft.NET.Test.Sdk 17.x → 18.5.1. Dropped: FluentAssertions (26 usages refactored to xUnit `Assert.*`) — avoids the v8 licensing-model change.

### Intentionally on prerelease

- **`OpenTelemetry.Instrumentation.EntityFrameworkCore 1.15.1-beta.1`** — no stable version exists and won't until upstream OpenTelemetry's DB semantic conventions stabilize (no ETA). The package's "beta" label is a versioning policy, not a stability signal; it's the only EFCore-specific instrumentation option. Alternatives (`Npgsql.OpenTelemetry` for driver-level spans, or dropping DB tracing) lose EFCore context and are not like-for-like. Re-evaluate when `OpenTelemetry.Instrumentation.EntityFrameworkCore` ships a stable `1.x` (or `2.0`). Note: `dotnet list package --outdated` omits this without `--include-prerelease` — that absence is expected, not a "missing source" anomaly.

## Frontend — npm (`/Application/Frigorino.Web/ClientApp`)

_All deferred frontend bumps have been applied._

> Applied: @mui/material 7.2.0 → 9.0.1, @mui/icons-material 7.2.0 → 9.0.1 (skipped v8). Codemods `v9.0.0/system-props` + `deprecations/all` migrated 20 files from Box system props (`display`/`alignItems`/`mt`/etc.) onto `sx={{}}`; one manual fix removed a duplicate `alignItems` key the codemod produced in `MembersPanel.tsx`.
>
> @tanstack/react-query has been bumped to 5.100.11. The other three @tanstack/* packages (`react-router`, `react-router-devtools`, `router-plugin`) stay at 1.128.8 — see the `TECH_DEBT.md` entry for the regression details.
>
> Vite has been bumped 7 → 8 (rolldown bundler) alongside `@vitejs/plugin-react` 4 → 6 and `vite-plugin-pwa` 1.0.2 → 1.3.0. `@tanstack/router-plugin` stays at 1.128.8 (its peer declares vite ≤6 but worked under 7 and continues to work under 8).
>
> TypeScript has been bumped 5.8.3 → 6.0.3 alongside `typescript-eslint` 8.35.1 → 8.59.4 (the latter's peer caps at TS &lt;6.1.0). TS 6's `types: []` default surfaced 5 implicit `@types/node` references in browser code; fixed by switching to `ReturnType<typeof setTimeout>` and `import.meta.env.DEV`.

## GitHub Actions (`/.github/workflows`)

_All deferred Action bumps have been applied (cache v5, setup-dotnet v5, setup-node v6, upload-artifact v7, download-artifact v8). `actions/checkout@v4` stays as-is._

## Docker (`/Application/Dockerfile`)

_All deferred Docker bumps have been applied — currently tracking active Node LTS (24)._

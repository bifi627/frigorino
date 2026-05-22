# Deferred dependency updates

Snapshot of the 16 updates Dependabot proposed on 2026-05-22 (PRs #18–#33) that were closed unmerged when we tightened the policy. After the new config (`/dependabot.yml`) lands, Dependabot will only open patch + minor PRs — every entry below was either a major bump or part of a group containing one. Re-evaluate manually; bump in `package.json` / `*.csproj` / workflow YAML and let CI verify.

## Status (2026-05-22)

Patches + safe minors shipped on `fix/update-dependencies` (waves 1–3). Remaining list below is the still-deferred set:
- **Backend NuGet**: 4 majors (FluentAssertions v8, FakeItEasy v9, coverlet.collector v10, Microsoft.NET.Test.Sdk v18).
- **Frontend npm**: 4 majors (TypeScript 6, @vitejs/plugin-react v6, @mui/material v9, @mui/icons-material v9). The @tanstack/react-router 1.170.7 minor is held back due to a runtime regression — tracked in `TECH_DEBT.md`.
- **GitHub Actions**: 5 majors.

## Backend — NuGet (`/Application`)

| Package | From | To | Bump |
|---|---|---|---|
| FluentAssertions (Frigorino.IntegrationTests) | 7.2.2 | 8.10.0 | **major** — v8 changed licensing model; check compatibility before bumping |
| FakeItEasy (Frigorino.Test) | 8.3.0 | 9.0.1 | **major** |
| coverlet.collector (Frigorino.Test) | 6.0.0 | 10.0.1 | **major** (4 majors at once — verify reporter output) |
| Microsoft.NET.Test.Sdk (multiple projects) | 17.8.0 / 17.14.1 | 18.5.1 | **major** |

> All Microsoft.* 10.0.7 → 10.0.8 patches, FirebaseAdmin 3.3.0 → 3.5.0, Microsoft.Playwright 1.59.0 → 1.60.0, and Microsoft.VisualStudio.Azure.Containers.Tools.Targets 1.22.1 → 1.23.0 have been applied. Only the four majors above need manual decisions.

## Frontend — npm (`/Application/Frigorino.Web/ClientApp`)

| Package | From | To | Bump |
|---|---|---|---|
| typescript | 5.8.3 | 6.0.3 | **major** — TS 6 is stable; needs a focused upgrade session (compiler diagnostics, lib.d.ts changes) |
| @vitejs/plugin-react | 4.7.0 | 6.0.2 | **major** (skipped v5) |
| @mui/material | 7.2.0 | 9.0.1 | **major** (skipped v8) — has codemods; allocate time |
| @mui/icons-material | 7.2.0 | 9.0.1 | **major** |

> @tanstack/react-query has been bumped to 5.100.11. The other three @tanstack/* packages (`react-router`, `react-router-devtools`, `router-plugin`) stay at 1.128.8 — see the `TECH_DEBT.md` entry for the regression details.

## GitHub Actions (`/.github/workflows`)

| Action | From | To | Bump |
|---|---|---|---|
| actions/cache | 4 | 5 | **major** |
| actions/setup-dotnet | 4 | 5 | **major** |
| actions/setup-node | 4 | 6 | **major** (skipped v5) |
| actions/upload-artifact | 4 | 7 | **major** — v4↔v5 changed default artifact behavior (no longer overwrites); review before bump |
| actions/download-artifact | 4 | 8 | **major** — same v4↔v5 breaking change |

> All five were majors. With the new policy these won't re-open; they need a deliberate workflow update.

## Docker (`/Application/Dockerfile`)

_All deferred Docker bumps have been applied — currently tracking active Node LTS (24)._

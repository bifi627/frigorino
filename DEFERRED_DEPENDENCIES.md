# Deferred dependency updates

Snapshot of the 16 updates Dependabot proposed on 2026-05-22 (PRs #18–#33) that were closed unmerged when we tightened the policy. After the new config (`/dependabot.yml`) lands, Dependabot will only open patch + minor PRs — every entry below was either a major bump or part of a group containing one. Re-evaluate manually; bump in `package.json` / `*.csproj` / workflow YAML and let CI verify.

## Status (2026-05-22)

Patches + safe minors shipped on `fix/update-dependencies` (waves 1–3). Remaining list below is the still-deferred set:
- **Backend NuGet**: none.
- **Frontend npm**: 4 majors (TypeScript 6, @vitejs/plugin-react v6, @mui/material v9, @mui/icons-material v9). The @tanstack/react-router 1.170.7 minor is held back due to a runtime regression — tracked in `TECH_DEBT.md`.

## Backend — NuGet (`/Application`)

_All deferred backend bumps have been applied or dropped._

> Applied: all Microsoft.* 10.0.7 → 10.0.8 patches, FirebaseAdmin 3.3.0 → 3.5.0, Microsoft.Playwright 1.59.0 → 1.60.0, Microsoft.VisualStudio.Azure.Containers.Tools.Targets 1.22.1 → 1.23.0, FakeItEasy 8.3.0 → 9.0.1, coverlet.collector 6.0.0 → 10.0.1, Microsoft.NET.Test.Sdk 17.x → 18.5.1. Dropped: FluentAssertions (26 usages refactored to xUnit `Assert.*`) — avoids the v8 licensing-model change.

## Frontend — npm (`/Application/Frigorino.Web/ClientApp`)

| Package | From | To | Bump |
|---|---|---|---|
| typescript | 5.8.3 | 6.0.3 | **major** — TS 6 is stable; needs a focused upgrade session (compiler diagnostics, lib.d.ts changes) |
| @vitejs/plugin-react | 4.7.0 | 6.0.2 | **major** (skipped v5) |
| @mui/material | 7.2.0 | 9.0.1 | **major** (skipped v8) — has codemods; allocate time |
| @mui/icons-material | 7.2.0 | 9.0.1 | **major** |

> @tanstack/react-query has been bumped to 5.100.11. The other three @tanstack/* packages (`react-router`, `react-router-devtools`, `router-plugin`) stay at 1.128.8 — see the `TECH_DEBT.md` entry for the regression details.

## GitHub Actions (`/.github/workflows`)

_All deferred Action bumps have been applied (cache v5, setup-dotnet v5, setup-node v6, upload-artifact v7, download-artifact v8). `actions/checkout@v4` stays as-is._

## Docker (`/Application/Dockerfile`)

_All deferred Docker bumps have been applied — currently tracking active Node LTS (24)._

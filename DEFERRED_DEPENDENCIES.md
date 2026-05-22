# Deferred dependency updates

Snapshot of the 16 updates Dependabot proposed on 2026-05-22 (PRs #18–#33) that were closed unmerged when we tightened the policy. After the new config (`/dependabot.yml`) lands, Dependabot will only open patch + minor PRs — every entry below was either a major bump or part of a group containing one. Re-evaluate manually; bump in `package.json` / `*.csproj` / workflow YAML and let CI verify.

## Backend — NuGet (`/Application`)

| Package | From | To | Bump |
|---|---|---|---|
| FluentAssertions (Frigorino.IntegrationTests) | 7.2.2 | 8.10.0 | **major** — v8 changed licensing model; check compatibility before bumping |
| FakeItEasy (Frigorino.Test) | 8.3.0 | 9.0.1 | **major** |
| coverlet.collector (Frigorino.Test) | 6.0.0 | 10.0.1 | **major** (4 majors at once — verify reporter output) |
| FirebaseAdmin (Frigorino.Features) | 3.3.0 | 3.5.0 | minor — safe to take |
| Microsoft.NET.Test.Sdk (multiple projects) | 17.8.0 / 17.14.1 | 18.5.1 | **major** |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.7 | 10.0.8 | patch |
| Microsoft.AspNetCore.OpenApi | 10.0.7 | 10.0.8 | patch |
| Microsoft.EntityFrameworkCore (+ Design, InMemory, Tools) | 10.0.7 | 10.0.8 | patch |
| Microsoft.Extensions.ApiDescription.Server | 10.0.7 | 10.0.8 | patch |
| Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions | 10.0.7 | 10.0.8 | patch |
| Microsoft.Extensions.Options | 10.0.7 | 10.0.8 | patch |
| Microsoft.Playwright | 1.59.0 | 1.60.0 | minor |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets | 1.22.1 | 1.23.0 | minor |

> The patch/minor rows above will re-open automatically next Dependabot cycle (now bundled in the `microsoft` group); only the four major bumps need manual decisions.

## Frontend — npm (`/Application/Frigorino.Web/ClientApp`)

| Package | From | To | Bump |
|---|---|---|---|
| typescript | 5.8.3 | 6.0.3 | **major** — TS 6 is stable; needs a focused upgrade session (compiler diagnostics, lib.d.ts changes) |
| @vitejs/plugin-react | 4.7.0 | 6.0.2 | **major** (skipped v5) |
| @types/node | 22.16.5 | 25.9.1 | **major** — keep aligned with the Node version in `Dockerfile` (currently 22 LTS) |
| @mui/material | 7.2.0 | 9.0.1 | **major** (skipped v8) — has codemods; allocate time |
| @mui/icons-material | 7.2.0 | 9.0.1 | **major** |
| @tanstack/react-query | 5.83.0 | 5.100.11 | minor |
| @tanstack/react-router | 1.128.8 | 1.170.7 | minor |
| @tanstack/react-router-devtools | 1.128.8 | 1.167.0 | minor |
| @tanstack/router-plugin | 1.128.8 | 1.168.10 | minor |

> All @tanstack/* minors will re-open next cycle in the `tanstack` group. Majors above stay manual.

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

| Image | From | To | Bump |
|---|---|---|---|
| node | 22-bookworm-slim | 26-bookworm-slim | **major** — stay on 22 (active LTS). Bump only when Node 24 LTS is mature and `@types/node` is bumped in lockstep. |

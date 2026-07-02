---
name: deps-bump
description: >-
  Use when bumping dependencies in Frigorino — NuGet (Microsoft.* / EFCore / test
  infra), npm (React / Vite / TanStack / MUI / TypeScript), GitHub Actions, or the
  Dockerfile node base image. Triggered by Dependabot PRs, an explicit "update X"
  request, or a periodic outdated sweep. Do NOT use for adding a brand-new
  dependency or for code refactors that happen to touch package.json.
---

# Frigorino dependency bumps

## Core principle

**Group into smart waves to minimize verify runs; commit per wave on `fix/update-dependencies` (or a topic branch).** One full verify cycle (tsc + lint + npm build + dotnet test sln + docker build) is the budget unit — don't burn it per package.

## Derive the work, then wave it

This skill stores no pending-bump state — derive the candidate set at runtime:

```bash
cd Application/Frigorino.Web/ClientApp && npm outdated
dotnet list Application/Frigorino.sln package --outdated
# plus: Dockerfile node base image, action versions in .github/workflows/*.yml
```

Then:

1. **Patches + safe minors, all stacks together** — one wave, one verify.
2. **Majors, one at a time**, ordered least → most blast radius: CI/build tooling < test infra < build chain (Vite/TS) < frameworks < UI libraries. Pause and confirm before starting each major. Workflow pattern: user says "continue with X" → do X → summarize → wait.

**Lockstep groups** — bump together, never split:

| Group | Members |
|---|---|
| EF Core | all `Microsoft.EntityFrameworkCore*` packages |
| .NET test infra | FakeItEasy + `Microsoft.NET.Test.Sdk` + `coverlet.collector` (verify TRX reporter after) |
| Vite | `vite` + `@vitejs/plugin-react` + `vite-plugin-pwa` (check peers first: `npm view <pkg> peerDependencies`) |
| TypeScript | `typescript` + `typescript-eslint` (the latter's peer range caps the TS version) |

**MUI majors go last** — codemods, biggest UI risk, dedicated session.
**Node base image:** user wants **active LTS**, not the newest major — check the Node release schedule at bump time.

## Per-wave verify (default, don't skip)

**Sequence matters** — never parallelize verify steps that share state, see `feedback-verify-with-integration-tests` memory. The sln test internally calls `npm run build` via MSBuild, so a standalone build alongside it races; a second `dotnet test` against IT collides on the Testcontainers port.

```bash
# 1. NuGet (after Directory.Packages.props edits)
dotnet restore Application/Frigorino.sln

# 2. npm (range edit → `npm install`; within-^ → `npm update <pkgs>` — see npm specifics)
cd Application/Frigorino.Web/ClientApp && npm install

# 3. Frontend cheap checks (no build — sln test does that)
npm run tsc && npm run lint

# 4. Backend (covers Test + IT; also builds the SPA via MSBuild target)
cd ../../.. && dotnet test Application/Frigorino.sln       # ~3 min total

# 5. Full container build
docker build -f Application/Dockerfile -t frigorino .
```

IntegrationTests (~2 min) are NOT "expensive" — include by default. They catch SPA / pipeline / Dockerfile drift unit tests miss. Docker daemon down → prompt the user to start Desktop, don't skip.

## NuGet specifics

- Central Package Management: **all** NuGet versions live in `Application/Directory.Packages.props` (`<PackageVersion>`), the `.csproj` files carry version-less `<PackageReference>`. Edit the version there, once. No lock files / `--locked-mode` — a plain `dotnet restore` is enough.

## npm specifics

- **Use caret-minor (`^x.y.z`); never `~`, `*`, or `x.*`.** Exact pinning was reverted because it makes `npm audit fix` flag every advisory as breaking (no range overlap with patched-versions ⇒ requires `--force`). Reproducibility comes from the committed `package-lock.json` + `npm ci` in CI, not from declared ranges.
- **Some packages may be exact-pinned as deliberate regression holds.** Before widening any exact pin to `^`, check `TECH_DEBT.md` for the hold and confirm with the user.
- **`.npmrc` hardening:** `ignore-scripts=true` (skips lifecycle scripts during install — supply-chain defense) and `min-release-age=7` (refuses versions <7 days old, catches malicious releases before they're yanked). **Never override `min-release-age`** — no `--min-release-age=0`, not even for a CVE patch; take the newest version that already clears the 7-day quarantine instead.
- **Two workflows depending on what's moving:**
  - **Range change** (bump the `^x.y.z` base, e.g. for majors or to take a specific minor): edit `package.json`, then `npm install`. The new range forces re-resolution.
  - **Within-range bump** (take "Wanted" from `npm outdated` without changing `package.json`): `npm update <pkg1> <pkg2> ...`. **Plain `npm install` is a no-op** here — it only re-resolves when `package.json` changes or the lockfile is missing.
- `npm update` with no args re-resolves the whole tree, which **cascades-fails on `min-release-age=7`** if ANY single package in the tree has a fresh release (you get `Found: <pkg>@undefined` for the fresh one and nothing moves). Always pass an explicit package list and exclude anything <7 days old. Check release dates with `npm view <pkg> time --json | tail`.
- Commit `package.json` (if edited) + `package-lock.json` (+ `.npmrc` if changed). The lockfile diff is the source of truth for what actually moved.
- Before bumping a build-chain package (Vite plugins, eslint plugins): `npm view <pkg> peerDependencies` to confirm host compatibility.
- TypeScript defaults `types: []` (since TS 6), so SPA code must not lean on implicit `@types/node`. Fix surfacing type errors with cross-env idioms — `ReturnType<typeof setTimeout>` instead of `NodeJS.Timeout`, `import.meta.env.DEV` instead of `process.env.NODE_ENV` — rather than adding `@types/node` to browser code.

## Commit format (per wave)

Branch: `fix/update-dependencies` (or `chore/deps-<scope>` for one-offs).

```
chore(deps): <scope summary>

<bullet list of bumps with from→to>

Verified: tsc, lint, build, dotnet test (XX/XX), integration (XX/XX), docker build.
```

Use a HEREDOC for the message so multi-line formatting survives.

## Regression handling

If a bump fails verify and bisect isolates it to one package: **revert that one package only**, keep the rest of the wave. Add an entry to `TECH_DEBT.md` ("tried, regressed, parked") with:

- exact failing scenario (test name + symptom)
- bisect range to narrow next attempt
- why the rest of the wave was worth keeping

## Files-to-touch matrix

| Bump | Touch |
|---|---|
| Node major | `Application/Dockerfile` (build_frontend stage), `.github/workflows/ci.yml` (setup-node `node-version`) |
| .NET test infra | `Application/Directory.Packages.props` (CPM — versions live there, not in the `.csproj` files) |
| Vite | `package.json` (vite + `@vitejs/plugin-react` + `vite-plugin-pwa`), possibly `vite.config.ts` |
| TypeScript | `package.json` (typescript + typescript-eslint), expect a handful of type fixes across SPA code |
| MUI | `package.json`, run codemods, audit every `sx`/theme override |

## Tool reminders (carry forward)

- Library docs: `mcp__context7__resolve-library-id` → `query-docs`. Not WebFetch, not DLL inspection.
- Shell: Bash tool. PowerShell needs per-execution approval.
- Don't read `secrets.json` or run `dotnet user-secrets list`. Override via env vars / launch profiles for verify runs.

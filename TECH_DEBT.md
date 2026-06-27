# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:

- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

## - **NuGet lock-file setup may be over-strict for Dependabot** — `--locked-mode` everywhere + per-project lock files break multi-project Dependabot PRs; decide drop / CPM / auto-fix.
- **Where:** `Application/Directory.Build.props` (`RestorePackagesWithLockFile=true`), `Application/Dockerfile:32` + `.github/workflows/ci.yml:90,132` (`--locked-mode`), the six `Application/**/packages.lock.json`, and `.github/dependabot.yml`. Project graph that triggers it: `Frigorino.IntegrationTests → Web` and `Frigorino.Test → {Domain, Infrastructure, Features}`.
- **Why deferred:** flagged during the dependabot.yml patch-ignore change; user wants to weigh the security/friction trade-off later rather than decide under a config tweak.
- **Why it matters:** Dependabot regenerates the lock file only for the project that *directly* declares a bumped `PackageReference`, not for projects that consume it transitively via `ProjectReference`. So any bump to a `Web`/`Infrastructure`/`Features` package leaves the `IntegrationTests`/`Test` lock files stale → `--locked-mode` restore fails with **NU1004** ("The project references frigorino.web whose dependencies has changed"). This fails CI *and* the Railway Docker deploy (Dockerfile also restores `--locked-mode`), so a raw Dependabot PR can't be merged without regenerating locks.
- **Context worth keeping:** lock files buy less on NuGet than on npm — nuget.org versions are immutable (no republish; deletion = unlisting), so the "retagged transitive" threat is weak without a private feed. Given direct deps are already exact-pinned ([[feedback_dependency_pinning]]), the lock file's only real remaining benefit is pinning *transitive* float — modest return for the multi-project regeneration friction. Industry norm for .NET is no lock files (most common) or Central Package Management (the modern multi-project standard); lock-files + `--locked-mode`-everywhere is the strict minority tier.
- **Plan (pick one):** (1) **Drop lock files** + `RestorePackagesWithLockFile`/`--locked-mode`, keep exact-pinned direct deps — mainstream .NET posture, removes the problem entirely, loses only transitive-float pinning on an already-immutable feed. (2) **Migrate to Central Package Management** (`Directory.Packages.props`) — single source of versions, Dependabot-friendly, kills the deps-bump "match exact version across projects" toil; keep lock files on top only if transitive pinning is still wanted (then still needs option 3). (3) **Keep current strict setup + add an auto-fix workflow** — a `pull_request` job gated on `github.actor == 'dependabot[bot]'` with `permissions: contents: write` that checks out the PR head, runs `dotnet restore Application/Frigorino.sln --force-evaluate` ([[feedback_nuget_lockfile_force_evaluate]]), and commits the refreshed locks back; test jobs must `needs:` it and check out `head_ref` so they see the fix (a GITHUB_TOKEN push doesn't re-trigger CI).
- **Risk if left:** every Dependabot PR that bumps a transitively-consumed package needs a manual `dotnet restore --force-evaluate` + commit (the `deps-bump` skill already does this) before it can merge or deploy; auto-merge is unsafe, and a missed regen surfaces as an opaque NU1004 at Railway deploy time, not just in CI.

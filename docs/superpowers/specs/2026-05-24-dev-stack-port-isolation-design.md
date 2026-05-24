# Dev stack port isolation (parallel worktrees) — design

**Date:** 2026-05-24
**Status:** Approved approach, spec under review
**Scope:** `scripts/dev-up.ps1`, `scripts/dev-down.ps1`, `docker-compose.yml`, `Application/Frigorino.Web/ClientApp/vite.config.ts`, `.claude/skills/dev-up/SKILL.md`, `.claude/skills/dev-down/SKILL.md`. New file: `scripts/dev-common.ps1`.

## Problem

`scripts/dev-up.ps1` and `scripts/dev-down.ps1` bring up the agent dev stack on hardcoded ports (backend `5001`, vite `44375`, postgres `5432`, pgAdmin `8080`) with fixed docker container names (`frigorino-postgres`, `frigorino-pgadmin`). When two git worktrees run the scripts in parallel they collide. Two failure modes:

1. **Active sabotage.** `dev-up` begins by killing *anything* listening on `5001`/`44375` (the "idempotent stale-listener" kill). A second worktree's `dev-up` therefore kills the first worktree's backend and vite, not just stale processes.
2. **Shared docker singletons.** Fixed `container_name` + fixed host ports mean the second `docker compose up` can't create its own `frigorino-postgres`/`frigorino-pgadmin`, and both stacks share one database.

## Goals

- Two or more worktrees can each run `dev-up`/`dev-down` concurrently with zero interference.
- Each worktree instance is **fully isolated**: its own backend port, vite port, postgres (own container + own port + own volume), and pgAdmin (own container + own port).
- `dev-down` only ever tears down *its own* worktree's stack.

## Non-goals / accepted costs

- **Wastefulness is accepted.** Full isolation means one postgres container + one pgAdmin container *per active worktree*. We are not optimizing container count.
- **No shared database** between instances. Diverging branches/migrations must not collide.

## Hard constraint: the user's fixed environment is off-limits

The `dev-*.ps1` scripts are used **only by agents**, never by the user. The user runs the app through their own tooling (Visual Studio, `npm run dev`, their own database) on the **fixed canonical ports `5001` / `44375` (and, if they use the compose DB, `5432` / `8080`)**.

Therefore the agent stack **must never bind the canonical ports**, even when they appear free — the user may start their environment at any moment and expect those ports. The port scan deliberately starts at *canonical + 1* and skips the canonical port entirely.

## Design overview

Two changes work together:

1. **Scope every kill/teardown to recorded per-worktree state, never to global ports.** A per-worktree state file `.dev/stack.json` records exactly which ports + compose project this instance owns. `dev-up` re-runs and `dev-down` read it; nothing ever blind-kills `5001`/`44375` again.
2. **Allocate distinct ports + a distinct compose project per worktree at bring-up time** via dynamic free-port scanning, and namespace docker by compose project so each worktree gets its own containers and volume.

### Per-worktree state file — `.dev/stack.json`

`.dev/` is already gitignored, and each worktree has its own `.dev/`, so this is naturally per-instance. Written by `dev-up`, read by `dev-down` and by idempotent `dev-up` re-runs.

```json
{
  "backendPort": 5002,
  "vitePort": 44376,
  "pgPort": 5433,
  "pgAdminPort": 8081,
  "composeProject": "frigorino-improve-local-agent-dev-env",
  "backendPid": 12345,
  "vitePid": 12346
}
```

### Port allocation — dynamic free-port scan, above canonical

`Find-FreePort -Start <n>` scans upward from a base and returns the first port not currently in a `Listen` state (`Get-NetTCPConnection`). Scan bases are **canonical + 1** so the user's fixed ports are never taken:

| Service  | Canonical (user-only) | Agent scan base |
|----------|-----------------------|-----------------|
| backend  | 5001                  | 5002            |
| vite     | 44375                 | 44376           |
| postgres | 5432                  | 5433            |
| pgAdmin  | 8080                  | 8081            |

On an idempotent re-run, if `.dev/stack.json` exists the script reuses its recorded ports (killing only those first), rather than re-scanning. There is a small TOCTOU window between scan and bind; acceptable for local dev.

### Docker isolation — compose project per worktree

`docker-compose.yml` changes (both backwards-compatible for a plain `docker compose up` with no env/flags):

- **Remove `container_name:`** from both services, so compose auto-names containers per project (`<project>-postgres-1`). (Minor visible change for anyone running plain `docker compose up`: container names become project-prefixed.)
- **Parameterize host ports** with defaults: `"${PG_PORT:-5432}:5432"` and `"${PGADMIN_PORT:-8080}:80"`.

`dev-up` runs `docker compose -p <project> up -d` with `PG_PORT`/`PGADMIN_PORT` set in the session env. The compose **project name** namespaces containers *and* the named volumes (`<project>_frigorino-postgres-data`), so each worktree gets an isolated DB volume automatically.

Project name derivation (`Get-ComposeProject`): sanitized leaf directory name of the repo root (lowercase, non-`[a-z0-9_-]` → `-`), e.g. worktree dir `improve-local-agent-dev-env` → project `frigorino-improve-local-agent-dev-env`; the main checkout `frigorino` → `frigorino` (preserves the existing default volume). Persisted to `stack.json`; `dev-down` prefers the stored value.

The postgres healthcheck wait replaces `docker inspect frigorino-postgres` with a project-scoped lookup: `docker compose -p <project> ps -q postgres` → inspect that container id's `.State.Health.Status`.

### Injecting ports into backend + vite (no profile clobbering)

- **Backend.** Keep `--launch-profile LocalDb` (we still want its `DevAuth__*` env), but override the URL and connection string via **command-line config args**, which outrank launch-profile env vars in ASP.NET config precedence:
  ```
  dotnet run --project Application/Frigorino.Web --launch-profile LocalDb -- \
    --urls "https://localhost:<backendPort>" \
    --ConnectionStrings:Database "Host=localhost;Port=<pgPort>;Database=frigorino;Username=postgres;Password=postgres"
  ```
  > **Verify during implementation:** confirm command-line args actually override the `LocalDb` profile's `applicationUrl` (`ASPNETCORE_URLS`) and `ConnectionStrings__Database` env var. If precedence doesn't hold, fall back to `--no-launch-profile` and set all env vars (incl. `DevAuth__*`) explicitly in the script.
- **Vite.** `dev-up` sets `VITE_DEV_PORT=<vitePort>` and `VITE_PROXY_TARGET=https://localhost:<backendPort>` before spawning `npm run dev` (same mechanism as the existing `VITE_DEV_AUTH`).

### Committed-file change — `vite.config.ts` (backwards-compatible)

Read the port and proxy target from env with the current values as defaults, so the user's manual `npm run dev` is unchanged:

```ts
const target = process.env.VITE_PROXY_TARGET ?? "https://localhost:5001";
// ...
server: {
    proxy: { /* all entries use `target` */ },
    port: Number(process.env.VITE_DEV_PORT) || 44375,
    https: command === "serve" ? loadDevCertHttps() : undefined,
}
```

The `localhost` dev cert is host-based, not port-based, so it remains valid on any scanned port — no cert changes needed.

### Shared helpers — `scripts/dev-common.ps1`

Dot-sourced by both scripts to keep them thin and consistent:

- `Find-FreePort -Start <int>` — first free port at/above base.
- `Get-ComposeProject` — derive project name from repo root leaf dir.
- `Get-StackStatePath` / `Read-StackState` / `Write-StackState` — `.dev/stack.json` I/O.
- `Stop-Listener -Port <int>` — kill the PID listening on a port (moved out of both scripts).

### `dev-up.ps1` flow (revised)

1. Dot-source `dev-common.ps1`; ensure `.dev/` exists.
2. If `stack.json` exists → reuse its ports + project, and `Stop-Listener` **only those recorded** backend/vite ports. Else → scan free ports from the bases above, derive the compose project.
3. `docker compose -p <project> up -d` with `PG_PORT`/`PGADMIN_PORT` env; wait for the project-scoped postgres healthcheck.
4. Start backend (`dotnet run ... -- --urls ... --ConnectionStrings:Database ...`), start vite (`VITE_DEV_PORT`/`VITE_PROXY_TARGET`/`VITE_DEV_AUTH`).
5. Wait on the **chosen** `https://localhost:<backendPort>/healthz` and `https://localhost:<vitePort>/`.
6. `Write-StackState` (ports + project + pids); print the resolved URLs.

### `dev-down.ps1` flow (revised)

1. Dot-source `dev-common.ps1`; `Read-StackState`.
2. If state present → `Stop-Listener` the recorded backend/vite ports; `docker compose -p <project> down` (no `-v`, volume preserved as today); remove `stack.json`.
3. If no state → no-op with a message (nothing this worktree owns). **Never** blind-kill canonical ports.

### Skill doc updates

- **`dev-up/SKILL.md`**: drop "kills stale listeners on 5001/44375"; describe dynamic ports + isolation; the Playwright nav step reads the SPA URL the script prints (or `.dev/stack.json`) instead of hardcoded `44375`. Soften the description's hardcoded `5001`/`44375` mentions to "the URLs the script prints."
- **`dev-down/SKILL.md`**: "kills the ports recorded in `.dev/stack.json` for this worktree" + project-scoped `docker compose down`.

## Testing / verification

- **Unit-ish (PowerShell):** `Find-FreePort` returns a free port at/above base and skips an occupied one; `Get-ComposeProject` sanitizes correctly; state round-trips through write/read.
- **Backend precedence (manual, during implementation):** start backend with the command-line overrides and confirm it binds the scanned port and connects to the scanned pg port (check `.dev/backend.*.log`).
- **End-to-end:** run `dev-up` in two worktrees concurrently; assert two distinct backend/vite/pg/pgAdmin ports, two compose projects, two postgres containers; the SPA in each authenticates as `dev@frigorino.local`; `dev-down` in worktree A leaves worktree B fully running.
- **Backwards-compat:** plain `npm run dev` still serves on `44375`→`5001`; plain `docker compose up` still maps `5432`/`8080`.

## Risks / open items

- **TOCTOU** between port scan and bind — accepted for local dev; the scan-upward retry covers the common case.
- **Same-leaf-name worktrees** in different parent dirs would derive the same compose project. Unlikely; documented limitation. (Could append a short path hash if it ever bites.)
- **Backend config precedence** is the one assumption to verify early (see note above); has a defined fallback.
- **Container-name change** for plain `docker compose up` is cosmetic but worth a line in the skill/CLAUDE notes.

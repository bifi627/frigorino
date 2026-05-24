# Dev Stack Port Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `dev-up`/`dev-down` safe to run concurrently across git worktrees by giving each worktree its own scanned ports (above the user's fixed canonical ports) and its own docker compose project, recorded in a per-worktree `.dev/stack.json`.

**Architecture:** A new dot-sourced `scripts/dev-common.ps1` holds pure helpers (free-port scan, compose-project derivation, state I/O, listener kill). `dev-up.ps1` scans free ports above canonical, starts a per-worktree compose project + backend + vite wired to those ports, and records state. `dev-down.ps1` reads that state and tears down only what this worktree owns. `vite.config.ts` and `docker-compose.yml` gain backwards-compatible env parameterization so the user's manual flow on 5001/44375/5432/8080 is untouched.

**Tech Stack:** Windows PowerShell 5.1, docker compose, .NET 8 (`dotnet run`), Vite, curl.

**Reference spec:** `docs/superpowers/specs/2026-05-24-dev-stack-port-isolation-design.md`

---

## File Structure

- **Create** `scripts/dev-common.ps1` — shared helpers, dot-sourced by both scripts.
- **Create** `scripts/Test-DevCommon.ps1` — lightweight assertion harness for the pure helpers (no Pester dependency).
- **Modify** `docker-compose.yml` — remove fixed `container_name`, parameterize host ports.
- **Modify** `Application/Frigorino.Web/ClientApp/vite.config.ts` — read port + proxy target from env with current defaults.
- **Modify** `scripts/dev-up.ps1` — scan ports, per-worktree compose project, inject ports into backend/vite, record state.
- **Modify** `scripts/dev-down.ps1` — read state, scoped teardown.
- **Modify** `.claude/skills/dev-up/SKILL.md`, `.claude/skills/dev-down/SKILL.md`, `CLAUDE.md` — reflect dynamic per-worktree ports.

---

### Task 1: Shared helpers `dev-common.ps1` (+ assertions)

**Files:**
- Create: `scripts/dev-common.ps1`
- Test: `scripts/Test-DevCommon.ps1`

- [ ] **Step 1: Write the failing test harness**

Create `scripts/Test-DevCommon.ps1`:

```powershell
# Assertions for dev-common.ps1 helpers (no Pester dependency).
# Run: powershell -ExecutionPolicy Bypass -File scripts/Test-DevCommon.ps1
$ErrorActionPreference = "Stop"
. "$PSScriptRoot/dev-common.ps1"

$script:failures = 0
function Assert-Equal {
    param($Expected, $Actual, [string]$Name)
    if ($Expected -ne $Actual) {
        Write-Host "FAIL: $Name - expected '$Expected', got '$Actual'" -ForegroundColor Red
        $script:failures++
    } else {
        Write-Host "PASS: $Name" -ForegroundColor Green
    }
}

# Get-ComposeProject
Assert-Equal "frigorino" (Get-ComposeProject -RepoRoot "C:\Repositories\frigorino") "compose project: main checkout"
Assert-Equal "frigorino-improve-local-agent-dev-env" (Get-ComposeProject -RepoRoot "C:\x\improve-local-agent-dev-env") "compose project: worktree leaf"
Assert-Equal "frigorino-feat-x" (Get-ComposeProject -RepoRoot "C:\x\Feat X") "compose project: sanitized illegal chars"

# State round-trip
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("devtest-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Write-StackState -RepoRoot $tmp -State @{ backendPort = 5002; composeProject = "frigorino-x" }
    $state = Read-StackState -RepoRoot $tmp
    Assert-Equal 5002 $state.backendPort "state round-trip: backendPort"
    Assert-Equal "frigorino-x" $state.composeProject "state round-trip: composeProject"
    Assert-Equal $null (Read-StackState -RepoRoot (Join-Path $tmp "missing")) "state: missing dir returns null"
} finally {
    Remove-Item -Recurse -Force $tmp
}

# Find-FreePort skips an occupied port
$listener = New-Object System.Net.Sockets.TcpListener ([System.Net.IPAddress]::Loopback), 0
$listener.Start()
$busy = $listener.LocalEndpoint.Port
try {
    $free = Find-FreePort -Start $busy
    if ($free -le $busy) {
        Write-Host "FAIL: Find-FreePort should skip busy $busy, got $free" -ForegroundColor Red
        $script:failures++
    } else {
        Write-Host "PASS: Find-FreePort skips busy port ($busy -> $free)" -ForegroundColor Green
    }
} finally {
    $listener.Stop()
}

if ($script:failures -gt 0) { throw "$script:failures assertion(s) failed" }
Write-Host "All dev-common assertions passed." -ForegroundColor Green
```

- [ ] **Step 2: Run the harness to verify it fails**

Run: `powershell -ExecutionPolicy Bypass -File scripts/Test-DevCommon.ps1`
Expected: FAIL — terminating error because `dev-common.ps1` does not exist yet (dot-source throws "The term ... is not recognized" / file-not-found).

- [ ] **Step 3: Implement `dev-common.ps1`**

Create `scripts/dev-common.ps1`:

```powershell
# Shared helpers for the agent dev stack scripts (dev-up.ps1 / dev-down.ps1).
# Dot-source: . "$PSScriptRoot/dev-common.ps1"
# Path helpers take an explicit -RepoRoot so they don't depend on $PSScriptRoot scope.

function Get-StackStatePath {
    param([Parameter(Mandatory)][string]$RepoRoot)
    return Join-Path $RepoRoot ".dev/stack.json"
}

function Find-FreePort {
    param(
        [Parameter(Mandatory)][int]$Start,
        [int]$MaxTries = 200
    )
    for ($p = $Start; $p -lt ($Start + $MaxTries); $p++) {
        $inUse = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
        if (-not $inUse) {
            return $p
        }
    }
    throw "No free port found in range $Start..$($Start + $MaxTries - 1)"
}

function Get-ComposeProject {
    param([Parameter(Mandatory)][string]$RepoRoot)
    $leaf = Split-Path -Leaf $RepoRoot
    $slug = ($leaf.ToLowerInvariant() -replace '[^a-z0-9_-]', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) { $slug = "default" }
    if ($slug -eq "frigorino") { return "frigorino" }
    return "frigorino-$slug"
}

function Read-StackState {
    param([Parameter(Mandatory)][string]$RepoRoot)
    $path = Get-StackStatePath -RepoRoot $RepoRoot
    if (-not (Test-Path $path)) { return $null }
    return Get-Content $path -Raw | ConvertFrom-Json
}

function Write-StackState {
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][hashtable]$State
    )
    $path = Get-StackStatePath -RepoRoot $RepoRoot
    $dir = Split-Path -Parent $path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $State | ConvertTo-Json | Set-Content -Path $path -Encoding UTF8
}

function Stop-Listener {
    param([Parameter(Mandatory)][int]$Port)
    $listenerPids = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $listenerPids) {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }
}
```

- [ ] **Step 4: Run the harness to verify it passes**

Run: `powershell -ExecutionPolicy Bypass -File scripts/Test-DevCommon.ps1`
Expected: every line `PASS:`, final `All dev-common assertions passed.`, exit 0.

- [ ] **Step 5: Commit**

```bash
git add scripts/dev-common.ps1 scripts/Test-DevCommon.ps1
git commit -m "feat: add dev-common.ps1 helpers for per-worktree dev stack"
```

---

### Task 2: Parameterize `docker-compose.yml`

**Files:**
- Modify: `docker-compose.yml`

- [ ] **Step 1: Write the failing verification**

Run: `docker compose config`
Expected (current, pre-change): output contains `container_name: frigorino-postgres` and a published port `5432`. This is the "before" — we will change it.

- [ ] **Step 2: Edit `docker-compose.yml`**

Remove the two `container_name:` lines and parameterize both host ports. Resulting file:

```yaml
services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: frigorino
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "${PG_PORT:-5432}:5432"
    volumes:
      - frigorino-postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d frigorino"]
      interval: 5s
      timeout: 5s
      retries: 10

  pgadmin:
    image: dpage/pgadmin4
    environment:
      PGADMIN_DEFAULT_EMAIL: test@test.de
      PGADMIN_DEFAULT_PASSWORD: test
    ports:
      - "${PGADMIN_PORT:-8080}:80"
    volumes:
      - pgadmin-data:/var/lib/pgadmin
    depends_on:
      postgres:
        condition: service_healthy

volumes:
  frigorino-postgres-data:
  pgadmin-data:
```

- [ ] **Step 3: Verify defaults unchanged (backwards-compat)**

Run: `docker compose config`
Expected: no `container_name:` lines; postgres published port resolves to `5432`, pgadmin to `8080`.

- [ ] **Step 4: Verify override works**

PowerShell:
```powershell
$env:PG_PORT="5599"; $env:PGADMIN_PORT="8181"; docker compose -p devtest config; Remove-Item Env:PG_PORT; Remove-Item Env:PGADMIN_PORT
```
Expected: published ports resolve to `5599` (postgres) and `8181` (pgadmin); project name shown as `devtest`.

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml
git commit -m "feat: parameterize compose ports + drop fixed container names"
```

---

### Task 3: Parameterize `vite.config.ts`

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/vite.config.ts:15` and `:89`

- [ ] **Step 1: Edit the proxy target (line 15)**

Change:
```ts
const target = "https://localhost:5001";
```
to:
```ts
const target = env.VITE_PROXY_TARGET ?? "https://localhost:5001";
```
(`env` is already imported on line 8: `import { env } from "process";`)

- [ ] **Step 2: Edit the dev server port (line 89)**

Change:
```ts
        port: 44375,
```
to:
```ts
        port: Number(env.VITE_DEV_PORT) || 44375,
```

- [ ] **Step 3: Verify type-check passes**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc`
Expected: exits 0, no type errors. Defaults preserve current behavior for a plain `npm run dev` (no `VITE_*` env set → `44375` / `https://localhost:5001`).

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/vite.config.ts
git commit -m "feat: let vite dev port + proxy target come from env"
```

---

### Task 4: Spike — verify backend config-arg precedence

**Goal:** Confirm that command-line config args after `--` override the `LocalDb` launch profile's `applicationUrl` and connection string. This de-risks the one load-bearing assumption before the `dev-up` rewrite. Requires Docker Desktop running. **No commit unless the fallback is needed.**

- [ ] **Step 1: Bring up a throwaway postgres on a non-canonical port**

PowerShell, from repo root:
```powershell
$env:PG_PORT="5599"; docker compose -p spike up -d postgres; Remove-Item Env:PG_PORT
```
Wait until healthy:
```powershell
docker inspect --format "{{.State.Health.Status}}" (docker compose -p spike ps -q postgres)
```
Expected: `healthy`.

- [ ] **Step 2: Start the backend with overrides on a non-canonical port**

PowerShell, from repo root (runs in foreground; Ctrl+C after verifying):
```powershell
dotnet run --project Application/Frigorino.Web --launch-profile LocalDb -- --urls "https://localhost:5099" --ConnectionStrings:Database "Host=localhost;Port=5599;Database=frigorino;Username=postgres;Password=postgres"
```
Watch the console: it should log `Now listening on: https://localhost:5099` (NOT 5001) and complete EF migrations against the spike DB without connection errors.

- [ ] **Step 3: Confirm it bound the overridden port**

In a second shell:
```powershell
curl.exe -ks -o NUL -w "%{http_code}" https://localhost:5099/healthz
```
Expected: `200`. Then stop the backend (Ctrl+C in the first shell).

- [ ] **Step 4: Tear down the spike**

```powershell
docker compose -p spike down -v
```

- [ ] **Step 5: Record the finding**

- **If Step 2 logged `5099` and Step 3 returned `200`:** precedence holds. Use the primary backend-launch approach in Task 5 as written. Done — no commit.
- **If it bound `5001` instead (precedence fails):** use the FALLBACK approach in Task 5 (drop `--launch-profile`, set every env var explicitly). The fallback is included inline in Task 5, Step 1.

---

### Task 5: Rewrite `dev-up.ps1`

**Files:**
- Modify: `scripts/dev-up.ps1` (full rewrite)
- Depends on: Task 1 (helpers), Task 2 (compose), Task 4 (precedence decision)

- [ ] **Step 1: Replace `scripts/dev-up.ps1` with the isolated version**

```powershell
# Local dev stack bring-up (agent-only; the user runs their own env on 5001/44375).
# Usage: powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1
# Per-worktree isolation: scans free ports ABOVE the user's canonical ports, runs a
# per-worktree docker compose project, records everything in .dev/stack.json.
# Idempotent: re-running reuses recorded ports and kills only THIS worktree's listeners.
# See .claude/skills/dev-up/SKILL.md for context.

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
. "$PSScriptRoot/dev-common.ps1"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LogDir = Join-Path $RepoRoot ".dev"
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }

function Wait-HttpOk {
    param([string]$Url, [int]$TimeoutSeconds = 90, [string]$Name = $Url)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $code = & curl.exe -ks -o NUL -w "%{http_code}" --max-time 5 $Url 2>$null
        if ($code -eq "200") { return }
        Start-Sleep -Seconds 1
    }
    throw "$Name did not return 200 at $Url within $TimeoutSeconds seconds"
}

# Reuse recorded ports on re-run; otherwise scan fresh ones above the canonical ports.
$existing = Read-StackState -RepoRoot $RepoRoot
if ($existing) {
    Write-Host "[dev-up] Reusing recorded stack state (.dev/stack.json)."
    $backendPort = [int]$existing.backendPort
    $vitePort = [int]$existing.vitePort
    $pgPort = [int]$existing.pgPort
    $pgAdminPort = [int]$existing.pgAdminPort
    $composeProject = [string]$existing.composeProject
    Write-Host "[dev-up] Killing this worktree's stale listeners on $backendPort/$vitePort..."
    Stop-Listener -Port $backendPort
    Stop-Listener -Port $vitePort
} else {
    $backendPort = Find-FreePort -Start 5002
    $vitePort = Find-FreePort -Start 44376
    $pgPort = Find-FreePort -Start 5433
    $pgAdminPort = Find-FreePort -Start 8081
    $composeProject = Get-ComposeProject -RepoRoot $RepoRoot
}

Write-Host "[dev-up] Project '$composeProject' - backend $backendPort, vite $vitePort, pg $pgPort, pgAdmin $pgAdminPort"

Write-Host "[dev-up] Starting compose (postgres + pgadmin)..."
Push-Location $RepoRoot
try {
    $env:PG_PORT = "$pgPort"
    $env:PGADMIN_PORT = "$pgAdminPort"
    & docker compose -p $composeProject up -d | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "docker compose up failed (exit $LASTEXITCODE)" }
} finally {
    Remove-Item Env:PG_PORT -ErrorAction SilentlyContinue
    Remove-Item Env:PGADMIN_PORT -ErrorAction SilentlyContinue
    Pop-Location
}

Write-Host "[dev-up] Waiting for postgres healthcheck..."
$pgContainer = & docker compose -p $composeProject ps -q postgres
if (-not $pgContainer) { throw "Could not resolve postgres container for project $composeProject" }
$pgDeadline = (Get-Date).AddSeconds(45)
$pgHealth = ""
while ((Get-Date) -lt $pgDeadline) {
    $pgHealth = & docker inspect --format "{{.State.Health.Status}}" $pgContainer 2>$null
    if ($pgHealth -eq "healthy") { break }
    Start-Sleep -Seconds 1
}
if ($pgHealth -ne "healthy") { throw "Postgres did not become healthy (last status: $pgHealth)" }

$connString = "Host=localhost;Port=$pgPort;Database=frigorino;Username=postgres;Password=postgres"

Write-Host "[dev-up] Starting backend (LocalDb profile) on $backendPort..."
$backend = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "Application/Frigorino.Web", "--launch-profile", "LocalDb", "--", `
        "--urls", "https://localhost:$backendPort", `
        "--ConnectionStrings:Database", $connString `
    -WorkingDirectory $RepoRoot `
    -RedirectStandardOutput (Join-Path $LogDir "backend.out.log") `
    -RedirectStandardError (Join-Path $LogDir "backend.err.log") `
    -WindowStyle Hidden -PassThru

Write-Host "[dev-up] Starting vite (VITE_DEV_AUTH=true) on $vitePort..."
# VITE_* set here (not in .env.development) so manual `npm run dev` keeps the real
# Firebase flow + default 44375/5001. Start-Process inherits the current env.
$env:VITE_DEV_AUTH = "true"
$env:VITE_DEV_PORT = "$vitePort"
$env:VITE_PROXY_TARGET = "https://localhost:$backendPort"
$vite = Start-Process -FilePath "npm.cmd" `
    -ArgumentList "run", "dev" `
    -WorkingDirectory (Join-Path $RepoRoot "Application/Frigorino.Web/ClientApp") `
    -RedirectStandardOutput (Join-Path $LogDir "vite.out.log") `
    -RedirectStandardError (Join-Path $LogDir "vite.err.log") `
    -WindowStyle Hidden -PassThru
Remove-Item Env:VITE_DEV_AUTH
Remove-Item Env:VITE_DEV_PORT
Remove-Item Env:VITE_PROXY_TARGET

Write-Host "[dev-up] Waiting for backend https://localhost:$backendPort/healthz..."
Wait-HttpOk -Url "https://localhost:$backendPort/healthz" -TimeoutSeconds 120 -Name "Backend"

Write-Host "[dev-up] Waiting for vite https://localhost:$vitePort/..."
Wait-HttpOk -Url "https://localhost:$vitePort/" -TimeoutSeconds 60 -Name "Vite"

Write-StackState -RepoRoot $RepoRoot -State @{
    backendPort = $backendPort
    vitePort = $vitePort
    pgPort = $pgPort
    pgAdminPort = $pgAdminPort
    composeProject = $composeProject
    backendPid = $backend.Id
    vitePid = $vite.Id
}

Write-Host ""
Write-Host "[dev-up] Ready."
Write-Host "  Backend  https://localhost:$backendPort  (pid $($backend.Id))"
Write-Host "  SPA      https://localhost:$vitePort (pid $($vite.Id))"
Write-Host "  pgAdmin  http://localhost:$pgAdminPort   (test@test.de / test)"
Write-Host "  Project  $composeProject"
Write-Host "  Logs     $LogDir"
Write-Host "  Identity dev@frigorino.local (DevAuth + VITE_DEV_AUTH)"
```

**FALLBACK (only if Task 4 found precedence fails):** replace the backend-launch block (`Write-Host "[dev-up] Starting backend..."` through the `-PassThru` line) with:

```powershell
Write-Host "[dev-up] Starting backend (explicit env, no launch profile) on $backendPort..."
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "https://localhost:$backendPort"
$env:ConnectionStrings__Database = $connString
$env:DevAuth__Enabled = "true"
$env:DevAuth__UserId = "dev-user"
$env:DevAuth__Email = "dev@frigorino.local"
$env:DevAuth__Name = "Dev User"
$backend = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "Application/Frigorino.Web", "--no-launch-profile" `
    -WorkingDirectory $RepoRoot `
    -RedirectStandardOutput (Join-Path $LogDir "backend.out.log") `
    -RedirectStandardError (Join-Path $LogDir "backend.err.log") `
    -WindowStyle Hidden -PassThru
Remove-Item Env:ASPNETCORE_URLS
Remove-Item Env:ConnectionStrings__Database
Remove-Item Env:DevAuth__Enabled
Remove-Item Env:DevAuth__UserId
Remove-Item Env:DevAuth__Email
Remove-Item Env:DevAuth__Name
```

- [ ] **Step 2: Run it (single instance) — requires Docker Desktop running**

Run: `powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1`
Expected: ends with `[dev-up] Ready.` and prints backend/SPA/pgAdmin ports that are all **above** canonical (e.g. 5002/44376/8081) and a project name `frigorino-improve-local-agent-dev-env`.

- [ ] **Step 3: Verify state + reachability**

```powershell
Get-Content .dev/stack.json
$s = Get-Content .dev/stack.json -Raw | ConvertFrom-Json
curl.exe -ks -o NUL -w "%{http_code}`n" "https://localhost:$($s.backendPort)/healthz"
curl.exe -ks -o NUL -w "%{http_code}`n" "https://localhost:$($s.vitePort)/"
```
Expected: `stack.json` has all six fields + project; both curls print `200`. Confirm the user's canonical ports are untouched: `Get-NetTCPConnection -LocalPort 5001,44375 -State Listen -ErrorAction SilentlyContinue` returns nothing (assuming the user's env isn't running).

- [ ] **Step 4: Verify idempotent re-run reuses the same ports**

Run `scripts/dev-up.ps1` again. Expected: logs `Reusing recorded stack state`, same ports as Step 2, ends `Ready.`.

- [ ] **Step 5: Commit**

```bash
git add scripts/dev-up.ps1
git commit -m "feat: scan per-worktree ports + compose project in dev-up"
```

---

### Task 6: Rewrite `dev-down.ps1`

**Files:**
- Modify: `scripts/dev-down.ps1` (full rewrite)
- Depends on: Task 1, Task 5

- [ ] **Step 1: Replace `scripts/dev-down.ps1`**

```powershell
# Local dev stack tear-down (agent-only). Tears down ONLY this worktree's stack,
# read from .dev/stack.json. Never blind-kills the user's canonical 5001/44375.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/dev-down.ps1
# Idempotent: safe when the stack is already down. Preserves the postgres volume.

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
. "$PSScriptRoot/dev-common.ps1"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$state = Read-StackState -RepoRoot $RepoRoot

if (-not $state) {
    Write-Host "[dev-down] No .dev/stack.json - nothing this worktree owns. Done."
    return
}

$composeProject = [string]$state.composeProject
Write-Host "[dev-down] Killing listeners on $($state.backendPort)/$($state.vitePort)..."
Stop-Listener -Port ([int]$state.backendPort)
Stop-Listener -Port ([int]$state.vitePort)

Write-Host "[dev-down] docker compose -p $composeProject down (volume preserved)..."
Push-Location $RepoRoot
try {
    & docker compose -p $composeProject down | Out-Null
} finally {
    Pop-Location
}

Remove-Item (Get-StackStatePath -RepoRoot $RepoRoot) -ErrorAction SilentlyContinue
Write-Host "[dev-down] Done."
```

- [ ] **Step 2: Verify scoped teardown (after Task 5's stack is up)**

Run: `powershell -ExecutionPolicy Bypass -File scripts/dev-down.ps1`
Expected: prints the recorded ports, `docker compose -p frigorino-... down`, `Done.`. Then:
```powershell
Test-Path .dev/stack.json        # -> False
docker compose -p frigorino-improve-local-agent-dev-env ps   # -> no running services
```

- [ ] **Step 3: Verify idempotent no-op**

Run `scripts/dev-down.ps1` again. Expected: `No .dev/stack.json - nothing this worktree owns. Done.` (exit 0, no error).

- [ ] **Step 4: Commit**

```bash
git add scripts/dev-down.ps1
git commit -m "feat: scope dev-down teardown to this worktree's recorded stack"
```

---

### Task 7: Update skill docs + CLAUDE.md

**Files:**
- Modify: `.claude/skills/dev-up/SKILL.md`
- Modify: `.claude/skills/dev-down/SKILL.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: `dev-up/SKILL.md` — description frontmatter**

Replace the second paragraph of the `description:` (the "After the script returns ... pointed there." block) with:

```
  After the script returns, it prints the resolved URLs. Ports are scanned per
  worktree ABOVE the user's canonical 5001/44375 (so parallel worktrees don't
  collide and the user's own env is untouched), and recorded in .dev/stack.json.
  Read the backend/SPA URLs from the script output or .dev/stack.json; the
  SPA is authenticated as dev@frigorino.local via DevAuth + VITE_DEV_AUTH, and
  the Playwright MCP browser can be pointed at the printed SPA URL.
```

- [ ] **Step 2: `dev-up/SKILL.md` — Step 1 body**

In the numbered Step 1 paragraph, replace "kills stale listeners on 5001/44375, brings up `docker compose` (postgres + pgadmin)" with "kills only this worktree's previously-recorded listeners (from `.dev/stack.json`), scans free ports above the canonical 5001/44375/5432/8080, brings up a per-worktree `docker compose` project (postgres + pgadmin)".

- [ ] **Step 3: `dev-up/SKILL.md` — Step 2 + Step 3 (Playwright target)**

In Step 2, replace the hardcoded nav line:
```
   mcp__playwright__browser_navigate -> https://localhost:44375/
```
with:
```
   mcp__playwright__browser_navigate -> the SPA URL printed by the script
   (or read .dev/stack.json -> vitePort: https://localhost:<vitePort>/)
```
In Step 3, change "vite was likely already running on 44375" to "vite was likely already running on the recorded port".

- [ ] **Step 4: `dev-down/SKILL.md` — body**

Replace the two "What it does" bullets with:
```
- Reads `.dev/stack.json` and kills only the backend/vite ports THIS worktree recorded. Port-based (Windows orphans node processes after their wrapper shell exits) and worktree-scoped, so it never touches the user's 5001/44375 or another worktree's stack.
- Runs `docker compose -p <project> down` (project name from `.dev/stack.json`) without `-v`, so this worktree's named volume is preserved. Next `/dev-up` finds the same DB state. If there is no `.dev/stack.json`, it's a no-op.
```

- [ ] **Step 5: `CLAUDE.md` — dev section**

In the "Local dev: two modes" area, replace `port-based kill handles Windows' missing process-group cascade` with `per-worktree port-based kill — ports are scanned above the user's 5001/44375/5432/8080 and recorded in .dev/stack.json, so parallel worktrees coexist and the user's fixed env is untouched; handles Windows' missing process-group cascade`.

- [ ] **Step 6: Commit**

```bash
git add .claude/skills/dev-up/SKILL.md .claude/skills/dev-down/SKILL.md CLAUDE.md
git commit -m "docs: dev-up/dev-down skills reflect per-worktree dynamic ports"
```

---

### Task 8: End-to-end concurrency verification

**Goal:** Prove two worktrees run simultaneously without interference. Requires Docker Desktop running.

- [ ] **Step 1: Bring up this worktree's stack**

Run: `powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1`
Record its ports from `.dev/stack.json` (call them A-ports).

- [ ] **Step 2: Create a second worktree and bring up its stack**

```bash
git worktree add ../frigorino-second -b dev-stack-second-instance
```
From `../frigorino-second`: `powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1`
Expected: B-ports differ from A-ports on all four services; project name `frigorino-frigorino-second`; two postgres containers exist (`docker ps --format "{{.Names}}"` shows both projects' containers).

- [ ] **Step 3: Assert mutual non-interference**

Both SPAs reachable at the same time:
```powershell
# from each worktree, using its own stack.json
curl.exe -ks -o NUL -w "%{http_code}`n" "https://localhost:<A-vitePort>/"
curl.exe -ks -o NUL -w "%{http_code}`n" "https://localhost:<B-vitePort>/"
```
Expected: both `200`.

- [ ] **Step 4: Tear down B, confirm A survives**

From `../frigorino-second`: `powershell -ExecutionPolicy Bypass -File scripts/dev-down.ps1`
Then re-check A's SPA: `curl.exe -ks -o NUL -w "%{http_code}`n" "https://localhost:<A-vitePort>/"` → still `200`. A's `.dev/stack.json` still present; A's backend/vite still listening.

- [ ] **Step 5: Tear down A and remove the scratch worktree**

From this worktree: `powershell -ExecutionPolicy Bypass -File scripts/dev-down.ps1`
Then: `git worktree remove ../frigorino-second --force` and `git branch -D dev-stack-second-instance`.
Expected: both stacks down, no leftover `frigorino-*` containers from these projects (`docker ps -a --format "{{.Names}}"`).

---

## Notes for the implementer

- **Confirm `.dev/stack.json` is gitignored (do this in Task 1):** run `git check-ignore .dev/stack.json`. If it prints the path, it's ignored — good. If it prints nothing, add `.dev/` to `.gitignore` and commit that with Task 1, so the per-worktree state file never gets tracked.
- **Docker dependency:** Tasks 4, 5, 6, 8 need Docker Desktop running. If `docker` errors with daemon-unreachable, stop and ask the user to start Docker Desktop (do not skip the verification).
- **Foreground vs background in Task 4:** the spike's `dotnet run` runs in the foreground intentionally so you can read the `Now listening on:` line; the real `dev-up.ps1` detaches it via `Start-Process`.
- **Manual flow stays on canonical ports:** none of these changes affect the user's `npm run dev` / Visual Studio path — that's the whole point of the env defaults in Tasks 2 and 3.

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

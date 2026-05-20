# Local dev stack bring-up.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1
# Idempotent: re-running kills stale listeners on 5001/44375 first.
# Logs land in .dev/ (gitignored). See .claude/skills/dev-up/SKILL.md for context.

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$LogDir = Join-Path $RepoRoot ".dev"
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir | Out-Null
}

function Stop-Listener {
    param([int]$Port)
    $listenerPids = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $listenerPids) {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }
}

function Wait-HttpOk {
    param([string]$Url, [int]$TimeoutSeconds = 90, [string]$Name = $Url)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $code = & curl.exe -ks -o NUL -w "%{http_code}" --max-time 5 $Url 2>$null
        if ($code -eq "200") {
            return
        }
        Start-Sleep -Seconds 1
    }
    throw "$Name did not return 200 at $Url within $TimeoutSeconds seconds"
}

Write-Host "[dev-up] Killing stale listeners on 5001/44375..."
Stop-Listener -Port 5001
Stop-Listener -Port 44375

Write-Host "[dev-up] Starting compose (postgres + pgadmin)..."
Push-Location $RepoRoot
try {
    & docker compose up -d | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed (exit $LASTEXITCODE)"
    }
} finally {
    Pop-Location
}

Write-Host "[dev-up] Waiting for postgres healthcheck..."
$pgDeadline = (Get-Date).AddSeconds(45)
$pgHealth = ""
while ((Get-Date) -lt $pgDeadline) {
    $pgHealth = & docker inspect --format "{{.State.Health.Status}}" frigorino-postgres 2>$null
    if ($pgHealth -eq "healthy") { break }
    Start-Sleep -Seconds 1
}
if ($pgHealth -ne "healthy") {
    throw "Postgres did not become healthy (last status: $pgHealth)"
}

Write-Host "[dev-up] Starting backend (LocalDb profile)..."
$backend = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "Application/Frigorino.Web", "--launch-profile", "LocalDb" `
    -WorkingDirectory $RepoRoot `
    -RedirectStandardOutput (Join-Path $LogDir "backend.out.log") `
    -RedirectStandardError (Join-Path $LogDir "backend.err.log") `
    -WindowStyle Hidden -PassThru

Write-Host "[dev-up] Starting vite (VITE_DEV_AUTH=true)..."
# VITE_DEV_AUTH is set here, not in .env.development, so manual `npm run dev` keeps
# the real Firebase flow. Start-Process inherits the current PowerShell environment.
$env:VITE_DEV_AUTH = "true"
$vite = Start-Process -FilePath "npm.cmd" `
    -ArgumentList "run", "dev" `
    -WorkingDirectory (Join-Path $RepoRoot "Application/Frigorino.Web/ClientApp") `
    -RedirectStandardOutput (Join-Path $LogDir "vite.out.log") `
    -RedirectStandardError (Join-Path $LogDir "vite.err.log") `
    -WindowStyle Hidden -PassThru
Remove-Item Env:VITE_DEV_AUTH

Write-Host "[dev-up] Waiting for backend https://localhost:5001/healthz..."
Wait-HttpOk -Url "https://localhost:5001/healthz" -TimeoutSeconds 120 -Name "Backend"

Write-Host "[dev-up] Waiting for vite https://localhost:44375/..."
Wait-HttpOk -Url "https://localhost:44375/" -TimeoutSeconds 60 -Name "Vite"

Write-Host ""
Write-Host "[dev-up] Ready."
Write-Host "  Backend  https://localhost:5001  (pid $($backend.Id))"
Write-Host "  SPA      https://localhost:44375 (pid $($vite.Id))"
Write-Host "  pgAdmin  http://localhost:8080   (test@test.de / test)"
Write-Host "  Logs     $LogDir"
Write-Host "  Identity dev@frigorino.local (DevAuth + VITE_DEV_AUTH)"

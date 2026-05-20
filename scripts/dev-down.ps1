# Local dev stack tear-down.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/dev-down.ps1
# Idempotent: safe to run when stack is already down. Preserves the postgres volume.

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Stop-Listener {
    param([int]$Port)
    $listenerPids = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $listenerPids) {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "[dev-down] Killing listeners on 5001/44375..."
Stop-Listener -Port 5001
Stop-Listener -Port 44375

Write-Host "[dev-down] docker compose down (volume preserved)..."
Push-Location $RepoRoot
try {
    & docker compose down | Out-Null
} finally {
    Pop-Location
}

Write-Host "[dev-down] Done."

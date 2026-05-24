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

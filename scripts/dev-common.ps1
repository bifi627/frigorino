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

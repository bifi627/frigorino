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

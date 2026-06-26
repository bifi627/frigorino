#requires -Version 5.1
<#
.SYNOPSIS
  Restore a Frigorino Postgres dump (local file) into a fresh Postgres server.

.DESCRIPTION
  Restores -DumpPath into -TargetUrl using pg_restore from the postgres:18 image, so the
  client version always matches the server and no local psql/pg_restore install is needed.

  Provision the fresh server first (Railway: `railway add` -> Postgres, or the dashboard),
  enable its TCP proxy, and pass its DATABASE_PUBLIC_URL as -TargetUrl (append
  ?sslmode=require if missing). Requires docker.

  A few CREATE EXTENSION / notice lines during restore are benign — the \dt list at the
  end is the real proof.

.EXAMPLE
  ./scripts/restore-db.ps1 -DumpPath .\frigorino-production-20260625T192041Z.dump -TargetUrl 'postgresql://postgres:pw@host.proxy.rlwy.net:1234/railway?sslmode=require'

.EXAMPLE
  # overwrite a NON-empty target (drops existing objects first)
  ./scripts/restore-db.ps1 -DumpPath .\stage.dump -TargetUrl '...' -Clean
#>
param(
    [Parameter(Mandatory)][string]$DumpPath,
    [Parameter(Mandatory)][string]$TargetUrl,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $DumpPath)) { throw "Dump file not found: $DumpPath" }
$local = (Resolve-Path $DumpPath).Path
$dir = Split-Path -Parent $local
$file = Split-Path -Leaf $local

# Confirm the target before anything destructive
$uri = [uri]$TargetUrl
Write-Host ''
Write-Host 'About to restore:' -ForegroundColor Yellow
Write-Host "  dump   : $file"
Write-Host "  target : $($uri.Host):$($uri.Port)$($uri.AbsolutePath)"
Write-Host "  mode   : $(if ($Clean) { '--clean (drops existing objects first)' } else { 'into empty server' })"
if ((Read-Host "Type 'restore' to proceed") -ne 'restore') { Write-Host 'Aborted.'; return }

# Restore via postgres:18 (client matches server, no local pg tools)
$cleanArgs = if ($Clean) { @('--clean', '--if-exists') } else { @() }
Write-Host 'Restoring ...'
docker run --rm -v "${dir}:/d" postgres:18 `
    pg_restore --no-owner --no-privileges @cleanArgs -d $TargetUrl "/d/$file"
if ($LASTEXITCODE -ne 0) { throw "pg_restore failed (exit $LASTEXITCODE)" }

# Verify
Write-Host 'Restored. Tables now in target:' -ForegroundColor Green
docker run --rm postgres:18 psql -d $TargetUrl -c '\dt'

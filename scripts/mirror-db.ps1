#requires -Version 5.1
<#
.SYNOPSIS
  Mirror one Frigorino Postgres database into another (e.g. production -> stage).

.DESCRIPTION
  Streams pg_dump from -SourceUrl straight into pg_restore on -TargetUrl via the
  postgres:18 image (client matches server, no local pg tools, no temp file).

  The TARGET is OVERWRITTEN: existing objects are dropped (--clean --if-exists) and
  replaced with the source's data. You must type the target host to confirm.

  Both URLs must be reachable from your machine — use each server's DATABASE_PUBLIC_URL
  (TCP proxy, ?sslmode=require). Requires docker.

  NOTE 1 (data): this copies real source data (incl. any PII) into the target. Only
  mirror "down" into an environment whose access controls you trust.
  NOTE 2 (schema): if the source is on an OLDER migration than the target, mirroring
  reverts the target's schema. Restart the target app afterwards — startup
  MigrateAsync() re-applies the missing migrations on top of the mirrored data.

.EXAMPLE
  ./scripts/mirror-db.ps1 `
    -SourceUrl 'postgresql://postgres:pw@prod.proxy.rlwy.net:1111/railway?sslmode=require' `
    -TargetUrl 'postgresql://postgres:pw@stage.proxy.rlwy.net:2222/railway?sslmode=require'
#>
param(
    [Parameter(Mandatory)][string]$SourceUrl,
    [Parameter(Mandatory)][string]$TargetUrl
)

$ErrorActionPreference = 'Stop'

$src = [uri]$SourceUrl
$tgt = [uri]$TargetUrl
$srcId = "$($src.Host):$($src.Port)$($src.AbsolutePath)"
$tgtId = "$($tgt.Host):$($tgt.Port)$($tgt.AbsolutePath)"
if ($srcId -eq $tgtId) { throw 'Source and target are the same database — refusing.' }

Write-Host ''
Write-Host 'MIRROR — the target will be OVERWRITTEN with the source.' -ForegroundColor Red
Write-Host "  source : $srcId"
Write-Host "  target : $tgtId   <-- DROPPED & REPLACED" -ForegroundColor Yellow
if ((Read-Host "Type the TARGET host ('$($tgt.Host)') to confirm") -ne $tgt.Host) {
    Write-Host 'Aborted.'; return
}

# Stream dump -> restore in one postgres:18 container; pipefail so a source failure
# is not masked by the restore exiting 0.
Write-Host 'Mirroring ...'
docker run --rm -e SRC=$SourceUrl -e TGT=$TargetUrl postgres:18 `
    bash -c 'set -o pipefail; pg_dump -Fc "$SRC" | pg_restore --no-owner --no-privileges --clean --if-exists -d "$TGT"'
if ($LASTEXITCODE -ne 0) { throw "mirror failed (exit $LASTEXITCODE)" }

Write-Host 'Done. Tables now in target:' -ForegroundColor Green
docker run --rm postgres:18 psql -d $TargetUrl -c '\dt'

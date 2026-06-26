# Database Backup & Recovery

Frigorino's Postgres runs on Railway (managed). This doc is the *what exists & why*. The
step-by-step **recovery runbook** (it's broken — restore it) lives separately:
[../playbooks/database-recovery.md](../playbooks/database-recovery.md).

## Overview

Railway's native scheduled backups are gated behind the Pro plan, so backups are
**self-hosted**: a scheduled GitHub Actions job dumps each environment's database to a
Google Cloud Storage bucket. Restore and prod→stage mirroring are PowerShell scripts under
`scripts/`. Everything uses the `postgres:18` Docker image as the Postgres client — no local
`pg_dump`/`pg_restore` install, and the client version is always ≥ the server.

## The backup job — `.github/workflows/db-backup.yml`

- **Schedule:** daily 02:00 UTC, plus manual `workflow_dispatch` (run it before any risky
  migration so you have a fresh pre-change dump).
- **Per environment:** a `[stage, production]` matrix, `fail-fast: false`. Each leg runs
  under its GitHub **Environment**, so it reads that env's own `DB_BACKUP_URL` secret.
- **What it does:** `pg_dump -Fc --no-owner --no-privileges` (custom format) via
  `postgres:18` → uploads to `gs://<bucket>/<env>/frigorino-<env>-<UTCstamp>.dump`.

## Configuration

| Where | Name | Value |
|---|---|---|
| GitHub Environment `stage` / `production` | secret `DB_BACKUP_URL` | that env's Railway **public** connection string (`DATABASE_PUBLIC_URL`, with `?sslmode=require`) |
| Repo secret | `GCS_BACKUP_SA_KEY` | service-account JSON with object write on the bucket |
| Repo variable | `GCS_BACKUP_BUCKET` | `frigorino-db-backup` |
| GCS bucket (not in repo) | lifecycle rule | delete objects older than 30 days — **retention lives here**, set once via `gcloud storage buckets update <bucket> --lifecycle-file` |

The public TCP proxy must be enabled on each Railway Postgres so the runner (and the
scripts) can reach it. Proxy egress is billed — negligible at current size.

## Scripts (`scripts/`)

- **`restore-db.ps1 -DumpPath <file> -TargetUrl <url> [-Clean]`** — restore a local `.dump`
  into a server. Confirms the target, restores via `postgres:18`, prints `\dt`. `-Clean`
  overwrites a non-empty target.
- **`mirror-db.ps1 -SourceUrl <prod> -TargetUrl <stage>`** — clone one DB into another
  (prod→stage refresh). Streams dump→restore in one container; overwrites the target (you
  must type the target host to confirm).

## Key decisions & rationale

- **Self-hosted, not Railway native** — native scheduled backups are Pro-plan only.
- **External GitHub Actions cron, not in-process `IMaintenanceTask`** — two reasons: the
  production image is distroless (no `pg_dump` binary, no shell), and Railway's serverless
  tier sleeps on idle so an in-container wall-clock scheduler would miss its window (see the
  background-jobs / idle-sleep notes in [Backend_Architecture.md](Backend_Architecture.md)
  and `CLAUDE.md`). An external runner connecting over the public proxy sidesteps both.
- **`postgres:18` image as the client** — `pg_dump` must be ≥ the server version; pg18 dumps
  both pg16 and pg18, which covers a mixed fleet during the 16→18 migration. Running it in
  the pinned image avoids the runner's preinstalled (older) client and flaky apt installs.
- **Custom format (`-Fc`)** — compressed, and restorable selectively / with `pg_restore`.
- **Retention via GCS lifecycle rule, not a prune script** — native bucket feature, zero
  code to maintain.

## Restore testing

An untested backup isn't a backup. Periodically (≥ quarterly, and before any major DB
change) run the drill in the playbook: restore the latest dump into a throwaway
`postgres:18` container and confirm the tables + a row count. The 16→18 upgrade rehearsal
was a worked example of exactly this.

## Links out

- **Recovery runbook:** [../playbooks/database-recovery.md](../playbooks/database-recovery.md)
- [File_Storage.md](File_Storage.md) — blobs are **not** in these DB dumps; a restore/mirror
  brings DB rows but not the attachments they reference (see the playbook's mirror caveats).
- [Backend_Architecture.md](Backend_Architecture.md) — the maintenance/background-job model
  and the Railway idle-sleep constraint that shaped the external-cron choice.

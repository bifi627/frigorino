# Database disaster recovery

Runbook for restoring or rebuilding Frigorino's Postgres after data loss, a bad migration,
or a Railway incident. How backups work + why: [../knowledge/Database_Backup.md](../knowledge/Database_Backup.md).

## Prerequisites

- **Docker** running locally (the scripts use the `postgres:18` image as the client).
- **gcloud** authenticated with read access to the backup bucket (`gs://frigorino-db-backup`).
- The target server's **`DATABASE_PUBLIC_URL`** (Railway → the Postgres service → Variables;
  append `?sslmode=require` if missing). The private `DATABASE_URL` is *not* reachable from
  your machine.

## Where the backups are

`gs://frigorino-db-backup/<env>/frigorino-<env>-<UTCstamp>.dump`, one per day, retained 30
days. Newest production dump:

```powershell
gcloud storage ls gs://frigorino-db-backup/production/ | Sort-Object | Select-Object -Last 1
```

Pull a specific one down:

```powershell
gcloud storage cp <gs://...the .dump...> .
```

## Scenario A — bad migration / accidental delete / `ExecuteDelete` bug

The DB is up but the *data* is wrong. **Do not restore over the live DB** — restore into a
*fresh* server, verify, then cut over. A direct overwrite is unrecoverable if the dump turns
out stale.

1. Provision a fresh Postgres in the **same Railway environment** (see quick ref below).
   Enable its TCP proxy; grab its `DATABASE_PUBLIC_URL`.
2. Pull a dump from *before* the incident (pick the timestamp).
3. Restore into the new server:
   ```powershell
   ./scripts/restore-db.ps1 -DumpPath .\frigorino-production-<stamp>.dump -TargetUrl '<new DATABASE_PUBLIC_URL>'
   ```
4. Verify (row counts, spot-check the affected tables).
5. **Cut over:** repoint the app's `ConnectionStrings__Database` (the `Frigorino.Web` service
   variable) to the new DB. Railway redeploys; startup `MigrateAsync()` is a no-op
   (`__EFMigrationsHistory` came over in the dump).
6. Keep the damaged DB **paused, not deleted**, ≥24h as a rollback path.

## Scenario B — total loss (DB deleted, Railway incident)

Same as A, but there's no live DB to preserve:

1. Provision a fresh Postgres in the environment; enable TCP proxy; get `DATABASE_PUBLIC_URL`.
2. Restore the latest dump into it (`restore-db.ps1`).
3. Repoint `ConnectionStrings__Database` → new DB; redeploy.
4. Smoke-test the golden flows (login, household switch, a list, a recipe).

**RPO/RTO:** with daily backups you lose at most ~24h of writes (RPO). RTO is provision +
restore + redeploy — minutes at current size.

## Scenario C — refresh stage from prod

Give stage real-shaped data:

```powershell
./scripts/mirror-db.ps1 -SourceUrl '<prod DATABASE_PUBLIC_URL>' -TargetUrl '<stage DATABASE_PUBLIC_URL>'
```

Caveats (the script header repeats these):

- **Schema:** if prod is on an *older* migration than stage (the usual case — stage→main FF
  promotion), the mirror reverts stage's schema. **Restart the stage app afterwards** —
  startup `MigrateAsync()` re-applies the missing migrations on top of the mirrored data.
- **PII:** this copies real client data (emails, household names) into stage verbatim, no
  scrubbing. Only mirror *down* (prod→stage), never up.
- **Blobs:** dumps are DB-only. Stage rows reference blobs in the *prod* blob area until/unless
  those are copied or repointed ([../knowledge/File_Storage.md](../knowledge/File_Storage.md));
  image/attachment previews on mirrored stage data may 404.
- **Test identities:** the `dev-user`/stage identities are overwritten by prod's — re-seed if
  a stage tester needs them.

## Restore drill (do this periodically)

An untested backup isn't a backup. Quarterly, and before any major DB change, restore the
latest dump into a throwaway and confirm it's real — no Railway server needed:

```powershell
gcloud storage cp "$(gcloud storage ls gs://frigorino-db-backup/production/ | Sort-Object | Select-Object -Last 1)" ./drill.dump
docker run -d --name drill -e POSTGRES_PASSWORD=drill postgres:18
Start-Sleep 5
docker cp ./drill.dump drill:/drill.dump
docker exec drill pg_restore --no-owner --no-privileges -U postgres -d postgres /drill.dump
docker exec drill psql -U postgres -c '\dt'              # tables present?
docker exec -it drill psql -U postgres                   # then: select count(*) from "Households";
docker rm -f drill; del drill.dump
```

Tables listed + non-zero rows = the backup is restorable.

## Provisioning a fresh Railway Postgres (quick ref)

```bash
railway link                 # select the project
railway environment <env>    # stage | production
railway add                  # choose Postgres
```

Then: the new service → Variables → enable the TCP proxy → copy **`DATABASE_PUBLIC_URL`**
(not the private `DATABASE_URL`). Append `?sslmode=require` when handing it to the scripts.

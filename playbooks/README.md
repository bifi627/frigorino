# playbooks/

Operational runbooks — step-by-step procedures for doing or fixing something in a live
environment. Distinct from `knowledge/` (which explains how the system is *built*); these are
the "follow these steps under fire" guides.

- [database-recovery.md](database-recovery.md) — restore or rebuild the Postgres database
  after data loss, a bad migration, or a Railway incident; also the prod→stage refresh.
  Reference: [../knowledge/Database_Backup.md](../knowledge/Database_Backup.md).

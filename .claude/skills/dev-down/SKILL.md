---
name: dev-down
description: >-
  Tears down the local Frigorino dev stack — kills the backend + vite processes
  (port-based, since Windows doesn't propagate signals to node grandchildren) and
  stops the docker compose containers (volume preserved, so DB state survives the
  next dev-up). DO NOT auto-invoke this skill. Only run when the user explicitly
  asks for it ("shut everything down", "tear down the stack", "/dev-down"). The
  user often keeps the stack up across sessions, and tearing it down unexpectedly
  destroys their working state.
---

# Tear down the Frigorino local dev stack

Run the script. It's idempotent — safe even if the stack is already partially down.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/dev-down.ps1
```

What it does:
- Kills any process listening on 5001 (backend) and 44375 (vite). Port-based because Windows orphan node processes after their wrapper shell exits — PID files would go stale.
- Runs `docker compose down` without `-v`, so the named `frigorino-postgres-data` volume is preserved. Next `/dev-up` finds the same DB state.

To also wipe the DB on the way down, add `docker compose down -v` manually after.

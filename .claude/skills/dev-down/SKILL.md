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
- Reads `.dev/stack.json` and kills only the backend/vite ports THIS worktree recorded. Port-based (Windows orphans node processes after their wrapper shell exits) and worktree-scoped, so it never touches the user's 5001/44375 or another worktree's stack.
- Runs `docker compose -p <project> down` (project name from `.dev/stack.json`) without `-v`, so this worktree's named volume is preserved. Next `/dev-up` finds the same DB state. If there is no `.dev/stack.json`, it's a no-op.

To also wipe the DB on the way down, add `docker compose down -v` manually after.

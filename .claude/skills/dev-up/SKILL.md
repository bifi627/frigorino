---
name: dev-up
description: >-
  Brings up the local Frigorino dev stack (postgres + backend + vite) and verifies
  it's reachable. Use this skill ONLY when you actually need to interact with the
  running app — verifying UI behavior, reproducing a frontend bug, end-to-end testing
  a change you just made, or driving the SPA via Playwright MCP. Do NOT invoke when
  the conversation merely discusses UI/frontend code; reading and editing files does
  not require a running stack. The bring-up takes ~30s and pulls compose/dotnet/vite
  logs into context, so only fire when the cost is justified.

  After the script returns, the stack is reachable at https://localhost:5001 (backend)
  and https://localhost:44375 (SPA, authenticated as dev@frigorino.local via DevAuth
  + VITE_DEV_AUTH). The Playwright MCP browser can then be pointed there.
---

# Bring up the Frigorino local dev stack

Run the orchestration script and then verify by hitting the SPA via Playwright.

## Steps

1. **Run the bring-up script.** It's idempotent: kills stale listeners on 5001/44375, brings up `docker compose` (postgres + pgadmin), waits for the postgres healthcheck, spawns the backend (`--launch-profile LocalDb`) and vite as detached background processes, polls `/healthz` and the Vite root until both return 200. Backend + vite stdout/stderr land in `.dev/backend.{out,err}.log` and `.dev/vite.{out,err}.log` — read those if anything fails.

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1
   ```

2. **Open the SPA in Playwright.** The MCP server is configured with `--isolated`, so each session starts in an ephemeral profile — no stale Firebase data.

   ```
   mcp__playwright__browser_navigate → https://localhost:44375/
   ```

3. **Verify the identity rendered.** A successful bring-up shows `dev@frigorino.local` in the SPA top-bar. If you see "Sign In" / "Get Started", the SPA's `VITE_DEV_AUTH` bypass didn't fire — check `.dev/vite.out.log`. The script sets `$env:VITE_DEV_AUTH=true` before spawning vite; `Start-Process` inherits the env, so if it didn't take effect, vite was likely already running on 44375 and the port-kill missed it.

   ```
   mcp__playwright__browser_evaluate → () => document.body.innerText.split("\n").filter(l => l.trim()).slice(0, 5).join(" | ")
   ```

   Expected output contains `dev@frigorino.local`.

## When NOT to run this

- The user is asking a code question that doesn't need the running app.
- You're editing a backend file and a unit test would cover it.
- You're reading frontend code to understand structure.
- The stack is already up (calling the script is safe but spends ~5s on the idempotent kill + compose check).

## When to tear down

Don't auto-invoke `/dev-down`. The user keeps the stack up across sessions; tear-down is theirs to trigger explicitly.

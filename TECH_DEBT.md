# Tech debt

Running list of known issues we've spotted but consciously deferred. Add new items as they're noticed; remove them once fixed (don't mark as done — git history covers that).

Format per item:

- **Title** — one-line hook.
- **Where:** file path(s) / line refs.
- **Why deferred:** the reason it didn't get fixed in the originating change.
- **Plan:** the fix sketch, concrete enough that a future contributor doesn't re-investigate from scratch.
- **Risk if left:** what could go wrong while it's still here.

---

- **Dead `/signalr` query-string bearer-token handler** — `OnMessageReceived` accepts the JWT from `?access_token=` for `/signalr` paths; no SignalR exists in the repo.
- **Where:** `Application/Frigorino.Infrastructure/Auth/FirebaseAuth.cs:43-56`.
- **Why deferred:** Found in the 2026-07-01 read-only audit. Unreachable today, not an auth bypass (token still fully validated).
- **Plan:** Delete the whole `OnMessageReceived` handler, keep `OnTokenValidated`.
- **Risk if left:** Tokens-in-URLs leak into logs/Referer if a `/signalr` route ever appears; loaded trap for whoever adds one.

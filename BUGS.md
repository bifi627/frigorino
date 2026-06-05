# Bugs

## Expiry notifications not visible while the app is in the foreground

Reported during testing: when the PWA is open and focused, an incoming expiry
notification does not appear. Needs verification with a **real** data-only FCM
message before fixing — the foreground `onMessage` handler reads `payload.data.*`,
so a *notification*-type message (e.g. one sent from the Firebase console test UI)
is silently ignored, which may fully explain the report. Even for real messages,
some browsers/OSes suppress a system notification raised while its own tab is
focused. Likely fix if confirmed: surface foreground messages as an in-app toast
(sonner) with a "View" deep-link action, keeping the service-worker
`showNotification` for the background path.

Deferred from the expiry-notifications feature work (testing feedback item #4).

## Inventory fails to load when opening a notification for a non-active household

With multiple households: if you receive an expiry notification for Household B
while Household A is the active household, tapping the notification opens the app
to the deep-link target (an inventory in Household B), but the inventory does not
load. The active household is still A (kept in the HTTP session + persisted to
`User.LastActiveHouseholdId`), so the household-scoped request is mismatched
against the link's Household B target. Likely fix: the notification deep-link
should carry the target household id and switch the active household to it on
open (or the inventory route should detect the mismatch and switch) before
issuing the scoped query. Root cause is the implicit household-context model —
see "Household context is implicit (LastActiveHouseholdId)…" in `TECH_DEBT.md`.

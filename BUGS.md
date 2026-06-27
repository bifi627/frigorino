# Bugs

## Expiry notifications not visible while the app is in the foreground

Reported during testing: when the PWA is open and focused, an incoming expiry
notification does not appear. Needs verification with a **real** data-only FCM
message before fixing — the foreground `onMessage` handler reads `payload.data.*`,
so a _notification_-type message (e.g. one sent from the Firebase console test UI)
is silently ignored, which may fully explain the report. Even for real messages,
some browsers/OSes suppress a system notification raised while its own tab is
focused. Likely fix if confirmed: surface foreground messages as an in-app toast
(sonner) with a "View" deep-link action, keeping the service-worker
`showNotification` for the background path.

Deferred from the expiry-notifications feature work (testing feedback item #4).

## Back navigation misbehaves after creating data, then redirecting to an edit/view page

Observed (anecdotally, across multiple features — not yet isolated): after a
create flow that redirects to a follow-up page (e.g. create recipe →
redirect to the recipe edit page to add ingredients), the browser/router back
stack doesn't unwind as expected — back may land on the now-stale create form
or skip the expected step instead of returning to the overview. Needs
investigation: audit how create handlers navigate (likely `router.navigate`
without `replace: true`, leaving the create route on the history stack). Likely
fix: use `replace` on the post-create redirect so the create route isn't a back
target, and audit the same pattern on the existing list/inventory create flows.

Noted while designing the recipe view/edit split (recipe metadata feature) —
the new "create recipe → land on /edit" flow will hit the same path, so verify
it there too. Not yet reproduced/debugged.

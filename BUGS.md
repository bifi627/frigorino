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

## Calendar view sorts by expiry in the wrong direction

Reported by users: in the calendar view, items appear to be ordered with the
furthest-away expiry first; the expected behaviour is the reverse — items with
the soonest (lowest) expiration should sit at the top, so the most urgent items
are seen first. The reporter is not fully certain of the current direction
("I think it's the other way round"), so the first step is to confirm the
existing sort order before flipping it. Investigation deferred.

Places to check: the calendar view's item ordering — likely a sort comparator on
the expiry date that needs its direction reversed (ascending by expiry date so
nearest-due is first). Verify whether any already-expired items belong above or
below upcoming ones once the order is corrected.

## Blueprint list items lack visual separation

Reported by users: items in the blueprint list are not visually separated from
one another, making the list hard to scan. They should match the look and feel
of the list/inventory views, where each item sits in its own box/card. The fix
is a styling alignment — give blueprint items the same boxed/card treatment used
elsewhere. Investigation deferred.

Places to check: the blueprint list rendering vs. the list/inventory item
components. Prefer reusing the existing shared item card/surface (per the theme,
`<Card>` / `<Paper variant="outlined">` rather than a hand-rolled `<Box>`) so the
separation is consistent rather than re-implemented for blueprints.

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

## Human-readable expiry text missing when expiry is more than ~1 month away

Reported by users: the relative-time label (e.g. "1 month", "1 week", "in 3
days") is blank when an item's expiry date is far enough in the future —
roughly beyond a month. Closer expiries render their text fine, so the relative
formatting appears to drop out past some upper bound rather than failing
everywhere. Investigation deferred.

Places to check (the relative-expiry label is rendered in more than one spot):
inventory list/cards, the promote-to-inventory sheet, and any other surface
that shows an expiry countdown. Confirm whether the gap is in a shared
relative-time formatter (single root cause across all surfaces) or duplicated
per-surface logic, then reproduce with a far-future expiry to pin the threshold
before fixing.

## Long list/inventory titles overflow and push the action buttons off-screen

Reported by users: when a list or inventory has a long title, the title text
does not wrap or truncate within its row — it expands the layout and shoves the
trailing action buttons (e.g. edit/delete/overflow menu) off the visible screen,
making them unreachable. Investigation deferred.

Places to check: the list/inventory row or card header where the title sits
alongside the action buttons (likely a flex row missing `min-width: 0` /
truncation on the title, or the actions lacking `flex-shrink: 0`). Confirm
whether this is one shared row/header component reused by both lists and
inventories or duplicated per surface, then decide between truncating (ellipsis)
vs. wrapping the title while keeping the actions pinned and reachable.

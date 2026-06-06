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

## Inventory promotion sheet blocks quantity entry when source item had no quantity

When a list item was created without a quantity (e.g. "Apples"), the promotion
sheet used to promote it to inventory does not allow the user to enter a quantity.
The intended flow: user lists "Apples" without specifying how many, buys 5 in the
store, opens the promotion sheet, types 5, and promotes "5 Apples" to inventory.
The sheet should always render a quantity input, pre-filled with the extracted
value if one exists and empty/placeholder otherwise.

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

## Calendar/date picker is not translated or ignores the user's selected language

The calendar (date picker) does not respect the user's selected language — it
renders untranslated (month/day names, labels) instead of following the active
i18next locale (`en`/`de`). The picker's locale needs to be wired to the current
i18n language so it switches alongside the rest of the UI.

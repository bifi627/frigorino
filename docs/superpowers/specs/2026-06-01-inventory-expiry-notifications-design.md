# Inventory item expiry notifications — design

- **Date:** 2026-06-01
- **Status:** Approved (design) — **blocked on** the "User settings & household settings" feature
  (which must also introduce **inventory-scoped** settings; see Sequencing). No implementation until that lands.
- **Branch (when unblocked):** `feat/expiry-notifications` (off `stage`)

## Summary

Inventory expiry tracking today is **passive** — colored bars, sort-by-expiry, and human-readable countdowns
in `InventoryItemContent.tsx` only help if the user opens the app and looks. This feature adds the missing
**active** half: a daily push notification ("3 items expire soon — Milk tomorrow, Yogurt in 2 days") that
reaches the user without them thinking to check.

A **once-daily external trigger** hits a secured internal endpoint, which runs an **expiry-scan** job. The job
finds soon-to-expire items grouped by household, and for each opted-in member composes a **daily digest** and
**enqueues** the send onto the existing in-process background runner. An `INotificationSender` port (FCM
adapter in v1) delivers via the user's stored FCM tokens. The frontend registers FCM tokens on a user-gesture
permission grant and renders the digest through a push handler in the **already-wired** service worker.

**This feature reuses three things the original idea assumed were greenfield — they already exist:**
- The in-process `System.Threading.Channels` runner (`BackgroundTaskQueue` + `QueuedHostedService`,
  `Frigorino.Infrastructure/Services/`) — shipped with the classifier work. Dispatch `TryEnqueue`s onto it.
- The PWA / service worker — `vite-plugin-pwa@^1.3.0` is wired in `vite.config.ts` (`registerType: "autoUpdate"`,
  manifest with 192/512 icons). A SW is already registered in prod; we only add a **push handler**.
- The Firebase **service-account JSON** already configured in `FirebaseSettings:AccessJson` — the Firebase
  Admin SDK sends FCM with that same credential, so there is **no new server-side secret**.

## Delivery-channel research (the question this idea was really about)

- **Android Chrome / desktop (Chrome, Edge, Firefox):** Web Push works, no install required.
- **iOS / iPadOS:** Web Push works **only** for PWAs **installed to the Home Screen** (iOS 16.4+, March 2023);
  in-Safari-tab push is unsupported. Permission must be requested from a **user gesture**, after install.
- **EU availability (the client is German — this was the decisive unknown):** Apple announced removing
  Home-Screen web apps in the EU in the iOS 17.4 *beta*, then **reversed it on 1 March 2024 before 17.4
  shipped** (9to5Mac / MacRumors / TechCrunch / AppleInsider). Home-Screen web apps — and therefore web push,
  which rides on them — **work in the EU today.** Articles claiming "no EU push" propagate the reverted beta.
- **FCM vs raw Web Push:** FCM is *built on* VAPID/Web Push, not an alternative. **Chosen: FCM**, to reuse the
  existing Firebase relationship (Admin SDK server-side with the already-present service account; Firebase JS
  messaging SDK client-side). Kept behind `INotificationSender` so a raw-`WebPush` adapter is a later swap per
  the vendor-agnostic rule.

## Goals

- A reliable daily expiry digest that reaches opted-in users **without** them opening the app.
- Anti-spam by construction: at most one notification per user per household per day.
- Per-inventory control so a noisy inventory can be muted/tuned without losing alerts elsewhere.
- Reuse the existing runner, service worker, and Firebase credential — minimal new infrastructure.
- Keep the channel behind a port (`INotificationSender`) so FCM is a swappable adapter.

## Non-goals / out of scope (v1)

- **Quiet hours**, **per-household muting** beyond the per-inventory toggle, and **per-user timezone storage** —
  deferred.
- **Per-item (non-digest) notifications** — digest only; the data model does not preclude adding this later.
- **Email channel** — separate, already-noted gap.
- **Retry / durability of the send.** Dispatch rides the lossy in-process runner; a missed send re-derives on
  the next day's scan (the `NotificationDispatch` ledger is written only after enqueue — see Accepted tradeoffs).
- **Settings UI plumbing itself** — the opt-in toggle, lead-time, and per-inventory config are owned by the
  "User settings & household settings" feature (incl. its new inventory-settings scope). This spec only
  *consumes* those fields and defines their notification semantics.

## Key decisions & rationale

1. **Reliable external daily trigger, not best-effort-on-wake.** Expiry is a *time* condition, so a purely
   in-process startup-batch scan (like `DeleteInactiveItems`) would fire only when a user happens to wake the
   container — unreliable for "notify the day before." A **once-a-day external trigger** is chosen instead.
   *This does not violate the "no synthetic uptime checks" rule:* that rule forbids continuous keep-alive pings
   (Hangfire heartbeat, minute-by-minute uptime monitors) that defeat Railway's idle sleep. A single daily wake
   that does **real notification work** is functional, costs seconds/day, and is the feature's whole point — not
   a keep-alive.

2. **Trigger mechanism: secured internal endpoint + external scheduler (MVP).** `POST /internal/expiry-scan`
   guarded by a shared-secret bearer header (its own secret, **not** the user JWT / Firebase auth), hit by a
   scheduled **GitHub Actions** cron (~07:00 Europe). *Rationale: keeps the single-deployable shape, is
   host-portable ("something hits this URL daily"), and is observable in Actions. Railway native cron is the
   alternative but is host-locked and may need a separate container entrypoint — rejected for MVP.*

3. **Daily digest per household, not per item.** At most one notification per user per household per scan,
   aggregating that household's expiring items ("3 items expire soon — …"), deep-linking into the inventory.
   *Rationale: the fastest way to get push permission revoked is to spam; a digest caps volume and survives a
   big weekly shop gracefully. Per-item is a possible later mode, not precluded by the model.*

4. **Layered preferences across two scopes; anti-spam stack.** Master controls live in **user settings**;
   fine-grained control lives in **inventory settings**:
   - User: `ExpiryNotificationsEnabled` (global opt-in) + `ExpiryLeadDays` (default **3**, the fallback window).
   - Inventory: `ExpiryNotificationsEnabled` (**default true** — discoverable) + `ExpiryLeadDays` (**nullable
     override**; `null` ⇒ use the user's value).
   Effective window for an inventory = `inventory.ExpiryLeadDays ?? user.ExpiryLeadDays`, so the scan window is
   computed **per inventory** (e.g. 7 days for a freezer, 1 for fresh). Anti-spam stack: daily digest (≤1/day) →
   per-inventory mute/tune → global user off-switch.

5. **Opt-in is the push grant *and* the user toggle.** A push can only reach a user who granted browser
   permission and registered a token — that is itself consent. We nonetheless keep an explicit user-settings
   opt-in so prefs have a proper home from day one (per the gating decision), rather than shipping on the grant
   alone.

6. **De-dup via a `NotificationDispatch` ledger.** A row per `(UserId, HouseholdId, SentOn)` with a unique
   constraint guarantees **at most one digest per user-household-day**, idempotent across re-triggers / double
   fires. *Rationale: simpler and more robust than a "last notified" stamp per item; the digest is the unit, so
   the ledger key matches it.*

7. **FCM tokens in a flat `FcmToken` table, pruned on send failure.** A user has many (per-device). When FCM
   reports a token unregistered/invalid on send, the adapter deletes that row. *Rationale: flat-schema
   preference; dead-token pruning keeps the table clean without a separate sweep.*

8. **Channel behind `INotificationSender`.** The scan/dispatch code depends only on the port; the FCM adapter
   lives in Infrastructure. A future raw-`WebPush` or native adapter is a swap, no caller change.

## Components

### Domain (`Frigorino.Domain`)
- **`FcmToken`** entity — `Id`, `UserId` (FK → `User`, cascade-delete), `Token`, `CreatedAt`, `LastSeenAt`.
- **`NotificationDispatch`** entity — `Id`, `UserId`, `HouseholdId`, `SentOn` (`DateOnly`). Unique
  `(UserId, HouseholdId, SentOn)`.
- **`INotificationSender`** interface (BCL types only, so it sits in Domain): send a digest payload to a user's
  tokens; report which tokens were rejected so the adapter can prune.
- *(Owned by the settings feature, consumed here):* user fields `ExpiryNotificationsEnabled`,
  `ExpiryLeadDays`; inventory fields `ExpiryNotificationsEnabled`, `ExpiryLeadDays?`.

### Infrastructure (`Frigorino.Infrastructure`)
- **`ExpiryNotificationScan`** — the scan job (invoked by the endpoint). Per run:
  1. Load active `InventoryItem`s with `ExpiryDate` set, joined inventory → household → members.
  2. Filter to inventories with notifications enabled; compute effective lead-days per inventory; keep items
     with `daysUntil ≤ effectiveLeadDays` (includes overdue).
  3. Per recipient (member with user-opt-in ON and ≥1 active token), skip if a `NotificationDispatch` row
     exists for `(user, household, today)`.
  4. Compose the digest, `TryEnqueue` one send per recipient onto `IBackgroundTaskQueue`, write the
     `NotificationDispatch` row.
  - **Timezone (v1 simplification):** `daysUntil` is computed against a single app-configured reference TZ
    (Europe-friendly), since per-user TZ is not stored. Known minor edge near midnight; revisit when TZ is captured.
- **`FcmNotificationSender`** — `INotificationSender` adapter using the Firebase Admin SDK (credential from
  `FirebaseSettings:AccessJson`); prunes `FcmToken` rows FCM reports as unregistered.
- DI: register the scan + sender + EF mappings.

### Web (`Frigorino.Web`)
- **`POST /internal/expiry-scan`** — minimal endpoint, **not** under the user-auth group; guarded by a
  shared-secret bearer header (new config/secret, e.g. `MaintenanceSettings:TriggerToken`). Invokes the scan.
- **Token-registration slice** (under the authenticated group): `POST /api/notifications/token` (store/refresh
  an `FcmToken` for the current user), `DELETE` to unregister.

### Frontend (`ClientApp`)
- **Token registration** — wired to the user-settings opt-in toggle (a **user gesture**, required by iOS):
  `Notification.requestPermission()` → fetch FCM token via the Firebase JS messaging SDK (the app is already a
  Firebase client) → POST to register. Unregister on toggle-off.
- **Service worker** — add an FCM background/push handler to the existing SW to render the digest; click →
  deep-link into the inventory.
- **iOS guidance** — when on iOS Safari and **not** home-screen-installed, the toggle shows an "Add to Home
  Screen to enable notifications" hint instead of failing silently (per the research-confirmed iOS constraint).
- Hooks/SDK regenerated via `npm run api` for the new token slice.

### CI
- A scheduled **GitHub Actions** workflow (cron ~07:00 Europe) that POSTs to `/internal/expiry-scan` with the
  trigger token (from Actions secrets).

## Persistence / API / Frontend summary

- **Schema:** 2 new tables (`FcmToken`, `NotificationDispatch`) — this feature's migration. The user/inventory
  settings columns are added by the **settings feature's** migration, not here.
- **API:** 1 internal trigger endpoint + 1–2 authenticated token slices.
- **Frontend:** token registration on the settings toggle, SW push handler, iOS install hint, client regen.

## Accepted tradeoffs

- **Lossy dispatch.** The send rides the in-process runner; a crash between enqueue and delivery loses that
  day's digest for that user. Because the ledger row is written in the same scan, the simplest safe ordering is
  **enqueue, then write the ledger row only on successful enqueue** — a dropped enqueue (full queue) leaves no
  ledger row, so the next day's scan still covers the item. Re-derivable; acceptable.
- **Overdue items re-appear** in the digest each day until removed — but the per-day de-dup means at most one
  gentle daily nudge total, which is acceptable.
- **Timezone edge** near midnight (single reference TZ) — minor; revisit with per-user TZ later.

## Testing (xUnit + FakeItEasy, `Frigorino.Test`, EF InMemory where needed)

- **`ExpiryNotificationScan`:** window selection (per-inventory effective lead-days, incl. override and overdue),
  inventory-disabled exclusion, recipient filtering (user opt-in + has-token), de-dup skip when a same-day
  `NotificationDispatch` exists, one enqueue per recipient.
- **`FcmNotificationSender`:** sends to all of a user's tokens; prunes tokens FCM reports unregistered.
- **Endpoint auth:** `/internal/expiry-scan` rejects a missing/wrong trigger token.
- The runner itself is already covered by existing tests.

## Verification

- Dev loop: filtered unit tests
  (`dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ExpiryNotificationScan|FullyQualifiedName~FcmNotificationSender"`).
- Gate: `dotnet test Application/Frigorino.sln` (full Test + IntegrationTests).
- **`docker build`** — a new EF migration + new endpoints ship in existing projects (no new project), so the
  Dockerfile layout is unchanged; still build to catch SPA/pipeline drift before Railway does.
- Manual: drive the SPA (dev-up), grant permission, trigger `/internal/expiry-scan` with the token, confirm a
  digest arrives. iOS path needs a real home-screen-installed device.

## Sequencing / relationship to other work

- **Blocked on "User settings & household settings"** — which must also introduce an **inventory-settings**
  scope (the per-inventory `ExpiryNotificationsEnabled` + `ExpiryLeadDays?`). Those fields are added there; the
  inventory-settings addition is being folded into that feature's spec.
- **Runner prerequisite already satisfied** — `BackgroundTaskQueue` / `QueuedHostedService` are built (the
  async-channels-runner spec, 2026-05-29).
- **Stale doc to fix (housekeeping):** CLAUDE.md still says the Channels runner is "not built yet" and the
  IDEAS.md entry says the Vite PWA plugin "is not wired today" — both are now false; correct when convenient.

## Open questions deferred to planning

- Exact digest copy / i18n keys (en + de) and deep-link route.
- Whether the scan paginates / batches for large households (unlikely to matter at current scale).
- Whether to add a queue-depth / sent / failed metric later (ILogger-only for v1, per the runner's precedent).

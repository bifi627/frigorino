# Push Notifications & Expiry Scan

Users get a daily **expiry digest** push ("3 items expiring soon in Fridge") via Firebase Cloud Messaging (FCM). A once-daily external cron hits a key-guarded endpoint that runs a **synchronous** scan: it plans one digest per (user, inventory), writes a dedupe-ledger row **before** sending (claim-slot-first), then dispatches. The service worker is push-only (no offline/precaching — see memory `project_pwa_push_only_sw`).

## Why synchronous, not queued

The scan does its real work in-request rather than via the `BackgroundTaskQueue`. The queue is in-memory and lossy (fine for best-effort request-triggered jobs); a once-daily cron whose ledger write is lost on a restart is unrecoverable until the next day. So the cron path trades the queue for in-request durability. (See memory `project_cron_batch_sync_send`.)

## Entities (`Frigorino.Domain/Entities/`)

| Entity | Fields | Role |
|---|---|---|
| `FcmToken.cs` | `UserId`, `Token` (globally unique), `CreatedAt`, `LastSeenAt` | One row per device/browser. Re-registering an existing token reassigns it to the current user. |
| `NotificationDispatch.cs` | `UserId`, `InventoryId`, `SentOn` (DateOnly) | Dedupe ledger. **Unique index on (UserId, InventoryId, SentOn)** is what makes the scan idempotent across re-triggers — the insert is the "claim". |
| `UserInventoryNotificationSetting.cs` | `UserId`, `InventoryId`, `Enabled`, `LeadDays?` | Per-inventory opt-out / lead-day override. **No row = default** (subscribed, inherit the user's global lead). |

## Sender (`Frigorino.Infrastructure/Notifications/`)

`INotificationSender` (Domain port) → `SendDigestAsync(userId, digest, ct)`. Impl chosen in `Program.cs`: `FcmNotificationSender` (real, FirebaseAdmin multicast to all the user's tokens) on the Firebase auth path; `LogOnlyNotificationSender` (logs instead of sends) on the DevAuth path and as the integration-test/build-time fallback. The FCM sender prunes permanently-dead tokens — `FcmTokenPruning.cs` treats only `Unregistered` / `SenderIdMismatch` as dead (a malformed-payload `InvalidArgument` is not a per-token death).

## Scan flow (`Notifications/ExpiryNotificationScan.cs`)

`RunAsync(today, ct)`:
1. Load candidate items (active, have an expiry date, in active inventories).
2. Load per-(user, inventory) settings and the recipient set (active household members, globally opted-in, with ≥1 token).
3. Load today's already-dispatched (user, inventory) keys.
4. `ExpiryDigestPlanner.Plan(...)` (pure) → one `DigestPlan` per (user, inventory) whose items fall within `[-OverdueGraceDays, +effectiveLeadDays]`, skipping muted inventories and already-dispatched slots.
5. Per plan: **insert the `NotificationDispatch` row and commit first** (claim the slot); on a unique-index race loss, skip — someone else already claimed it. Then compose (`DigestMessageComposer.Compose`, EN/DE, lists up to 3 items + a remainder count, deep-links `/inventories/{id}/view`) and send. A send failure after the claim is logged and accepted (lossy by design — the slot is burned).

## Endpoints (`Frigorino.Features/`)

- `Notifications/TriggerExpiryScan.cs` — `POST /internal/expiry-scan`, mapped **outside** the `RequireAuthorization()` group. Guarded by a constant-time check of a shared-secret header against `MaintenanceSettings:TriggerToken`; a missing/wrong key returns **404** (endpoint stays non-discoverable). This is the cron target.
- `Notifications/RegisterFcmToken.cs` / `UnregisterFcmToken.cs` — `POST` / `DELETE /api/notifications/token` (authorized), upsert/remove the caller's token.
- `Inventories/Notifications/GetMyInventoryNotification.cs` / `UpdateMyInventoryNotification.cs` — `GET` / `PUT .../inventories/{inventoryId}/notifications`, body `{ enabled, leadDays|null }` (the per-inventory settings UI).

DI: `Services/NotificationDependencyInjection.cs` (`AddExpiryNotifications`) registers `ExpiryNotificationScan` (scoped) and binds `MaintenanceSettings` (`TriggerToken`, `OverdueGraceDays`).

## Frontend (`ClientApp/`)

- `src/sw.ts` — push-only service worker: Firebase init, `onBackgroundMessage` renders the notification, `notificationclick` focuses/opens the deep-link. No `fetch` handler, no precache.
- `src/common/pushNotifications.ts` — the lifecycle API: `pushSupported()`, `getNotificationPermission()`, `enablePush()` (prompt → mint token w/ `VITE_FCM_VAPID_KEY` → register server-side), `ensurePushRegistered()` (idempotent reconcile + foreground `onMessage` wiring), `disablePush()`, `cleanupLocalPushToken()`, `isIosNeedingInstall()` (iOS web push needs an installed Home-Screen PWA).
- `src/features/notifications/` — `useRegisterFcmToken` / `useUnregisterFcmToken` (generated-mutation wrappers).
- `src/features/settings/components/NotificationsCard.tsx` — the toggle + global lead-days UI; re-syncs permission on focus/visibility change and reconciles the device token against intent.
- `vite.config.ts` — `VitePWA` with `injectManifest` (`srcDir: src`, `filename: sw.ts`, no precache injection, `registerType: "prompt"`); manifest name from `VITE_APP_NAME`.

## Environment

- Frontend: `VITE_FCM_VAPID_KEY` (browser token minting). Like all `VITE_*` it must be declared as **`ARG` + `ENV`** in the Dockerfile `build_frontend` stage and set in every Railway env, or push silently never prompts (memory `project_railway_vite_build_args`).
- Backend: the Firebase service account (`FirebaseSettings:AccessJson` etc.) backs FCM send; `MaintenanceSettings:TriggerToken` guards the cron endpoint.

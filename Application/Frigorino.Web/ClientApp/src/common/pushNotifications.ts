import {
    getMessaging,
    getToken,
    deleteToken,
    onMessage,
    isSupported,
} from "firebase/messaging";
import { firebaseApp } from "./auth";
import { registerFcmToken, unregisterFcmToken } from "../lib/api/sdk.gen";

const VAPID_KEY = import.meta.env.VITE_FCM_VAPID_KEY as string | undefined;

// Guards against stacking duplicate foreground handlers when push is toggled off/on
// repeatedly — onMessage adds a new observer each call and we discard its unsubscribe.
let foregroundHandlerRegistered = false;

// True only where web push can actually work (Chrome/Edge/Firefox; iOS only as an
// installed Home-Screen PWA). Used to gate the toggle + show the iOS hint.
export async function pushSupported(): Promise<boolean> {
    try {
        return await isSupported();
    } catch {
        return false;
    }
}

// Current browser notification permission, guarded for environments where the
// Notification API is absent. "denied" is the safe default — it disables the toggle.
export function getNotificationPermission(): NotificationPermission {
    if (typeof Notification === "undefined") {
        return "denied";
    }
    return Notification.permission;
}

// Best-effort removal of THIS device's FCM token when its permission was revoked
// externally. We can't always retrieve the token to unregister it server-side once
// permission is blocked, but the server prunes dead tokens on the next failed send,
// so dropping the local registration is enough to keep this device consistent.
export async function cleanupLocalPushToken(): Promise<void> {
    if (!VAPID_KEY || !(await pushSupported())) {
        return;
    }
    try {
        await deleteToken(getMessaging(firebaseApp));
    } catch {
        // Token may be unretrievable when permission is blocked — ignore.
    }
}

// iOS Safari supports web push ONLY when launched from the Home Screen.
export function isIosNeedingInstall(): boolean {
    const ua = navigator.userAgent;
    const isIos = /iPad|iPhone|iPod/.test(ua);
    const standalone =
        window.matchMedia("(display-mode: standalone)").matches ||
        (navigator as unknown as { standalone?: boolean }).standalone === true;
    return isIos && !standalone;
}

async function swRegistration(): Promise<
    ServiceWorkerRegistration | undefined
> {
    if (!("serviceWorker" in navigator)) {
        return undefined;
    }
    return navigator.serviceWorker.ready;
}

// Foreground messages: surface a lightweight in-app notification. Registered once
// (the guard prevents stacking duplicate handlers when push is toggled or on re-init).
function registerForegroundHandler(
    messaging: ReturnType<typeof getMessaging>,
): void {
    if (foregroundHandlerRegistered) {
        return;
    }
    foregroundHandlerRegistered = true;
    onMessage(messaging, async (payload) => {
        const title = payload.data?.title;
        const body = payload.data?.body;
        if (title) {
            const registration = await swRegistration();
            if (registration) {
                // Carry the deep link so the SW's notificationclick handler routes
                // a click on a foreground-shown digest the same as a background one.
                registration.showNotification(title, {
                    body,
                    icon: "/192.png",
                    data: { link: payload.data?.link },
                });
            }
        }
    });
}

// Called once at app boot: if push is supported and the user has already granted
// permission, wire the foreground handler so digests show even when they didn't
// re-toggle the switch this session.
export async function initForegroundPush(): Promise<void> {
    if (!VAPID_KEY || !(await pushSupported())) {
        return;
    }
    if (Notification.permission !== "granted") {
        return;
    }
    registerForegroundHandler(getMessaging(firebaseApp));
}

// Mints (or refreshes) this device's FCM token and registers it server-side.
// Assumes permission is already granted — callers gate on that. Idempotent: getToken
// returns the existing token when present and registration upserts by token, so this
// doubles as a re-arm after an external revoke→re-grant and keeps a rotated token
// fresh. Returns true when a token was registered.
export async function ensurePushRegistered(): Promise<boolean> {
    if (!VAPID_KEY || !(await pushSupported())) {
        return false;
    }
    if (getNotificationPermission() !== "granted") {
        return false;
    }

    const registration = await swRegistration();
    const messaging = getMessaging(firebaseApp);
    const token = await getToken(messaging, {
        vapidKey: VAPID_KEY,
        serviceWorkerRegistration: registration,
    });
    if (!token) {
        return false;
    }

    await registerFcmToken({ body: { token } });

    registerForegroundHandler(messaging);

    return true;
}

// Requests permission, then mints + registers the token.
// Returns true on success; false if denied/unsupported/misconfigured.
export async function enablePush(): Promise<boolean> {
    if (!VAPID_KEY || !(await pushSupported())) {
        return false;
    }

    const permission = await Notification.requestPermission();
    if (permission !== "granted") {
        return false;
    }

    return ensurePushRegistered();
}

// Deletes the local token + unregisters it server-side.
export async function disablePush(): Promise<void> {
    if (!VAPID_KEY || !(await pushSupported())) {
        return;
    }
    const registration = await swRegistration();
    const messaging = getMessaging(firebaseApp);
    let token: string | null;
    try {
        token = await getToken(messaging, {
            vapidKey: VAPID_KEY,
            serviceWorkerRegistration: registration,
        });
    } catch {
        token = null;
    }
    if (token) {
        await unregisterFcmToken({ query: { token } });
        await deleteToken(messaging);
    }
}

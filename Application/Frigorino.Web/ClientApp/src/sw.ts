/// <reference lib="webworker" />
import { precacheAndRoute } from "workbox-precaching";
import { clientsClaim } from "workbox-core";
import { initializeApp } from "firebase/app";
import { getMessaging, onBackgroundMessage } from "firebase/messaging/sw";

declare const self: ServiceWorkerGlobalScope & {
    __WB_MANIFEST: Array<{ url: string; revision: string | null }>;
};

// Precache the Vite build (injected at build time).
precacheAndRoute(self.__WB_MANIFEST);
self.skipWaiting();
clientsClaim();

// Firebase config mirrors src/common/auth.ts (public values).
const firebaseApp = initializeApp({
    apiKey: "AIzaSyAXJH3Z66XYUA-_7rB7ZQzCDHENBlmUxjs",
    authDomain: "frigorino-2acd1.firebaseapp.com",
    projectId: "frigorino-2acd1",
    storageBucket: "frigorino-2acd1.firebasestorage.app",
    messagingSenderId: "97032277670",
    appId: "1:97032277670:web:970459c8367113abdc2e67",
    measurementId: "G-09LMYWB1XG",
});

const messaging = getMessaging(firebaseApp);

// Data-only messages from the server: render the notification ourselves.
onBackgroundMessage(messaging, (payload) => {
    const title = payload.data?.title ?? "Frigorino";
    const body = payload.data?.body ?? "";
    const link = payload.data?.link ?? "/";
    void self.registration.showNotification(title, {
        body,
        icon: "/192.png",
        data: { link },
    });
});

// Focus an existing window (navigating it to the deep link) or open a new one.
self.addEventListener("notificationclick", (event) => {
    event.notification.close();
    const link =
        (event.notification.data as { link?: string } | null)?.link ?? "/";
    event.waitUntil(
        self.clients
            .matchAll({ type: "window", includeUncontrolled: true })
            .then((clients) => {
                for (const client of clients) {
                    if ("focus" in client) {
                        const windowClient = client as WindowClient;
                        // Return the chained promise so the SW stays alive until the
                        // navigate + focus settle. A bare `void` lets the SW be killed
                        // mid-navigation, which on Android can drop back to the browser.
                        return windowClient
                            .navigate(link)
                            .then((navigated) =>
                                (navigated ?? windowClient).focus(),
                            );
                    }
                }
                return self.clients.openWindow(link);
            }),
    );
});

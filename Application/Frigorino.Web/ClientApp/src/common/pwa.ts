import { registerSW } from "virtual:pwa-register";

// sessionStorage key + cooldown for the one-shot chunk-error reload (see below).
const CHUNK_RELOAD_AT = "frigorino:chunk-reload-at";
const CHUNK_RELOAD_COOLDOWN_MS = 10_000;

// Wire up the service worker.
//
// The SW is push-only (see src/sw.ts) — it does not cache or serve the app, so a
// normal page load already gets the latest deploy (index.html is `no-cache`,
// hashed assets are immutable). There is therefore no version-reload coupling
// here: registerType is "prompt" and we pass no refresh handler, so registration
// only keeps Firebase background push alive and never reloads the page.
export function initPwa(): void {
    registerSW({ immediate: true });
    installChunkErrorBackstop();
}

// Safety net for a deploy landing while a tab is open: an old, still-running
// session can lazy-load a route chunk whose hashed filename the new deploy already
// purged from the server → "Failed to fetch dynamically imported module" and a
// broken screen. This can happen to any code-split SPA, service worker or not.
// Reload once to pick up the fresh build. The timestamped cooldown lets a *later*
// deploy in the same session recover too, while stopping a refresh loop if a
// reload doesn't resolve it (e.g. a genuinely missing file).
function installChunkErrorBackstop(): void {
    const reloadOnce = () => {
        const last = Number(sessionStorage.getItem(CHUNK_RELOAD_AT) ?? "0");
        if (Date.now() - last < CHUNK_RELOAD_COOLDOWN_MS) {
            return;
        }
        sessionStorage.setItem(CHUNK_RELOAD_AT, String(Date.now()));
        window.location.reload();
    };

    const isChunkLoadError = (message: string) =>
        /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module|ChunkLoadError/i.test(
            message,
        );

    window.addEventListener("error", (event) => {
        if (isChunkLoadError(event.message ?? "")) {
            reloadOnce();
        }
    });
    window.addEventListener("unhandledrejection", (event) => {
        const reason = event.reason;
        const message =
            reason instanceof Error ? reason.message : String(reason ?? "");
        if (isChunkLoadError(message)) {
            reloadOnce();
        }
    });
}

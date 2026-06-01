/// <reference types="vite/client" />
/// <reference types="vite-plugin-pwa/client" />

interface ImportMetaEnv {
    readonly VITE_FCM_VAPID_KEY?: string;
    readonly VITE_DEV_AUTH?: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}

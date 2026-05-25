/// <reference types="vite/client" />

interface ImportMetaEnv {
    // Comma-separated admin emails; mirrors Hangfire:AdminEmail. Cosmetic only —
    // gates visibility of the Hangfire menu item. The dashboard is enforced server-side.
    readonly VITE_ADMIN_EMAILS?: string;
}

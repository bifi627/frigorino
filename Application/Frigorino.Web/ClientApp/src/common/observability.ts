import {
    type Faro,
    getWebInstrumentations,
    initializeFaro,
} from "@grafana/faro-web-sdk";
import { TracingInstrumentation } from "@grafana/faro-web-tracing";

// Gate: empty VITE_FARO_URL → SDK never initializes, no network calls, no state.
// Mirrors the backend OpenTelemetry:OtlpHeaders activation pattern (see OBSERVABILITY.md
// "Local development policy"). Stage/prod set both env vars via Railway; locally either
// leave them unset (default) or set them to opt in.
const collectorUrl = import.meta.env.VITE_FARO_URL as string | undefined;
const appName =
    (import.meta.env.VITE_FARO_APP_NAME as string | undefined) ?? "frigorino-web";
const envTag = (import.meta.env.VITE_FARO_ENV as string | undefined) ?? "local";
const appVersion =
    (import.meta.env.VITE_APP_VERSION as string | undefined) ?? "0.0.0";

let faro: Faro | undefined;

export function initObservability(): void {
    if (faro || !collectorUrl) {
        if (!collectorUrl) {
            window.console.log(
                "[Faro] Not initialized (VITE_FARO_URL is empty)",
            );
        }
        return;
    }

    faro = initializeFaro({
        url: collectorUrl,
        app: {
            name: appName,
            version: appVersion,
            environment: envTag,
        },
        instrumentations: [
            ...getWebInstrumentations(),
            new TracingInstrumentation(),
        ],
    });

    window.console.log(
        `[Faro] Initialized. app=${appName} environment=${envTag} version=${appVersion}`,
    );
}

export function identifyUser(user: { id: string; email?: string | null }): void {
    if (!faro) {
        return;
    }
    faro.api.setUser({
        id: user.id,
        email: user.email ?? undefined,
    });
}

export function resetUser(): void {
    if (!faro) {
        return;
    }
    faro.api.resetUser();
}

export function pushPageView(path: string): void {
    if (!faro) {
        return;
    }
    faro.api.setView({ name: path });
}

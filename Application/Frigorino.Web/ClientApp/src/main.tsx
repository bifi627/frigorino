// Fire-and-forget wake-ping: kick the backend awake in parallel with Firebase
// init, i18n init, and the user reading the login UI. Errors are irrelevant —
// real API calls will surface any actual outage.
void fetch("/healthz", { credentials: "omit", cache: "no-store" }).catch(
    () => {},
);

import { initObservability, pushPageView } from "./common/observability";

// Init Faro before the auth/router imports so its instrumentations are installed
// before any fetch/XHR fires. Gated on VITE_FARO_URL — no-op when unset.
initObservability();

import "./common/auth"; // Ensure Firebase is initialized
import "./common/apiClient"; // Configure hey-api client with Firebase token resolver
import "./i18n"; // Initialize i18n

import { initForegroundPush } from "./common/pushNotifications";

// Wire the foreground push handler for users who already granted permission, so a
// digest arriving while the tab is open surfaces without re-toggling. No-op when
// push is unsupported or permission isn't granted.
void initForegroundPush();

import { initPwa } from "./common/pwa";

// Register the push service worker and install the chunk-load-error reload net.
initPwa();

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRouter, RouterProvider } from "@tanstack/react-router";
import { StrictMode, Suspense } from "react";
import { createRoot } from "react-dom/client";

// Import the generated route tree
import {
    Box,
    CircularProgress,
    CssBaseline,
    GlobalStyles,
    ThemeProvider,
} from "@mui/material";
import { Toaster } from "sonner";
import { AppLocalizationProvider } from "./common/AppLocalizationProvider";
import { routeTree } from "./routeTree.gen";
import { appTheme } from "./theme";

// Create a new router instance
const router = createRouter({ routeTree });

router.subscribe("onResolved", ({ toLocation }) => {
    pushPageView(toLocation.pathname);
});

// Create a client
const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 5 * 60 * 1000, // 5 minutes
            gcTime: 10 * 60 * 1000, // 10 minutes
        },
    },
});

// Register the router instance for type safety
declare module "@tanstack/react-router" {
    interface Register {
        router: typeof router;
    }
}

createRoot(document.getElementById("root")!).render(
    <StrictMode>
        <QueryClientProvider client={queryClient}>
            <ThemeProvider theme={appTheme}>
                <CssBaseline />
                <AppLocalizationProvider>
                    <Suspense
                        fallback={
                            <Box
                                sx={{
                                    display: "flex",
                                    justifyContent: "center",
                                    alignItems: "center",
                                    minHeight: "100vh",
                                }}
                            >
                                <CircularProgress />
                            </Box>
                        }
                    >
                        <RouterProvider router={router} />
                    </Suspense>
                </AppLocalizationProvider>
                {/* Theme sonner to match the app: dark surfaces, green action
                    button, and the close button pulled inline to the right instead
                    of floating at the top-left corner. Driven by theme tokens (this
                    sits inside ThemeProvider) so it tracks the palette. Selectors are
                    scoped under [data-sonner-toaster] for specificity over sonner's
                    own runtime-injected styles. */}
                <GlobalStyles
                    styles={(theme) => ({
                        "[data-sonner-toaster]": {
                            "--normal-bg": theme.palette.background.paper,
                            "--normal-text": theme.palette.text.primary,
                            "--normal-border": theme.palette.divider,
                            "--success-bg": theme.palette.background.paper,
                            "--success-text": theme.palette.text.primary,
                            "--success-border": theme.palette.divider,
                        },
                        "[data-sonner-toaster] [data-sonner-toast]": {
                            borderRadius: theme.shape.borderRadius,
                        },
                        "[data-sonner-toaster] [data-sonner-toast] [data-button]":
                            {
                                backgroundColor: theme.palette.primary.main,
                                color: theme.palette.primary.contrastText,
                            },
                        "[data-sonner-toaster] [data-sonner-toast] [data-close-button]":
                            {
                                position: "static",
                                order: 5,
                                transform: "none",
                                marginLeft: theme.spacing(1),
                                background: "transparent",
                                border: "none",
                                color: theme.palette.text.secondary,
                            },
                        "[data-sonner-toaster] [data-sonner-toast] [data-close-button]:hover":
                            {
                                color: theme.palette.text.primary,
                                backgroundColor: theme.palette.action.hover,
                            },
                    })}
                />
                <Toaster
                    theme="dark"
                    closeButton
                    toastOptions={{
                        classNames: { actionButton: "undo-action-button" },
                    }}
                />
                {/* <App /> */}
            </ThemeProvider>
        </QueryClientProvider>
    </StrictMode>,
);

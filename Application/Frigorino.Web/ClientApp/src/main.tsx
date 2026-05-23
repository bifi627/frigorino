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

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRouter, RouterProvider } from "@tanstack/react-router";
import { StrictMode, Suspense } from "react";
import { createRoot } from "react-dom/client";

// Import the generated route tree
import {
    Box,
    CircularProgress,
    CssBaseline,
    ThemeProvider,
} from "@mui/material";
import { Toaster } from "sonner";
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
                <Toaster />
                {/* <App /> */}
            </ThemeProvider>
        </QueryClientProvider>
    </StrictMode>,
);

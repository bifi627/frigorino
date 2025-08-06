import { Box } from "@mui/material";
import { createRootRoute, Outlet, useLocation } from "@tanstack/react-router";
import { getAuth } from "firebase/auth";
import { Navigation } from "../components/layout/Navigation";
import { useAuth } from "../hooks/useAuth";

export const Route = createRootRoute({
    component: RootComponent,
    beforeLoad: async () => {
        // Wait for Firebase auth to initialize
        const auth = getAuth();

        return new Promise((resolve) => {
            const unsubscribe = auth.onAuthStateChanged((user) => {
                unsubscribe();
                resolve({ user });
            });
        });
    },
});

function RootComponent() {
    const { isAuthenticated } = useAuth();
    const location = useLocation();

    // Hide navigation on landing page for non-authenticated users
    const showNavigation =
        isAuthenticated || location.pathname.startsWith("/auth");

    return (
        <Box>
            {showNavigation && <Navigation />}
            <Box component="main">
                <Outlet />
            </Box>
            {/* <TanStackRouterDevtools /> */}
        </Box>
    );
}

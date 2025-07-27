import { Box } from "@mui/material";
import { createRootRoute, Outlet, useLocation } from "@tanstack/react-router";
import { TanStackRouterDevtools } from "@tanstack/react-router-devtools";
import { Navigation } from "../components/layout/Navigation";
import { useAuth } from "../hooks/useAuth";

export const Route = createRootRoute({
    component: RootComponent,
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
            <TanStackRouterDevtools />
        </Box>
    );
}

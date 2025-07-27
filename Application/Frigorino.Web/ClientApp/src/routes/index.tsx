import { Box, CircularProgress } from "@mui/material";
import { createFileRoute } from "@tanstack/react-router";
import { useAuthStore } from "../common/authProvider";
import { WelcomePage } from "../components/dashboard/WelcomePage";
import { LandingPage } from "../components/landing/LandingPage";
import { useAuth } from "../hooks/useAuth";

export const Route = createFileRoute("/")({
    component: Index,
});

function Index() {
    const { isAuthenticated } = useAuth();
    const { loading } = useAuthStore();

    // Show loading spinner while authentication state is being determined
    if (loading) {
        return (
            <Box
                display="flex"
                justifyContent="center"
                alignItems="center"
                minHeight="100vh"
            >
                <CircularProgress size={40} />
            </Box>
        );
    }

    // Show appropriate page based on authentication status
    return isAuthenticated ? <WelcomePage /> : <LandingPage />;
}

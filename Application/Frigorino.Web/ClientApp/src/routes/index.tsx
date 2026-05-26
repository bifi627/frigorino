import { Box, CircularProgress } from "@mui/material";
import { createFileRoute, Navigate } from "@tanstack/react-router";
import { useAuthStore } from "../common/authProvider";
import { WelcomePage } from "../components/dashboard/WelcomePage";
import { LandingPage } from "../components/landing/LandingPage";
import { getOnboardingSkipped } from "../features/households/onboardingSkip";
import { useUserHouseholds } from "../features/households/useUserHouseholds";
import { useAuth } from "../hooks/useAuth";

export const Route = createFileRoute("/")({
    component: Index,
});

function FullPageSpinner() {
    return (
        <Box
            sx={{
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                minHeight: "100vh",
            }}
        >
            <CircularProgress size={40} />
        </Box>
    );
}

function Index() {
    const { isAuthenticated } = useAuth();
    const { loading } = useAuthStore();
    const { data: households, isLoading: householdsLoading } =
        useUserHouseholds(isAuthenticated);

    // Wait for auth to resolve before deciding anything.
    if (loading) {
        return <FullPageSpinner />;
    }

    if (!isAuthenticated) {
        return <LandingPage />;
    }

    // Authenticated: wait for the households list, then route first-run users.
    if (householdsLoading) {
        return <FullPageSpinner />;
    }

    if ((households?.length ?? 0) === 0 && !getOnboardingSkipped()) {
        return <Navigate to="/onboarding" />;
    }

    return <WelcomePage />;
}

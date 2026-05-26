import { Box, Button, Container, Typography } from "@mui/material";
import { Navigate, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { CreateHouseholdForm } from "../components/CreateHouseholdForm";
import { setOnboardingSkipped } from "../onboardingSkip";
import { useUserHouseholds } from "../useUserHouseholds";
import { pageContainerSx } from "../../../theme";

export function OnboardingPage() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: households, isLoading } = useUserHouseholds();

    // An existing user who already has a household has no business here.
    if (!isLoading && (households?.length ?? 0) > 0) {
        return <Navigate to="/" />;
    }

    const handleSkip = () => {
        setOnboardingSkipped();
        navigate({ to: "/" });
    };

    return (
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Box sx={{ mb: { xs: 3, sm: 4 }, textAlign: "center" }}>
                <Typography
                    variant="h4"
                    component="h1"
                    sx={{ fontWeight: 700, mb: 1 }}
                >
                    {t("onboarding.title")}
                </Typography>
                <Typography variant="body1" color="text.secondary">
                    {t("onboarding.subtitle")}
                </Typography>
            </Box>

            <CreateHouseholdForm />

            <Box sx={{ mt: 2, textAlign: "center" }}>
                <Button
                    data-testid="onboarding-skip-button"
                    variant="text"
                    onClick={handleSkip}
                >
                    {t("onboarding.skip")}
                </Button>
            </Box>
        </Container>
    );
}

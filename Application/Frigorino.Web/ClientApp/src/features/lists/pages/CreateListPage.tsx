import { ArrowBack } from "@mui/icons-material";
import { Alert, Box, Button, Container, IconButton, Typography } from "@mui/material";
import { useRouter } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { pageContainerSx } from "../../../theme";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { CreateListForm } from "../components/CreateListForm";

export const CreateListPage = () => {
    const { t } = useTranslation();
    const router = useRouter();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const handleBack = () => router.history.back();

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("common.selectHouseholdFirst")}
                    <Button
                        onClick={handleBack}
                        sx={{ mt: 1, display: "block" }}
                    >
                        {t("common.goBackToDashboard")}
                    </Button>
                </Alert>
            </Container>
        );
    }

    return (
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Box
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: { xs: 1, sm: 2 },
                    mb: { xs: 2, sm: 3 },
                }}
            >
                <IconButton onClick={handleBack}>
                    <ArrowBack />
                </IconButton>
                <Typography
                    variant="h5"
                    component="h1"
                    sx={{ fontWeight: 600 }}
                >
                    {t("lists.createNewList")}
                </Typography>
            </Box>

            <CreateListForm householdId={householdId} />
        </Container>
    );
};

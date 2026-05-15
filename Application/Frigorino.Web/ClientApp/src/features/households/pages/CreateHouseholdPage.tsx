import { ArrowBack } from "@mui/icons-material";
import { Box, Container, IconButton, Typography } from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { CreateHouseholdForm } from "../components/CreateHouseholdForm";
import { pageContainerSx } from "../../../theme";

export function CreateHouseholdPage() {
    const { t } = useTranslation();
    const navigate = useNavigate();

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
                <IconButton onClick={() => navigate({ to: "/" })}>
                    <ArrowBack />
                </IconButton>
                <Typography
                    variant="h5"
                    component="h1"
                    sx={{ fontWeight: 600 }}
                >
                    {t("household.createNewHousehold")}
                </Typography>
            </Box>

            <CreateHouseholdForm />
        </Container>
    );
}

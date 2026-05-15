import { Add } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useSetCurrentHousehold } from "../../me/activeHousehold/useSetCurrentHousehold";
import { useCreateHousehold } from "../useCreateHousehold";

export const CreateHouseholdForm = () => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const createHouseholdMutation = useCreateHousehold();
    const setCurrentHouseholdMutation = useSetCurrentHousehold();
    const [name, setName] = useState("");

    const isLoading =
        createHouseholdMutation.isPending ||
        setCurrentHouseholdMutation.isPending;
    const error =
        createHouseholdMutation.error || setCurrentHouseholdMutation.error;

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (!name.trim()) return;

        try {
            const household = await createHouseholdMutation.mutateAsync({
                name: name.trim(),
                description: null,
            });
            if (household.id) {
                await setCurrentHouseholdMutation.mutateAsync(household.id);
            }
            navigate({ to: "/" });
        } catch (err) {
            console.error("Failed to create household:", err);
        }
    };

    const isInvalid = !name.trim() && name.length > 0;

    return (
        <Card elevation={4}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                <form onSubmit={handleSubmit}>
                    <Stack spacing={3}>
                        {error && (
                            <Alert severity="error">
                                {error instanceof Error
                                    ? error.message
                                    : t("common.errorOccurred")}
                            </Alert>
                        )}

                        <Box>
                            <Typography
                                variant="subtitle1"
                                sx={{ fontWeight: 600, mb: 1 }}
                            >
                                {t("household.householdName")} *
                            </Typography>
                            <TextField
                                fullWidth
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                                disabled={isLoading}
                                error={isInvalid}
                                helperText={
                                    isInvalid
                                        ? t("household.householdNameRequired")
                                        : t("household.chooseRecognizableName")
                                }
                            />
                        </Box>

                        <Button
                            data-testid="household-create-submit-button"
                            type="submit"
                            variant="contained"
                            size="large"
                            disabled={isLoading || !name.trim()}
                            startIcon={
                                isLoading ? (
                                    <CircularProgress
                                        size={20}
                                        color="inherit"
                                    />
                                ) : (
                                    <Add />
                                )
                            }
                            sx={{
                                py: { xs: 1, sm: 1.25 },
                                fontWeight: 600,
                                mt: 2,
                            }}
                        >
                            {isLoading
                                ? t("common.creating")
                                : t("household.createHousehold")}
                        </Button>
                    </Stack>
                </form>
            </CardContent>
        </Card>
    );
};

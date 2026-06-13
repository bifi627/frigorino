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
import { ALL_AISLES } from "../aisles";
import { useCreateSortBlueprint } from "../useCreateSortBlueprint";

interface BlueprintCreateFormProps {
    householdId: number;
}

export const BlueprintCreateForm = ({
    householdId,
}: BlueprintCreateFormProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const createBlueprint = useCreateSortBlueprint();
    const [name, setName] = useState("");

    const isLoading = createBlueprint.isPending;
    const error: unknown = createBlueprint.error;
    const isInvalid = !name.trim() && name.length > 0;

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (!name.trim()) {
            return;
        }

        try {
            // New blueprints start with the full default aisle order; the user prunes and
            // reorders on the details page they land on next.
            const response = await createBlueprint.mutateAsync({
                path: { householdId },
                body: { name: name.trim(), categories: ALL_AISLES },
            });
            if (response?.id) {
                navigate({
                    to: "/household/blueprints/$blueprintId/view",
                    params: { blueprintId: response.id.toString() },
                });
            }
        } catch (err) {
            console.error("Failed to create blueprint:", err);
        }
    };

    return (
        <Card elevation={4}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                <form onSubmit={handleSubmit}>
                    <Stack spacing={3}>
                        {error ? (
                            <Alert severity="error">
                                {error instanceof Error
                                    ? error.message
                                    : t("common.errorOccurred")}
                            </Alert>
                        ) : null}

                        <Box>
                            <Typography
                                variant="subtitle1"
                                sx={{ fontWeight: 600, mb: 1 }}
                            >
                                {t("blueprints.nameLabel")} *
                            </Typography>
                            <TextField
                                fullWidth
                                value={name}
                                placeholder={t("blueprints.namePlaceholder")}
                                onChange={(e) => setName(e.target.value)}
                                disabled={isLoading}
                                error={isInvalid}
                                helperText={
                                    isInvalid
                                        ? t("blueprints.nameRequired")
                                        : ""
                                }
                                slotProps={{
                                    htmlInput: {
                                        "data-testid": "blueprint-name-input",
                                    },
                                }}
                            />
                        </Box>

                        <Button
                            data-testid="blueprint-create-submit-button"
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
                                : t("blueprints.createBlueprint")}
                        </Button>
                    </Stack>
                </form>
            </CardContent>
        </Card>
    );
};

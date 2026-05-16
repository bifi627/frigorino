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
import { useCreateInventory } from "../useCreateInventory";

interface CreateInventoryFormProps {
    householdId: number;
}

export const CreateInventoryForm = ({
    householdId,
}: CreateInventoryFormProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const createInventoryMutation = useCreateInventory(householdId);
    const [name, setName] = useState("");

    const isLoading = createInventoryMutation.isPending;
    const error = createInventoryMutation.error;
    const isInvalid = !name.trim() && name.length > 0;

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (!name.trim()) return;

        try {
            const response = await createInventoryMutation.mutateAsync({
                name: name.trim(),
                description: null,
            });
            if (response.id) {
                navigate({ to: `/inventories/${response.id}/view` });
            }
        } catch (err) {
            console.error("Failed to create inventory:", err);
        }
    };

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
                                {t("common.name")} *
                            </Typography>
                            <TextField
                                fullWidth
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                                disabled={isLoading}
                                error={isInvalid}
                                helperText={
                                    isInvalid ? t("common.nameRequired") : ""
                                }
                            />
                        </Box>

                        <Button
                            data-testid="inventory-create-submit-button"
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
                                : t("inventory.createInventory")}
                        </Button>
                    </Stack>
                </form>
            </CardContent>
        </Card>
    );
};

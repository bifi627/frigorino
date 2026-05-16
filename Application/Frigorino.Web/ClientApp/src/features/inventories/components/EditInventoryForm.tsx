import { Save } from "@mui/icons-material";
import {
    Box,
    Button,
    Card,
    CardContent,
    Stack,
    TextField,
} from "@mui/material";
import { useRouter } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { InventoryResponse } from "../../../lib/api";
import { useUpdateInventory } from "../useUpdateInventory";

interface EditInventoryFormProps {
    householdId: number;
    inventory: InventoryResponse;
}

export const EditInventoryForm = ({
    householdId,
    inventory,
}: EditInventoryFormProps) => {
    const { t } = useTranslation();
    const router = useRouter();
    const updateInventoryMutation = useUpdateInventory();
    const [editedName, setEditedName] = useState(inventory.name || "");

    useEffect(() => {
        setEditedName(inventory.name || "");
    }, [inventory.name]);

    const isFormValid = editedName.trim().length > 0;
    const isPending = updateInventoryMutation.isPending;

    const handleSave = () => {
        if (!inventory.id) return;
        updateInventoryMutation.mutate(
            {
                householdId,
                inventoryId: inventory.id,
                data: {
                    name: editedName.trim(),
                    description: inventory.description ?? null,
                },
            },
            {
                onSuccess: () => router.history.back(),
            },
        );
    };

    const handleCancel = () => router.history.back();

    return (
        <Card elevation={4}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                <Stack spacing={3}>
                    <TextField
                        label={t("inventory.inventoryName")}
                        value={editedName}
                        onChange={(e) => setEditedName(e.target.value)}
                        fullWidth
                        required
                        error={editedName.trim().length === 0}
                        helperText={
                            editedName.trim().length === 0
                                ? t("inventory.inventoryNameRequired")
                                : ""
                        }
                    />

                    <Box
                        sx={{
                            display: "flex",
                            gap: 2,
                            justifyContent: "flex-end",
                        }}
                    >
                        <Button
                            variant="outlined"
                            onClick={handleCancel}
                            disabled={isPending}
                            sx={{ minWidth: 100 }}
                        >
                            {t("common.cancel")}
                        </Button>
                        <Button
                            variant="contained"
                            onClick={handleSave}
                            disabled={isPending || !isFormValid}
                            startIcon={<Save />}
                            data-testid="inventory-edit-save-button"
                            sx={{ minWidth: 100, fontWeight: 600 }}
                        >
                            {isPending
                                ? t("common.saving")
                                : t("common.save")}
                        </Button>
                    </Box>
                </Stack>
            </CardContent>
        </Card>
    );
};

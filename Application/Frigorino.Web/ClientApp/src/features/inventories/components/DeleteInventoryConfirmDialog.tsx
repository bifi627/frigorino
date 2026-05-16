import { Alert, Stack, TextField, Typography } from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ConfirmDialog } from "../../../components/dialogs/ConfirmDialog";
import { useDeleteInventory } from "../useDeleteInventory";

interface DeleteInventoryConfirmDialogProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    inventoryId: number;
    inventoryName: string;
}

export const DeleteInventoryConfirmDialog = ({
    open,
    onClose,
    householdId,
    inventoryId,
    inventoryName,
}: DeleteInventoryConfirmDialogProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [confirmationText, setConfirmationText] = useState("");
    const deleteInventoryMutation = useDeleteInventory();

    useEffect(() => {
        if (!open) setConfirmationText("");
    }, [open]);

    const handleConfirm = () => {
        if (confirmationText !== inventoryName) return;
        deleteInventoryMutation.mutate(
            { householdId, inventoryId },
            {
                onSuccess: () => {
                    onClose();
                    navigate({ to: "/" });
                },
            },
        );
    };

    const confirmationMismatch =
        confirmationText.length > 0 && confirmationText !== inventoryName;

    return (
        <ConfirmDialog
            open={open}
            onClose={onClose}
            onConfirm={handleConfirm}
            title={t("inventory.deleteInventory")}
            description={
                <>
                    {t("common.confirmDelete")} "{inventoryName}"?
                </>
            }
            confirmLabel={t("inventory.deleteInventory")}
            confirmLabelPending={t("common.deleting")}
            cancelLabel={t("common.cancel")}
            confirmDisabled={confirmationText !== inventoryName}
            isPending={deleteInventoryMutation.isPending}
        >
            <Alert severity="warning" sx={{ mb: 3 }}>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>
                    {t("common.warningPermanentlyDelete")}
                </Typography>
                <Stack component="ul" sx={{ mt: 1, mb: 0, pl: 2 }}>
                    <Typography component="li" variant="body2">
                        {t("inventory.entireInventory")}
                    </Typography>
                    <Typography component="li" variant="body2">
                        {t("inventory.allInventoryItems")}
                    </Typography>
                </Stack>
            </Alert>

            <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                {t("inventory.confirmTypeInventoryName")}{" "}
                <strong>{inventoryName}</strong>
            </Typography>
            <TextField
                fullWidth
                variant="outlined"
                value={confirmationText}
                onChange={(e) => setConfirmationText(e.target.value)}
                placeholder={t("inventory.typeInventoryNameToConfirm", {
                    inventoryName,
                })}
                disabled={deleteInventoryMutation.isPending}
                error={confirmationMismatch}
                helperText={
                    confirmationMismatch ? t("lists.nameDoesntMatch") : ""
                }
            />
        </ConfirmDialog>
    );
};

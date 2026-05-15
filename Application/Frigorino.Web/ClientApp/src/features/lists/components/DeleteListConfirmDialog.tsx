import { Alert, Stack, TextField, Typography } from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ConfirmDialog } from "../../../components/dialogs/ConfirmDialog";
import { useDeleteList } from "../useDeleteList";

interface DeleteListConfirmDialogProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    listId: number;
    listName: string;
}

export const DeleteListConfirmDialog = ({
    open,
    onClose,
    householdId,
    listId,
    listName,
}: DeleteListConfirmDialogProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [confirmationText, setConfirmationText] = useState("");
    const deleteListMutation = useDeleteList();

    useEffect(() => {
        if (!open) setConfirmationText("");
    }, [open]);

    const handleConfirm = () => {
        if (confirmationText !== listName) return;
        deleteListMutation.mutate(
            { householdId, listId },
            {
                onSuccess: () => {
                    onClose();
                    navigate({ to: "/" });
                },
            },
        );
    };

    const confirmationMismatch =
        confirmationText.length > 0 && confirmationText !== listName;

    return (
        <ConfirmDialog
            open={open}
            onClose={onClose}
            onConfirm={handleConfirm}
            title={t("lists.deleteList")}
            description={
                <>
                    {t("common.confirmDelete")} "{listName}"?{" "}
                    {t("lists.actionCannotBeUndone")}
                </>
            }
            confirmLabel={t("lists.deleteList")}
            confirmLabelPending={t("common.deleting")}
            cancelLabel={t("common.cancel")}
            confirmDisabled={confirmationText !== listName}
            isPending={deleteListMutation.isPending}
        >
            <Alert severity="warning" sx={{ mb: 3 }}>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>
                    {t("common.warningPermanentlyDelete")}
                </Typography>
                <Stack component="ul" sx={{ mt: 1, mb: 0, pl: 2 }}>
                    <Typography component="li" variant="body2">
                        {t("lists.entireListAndSettings")}
                    </Typography>
                    <Typography component="li" variant="body2">
                        {t("lists.allListItemsFuture")}
                    </Typography>
                    <Typography component="li" variant="body2">
                        {t("lists.allAssociatedDataHistory")}
                    </Typography>
                </Stack>
            </Alert>

            <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                {t("lists.confirmTypeListName")} <strong>{listName}</strong>
            </Typography>
            <TextField
                fullWidth
                variant="outlined"
                value={confirmationText}
                onChange={(e) => setConfirmationText(e.target.value)}
                placeholder={t("lists.typeNameToConfirm", { listName })}
                disabled={deleteListMutation.isPending}
                error={confirmationMismatch}
                helperText={
                    confirmationMismatch ? t("lists.nameDoesntMatch") : ""
                }
            />
        </ConfirmDialog>
    );
};

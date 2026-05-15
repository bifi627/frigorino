import { Box, TextField, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ConfirmDialog } from "../../../components/dialogs/ConfirmDialog";
import { useDeleteHousehold } from "../useDeleteHousehold";

interface DeleteHouseholdDialogProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    householdName: string;
}

export const DeleteHouseholdDialog = ({
    open,
    onClose,
    householdId,
    householdName,
}: DeleteHouseholdDialogProps) => {
    const { t } = useTranslation();
    const [confirmationText, setConfirmationText] = useState("");
    const deleteHouseholdMutation = useDeleteHousehold();

    useEffect(() => {
        if (!open) {
            setConfirmationText("");
        }
    }, [open]);

    const handleConfirm = () => {
        if (confirmationText === householdName) {
            deleteHouseholdMutation.mutate(householdId);
        }
    };

    const confirmationMismatch =
        confirmationText.length > 0 && confirmationText !== householdName;

    return (
        <ConfirmDialog
            open={open}
            onClose={onClose}
            onConfirm={handleConfirm}
            title={t("household.deleteHousehold")}
            description={
                <>
                    {t("common.confirmDelete")} "{householdName}
                    "? {t("lists.actionCannotBeUndone")}
                </>
            }
            confirmLabel={t("household.deleteHousehold")}
            confirmLabelPending={t("common.deleting")}
            cancelLabel={t("common.cancel")}
            confirmDisabled={confirmationText !== householdName}
            isPending={deleteHouseholdMutation.isPending}
            confirmTestId="household-delete-confirm-button"
            cancelTestId="household-delete-cancel-button"
        >
            <Box
                sx={{
                    bgcolor: "error.light",
                    p: 2,
                    border: 1,
                    borderColor: "error.main",
                    mb: 3,
                }}
            >
                <Typography
                    variant="body2"
                    color="error.dark"
                    sx={{ fontWeight: 500 }}
                >
                    {t("common.warningPermanentlyDelete")}
                </Typography>
                <Typography
                    variant="body2"
                    color="error.dark"
                    sx={{ mt: 1, ml: 2 }}
                >
                    {t("household.allHouseholdDataSettings")}
                </Typography>
                <Typography variant="body2" color="error.dark" sx={{ ml: 2 }}>
                    {t("household.allMemberAssociations")}
                </Typography>
                <Typography variant="body2" color="error.dark" sx={{ ml: 2 }}>
                    {t("household.allSharedContentFuture")}
                </Typography>
            </Box>

            <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                {t("household.confirmTypeHouseholdName")}{" "}
                <strong>{householdName}</strong>
            </Typography>
            <TextField
                fullWidth
                variant="outlined"
                slotProps={{
                    htmlInput: {
                        "data-testid": "household-delete-confirm-input",
                    },
                }}
                value={confirmationText}
                onChange={(e) => setConfirmationText(e.target.value)}
                placeholder={t("household.typeHouseholdNameToConfirm", {
                    householdName,
                })}
                disabled={deleteHouseholdMutation.isPending}
                error={confirmationMismatch}
                helperText={confirmationMismatch ? t("lists.nameDoesntMatch") : ""}
            />
        </ConfirmDialog>
    );
};

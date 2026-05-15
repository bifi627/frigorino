import {
    Box,
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    TextField,
    Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
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

    return (
        <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
            <DialogTitle sx={{ pb: 1 }}>
                <Typography
                    variant="h6"
                    component="div"
                    sx={{ fontWeight: 600 }}
                >
                    {t("household.deleteHousehold")}
                </Typography>
            </DialogTitle>
            <DialogContent>
                <DialogContentText sx={{ mb: 2 }}>
                    {t("common.confirmDelete")} "{householdName}
                    "? {t("lists.actionCannotBeUndone")}
                </DialogContentText>

                <Box
                    sx={{
                        bgcolor: "error.light",
                        borderRadius: 1,
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
                    <Typography
                        variant="body2"
                        color="error.dark"
                        sx={{ ml: 2 }}
                    >
                        {t("household.allMemberAssociations")}
                    </Typography>
                    <Typography
                        variant="body2"
                        color="error.dark"
                        sx={{ ml: 2 }}
                    >
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
                    error={
                        confirmationText.length > 0 &&
                        confirmationText !== householdName
                    }
                    helperText={
                        confirmationText.length > 0 &&
                        confirmationText !== householdName
                            ? t("lists.nameDoesntMatch")
                            : ""
                    }
                    sx={{
                        "& .MuiOutlinedInput-root": {
                            borderRadius: 2,
                        },
                    }}
                />
            </DialogContent>
            <DialogActions sx={{ p: 3, pt: 1 }}>
                <Button
                    data-testid="household-delete-cancel-button"
                    onClick={onClose}
                    disabled={deleteHouseholdMutation.isPending}
                    sx={{ borderRadius: 2 }}
                >
                    {t("common.cancel")}
                </Button>
                <Button
                    data-testid="household-delete-confirm-button"
                    onClick={handleConfirm}
                    color="error"
                    variant="contained"
                    disabled={
                        deleteHouseholdMutation.isPending ||
                        confirmationText !== householdName
                    }
                    sx={{
                        borderRadius: 2,
                        fontWeight: 600,
                        minWidth: 120,
                    }}
                >
                    {deleteHouseholdMutation.isPending
                        ? t("common.deleting")
                        : t("household.deleteHousehold")}
                </Button>
            </DialogActions>
        </Dialog>
    );
};

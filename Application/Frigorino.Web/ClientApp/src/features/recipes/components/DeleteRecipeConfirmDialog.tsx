import { Alert, Stack, TextField, Typography } from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ConfirmDialog } from "../../../components/dialogs/ConfirmDialog";
import { useDeleteRecipe } from "../useDeleteRecipe";

interface DeleteRecipeConfirmDialogProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    recipeId: number;
    recipeName: string;
}

export const DeleteRecipeConfirmDialog = ({
    open,
    onClose,
    householdId,
    recipeId,
    recipeName,
}: DeleteRecipeConfirmDialogProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [confirmationText, setConfirmationText] = useState("");
    const deleteRecipeMutation = useDeleteRecipe();

    // Clear the typed confirmation on every close path — cancel, backdrop and escape all funnel
    // through onClose — so the next open starts empty (replaces a reset-on-close effect).
    const handleClose = () => {
        setConfirmationText("");
        onClose();
    };

    const handleConfirm = () => {
        if (confirmationText !== recipeName) return;
        deleteRecipeMutation.mutate(
            { path: { householdId, recipeId } },
            {
                onSuccess: () => {
                    handleClose();
                    navigate({ to: "/recipes" });
                },
            },
        );
    };

    const confirmationMismatch =
        confirmationText.length > 0 && confirmationText !== recipeName;

    return (
        <ConfirmDialog
            open={open}
            onClose={handleClose}
            onConfirm={handleConfirm}
            title={t("recipes.deleteRecipe")}
            description={
                <>
                    {t("common.confirmDelete")} "{recipeName}"?
                </>
            }
            confirmLabel={t("recipes.deleteRecipe")}
            confirmLabelPending={t("common.deleting")}
            cancelLabel={t("common.cancel")}
            confirmDisabled={confirmationText !== recipeName}
            isPending={deleteRecipeMutation.isPending}
        >
            <Alert severity="warning" sx={{ mb: 3 }}>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>
                    {t("common.warningPermanentlyDelete")}
                </Typography>
                <Stack component="ul" sx={{ mt: 1, mb: 0, pl: 2 }}>
                    <Typography component="li" variant="body2">
                        {recipeName}
                    </Typography>
                </Stack>
            </Alert>

            <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                {t("recipes.confirmTypeRecipeName")}{" "}
                <strong>{recipeName}</strong>
            </Typography>
            <TextField
                fullWidth
                variant="outlined"
                value={confirmationText}
                onChange={(e) => setConfirmationText(e.target.value)}
                placeholder={t("recipes.typeRecipeNameToConfirm")}
                disabled={deleteRecipeMutation.isPending}
                error={confirmationMismatch}
                helperText={
                    confirmationMismatch ? t("lists.nameDoesntMatch") : ""
                }
            />
        </ConfirmDialog>
    );
};

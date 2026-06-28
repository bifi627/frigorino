import {
    Alert,
    Button,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    TextField,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useImportRecipe } from "../useImportRecipe";

interface ImportRecipeSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
}

export const ImportRecipeSheet = ({
    open,
    onClose,
    householdId,
}: ImportRecipeSheetProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const importRecipe = useImportRecipe();
    const [url, setUrl] = useState("");

    const messageFor = (error: unknown): string => {
        const code = (error as { code?: string } | null)?.code;
        if (code === "no_recipe_found") {
            return t("recipes.import.noRecipeFound");
        }
        if (code === "fetch_failed") {
            return t("recipes.import.fetchFailed");
        }
        // 400 ValidationProblem (invalid_url) has an { errors: { Url: [...] } } body and no code.
        const errors = (error as { errors?: Record<string, string[]> } | null)
            ?.errors;
        if (errors && Object.keys(errors).length > 0) {
            return t("recipes.import.invalidUrl");
        }
        return t("common.errorOccurred");
    };

    const handleClose = () => {
        importRecipe.reset();
        setUrl("");
        onClose();
    };

    const handleSubmit = async () => {
        try {
            const recipe = await importRecipe.mutateAsync({
                path: { householdId },
                body: { url: url.trim() },
            });
            toast.success(t("recipes.import.success"));
            onClose();
            setUrl("");
            navigate({
                to: "/recipes/$recipeId/edit",
                params: { recipeId: String(recipe.id) },
            });
        } catch {
            // Error is surfaced inline via importRecipe.error below.
        }
    };

    return (
        <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
            <DialogTitle>{t("recipes.import.title")}</DialogTitle>
            <DialogContent>
                <TextField
                    autoFocus
                    fullWidth
                    type="url"
                    label={t("recipes.import.urlLabel")}
                    placeholder={t("recipes.import.urlPlaceholder")}
                    value={url}
                    onChange={(e) => setUrl(e.target.value)}
                    slotProps={{
                        htmlInput: { "data-testid": "recipe-import-url" },
                    }}
                    sx={{ mt: 1 }}
                />
                {importRecipe.isError && (
                    <Alert
                        severity="error"
                        sx={{ mt: 2 }}
                        data-testid="recipe-import-error"
                    >
                        {messageFor(importRecipe.error)}
                    </Alert>
                )}
            </DialogContent>
            <DialogActions>
                <Button onClick={handleClose}>{t("common.cancel")}</Button>
                <Button
                    variant="contained"
                    onClick={handleSubmit}
                    disabled={!url.trim() || importRecipe.isPending}
                    startIcon={
                        importRecipe.isPending ? (
                            <CircularProgress size={16} />
                        ) : undefined
                    }
                    data-testid="recipe-import-submit"
                >
                    {importRecipe.isPending
                        ? t("recipes.import.importing")
                        : t("recipes.import.submit")}
                </Button>
            </DialogActions>
        </Dialog>
    );
};

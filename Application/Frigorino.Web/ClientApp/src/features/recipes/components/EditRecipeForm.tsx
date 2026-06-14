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
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";
import { useUpdateRecipe } from "../useUpdateRecipe";

interface EditRecipeFormProps {
    householdId: number;
    recipe: RecipeResponse;
}

export const EditRecipeForm = ({
    householdId,
    recipe,
}: EditRecipeFormProps) => {
    const { t } = useTranslation();
    const router = useRouter();
    const updateRecipeMutation = useUpdateRecipe();
    // Seeded once on mount. The parent keys this form by recipe.id, so switching to a different
    // recipe remounts and reseeds — no reset-on-prop effect (which would also clobber edits).
    const [editedName, setEditedName] = useState(recipe.name || "");
    const [editedDescription, setEditedDescription] = useState(
        recipe.description ?? "",
    );
    const [editedServings, setEditedServings] = useState(
        recipe.servings != null ? String(recipe.servings) : "",
    );

    const isFormValid = editedName.trim().length > 0;
    const isPending = updateRecipeMutation.isPending;

    const handleSave = () => {
        if (!recipe.id) return;
        updateRecipeMutation.mutate(
            {
                path: { householdId, recipeId: recipe.id },
                body: {
                    name: editedName.trim(),
                    description: editedDescription.trim() || null,
                    servings:
                        editedServings === "" ? null : Number(editedServings),
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
                        label={t("recipes.recipeName")}
                        value={editedName}
                        onChange={(e) => setEditedName(e.target.value)}
                        fullWidth
                        required
                        error={editedName.trim().length === 0}
                        helperText={
                            editedName.trim().length === 0
                                ? t("recipes.recipeNameRequired")
                                : ""
                        }
                    />

                    <TextField
                        label={t("recipes.description")}
                        value={editedDescription}
                        onChange={(e) => setEditedDescription(e.target.value)}
                        fullWidth
                        multiline
                        minRows={2}
                        placeholder={t("recipes.descriptionPlaceholder")}
                        slotProps={{ htmlInput: { maxLength: 1000 } }}
                    />

                    <TextField
                        type="number"
                        label={t("recipes.servings")}
                        value={editedServings}
                        onChange={(e) => setEditedServings(e.target.value)}
                        sx={{ width: 140 }}
                        slotProps={{
                            htmlInput: {
                                min: 1,
                                max: 99,
                                "data-testid": "recipe-servings-input",
                            },
                        }}
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
                            data-testid="recipe-edit-save-button"
                            sx={{ minWidth: 100, fontWeight: 600 }}
                        >
                            {isPending ? t("common.saving") : t("common.save")}
                        </Button>
                    </Box>
                </Stack>
            </CardContent>
        </Card>
    );
};

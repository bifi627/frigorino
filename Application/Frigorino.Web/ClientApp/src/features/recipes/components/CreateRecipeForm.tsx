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
import { useCreateRecipe } from "../useCreateRecipe";

interface CreateRecipeFormProps {
    householdId: number;
}

export const CreateRecipeForm = ({ householdId }: CreateRecipeFormProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const createRecipeMutation = useCreateRecipe();
    const [name, setName] = useState("");
    const [description, setDescription] = useState("");
    const [servings, setServings] = useState("");

    const isLoading = createRecipeMutation.isPending;
    const error: unknown = createRecipeMutation.error;
    const isInvalid = !name.trim() && name.length > 0;

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (!name.trim()) return;

        try {
            const response = await createRecipeMutation.mutateAsync({
                path: { householdId },
                body: {
                    name: name.trim(),
                    description: description.trim() || null,
                    servings: servings === "" ? null : Number(servings),
                },
            });
            if (response?.id) {
                navigate({
                    to: "/recipes/$recipeId/edit",
                    params: { recipeId: response.id.toString() },
                    replace: true,
                });
            }
        } catch (err) {
            console.error("Failed to create recipe:", err);
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
                                {t("common.name")} *
                            </Typography>
                            <TextField
                                fullWidth
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                                disabled={isLoading}
                                error={isInvalid}
                                helperText={
                                    isInvalid
                                        ? t("recipes.recipeNameRequired")
                                        : ""
                                }
                            />
                        </Box>

                        <Box>
                            <Typography
                                variant="subtitle1"
                                sx={{ fontWeight: 600, mb: 1 }}
                            >
                                {t("recipes.description")}
                            </Typography>
                            <TextField
                                fullWidth
                                multiline
                                minRows={2}
                                value={description}
                                onChange={(e) => setDescription(e.target.value)}
                                disabled={isLoading}
                                placeholder={t(
                                    "recipes.descriptionPlaceholder",
                                )}
                                slotProps={{ htmlInput: { maxLength: 1000 } }}
                            />
                        </Box>

                        <Box>
                            <Typography
                                variant="subtitle1"
                                sx={{ fontWeight: 600, mb: 1 }}
                            >
                                {t("recipes.servings")}
                            </Typography>
                            <TextField
                                type="number"
                                value={servings}
                                onChange={(e) => setServings(e.target.value)}
                                disabled={isLoading}
                                sx={{ width: 120 }}
                                slotProps={{
                                    htmlInput: {
                                        min: 1,
                                        max: 99,
                                        "data-testid": "recipe-servings-input",
                                    },
                                }}
                            />
                        </Box>

                        <Button
                            data-testid="recipe-create-submit-button"
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
                                : t("recipes.createRecipe")}
                        </Button>
                    </Stack>
                </form>
            </CardContent>
        </Card>
    );
};

import { Add, Download } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Divider,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { recipeImportErrorMessage } from "../recipeImportError";
import { useCreateRecipe } from "../useCreateRecipe";
import { useImportRecipe } from "../useImportRecipe";
import { usePreviewRecipeImport } from "../usePreviewRecipeImport";
import { RecipeImportPreviewCard } from "./RecipeImportPreviewCard";

interface CreateRecipeFormProps {
    householdId: number;
}

// A non-empty value that parses as an http(s) URL. The backend revalidates — this is UX only.
const isValidHttpUrl = (value: string): boolean => {
    try {
        const u = new URL(value.trim());
        return u.protocol === "http:" || u.protocol === "https:";
    } catch {
        return false;
    }
};

export const CreateRecipeForm = ({ householdId }: CreateRecipeFormProps) => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const createRecipeMutation = useCreateRecipe();
    const importRecipeMutation = useImportRecipe();
    const previewMutation = usePreviewRecipeImport();
    const [name, setName] = useState("");
    const [description, setDescription] = useState("");
    const [servings, setServings] = useState("");
    const [url, setUrl] = useState("");

    const isBusy =
        createRecipeMutation.isPending ||
        importRecipeMutation.isPending ||
        previewMutation.isPending;
    const createError: unknown = createRecipeMutation.error;
    const isInvalid = !name.trim() && name.length > 0;

    const trimmedUrl = url.trim();
    const urlInvalid = trimmedUrl.length > 0 && !isValidHttpUrl(trimmedUrl);
    const hasPreview = previewMutation.data !== undefined;
    const showPreview =
        previewMutation.isPending || previewMutation.isError || hasPreview;

    const handlePeek = (event: React.FormEvent) => {
        event.preventDefault();
        if (!isValidHttpUrl(trimmedUrl) || isBusy) {
            return;
        }
        previewMutation.mutate({ body: { url: trimmedUrl } });
    };

    const handleConfirmImport = async () => {
        if (!isValidHttpUrl(trimmedUrl) || isBusy) {
            return;
        }
        try {
            // Typed name/description (if any) win over the parsed page — the slice prefers them.
            const recipe = await importRecipeMutation.mutateAsync({
                path: { householdId },
                body: {
                    url: trimmedUrl,
                    name: name.trim() || null,
                    description: description.trim() || null,
                },
            });
            toast.success(t("recipes.import.success"));
            navigate({
                to: "/recipes/$recipeId/edit",
                params: { recipeId: String(recipe.id) },
                replace: true,
            });
        } catch {
            // Surfaced inline via importRecipeMutation.error below.
        }
    };

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (!name.trim() || isBusy) {
            return;
        }
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
                <Stack spacing={3}>
                    <form onSubmit={handlePeek}>
                        <Stack spacing={2}>
                            <Typography
                                variant="subtitle1"
                                sx={{ fontWeight: 600 }}
                            >
                                {t("recipes.import.open")}
                            </Typography>

                            <Stack
                                direction="row"
                                spacing={1}
                                sx={{ alignItems: "flex-start" }}
                            >
                                <TextField
                                    fullWidth
                                    type="url"
                                    placeholder={t(
                                        "recipes.import.urlPlaceholder",
                                    )}
                                    value={url}
                                    onChange={(e) => {
                                        setUrl(e.target.value);
                                        // Editing the URL invalidates a prior peek — drop its result
                                        // (or error) so the confirm button/card hides and the user
                                        // must re-peek the new URL (else we'd import a URL different
                                        // from the one previewed).
                                        if (
                                            previewMutation.isSuccess ||
                                            previewMutation.isError
                                        ) {
                                            previewMutation.reset();
                                        }
                                    }}
                                    disabled={isBusy}
                                    error={urlInvalid}
                                    helperText={
                                        urlInvalid
                                            ? t("recipes.import.invalidUrl")
                                            : ""
                                    }
                                    slotProps={{
                                        htmlInput: {
                                            "data-testid": "recipe-import-url",
                                        },
                                    }}
                                />
                                <Button
                                    type="submit"
                                    variant="outlined"
                                    disabled={
                                        !trimmedUrl || urlInvalid || isBusy
                                    }
                                    startIcon={
                                        previewMutation.isPending ? (
                                            <CircularProgress
                                                size={16}
                                                color="inherit"
                                            />
                                        ) : (
                                            <Download />
                                        )
                                    }
                                    sx={{ flexShrink: 0, mt: 0.5 }}
                                    data-testid="recipe-import-submit"
                                >
                                    {t("recipes.import.preview")}
                                </Button>
                            </Stack>

                            {showPreview ? (
                                <RecipeImportPreviewCard
                                    isPending={previewMutation.isPending}
                                    isError={previewMutation.isError}
                                    error={previewMutation.error}
                                    preview={previewMutation.data}
                                />
                            ) : null}

                            {hasPreview ? (
                                <Button
                                    variant="contained"
                                    onClick={handleConfirmImport}
                                    disabled={isBusy}
                                    startIcon={
                                        importRecipeMutation.isPending ? (
                                            <CircularProgress
                                                size={16}
                                                color="inherit"
                                            />
                                        ) : (
                                            <Download />
                                        )
                                    }
                                    data-testid="recipe-import-confirm"
                                >
                                    {importRecipeMutation.isPending
                                        ? t("recipes.import.importing")
                                        : t("recipes.import.submit")}
                                </Button>
                            ) : null}

                            {importRecipeMutation.isError ? (
                                <Alert severity="error">
                                    {recipeImportErrorMessage(
                                        importRecipeMutation.error,
                                        t,
                                    )}
                                </Alert>
                            ) : null}
                        </Stack>
                    </form>

                    <Divider>{t("recipes.import.orManually")}</Divider>

                    <form onSubmit={handleSubmit}>
                        <Stack spacing={3}>
                            {createError ? (
                                <Alert severity="error">
                                    {createError instanceof Error
                                        ? createError.message
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
                                    disabled={isBusy}
                                    error={isInvalid}
                                    helperText={
                                        isInvalid
                                            ? t("recipes.recipeNameRequired")
                                            : ""
                                    }
                                    slotProps={{
                                        htmlInput: {
                                            "data-testid": "recipe-create-name",
                                        },
                                    }}
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
                                    onChange={(e) =>
                                        setDescription(e.target.value)
                                    }
                                    disabled={isBusy}
                                    placeholder={t(
                                        "recipes.descriptionPlaceholder",
                                    )}
                                    slotProps={{
                                        htmlInput: { maxLength: 1000 },
                                    }}
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
                                    onChange={(e) =>
                                        setServings(e.target.value)
                                    }
                                    disabled={isBusy}
                                    sx={{ width: 120 }}
                                    slotProps={{
                                        htmlInput: {
                                            min: 1,
                                            max: 99,
                                            "data-testid":
                                                "recipe-servings-input",
                                        },
                                    }}
                                />
                            </Box>

                            <Button
                                data-testid="recipe-create-submit-button"
                                type="submit"
                                variant="contained"
                                size="large"
                                disabled={isBusy || !name.trim()}
                                startIcon={
                                    createRecipeMutation.isPending ? (
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
                                {createRecipeMutation.isPending
                                    ? t("common.creating")
                                    : t("recipes.createRecipe")}
                            </Button>
                        </Stack>
                    </form>
                </Stack>
            </CardContent>
        </Card>
    );
};

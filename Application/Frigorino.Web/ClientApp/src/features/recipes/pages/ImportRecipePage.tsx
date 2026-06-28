import {
    Alert,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    List,
    ListItemButton,
    ListItemText,
    Stack,
    Typography,
} from "@mui/material";
import { getRouteApi, useNavigate } from "@tanstack/react-router";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { pageContainerSx } from "../../../theme";
import { useUserHouseholds } from "../../households/useUserHouseholds";
import { useSetCurrentHousehold } from "../../me/activeHousehold/useSetCurrentHousehold";
import { RecipeImportPreviewCard } from "../components/RecipeImportPreviewCard";
import { useImportRecipe } from "../useImportRecipe";
import { usePreviewRecipeImport } from "../usePreviewRecipeImport";

const routeApi = getRouteApi("/recipes/import");

// Receiver for the PWA share-target: peek the shared URL, pick a household (skipped when there's
// only one), then import into it. The chosen household MUST become active before navigating to the
// edit page — that page resolves its household from useCurrentHousehold, so a non-active import
// would 404.
export const ImportRecipePage = () => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { sharedUrl } = routeApi.useSearch();
    const previewMutation = usePreviewRecipeImport();
    const importMutation = useImportRecipe();
    const setCurrentHousehold = useSetCurrentHousehold();
    const { data: households } = useUserHouseholds();
    const peekedRef = useRef(false);

    useEffect(() => {
        if (sharedUrl && !peekedRef.current) {
            peekedRef.current = true; // ref-guard: fire the peek once (StrictMode double-invokes effects)
            previewMutation.mutate({ body: { url: sharedUrl } });
        }
    }, [sharedUrl, previewMutation]);

    const isBusy = setCurrentHousehold.isPending || importMutation.isPending;
    const canImport = previewMutation.data !== undefined && !isBusy;

    const handleImport = async (targetHouseholdId: number) => {
        if (!sharedUrl || !canImport) {
            return;
        }
        try {
            await setCurrentHousehold.mutateAsync({
                body: { householdId: targetHouseholdId },
            });
            const recipe = await importMutation.mutateAsync({
                path: { householdId: targetHouseholdId },
                body: { url: sharedUrl },
            });
            navigate({
                to: "/recipes/$recipeId/edit",
                params: { recipeId: String(recipe.id) },
                replace: true,
            });
        } catch {
            // Surfaced inline via importMutation.error below.
        }
    };

    if (!sharedUrl) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error" data-testid="recipe-import-error">
                    {t("recipes.import.noRecipeFound")}
                    <Button
                        onClick={() => navigate({ to: "/recipes/create" })}
                        sx={{ mt: 1, display: "block" }}
                    >
                        {t("recipes.createRecipe")}
                    </Button>
                </Alert>
            </Container>
        );
    }

    const multiHousehold = (households?.length ?? 0) > 1;
    const firstHousehold = households?.[0];

    return (
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Stack spacing={3}>
                <Typography
                    variant="h5"
                    component="h1"
                    sx={{ fontWeight: 600 }}
                >
                    {t("recipes.import.shareTitle")}
                </Typography>

                <RecipeImportPreviewCard
                    isPending={previewMutation.isPending}
                    isError={previewMutation.isError}
                    error={previewMutation.error}
                    preview={previewMutation.data}
                />

                {importMutation.isError ? (
                    <Alert severity="error">{t("common.errorOccurred")}</Alert>
                ) : null}

                {multiHousehold ? (
                    <Card variant="outlined">
                        <CardContent>
                            <Typography variant="subtitle2" sx={{ mb: 1 }}>
                                {t("recipes.import.chooseHousehold")}
                            </Typography>
                            <List disablePadding>
                                {households?.map((h) => (
                                    <ListItemButton
                                        key={h.id}
                                        disabled={!canImport}
                                        onClick={() => handleImport(h.id!)}
                                        data-testid="recipe-import-household"
                                    >
                                        <ListItemText primary={h.name} />
                                        {isBusy ? (
                                            <CircularProgress size={16} />
                                        ) : null}
                                    </ListItemButton>
                                ))}
                            </List>
                        </CardContent>
                    </Card>
                ) : (
                    <Button
                        variant="contained"
                        size="large"
                        disabled={!canImport || !firstHousehold}
                        onClick={() =>
                            firstHousehold && handleImport(firstHousehold.id!)
                        }
                        startIcon={
                            isBusy ? (
                                <CircularProgress size={16} color="inherit" />
                            ) : undefined
                        }
                        data-testid="recipe-import-confirm"
                    >
                        {t("recipes.import.submit")}
                    </Button>
                )}
            </Stack>
        </Container>
    );
};

import { Add } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    Stack,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";
import { pageContainerSx } from "../../../theme";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { RecipeActionsMenu } from "../components/RecipeActionsMenu";
import { RecipeSummaryCard } from "../components/RecipeSummaryCard";
import { useDeleteRecipe } from "../useDeleteRecipe";
import { useHouseholdRecipes } from "../useHouseholdRecipes";

export const RecipesPage = () => {
    const navigate = useNavigate();
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: recipes,
        isLoading,
        error,
    } = useHouseholdRecipes(householdId, householdId > 0);
    const deleteRecipeMutation = useDeleteRecipe();

    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [selectedRecipe, setSelectedRecipe] = useState<RecipeResponse | null>(
        null,
    );

    const handleBack = () => navigate({ to: "/" });
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const handleCreateRecipe = () => navigate({ to: "/recipes/create" as any });
    const handleRecipeClick = (recipeId: number) =>
        // Routes are wired in T14 (routeTree.gen.ts regeneration); cast until then.
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        navigate({ to: `/recipes/${recipeId}/view` as any });

    const handleMenuOpen = (
        event: React.MouseEvent<HTMLElement>,
        recipe: RecipeResponse,
    ) => {
        setAnchorEl(event.currentTarget);
        setSelectedRecipe(recipe);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
        setSelectedRecipe(null);
    };

    const handleDeleteRecipe = () => {
        if (selectedRecipe?.id && householdId) {
            deleteRecipeMutation.mutate({
                path: { householdId, recipeId: selectedRecipe.id },
            });
        }
        handleMenuClose();
    };

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("recipes.selectHouseholdToViewRecipes")}
                    <Button
                        onClick={handleBack}
                        sx={{ mt: 1, display: "block" }}
                    >
                        {t("common.goBackToDashboard")}
                    </Button>
                </Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={t("recipes.recipes")}
                section="recipes"
                directActions={[
                    { icon: <Add />, onClick: handleCreateRecipe },
                ]}
                menuActions={[]}
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
                {isLoading && (
                    <Box
                        sx={{
                            display: "flex",
                            justifyContent: "center",
                            py: 4,
                        }}
                    >
                        <CircularProgress />
                    </Box>
                )}
                {error && (
                    <Alert severity="error" sx={{ mb: 3 }}>
                        {t("recipes.failedToLoadRecipes")}
                    </Alert>
                )}
                {recipes && recipes.length === 0 && !isLoading && (
                    <Card elevation={1} sx={{ textAlign: "center", py: 4 }}>
                        <CardContent>
                            <Typography variant="h6" gutterBottom>
                                {t("recipes.noRecipesYet")}
                            </Typography>
                            <Typography
                                variant="body2"
                                sx={{
                                    color: "text.secondary",
                                    mb: 3,
                                }}
                            >
                                {t("recipes.createFirstRecipe")}
                            </Typography>
                            <Button
                                variant="contained"
                                startIcon={<Add />}
                                onClick={handleCreateRecipe}
                                sx={{ fontWeight: 600 }}
                            >
                                {t("recipes.createYourFirstRecipe")}
                            </Button>
                        </CardContent>
                    </Card>
                )}
                {recipes && recipes.length > 0 && (
                    <Stack spacing={2}>
                        {recipes.map((recipe) => (
                            <RecipeSummaryCard
                                key={recipe.id}
                                recipe={recipe}
                                onClick={handleRecipeClick}
                                onMenuOpen={handleMenuOpen}
                                menuDisabled={deleteRecipeMutation.isPending}
                            />
                        ))}
                    </Stack>
                )}
                <RecipeActionsMenu
                    anchorEl={anchorEl}
                    onClose={handleMenuClose}
                    onDelete={handleDeleteRecipe}
                    isDeleting={deleteRecipeMutation.isPending}
                />
            </Container>
        </>
    );
};

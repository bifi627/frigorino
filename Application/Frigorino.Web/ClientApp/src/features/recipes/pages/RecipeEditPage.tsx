import { Delete } from "@mui/icons-material";
import { Alert, Box, Container, Skeleton } from "@mui/material";
import { useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { DeleteRecipeConfirmDialog } from "../components/DeleteRecipeConfirmDialog";
import { EditRecipeForm } from "../components/EditRecipeForm";
import { useRecipe } from "../useRecipe";

export const RecipeEditPage = () => {
    const { recipeId } = useParams({ from: "/recipes/$recipeId/edit" });
    const { t } = useTranslation();
    const recipeIdNum = parseInt(recipeId, 10);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

    const {
        currentHousehold,
        isLoading: householdLoading,
        error: householdError,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();

    const householdId = currentHousehold?.householdId ?? 0;
    const {
        data: recipe,
        isLoading: recipeLoading,
        error: recipeError,
    } = useRecipe(
        householdId,
        recipeIdNum,
        hasActiveHousehold && !isNaN(recipeIdNum),
    );

    const handleDeleteClick = () => setDeleteDialogOpen(true);

    const isLoading = householdLoading || recipeLoading;
    const error = householdError || recipeError;

    if (isLoading) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    <Skeleton
                        variant="rectangular"
                        height={40}
                        sx={{ mb: 1 }}
                    />
                    <Skeleton variant="text" width="60%" height={32} />
                </Box>
                <Skeleton variant="rectangular" height={200} />
            </Container>
        );
    }

    if (error) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("recipes.failedToLoadRecipe")}
                </Alert>
            </Container>
        );
    }

    if (!hasActiveHousehold || !householdId) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    if (!recipe) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="warning">
                    {t("recipes.failedToLoadRecipe")}
                </Alert>
            </Container>
        );
    }

    const recipeName = recipe.name || t("recipes.untitledRecipe");

    const menuActions: HeadNavigationAction[] = [
        {
            text: t("recipes.deleteRecipe"),
            icon: <Delete fontSize="small" color="error" />,
            onClick: handleDeleteClick,
            color: "error",
        },
    ];

    return (
        <>
            <PageHeadActionBar
                title={t("recipes.editRecipe")}
                section="recipes"
                maxWidth="md"
                directActions={[]}
                menuActions={menuActions}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <EditRecipeForm
                    key={recipe.id}
                    householdId={householdId}
                    recipe={recipe}
                />

                {recipe.id && (
                    <DeleteRecipeConfirmDialog
                        open={deleteDialogOpen}
                        onClose={() => setDeleteDialogOpen(false)}
                        householdId={householdId}
                        recipeId={recipe.id}
                        recipeName={recipeName}
                    />
                )}
            </Container>
        </>
    );
};

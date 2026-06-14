import { Add, Delete } from "@mui/icons-material";
import { Alert, Box, Button, Container, Skeleton, Stack } from "@mui/material";
import { useParams } from "@tanstack/react-router";
import { useCallback, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { CollapsibleSection } from "../../../components/shared/CollapsibleSection";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { SortableSectionList } from "../../../components/sortables/SortableSectionList";
import { usePersistedExpanded } from "../../../hooks/usePersistedExpanded";
import { usePersistedNumber } from "../../../hooks/usePersistedNumber";
import type { QuantityDto, RecipeItemResponse } from "../../../lib/api";
import { featureContentPx, pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { DeleteRecipeConfirmDialog } from "../components/DeleteRecipeConfirmDialog";
import { EditRecipeForm } from "../components/EditRecipeForm";
import { RecipeSectionCard } from "../items/components/RecipeSectionCard";
import { RecipeFooter } from "../items/components/RecipeFooter";
import { useCreateRecipeItem } from "../items/useCreateRecipeItem";
import { useRecipeExtractionPoll } from "../items/useRecipeExtractionPoll";
import { useRecipeItems } from "../items/useRecipeItems";
import { useRecipeRevision } from "../items/useRecipeRevision";
import { useUpdateRecipeItem } from "../items/useUpdateRecipeItem";
import { useCreateRecipeSection } from "../sections/useCreateRecipeSection";
import { useDeleteRecipeSection } from "../sections/useDeleteRecipeSection";
import { useRecipeSections } from "../sections/useRecipeSections";
import { useReorderRecipeSection } from "../sections/useReorderRecipeSection";
import { useRecipe } from "../useRecipe";

export const RecipeEditPage = () => {
    const { recipeId: recipeIdParam } = useParams({
        from: "/recipes/$recipeId/edit",
    });
    const { t } = useTranslation();
    const recipeId = parseInt(recipeIdParam, 10);

    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [editingItem, setEditingItem] = useState<RecipeItemResponse | null>(
        null,
    );
    const [detailsExpanded, setDetailsExpanded] = usePersistedExpanded(
        "recipe-edit-section:details",
        true,
    );
    const [openSectionId, setOpenSectionId] = usePersistedNumber(
        "recipe-edit:open-section",
        0,
    );
    const [pendingExtraction, setPendingExtraction] = useState<{
        id: number;
        extractionPending: boolean;
    } | null>(null);

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
        recipeId,
        hasActiveHousehold && !isNaN(recipeId),
    );

    const { data: items = [] } = useRecipeItems(
        householdId,
        recipeId,
        !!recipe,
    );
    useRecipeRevision(householdId, recipeId);

    const { data: sections = [] } = useRecipeSections(
        householdId,
        recipeId,
        !!recipe,
    );
    const createSection = useCreateRecipeSection();
    const deleteSection = useDeleteRecipeSection();
    const reorderSection = useReorderRecipeSection();

    const createMutation = useCreateRecipeItem();
    const updateMutation = useUpdateRecipeItem();

    // The open accordion section; falls back to the first section when none is chosen
    // or the chosen one was deleted. The composer (and item-create) targets it.
    const effectiveOpenSectionId =
        sections.find((s) => s.id === openSectionId)?.id ?? sections[0]?.id ?? 0;
    const composerVisible = effectiveOpenSectionId > 0;
    const openSectionItems = items.filter(
        (i) => i.sectionId === effectiveOpenSectionId,
    );

    const { isExtracting, extractingItemId } = useRecipeExtractionPoll(
        householdId,
        recipeId,
        pendingExtraction?.id ?? null,
        pendingExtraction?.extractionPending ?? false,
    );

    const scrollToLastItem = useCallback(() => {
        if (scrollContainerRef.current) {
            const listItems =
                scrollContainerRef.current.querySelectorAll(
                    ".MuiListItem-root",
                );
            const lastItem = listItems[listItems.length - 1];
            if (lastItem) {
                lastItem.scrollIntoView({
                    behavior: "smooth",
                    block: "center",
                });
            }
        }
    }, []);

    const handleAddItem = useCallback(
        async (text: string, comment: string | null) => {
            if (!householdId || !effectiveOpenSectionId) return;
            try {
                const created = await createMutation.mutateAsync({
                    path: { householdId, recipeId },
                    body: { sectionId: effectiveOpenSectionId, text, comment },
                });
                setPendingExtraction({
                    id: created.id,
                    extractionPending: created.extractionPending,
                });
            } catch {
                // createMutation.onError rolls back the optimistic item.
            }
        },
        [createMutation, householdId, recipeId, effectiveOpenSectionId],
    );

    const handleUpdateItem = useCallback(
        (
            text: string,
            quantity: QuantityDto | null,
            comment: string | null,
        ) => {
            if (editingItem?.id && householdId) {
                updateMutation.mutate({
                    path: { householdId, recipeId, itemId: editingItem.id },
                    body: {
                        text,
                        quantity,
                        clearQuantity: quantity === null,
                        comment,
                    },
                });
                setEditingItem(null);
            }
        },
        [editingItem, updateMutation, householdId, recipeId],
    );

    // One section open at a time. Collapsing the open section hides the composer, so drop
    // any in-progress item edit (its editor would otherwise vanish mid-edit).
    const handleToggleSection = useCallback(
        (sectionId: number, expanded: boolean) => {
            setOpenSectionId(expanded ? sectionId : 0);
            if (!expanded) {
                setEditingItem(null);
            }
        },
        [setOpenSectionId],
    );

    const handleAddSection = useCallback(async () => {
        if (!householdId) return;
        const created = await createSection.mutateAsync({
            path: { householdId, recipeId },
            body: { name: null, description: null },
        });
        setOpenSectionId(created.id);
    }, [createSection, householdId, recipeId, setOpenSectionId]);

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

    const menuActions: HeadNavigationAction[] = [
        {
            text: t("recipes.deleteRecipe"),
            icon: <Delete fontSize="small" color="error" />,
            onClick: () => setDeleteDialogOpen(true),
            color: "error",
        },
    ];

    return (
        <Box
            sx={{
                height: "calc(100dvh - 56px)",
                display: "flex",
                flexDirection: "column",
                overflow: "hidden",
            }}
        >
            <PageHeadActionBar
                title={t("recipes.editRecipe")}
                section="recipes"
                directActions={[]}
                menuActions={menuActions}
                menuButtonTestId="recipe-edit-menu-toggle"
            />

            <Box
                ref={scrollContainerRef}
                sx={{ flex: 1, overflow: "auto", minHeight: 0 }}
            >
                <Container
                    maxWidth="sm"
                    sx={{ px: featureContentPx, pt: 2, pb: 1 }}
                >
                    <Stack spacing={2}>
                        <CollapsibleSection
                            title={t("recipes.detailsSection")}
                            expanded={detailsExpanded}
                            onChange={setDetailsExpanded}
                            testId="recipe-section-details"
                        >
                            <EditRecipeForm
                                key={recipe.id}
                                householdId={householdId}
                                recipe={recipe}
                            />
                        </CollapsibleSection>

                        <SortableSectionList
                            sections={sections}
                            onReorder={async (sectionId, afterId) => {
                                await reorderSection.mutateAsync({
                                    path: { householdId, recipeId, sectionId },
                                    body: { afterId },
                                });
                            }}
                            renderSection={(section, dragHandle) => (
                                <RecipeSectionCard
                                    householdId={householdId}
                                    recipeId={recipeId}
                                    section={section}
                                    expanded={
                                        section.id === effectiveOpenSectionId
                                    }
                                    onToggle={(exp) =>
                                        handleToggleSection(section.id, exp)
                                    }
                                    canDelete={sections.length > 1}
                                    onDelete={() =>
                                        deleteSection.mutate({
                                            path: {
                                                householdId,
                                                recipeId,
                                                sectionId: section.id,
                                            },
                                        })
                                    }
                                    editingItem={editingItem}
                                    onEditItem={setEditingItem}
                                    isExtracting={isExtracting}
                                    extractingItemId={extractingItemId}
                                    dragHandle={dragHandle}
                                />
                            )}
                        />

                        <Button
                            startIcon={<Add />}
                            onClick={handleAddSection}
                            disabled={createSection.isPending}
                            data-testid="recipe-add-section"
                            sx={{ alignSelf: "flex-start" }}
                        >
                            {t("recipes.addSection")}
                        </Button>
                    </Stack>
                </Container>
            </Box>

            {composerVisible ? (
                <RecipeFooter
                    editingItem={editingItem}
                    existingItems={openSectionItems}
                    onAddItem={handleAddItem}
                    onUpdateItem={handleUpdateItem}
                    onCancelEdit={() => setEditingItem(null)}
                    isLoading={
                        createMutation.isPending || updateMutation.isPending
                    }
                    onScrollToLast={scrollToLastItem}
                />
            ) : null}

            {recipe.id ? (
                <DeleteRecipeConfirmDialog
                    open={deleteDialogOpen}
                    onClose={() => setDeleteDialogOpen(false)}
                    householdId={householdId}
                    recipeId={recipe.id}
                    recipeName={recipe.name || t("recipes.untitledRecipe")}
                />
            ) : null}
        </Box>
    );
};

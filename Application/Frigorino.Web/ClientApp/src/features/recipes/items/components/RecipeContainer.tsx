import { Container, Paper, Typography } from "@mui/material";
import { forwardRef } from "react";
import { useTranslation } from "react-i18next";
import { featureContentPx } from "../../../../theme";
import { SortableList } from "../../../../components/sortables/SortableList";
import type { RecipeItemResponse } from "../../../../lib/api";
import { matchesQuery } from "../../../../utils/searchUtils";
import { useDeleteRecipeItem } from "../useDeleteRecipeItem";
import { useRecipeItems } from "../useRecipeItems";
import { useReorderRecipeItem } from "../useReorderRecipeItem";
import { RecipeItemContent } from "./RecipeItemContent";

interface RecipeContainerProps {
    householdId: number;
    recipeId: number;
    editingItem: RecipeItemResponse | null;
    onEdit: (item: RecipeItemResponse) => void;
    isExtracting?: boolean;
    extractingItemId?: number | null;
    searchQuery?: string;
    multiplier?: number;
}

// Ingredients are searched across their text AND comment so ingredient notes
// (which live in `comment`) are matched too — mirrors ListContainer.
const searchableText = (item: RecipeItemResponse): string =>
    [item.text, item.comment].filter(Boolean).join(" ");

export const RecipeContainer = forwardRef<HTMLDivElement, RecipeContainerProps>(
    (
        {
            householdId,
            recipeId,
            editingItem,
            onEdit,
            isExtracting,
            extractingItemId,
            searchQuery = "",
            multiplier = 1,
        },
        ref,
    ) => {
        const {
            data: items = [],
            isLoading,
            error,
        } = useRecipeItems(householdId, recipeId);
        const deleteMutation = useDeleteRecipeItem();
        const reorderMutation = useReorderRecipeItem();
        const { t } = useTranslation();

        const trimmedQuery = searchQuery.trim();
        const filterActive = trimmedQuery.length > 0;
        const visibleItems = filterActive
            ? items.filter((item) =>
                  matchesQuery(searchableText(item), trimmedQuery),
              )
            : items;
        const showNoMatches =
            filterActive && !isLoading && !error && visibleItems.length === 0;

        return (
            <Container
                ref={ref}
                maxWidth="sm"
                data-testid="recipe-items"
                sx={{
                    flex: 1,
                    overflow: "auto",
                    px: featureContentPx,
                    py: 0,
                    minHeight: 0,
                }}
            >
                {showNoMatches ? (
                    <Paper
                        elevation={0}
                        data-testid="recipe-search-no-results"
                        sx={{
                            p: 3,
                            textAlign: "center",
                            border: "2px dashed",
                            borderColor: "divider",
                            mx: 1,
                        }}
                    >
                        <Typography variant="body2" color="text.secondary">
                            {t("recipes.noSearchMatches")}
                        </Typography>
                    </Paper>
                ) : (
                    <SortableList
                        items={visibleItems}
                        isLoading={isLoading}
                        error={error}
                        onReorder={async (itemId, afterId) => {
                            await reorderMutation.mutateAsync({
                                path: { householdId, recipeId, itemId },
                                body: { afterId },
                            });
                        }}
                        onToggleStatus={async () => {}}
                        onEdit={onEdit}
                        onDelete={async (itemId) => {
                            await deleteMutation.mutateAsync({
                                path: { householdId, recipeId, itemId },
                            });
                        }}
                        editingItem={editingItem}
                        showDragHandles={!filterActive}
                        isItemProcessing={(item) =>
                            Boolean(isExtracting) &&
                            item.id === extractingItemId
                        }
                        renderContent={(item) => (
                            <RecipeItemContent
                                item={item}
                                multiplier={multiplier}
                            />
                        )}
                    />
                )}
            </Container>
        );
    },
);

RecipeContainer.displayName = "RecipeContainer";

import { Container } from "@mui/material";
import { forwardRef } from "react";
import { featureContentPx } from "../../../../theme";
import { SortableList } from "../../../../components/sortables/SortableList";
import type { RecipeItemResponse } from "../../../../lib/api";
import { useDeleteRecipeItem } from "../useDeleteRecipeItem";
import { useRecipeItems } from "../useRecipeItems";
import { useReorderRecipeItem } from "../useReorderRecipeItem";
import { RecipeItemContent } from "./RecipeItemContent";

interface RecipeContainerProps {
    householdId: number;
    recipeId: number;
    sectionId: number;
    editingItem: RecipeItemResponse | null;
    onEdit: (item: RecipeItemResponse) => void;
    /** Per-row predicate: true while the row's async quantity extraction is still pending. */
    isItemExtracting?: (id: number) => boolean;
    // When false, the container flows in its parent's scroll instead of being its own
    // scroll region (used on the edit page where it shares scroll with the metadata form).
    scrollable?: boolean;
}

export const RecipeContainer = forwardRef<HTMLDivElement, RecipeContainerProps>(
    (
        {
            householdId,
            recipeId,
            sectionId,
            editingItem,
            onEdit,
            isItemExtracting,
            scrollable = true,
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

        // The items query holds the whole recipe; render only this section's slice.
        // The server enforces same-section reorder, and afterId is computed from the
        // section-scoped array, so the optimistic cache stays correct.
        const sectionItems = items.filter((i) => i.sectionId === sectionId);

        return (
            <Container
                ref={ref}
                maxWidth="sm"
                data-testid="recipe-items"
                sx={{
                    ...(scrollable
                        ? { flex: 1, overflow: "auto", minHeight: 0 }
                        : {}),
                    px: featureContentPx,
                    py: 0,
                }}
            >
                <SortableList
                    items={sectionItems}
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
                    showDragHandles={true}
                    isItemProcessing={(item) =>
                        isItemExtracting?.(item.id) ?? false
                    }
                    renderContent={(item) => <RecipeItemContent item={item} />}
                />
            </Container>
        );
    },
);

RecipeContainer.displayName = "RecipeContainer";

import { Container } from "@mui/material";
import { forwardRef } from "react";
import { SortableList } from "../../../../components/sortables/SortableList";
import type { ListItemResponse } from "../../../../lib/api";
import { useDeleteListItem } from "../useDeleteListItem";
import { useListItems } from "../useListItems";
import { useReorderListItem } from "../useReorderListItem";
import { useToggleListItemStatus } from "../useToggleListItemStatus";
import { ListItemContent } from "./ListItemContent";

interface ListContainerProps {
    householdId: number;
    listId: number;
    editingItem: ListItemResponse | null;
    onEdit: (item: ListItemResponse) => void;
    /** Opens edit mode with the quantity panel expanded (triggered by the quantity chip). */
    onEditQuantity: (item: ListItemResponse) => void;
    showDragHandles: boolean;
    isExtracting?: boolean;
    extractingItemId?: number | null;
}

export const ListContainer = forwardRef<HTMLDivElement, ListContainerProps>(
    (
        {
            householdId,
            listId,
            editingItem,
            onEdit,
            onEditQuantity,
            showDragHandles,
            isExtracting,
            extractingItemId,
        },
        ref,
    ) => {
        const {
            data: items = [],
            isLoading,
            error,
        } = useListItems(householdId, listId);
        const deleteMutation = useDeleteListItem();
        const toggleMutation = useToggleListItemStatus();
        const reorderMutation = useReorderListItem();

        return (
            <Container
                ref={ref}
                maxWidth="sm"
                sx={{
                    flex: 1,
                    overflow: "auto",
                    px: 3,
                    py: 0,
                    minHeight: 0,
                }}
            >
                <SortableList
                    items={items}
                    isLoading={isLoading}
                    error={error}
                    onReorder={async (itemId, afterId) => {
                        await reorderMutation.mutateAsync({
                            path: { householdId, listId, itemId },
                            body: { afterId },
                        });
                    }}
                    onToggleStatus={async (itemId) => {
                        await toggleMutation.mutateAsync({
                            path: { householdId, listId, itemId },
                        });
                    }}
                    onEdit={onEdit}
                    onDelete={async (itemId) => {
                        await deleteMutation.mutateAsync({
                            path: { householdId, listId, itemId },
                        });
                    }}
                    editingItem={editingItem}
                    showDragHandles={showDragHandles}
                    showCheckbox={true}
                    isItemProcessing={(item) =>
                        Boolean(isExtracting) && item.id === extractingItemId
                    }
                    renderContent={(item) => (
                        <ListItemContent
                            item={item}
                            onEditQuantity={() => onEditQuantity(item)}
                        />
                    )}
                />
            </Container>
        );
    },
);

ListContainer.displayName = "ListContainer";

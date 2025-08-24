import { Container } from "@mui/material";
import { forwardRef, memo, useCallback } from "react";
import {
    useDeleteInventoryItem,
    useInventoryItems,
} from "../../hooks/useInventoryItemQueries";
import type { InventoryItemDto } from "../../lib/api";
import { SortableList } from "../sortables/SortableList";
import { InventoryItemContent } from "./InventoryItemContent";

interface ContainerProps {
    inventoryId: number;
    editingItem: InventoryItemDto | null;
    onEdit: (item: InventoryItemDto) => void;
}

export const InventoryContainer = memo(
    forwardRef<HTMLDivElement, ContainerProps>(
        ({ inventoryId, editingItem, onEdit }, ref) => {
            // Fetch data and setup mutations at the container level
            const {
                data: items = [],
                isLoading,
                error,
            } = useInventoryItems(inventoryId);

            const deleteMutation = useDeleteInventoryItem();
            // const toggleMutation = useToggleListItemStatus();
            // const reorderMutation = useReorderListItem();

            // Create callback handlers for the sortable list
            // const handleReorder = useCallback(
            //     async (itemId: number, afterId: number): Promise<void> => {
            //         await reorderMutation.mutateAsync({
            //             householdId,
            //             listId,
            //             itemId,
            //             data: { afterId },
            //         });
            //     },
            //     [reorderMutation, householdId, listId],
            // );

            // const handleToggleStatus = useCallback(
            //     async (itemId: number): Promise<void> => {
            //         await toggleMutation.mutateAsync({
            //             householdId,
            //             listId,
            //             itemId,
            //         });
            //     },
            //     [toggleMutation, householdId, listId],
            // );

            const handleDelete = useCallback(
                async (itemId: number): Promise<void> => {
                    await deleteMutation.mutateAsync({
                        inventoryId,
                        itemId,
                    });
                },
                [deleteMutation, inventoryId],
            );

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
                        onReorder={async () => {}}
                        onToggleStatus={async () => {}}
                        onEdit={onEdit}
                        onDelete={handleDelete}
                        editingItem={editingItem}
                        showDragHandles={false}
                        renderContent={(item) => (
                            <InventoryItemContent item={item} />
                        )}
                    />
                </Container>
            );
        },
    ),
);

InventoryContainer.displayName = "InventoryContainer";

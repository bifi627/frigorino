import { Container } from "@mui/material";
import { forwardRef, memo, useCallback } from "react";
import {
    useDeleteInventoryItem,
    useInventoryItems,
    useReorderInventoryItem,
} from "../../hooks/useInventoryItemQueries";
import type { InventoryItemResponse } from "../../lib/api";
import { SortableList } from "../sortables/SortableList";
import { InventoryItemContent } from "./InventoryItemContent";

type SortMode = "custom" | "expiryDateAsc" | "expiryDateDesc";

interface ContainerProps {
    householdId: number;
    inventoryId: number;
    editingItem: InventoryItemResponse | null;
    onEdit: (item: InventoryItemResponse) => void;
    sortMode?: SortMode;
}

export const InventoryContainer = memo(
    forwardRef<HTMLDivElement, ContainerProps>(
        (
            {
                householdId,
                inventoryId,
                editingItem,
                onEdit,
                sortMode = "custom",
            },
            ref,
        ) => {
            // Fetch data and setup mutations at the container level
            const {
                data: items = [],
                isLoading,
                error,
            } = useInventoryItems(householdId, inventoryId);

            const deleteMutation = useDeleteInventoryItem();
            const reorderMutation = useReorderInventoryItem();

            // Create callback handlers for the sortable list
            const handleReorder = useCallback(
                async (itemId: number, afterId: number): Promise<void> => {
                    await reorderMutation.mutateAsync({
                        householdId,
                        inventoryId,
                        itemId,
                        data: { afterId },
                    });
                },
                [reorderMutation, householdId, inventoryId],
            );

            const handleDelete = useCallback(
                async (itemId: number): Promise<void> => {
                    await deleteMutation.mutateAsync({
                        householdId,
                        inventoryId,
                        itemId,
                    });
                },
                [deleteMutation, householdId, inventoryId],
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
                        onReorder={handleReorder}
                        onToggleStatus={async () => {}}
                        onEdit={onEdit}
                        onDelete={handleDelete}
                        editingItem={editingItem}
                        showDragHandles={sortMode === "custom"}
                        sortMode={sortMode}
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

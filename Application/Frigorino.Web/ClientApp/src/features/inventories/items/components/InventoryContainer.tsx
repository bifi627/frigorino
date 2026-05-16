import { Container } from "@mui/material";
import { forwardRef } from "react";
import { SortableList } from "../../../../components/sortables/SortableList";
import type { InventoryItemResponse } from "../../../../lib/api";
import { useDeleteInventoryItem } from "../useDeleteInventoryItem";
import { useInventoryItems } from "../useInventoryItems";
import { useReorderInventoryItem } from "../useReorderInventoryItem";
import { InventoryItemContent } from "./InventoryItemContent";

type SortMode = "custom" | "expiryDateAsc" | "expiryDateDesc";

interface InventoryContainerProps {
    householdId: number;
    inventoryId: number;
    editingItem: InventoryItemResponse | null;
    onEdit: (item: InventoryItemResponse) => void;
    sortMode?: SortMode;
}

export const InventoryContainer = forwardRef<
    HTMLDivElement,
    InventoryContainerProps
>(
    (
        { householdId, inventoryId, editingItem, onEdit, sortMode = "custom" },
        ref,
    ) => {
        const {
            data: items = [],
            isLoading,
            error,
        } = useInventoryItems(householdId, inventoryId);
        const deleteMutation = useDeleteInventoryItem();
        const reorderMutation = useReorderInventoryItem();

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
                            householdId,
                            inventoryId,
                            itemId,
                            data: { afterId },
                        });
                    }}
                    onToggleStatus={async () => {}}
                    onEdit={onEdit}
                    onDelete={async (itemId) => {
                        await deleteMutation.mutateAsync({
                            householdId,
                            inventoryId,
                            itemId,
                        });
                    }}
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
);

InventoryContainer.displayName = "InventoryContainer";

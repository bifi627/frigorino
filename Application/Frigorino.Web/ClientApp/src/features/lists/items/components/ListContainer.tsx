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
    showDragHandles: boolean;
}

export const ListContainer = forwardRef<HTMLDivElement, ListContainerProps>(
    (
        { householdId, listId, editingItem, onEdit, showDragHandles },
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
                            householdId,
                            listId,
                            itemId,
                            data: { afterId },
                        });
                    }}
                    onToggleStatus={async (itemId) => {
                        await toggleMutation.mutateAsync({
                            householdId,
                            listId,
                            itemId,
                        });
                    }}
                    onEdit={onEdit}
                    onDelete={async (itemId) => {
                        await deleteMutation.mutateAsync({
                            householdId,
                            listId,
                            itemId,
                        });
                    }}
                    editingItem={editingItem}
                    showDragHandles={showDragHandles}
                    showCheckbox={true}
                    renderContent={(item) => <ListItemContent item={item} />}
                />
            </Container>
        );
    },
);

ListContainer.displayName = "ListContainer";

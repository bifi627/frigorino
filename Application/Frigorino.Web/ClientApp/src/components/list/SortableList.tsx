import { useCallback, useRef, useState } from "react";
import { SortableItemList } from "../shared/SortableItemList";
import { AddItemInput } from "../shared/AddItemInput";
import {
    useCreateListItem,
    useDeleteListItem,
    useListItems,
    useReorderListItem,
    useToggleListItemStatus,
    useUpdateListItem,
    type CreateListItemRequest,
    type ListItemDto,
    type UpdateListItemRequest,
} from "../../hooks/useListItemQueries";

interface SortableListProps {
    householdId: number;
    listId: number;
}

export const SortableList = ({ householdId, listId }: SortableListProps) => {
    const [editingItem, setEditingItem] = useState<ListItemDto | null>(null);
    const dividerRef = useRef<HTMLHRElement | null>(null);

    // Queries and mutations
    const {
        data: items = [],
        isLoading,
        error,
    } = useListItems(householdId, listId);
    const createMutation = useCreateListItem();
    const updateMutation = useUpdateListItem();
    const deleteMutation = useDeleteListItem();
    const toggleMutation = useToggleListItemStatus();
    const reorderMutation = useReorderListItem();

    // Event handlers - memoized to prevent unnecessary re-renders
    const handleEditItem = useCallback((item: ListItemDto) => {
        setEditingItem(item);
    }, []);

    const handleCancelEdit = useCallback(() => {
        setEditingItem(null);
    }, []);

    const handleUpdateItem = useCallback(
        (data: UpdateListItemRequest) => {
            if (editingItem?.id) {
                updateMutation.mutate({
                    householdId,
                    listId,
                    itemId: editingItem.id,
                    data,
                });
                setEditingItem(null);
            }
        },
        [editingItem?.id, updateMutation, householdId, listId],
    );

    const handleUncheckExisting = useCallback(
        (itemId: number) => {
            toggleMutation.mutate({ householdId, listId, itemId });
        },
        [toggleMutation, householdId, listId],
    );

    const handleDeleteItem = useCallback(
        (itemId: number) => {
            deleteMutation.mutate({ householdId, listId, itemId });
        },
        [deleteMutation, householdId, listId],
    );

    const handleToggleStatus = useCallback(
        (itemId: number) => {
            toggleMutation.mutate({ householdId, listId, itemId });
        },
        [toggleMutation, householdId, listId],
    );

    const handleAddItem = useCallback(
        (data: CreateListItemRequest) => {
            dividerRef.current?.scrollIntoView();
            createMutation.mutate({
                householdId,
                listId,
                data,
            });
        },
        [createMutation, householdId, listId],
    );

    const handleReorderItem = useCallback(
        (itemId: number, afterId: number) => {
            reorderMutation.mutate({
                householdId,
                listId,
                itemId,
                data: { afterId },
            });
        },
        [reorderMutation, householdId, listId],
    );

    return (
        <>
            {/* Sortable List Items */}
            <SortableItemList
                items={items}
                isLoading={isLoading}
                error={error}
                editingItem={editingItem}
                onEdit={handleEditItem}
                onDelete={handleDeleteItem}
                onToggleStatus={handleToggleStatus}
                onReorder={handleReorderItem}
                showStatus={true}
                emptyStateTitle="List is empty"
                emptyStateDescription="Add your first item to get started!"
                loadingText="Loading list items..."
            />

            {/* Add Item Input - Sticky at bottom */}
            <AddItemInput
                onAdd={handleAddItem}
                onUpdate={handleUpdateItem}
                onCancelEdit={handleCancelEdit}
                onUncheckExisting={handleUncheckExisting}
                editingItem={editingItem}
                existingItems={items}
                isLoading={createMutation.isPending || updateMutation.isPending}
                hasItems={items.length > 0}
                placeholder="Add item..."
                editingPlaceholder="Edit item..."
                showQuantity={true}
                showDatePicker={false}
            />
        </>
    );
};

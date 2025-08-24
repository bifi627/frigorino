import { Container, ListItemText, Typography } from "@mui/material";
import { forwardRef, memo, useCallback } from "react";
import type { ListItemDto } from "../../hooks/useListItemQueries";
import {
    useDeleteListItem,
    useListItems,
    useReorderListItem,
    useToggleListItemStatus,
} from "../../hooks/useListItemQueries";
import { SortableList } from "../sortables/SortableList";

interface ListContainerProps {
    householdId: number;
    listId: number;
    editingItem: ListItemDto | null;
    onEdit: (item: ListItemDto) => void;
    showDragHandles: boolean;
}

export const ListContainer = memo(
    forwardRef<HTMLDivElement, ListContainerProps>(
        (
            { householdId, listId, editingItem, onEdit, showDragHandles },
            ref,
        ) => {
            // Fetch data and setup mutations at the container level
            const {
                data: items = [],
                isLoading,
                error,
            } = useListItems(householdId, listId);
            const deleteMutation = useDeleteListItem();
            const toggleMutation = useToggleListItemStatus();
            const reorderMutation = useReorderListItem();

            // Create callback handlers for the sortable list
            const handleReorder = useCallback(
                async (itemId: number, afterId: number): Promise<void> => {
                    await reorderMutation.mutateAsync({
                        householdId,
                        listId,
                        itemId,
                        data: { afterId },
                    });
                },
                [reorderMutation, householdId, listId],
            );

            const handleToggleStatus = useCallback(
                async (itemId: number): Promise<void> => {
                    await toggleMutation.mutateAsync({
                        householdId,
                        listId,
                        itemId,
                    });
                },
                [toggleMutation, householdId, listId],
            );

            const handleDelete = useCallback(
                async (itemId: number): Promise<void> => {
                    await deleteMutation.mutateAsync({
                        householdId,
                        listId,
                        itemId,
                    });
                },
                [deleteMutation, householdId, listId],
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
                        onToggleStatus={handleToggleStatus}
                        onEdit={onEdit}
                        onDelete={handleDelete}
                        editingItem={editingItem}
                        showDragHandles={showDragHandles}
                        showCheckbox={true}
                        renderContent={(item) => (
                            <>
                                <ListItemText
                                    primary={
                                        <Typography
                                            variant="body2"
                                            sx={{
                                                fontWeight: 500,
                                                wordBreak: "break-word",
                                            }}
                                        >
                                            {item.text}
                                        </Typography>
                                    }
                                    secondary={
                                        item.quantity && (
                                            <Typography
                                                variant="caption"
                                                sx={{
                                                    color: item.status
                                                        ? "text.disabled"
                                                        : "text.secondary",
                                                    textDecoration: item.status
                                                        ? "line-through"
                                                        : "none",
                                                }}
                                            >
                                                {item.quantity}
                                            </Typography>
                                        )
                                    }
                                />
                            </>
                        )}
                    />
                </Container>
            );
        },
    ),
);

ListContainer.displayName = "ListContainer";

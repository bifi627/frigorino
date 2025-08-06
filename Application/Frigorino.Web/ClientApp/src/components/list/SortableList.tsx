import {
    closestCenter,
    DndContext,
    DragOverlay,
    PointerSensor,
    TouchSensor,
    useSensor,
    useSensors,
    type DragEndEvent,
    type DragStartEvent,
} from "@dnd-kit/core";
import {
    SortableContext,
    verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import {
    Alert,
    Box,
    CircularProgress,
    Divider,
    List,
    Paper,
    Typography,
} from "@mui/material";
import { memo, useCallback, useMemo, useRef, useState } from "react";
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
import { AddItemInput } from "./AddItemInput";
import { SortableListItem } from "./SortableListItem";

// Memoized list item renderer to prevent unnecessary re-renders
const MemoizedSortableListItem = memo(
    ({
        item,
        isEditing,
        onToggleStatus,
        onEdit,
        onDelete,
    }: {
        item: ListItemDto;
        isEditing: boolean;
        onToggleStatus: (itemId: number) => void;
        onEdit: (item: ListItemDto) => void;
        onDelete: (itemId: number) => void;
    }) => (
        <SortableListItem
            key={item.id}
            item={item}
            onToggleStatus={onToggleStatus}
            onEdit={onEdit}
            onDelete={onDelete}
            isEditing={isEditing}
        />
    ),
);

MemoizedSortableListItem.displayName = "MemoizedSortableListItem";

interface SortableListProps {
    householdId: number;
    listId: number;
}

export const SortableList = ({ householdId, listId }: SortableListProps) => {
    const [editingItem, setEditingItem] = useState<ListItemDto | null>(null);
    const [activeItem, setActiveItem] = useState<ListItemDto | null>(null);
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

    // Configure drag sensors - must be at top level, not in useMemo
    const sensors = useSensors(
        useSensor(PointerSensor, {
            activationConstraint: {
                distance: 8,
            },
        }),
        useSensor(TouchSensor, {
            activationConstraint: {
                delay: 200,
                tolerance: 5,
            },
        }),
    );

    // Memoize expensive sorting operations
    const { uncheckedItems, checkedItems } = useMemo(() => {
        const unchecked = items
            .filter((item) => !item.status)
            .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

        const checked = items
            .filter((item) => item.status)
            .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

        return { uncheckedItems: unchecked, checkedItems: checked };
    }, [items]);

    // Event handlers - memoized to prevent unnecessary re-renders
    const handleDragStart = useCallback(
        (event: DragStartEvent) => {
            const { active } = event;
            const item = items.find(
                (item) => item.id?.toString() === active.id,
            );
            setActiveItem(item || null);
        },
        [items],
    );

    const handleDragEnd = useCallback(
        (event: DragEndEvent) => {
            const { active, over } = event;
            setActiveItem(null);

            if (!over || active.id === over.id) return;

            const activeItem = items.find(
                (item) => item.id?.toString() === active.id,
            );
            const overItem = items.find(
                (item) => item.id?.toString() === over.id,
            );

            if (!activeItem || !overItem) return;

            // Prevent dragging between checked/unchecked sections
            if (activeItem.status !== overItem.status) return;

            // Calculate the appropriate section inside the callback to avoid dependency issues
            const currentSectionItems = items
                .filter((item) => item.status === activeItem.status)
                .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

            let overIndex = currentSectionItems.findIndex(
                (item) => item.id === overItem.id,
            );
            const activeIndex = currentSectionItems.findIndex(
                (item) => item.id === activeItem.id,
            );

            if (overIndex < activeIndex) {
                overIndex--;
            }

            // The afterId should be the ID of the item at overIndex, or 0 if placing at the beginning
            const afterItemId =
                overIndex >= 0 ? currentSectionItems[overIndex].id : 0;

            // Call reorder API
            if (activeItem.id) {
                reorderMutation.mutate({
                    householdId,
                    listId,
                    itemId: activeItem.id,
                    data: { afterId: afterItemId || 0 },
                });
            }
        },
        [items, reorderMutation, householdId, listId],
    );

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
                setEditingItem(null); // Clear editing state after update
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

    // Memoize item IDs for SortableContext to prevent unnecessary re-renders
    const uncheckedItemIds = useMemo(
        () => uncheckedItems.map((item) => item.id?.toString() || "0"),
        [uncheckedItems],
    );

    const checkedItemIds = useMemo(
        () => checkedItems.map((item) => item.id?.toString() || "0"),
        [checkedItems],
    );

    if (isLoading) {
        return (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
                <CircularProgress />
            </Box>
        );
    }

    if (error) {
        return (
            <Alert severity="error" sx={{ mx: 2 }}>
                Failed to load list items. Please try again.
            </Alert>
        );
    }

    return (
        <Box
            sx={{
                position: "relative",
                pb: items.length > 0 ? 10 : 0, // Increased padding bottom to provide space above input
                px: 0.5, // Reduced horizontal padding for more space
            }}
        >
            <Box>
                <DndContext
                    sensors={sensors}
                    collisionDetection={closestCenter}
                    onDragStart={handleDragStart}
                    onDragEnd={handleDragEnd}
                >
                    {/* Unchecked Items Section */}
                    {uncheckedItems.length > 0 && (
                        <SortableContext
                            items={uncheckedItemIds}
                            strategy={verticalListSortingStrategy}
                        >
                            <List
                                data-section="unchecked-items"
                                sx={{
                                    py: 0,
                                    "& .MuiListItem-root": { mb: 0.5 },
                                }}
                            >
                                {uncheckedItems.map((item) => (
                                    <MemoizedSortableListItem
                                        key={item.id}
                                        item={item}
                                        onToggleStatus={handleToggleStatus}
                                        onEdit={handleEditItem}
                                        onDelete={handleDeleteItem}
                                        isEditing={editingItem?.id === item.id}
                                    />
                                ))}
                            </List>
                        </SortableContext>
                    )}

                    {/* Checked Items Section */}
                    <Box sx={{ my: 1, textAlign: "center" }}>
                        <Divider ref={dividerRef} sx={{ m: 2 }} />
                    </Box>

                    <Box
                        sx={{
                            bgcolor: "success.25",
                        }}
                    >
                        <SortableContext
                            items={checkedItemIds}
                            strategy={verticalListSortingStrategy}
                        >
                            <List
                                sx={{
                                    py: 0,
                                    "& .MuiListItem-root": { mb: 0.5 },
                                }}
                            >
                                {checkedItems.map((item) => (
                                    <MemoizedSortableListItem
                                        key={item.id}
                                        item={item}
                                        onToggleStatus={handleToggleStatus}
                                        onEdit={handleEditItem}
                                        onDelete={handleDeleteItem}
                                        isEditing={editingItem?.id === item.id}
                                    />
                                ))}
                            </List>
                        </SortableContext>
                    </Box>

                    {/* Empty State */}
                    {items.length === 0 && (
                        <Paper
                            elevation={0}
                            sx={{
                                p: 3,
                                textAlign: "center",
                                border: "2px dashed",
                                borderColor: "divider",
                                borderRadius: 2,
                                mx: 1,
                            }}
                        >
                            <Typography
                                variant="h6"
                                color="text.secondary"
                                gutterBottom
                            >
                                List ist leer
                            </Typography>
                            <Typography
                                variant="body2"
                                color="text.secondary"
                                sx={{ mb: 2 }}
                            >
                                Füge deinen ersten Artikel hinzu, um zu
                                beginnen!
                            </Typography>
                        </Paper>
                    )}

                    {/* Drag Overlay */}
                    <DragOverlay>
                        {activeItem ? (
                            <Paper
                                elevation={8}
                                sx={{
                                    transform: "rotate(5deg)",
                                    opacity: 0.95,
                                }}
                            >
                                <SortableListItem
                                    item={activeItem}
                                    onToggleStatus={() => {}}
                                    onEdit={() => {}}
                                    onDelete={() => {}}
                                    isEditing={false}
                                />
                            </Paper>
                        ) : null}
                    </DragOverlay>
                </DndContext>
            </Box>

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
            />
        </Box>
    );
};

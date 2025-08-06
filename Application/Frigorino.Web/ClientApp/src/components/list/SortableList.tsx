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
    Chip,
    CircularProgress,
    Divider,
    List,
    Paper,
    Typography,
} from "@mui/material";
import { useState } from "react";
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

interface SortableListProps {
    householdId: number;
    listId: number;
}

export const SortableList = ({ householdId, listId }: SortableListProps) => {
    const [editingItem, setEditingItem] = useState<ListItemDto | null>(null);
    const [activeItem, setActiveItem] = useState<ListItemDto | null>(null);

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

    // Configure drag sensors for both mouse/pointer and touch
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

    // Separate items by status and sort by sortOrder
    const uncheckedItems = items
        .filter((item) => !item.status)
        .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

    const checkedItems = items
        .filter((item) => item.status)
        .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

    // Event handlers
    const handleDragStart = (event: DragStartEvent) => {
        const { active } = event;
        const item = items.find((item) => item.id?.toString() === active.id);
        setActiveItem(item || null);
    };

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        setActiveItem(null);

        if (!over || active.id === over.id) return;

        const activeItem = items.find(
            (item) => item.id?.toString() === active.id,
        );
        const overItem = items.find((item) => item.id?.toString() === over.id);

        if (!activeItem || !overItem) return;

        // Prevent dragging between checked/unchecked sections
        if (activeItem.status !== overItem.status) return;

        // Find the item after which to place the active item
        const activeSection = activeItem.status ? checkedItems : uncheckedItems;
        let overIndex = activeSection.findIndex(
            (item) => item.id === overItem.id,
        );
        const activeIndex = activeSection.findIndex(
            (item) => item.id === activeItem.id,
        );

        if (overIndex < activeIndex) {
            overIndex--;
        }

        console.log(overIndex);

        // The afterId should be the ID of the item at overIndex, or 0 if placing at the beginning
        const afterItemId = overIndex >= 0 ? activeSection[overIndex].id : 0;

        // Call reorder API
        if (activeItem.id) {
            reorderMutation.mutate({
                householdId,
                listId,
                itemId: activeItem.id,
                data: { afterId: afterItemId || 0 },
            });
        }
    };

    const handleEditItem = (item: ListItemDto) => {
        setEditingItem(item);
    };

    const handleCancelEdit = () => {
        setEditingItem(null);
    };

    const handleUpdateItem = (data: UpdateListItemRequest) => {
        if (editingItem?.id) {
            updateMutation.mutate({
                householdId,
                listId,
                itemId: editingItem.id,
                data,
            });
            setEditingItem(null); // Clear editing state after update
        }
    };

    const handleUncheckExisting = (itemId: number) => {
        toggleMutation.mutate({ householdId, listId, itemId });
    };

    const handleDeleteItem = (itemId: number) => {
        deleteMutation.mutate({ householdId, listId, itemId });
    };

    const handleToggleStatus = (itemId: number) => {
        toggleMutation.mutate({ householdId, listId, itemId });
    };

    const handleAddItem = (data: CreateListItemRequest) => {
        createMutation.mutate({
            householdId,
            listId,
            data,
        });
    };

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
                pb: items.length > 0 ? 8 : 0, // Reduced padding bottom when there are items
                px: 1, // Added small horizontal padding
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
                            items={uncheckedItems.map(
                                (item) => item.id?.toString() || "0",
                            )}
                            strategy={verticalListSortingStrategy}
                        >
                            <List sx={{ py: 0 }}>
                                {uncheckedItems.map((item) => (
                                    <SortableListItem
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
                    {checkedItems.length > 0 && (
                        <>
                            {uncheckedItems.length > 0 && (
                                <Box sx={{ my: 2, textAlign: "center" }}>
                                    <Divider sx={{ mb: 1 }}>
                                        <Chip
                                            label="Completed Items"
                                            size="small"
                                            color="success"
                                            variant="outlined"
                                            sx={{
                                                bgcolor: "success.50",
                                                color: "success.700",
                                                fontWeight: "bold",
                                                fontSize: "0.75rem",
                                            }}
                                        />
                                    </Divider>
                                </Box>
                            )}

                            <Box
                                sx={{
                                    bgcolor: "success.25",
                                }}
                            >
                                <SortableContext
                                    items={checkedItems.map(
                                        (item) => item.id?.toString() || "0",
                                    )}
                                    strategy={verticalListSortingStrategy}
                                >
                                    <List sx={{ py: 0 }}>
                                        {checkedItems.map((item) => (
                                            <SortableListItem
                                                key={item.id}
                                                item={item}
                                                onToggleStatus={
                                                    handleToggleStatus
                                                }
                                                onEdit={handleEditItem}
                                                onDelete={handleDeleteItem}
                                                isEditing={
                                                    editingItem?.id === item.id
                                                }
                                            />
                                        ))}
                                    </List>
                                </SortableContext>
                            </Box>
                        </>
                    )}

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

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
import { Add } from "@mui/icons-material";
import {
    Alert,
    Box,
    CircularProgress,
    Divider,
    Fab,
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
import { ListItemDialog } from "../list/ListItemDialog";
import { SortableListItem } from "../list/SortableListItem";

interface SortableListProps {
    householdId: number;
    listId: number;
}

export const SortableList = ({ householdId, listId }: SortableListProps) => {
    const [dialogOpen, setDialogOpen] = useState(false);
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

    const handleAddItem = () => {
        setEditingItem(null);
        setDialogOpen(true);
    };

    const handleEditItem = (item: ListItemDto) => {
        setEditingItem(item);
        setDialogOpen(true);
    };

    const handleDeleteItem = (itemId: number) => {
        if (confirm("Are you sure you want to delete this item?")) {
            deleteMutation.mutate({ householdId, listId, itemId });
        }
    };

    const handleToggleStatus = (itemId: number) => {
        toggleMutation.mutate({ householdId, listId, itemId });
    };

    const handleSaveItem = (
        data: CreateListItemRequest | UpdateListItemRequest,
    ) => {
        if (editingItem?.id) {
            // Update existing item
            updateMutation.mutate({
                householdId,
                listId,
                itemId: editingItem.id,
                data: data as UpdateListItemRequest,
            });
        } else {
            // Create new item
            createMutation.mutate({
                householdId,
                listId,
                data: data as CreateListItemRequest,
            });
        }
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
        <Box sx={{ position: "relative", pb: 10 }}>
            <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragStart={handleDragStart}
                onDragEnd={handleDragEnd}
            >
                {/* Unchecked Items Section */}
                {uncheckedItems.length > 0 && (
                    <Paper
                        elevation={0}
                        sx={{
                            mb: 2,
                            border: "1px solid",
                            borderColor: "divider",
                            borderRadius: 2,
                        }}
                    >
                        <Box sx={{ p: 2, pb: 1 }}>
                            <Typography
                                variant="subtitle1"
                                sx={{ fontWeight: 600, color: "text.primary" }}
                            >
                                Shopping List ({uncheckedItems.length})
                            </Typography>
                        </Box>

                        <SortableContext
                            items={uncheckedItems.map(
                                (item) => item.id?.toString() || "0",
                            )}
                            strategy={verticalListSortingStrategy}
                        >
                            <List sx={{ px: 2, pb: 2 }}>
                                {uncheckedItems.map((item) => (
                                    <SortableListItem
                                        key={item.id}
                                        item={item}
                                        onToggleStatus={handleToggleStatus}
                                        onEdit={handleEditItem}
                                        onDelete={handleDeleteItem}
                                    />
                                ))}
                            </List>
                        </SortableContext>
                    </Paper>
                )}

                {/* Checked Items Section */}
                {checkedItems.length > 0 && (
                    <>
                        {uncheckedItems.length > 0 && (
                            <Divider sx={{ my: 2 }} />
                        )}

                        <Paper
                            elevation={0}
                            sx={{
                                border: "1px solid",
                                borderColor: "success.200",
                                borderRadius: 2,
                                bgcolor: "success.50",
                            }}
                        >
                            <Box sx={{ p: 2, pb: 1 }}>
                                <Typography
                                    variant="subtitle1"
                                    sx={{
                                        fontWeight: 600,
                                        color: "success.700",
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 1,
                                    }}
                                >
                                    ✓ Completed ({checkedItems.length})
                                </Typography>
                            </Box>

                            <SortableContext
                                items={checkedItems.map(
                                    (item) => item.id?.toString() || "0",
                                )}
                                strategy={verticalListSortingStrategy}
                            >
                                <List sx={{ px: 2, pb: 2 }}>
                                    {checkedItems.map((item) => (
                                        <SortableListItem
                                            key={item.id}
                                            item={item}
                                            onToggleStatus={handleToggleStatus}
                                            onEdit={handleEditItem}
                                            onDelete={handleDeleteItem}
                                        />
                                    ))}
                                </List>
                            </SortableContext>
                        </Paper>
                    </>
                )}

                {/* Empty State */}
                {items.length === 0 && (
                    <Paper
                        elevation={0}
                        sx={{
                            p: 4,
                            textAlign: "center",
                            border: "2px dashed",
                            borderColor: "divider",
                            borderRadius: 2,
                        }}
                    >
                        <Typography
                            variant="h6"
                            color="text.secondary"
                            gutterBottom
                        >
                            Your list is empty
                        </Typography>
                        <Typography
                            variant="body2"
                            color="text.secondary"
                            sx={{ mb: 2 }}
                        >
                            Add your first item to get started!
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
                            />
                        </Paper>
                    ) : null}
                </DragOverlay>
            </DndContext>

            {/* Add Item FAB */}
            <Fab
                color="primary"
                aria-label="add item"
                onClick={handleAddItem}
                sx={{
                    position: "fixed",
                    bottom: 24,
                    right: 24,
                    zIndex: 1000,
                }}
            >
                <Add />
            </Fab>

            {/* Add/Edit Dialog */}
            <ListItemDialog
                open={dialogOpen}
                onClose={() => setDialogOpen(false)}
                onSave={handleSaveItem}
                item={editingItem}
                isLoading={createMutation.isPending || updateMutation.isPending}
            />
        </Box>
    );
};

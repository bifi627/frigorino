import { Add } from "@mui/icons-material";
import {
    Alert,
    Box,
    CircularProgress,
    Divider,
    Fab,
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
} from "../hooks/useListItemQueries";
import { SortableList as GenericSortableList } from "./common/sortable/SortableList";
import { ListItemDialog } from "./list/ListItemDialog";
import { SortableListItem } from "./list/SortableListItem";

interface SortableListProps {
    householdId: number;
    listId: number;
}

export const SortableList = ({ householdId, listId }: SortableListProps) => {
    const [dialogOpen, setDialogOpen] = useState(false);
    const [editingItem, setEditingItem] = useState<ListItemDto | null>(null);

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

    // Separate items by status and sort by sortOrder
    const uncheckedItems = items
        .filter((item) => !item.status)
        .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

    const checkedItems = items
        .filter((item) => item.status)
        .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

    // Event handlers
    const handleReorder = (
        activeItem: ListItemDto,
        overItem: ListItemDto,
        activeSection: string,
        overSection: string,
    ) => {
        // Prevent dragging between checked/unchecked sections
        if (activeSection !== overSection) return;

        const activeId = activeItem.id;
        const overId = overItem.id;

        if (!activeId || !overId) return;

        // Find the item after which to place the active item
        const targetSection =
            activeSection === "unchecked" ? uncheckedItems : checkedItems;
        let overIndex = targetSection.findIndex((item) => item.id === overId);
        const activeIndex = targetSection.findIndex(
            (item) => item.id === activeId,
        );

        if (overIndex < activeIndex) {
            overIndex--;
        }

        const afterItemId = overIndex > 0 ? targetSection[overIndex].id : 0;

        // Call reorder API
        reorderMutation.mutate({
            householdId,
            listId,
            itemId: activeId,
            data: { afterId: afterItemId || 0 },
        });
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

    const renderSectionHeader = (title: string) => (
        <Box sx={{ mb: 2 }}>
            <Typography variant="h6" color="text.secondary">
                {title}
            </Typography>
            <Divider sx={{ mt: 1 }} />
        </Box>
    );

    const renderListItem = (item: ListItemDto, isDragging = false) => (
        <SortableListItem
            key={item.id}
            item={item}
            onToggleStatus={handleToggleStatus}
            onEdit={handleEditItem}
            onDelete={handleDeleteItem}
            isDragging={isDragging}
        />
    );

    if (isLoading) {
        return (
            <Box
                display="flex"
                justifyContent="center"
                alignItems="center"
                minHeight="200px"
            >
                <CircularProgress />
            </Box>
        );
    }

    if (error) {
        return (
            <Alert severity="error" sx={{ m: 2 }}>
                Error loading list items. Please try again.
            </Alert>
        );
    }

    const sections = [
        {
            id: "unchecked",
            title: "To Do",
            items: uncheckedItems,
            renderItem: renderListItem,
        },
        ...(checkedItems.length > 0
            ? [
                  {
                      id: "checked",
                      title: "Completed",
                      items: checkedItems,
                      renderItem: renderListItem,
                  },
              ]
            : []),
    ];

    return (
        <Box sx={{ p: 2, maxWidth: 600, mx: "auto" }}>
            <Paper elevation={2} sx={{ p: 3 }}>
                <GenericSortableList
                    sections={sections}
                    onReorder={handleReorder}
                    allowCrossSectionDrag={false}
                    renderSectionHeader={(section) =>
                        renderSectionHeader(section.title || "")
                    }
                    containerSx={{ minHeight: 400 }}
                    sectionSx={{ mb: 3 }}
                />

                {/* Add Button */}
                <Fab
                    color="primary"
                    aria-label="add"
                    onClick={handleAddItem}
                    sx={{
                        position: "fixed",
                        bottom: 16,
                        right: 16,
                    }}
                >
                    <Add />
                </Fab>
            </Paper>

            {/* Dialog for adding/editing items */}
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

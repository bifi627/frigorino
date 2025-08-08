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
import { SortableListItem } from "../list/SortableListItem";

// Base item interface that both ListItemDto and InventoryItemDto can extend
export interface BaseItem {
    id?: number;
    text?: string | null;
    quantity?: string | null;
    status?: boolean;
    sortOrder?: number;
}

// Props for create/update operations
export interface BaseCreateRequest {
    text: string;
    quantity?: string;
}

export interface BaseUpdateRequest {
    text: string;
    quantity?: string;
    status?: boolean;
}

// Simple memoized item renderer without complex generics
const MemoizedSortableListItem = memo(
    ({
        item,
        isEditing,
        onToggleStatus,
        onEdit,
        onDelete,
    }: {
        item: BaseItem;
        isEditing: boolean;
        onToggleStatus?: (itemId: number) => void;
        onEdit: (item: BaseItem) => void;
        onDelete: (itemId: number) => void;
    }) => (
        <SortableListItem
            key={item.id}
            item={item}
            onToggleStatus={onToggleStatus || (() => {})}
            onEdit={onEdit}
            onDelete={onDelete}
            isEditing={isEditing}
        />
    ),
);

MemoizedSortableListItem.displayName = "MemoizedSortableListItem";

interface SortableItemListProps<T extends BaseItem> {
    items: T[];
    isLoading: boolean;
    error: any;
    editingItem: T | null;
    onEdit: (item: T) => void;
    onDelete: (itemId: number) => void;
    onToggleStatus?: (itemId: number) => void;
    onReorder?: (itemId: number, afterId: number) => void;
    showStatus?: boolean;
    emptyStateTitle?: string;
    emptyStateDescription?: string;
    loadingText?: string;
}

export function SortableItemList<T extends BaseItem>({
    items,
    isLoading,
    error,
    editingItem,
    onEdit,
    onDelete,
    onToggleStatus,
    onReorder,
    showStatus = true,
    emptyStateTitle = "List is empty",
    emptyStateDescription = "Add your first item to get started!",
    loadingText = "Loading items...",
}: SortableItemListProps<T>) {
    const [activeItem, setActiveItem] = useState<T | null>(null);
    const dividerRef = useRef<HTMLHRElement | null>(null);

    // Configure drag sensors
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
        if (!showStatus) {
            // If not showing status, treat all items as unchecked
            const sorted = items.sort(
                (a, b) => (a.sortOrder || 0) - (b.sortOrder || 0),
            );
            return { uncheckedItems: sorted, checkedItems: [] };
        }

        const unchecked = items
            .filter((item) => !item.status)
            .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

        const checked = items
            .filter((item) => item.status)
            .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

        return { uncheckedItems: unchecked, checkedItems: checked };
    }, [items, showStatus]);

    // Event handlers
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

            if (!over || active.id === over.id || !onReorder) return;

            const activeItem = items.find(
                (item) => item.id?.toString() === active.id,
            );
            const overItem = items.find(
                (item) => item.id?.toString() === over.id,
            );

            if (!activeItem || !overItem) return;

            // Prevent dragging between checked/unchecked sections when status is shown
            if (showStatus && activeItem.status !== overItem.status) return;

            // Calculate the appropriate section
            const currentSectionItems = showStatus
                ? items
                      .filter((item) => item.status === activeItem.status)
                      .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
                : items.sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

            let overIndex = currentSectionItems.findIndex(
                (item) => item.id === overItem.id,
            );
            const activeIndex = currentSectionItems.findIndex(
                (item) => item.id === activeItem.id,
            );

            if (overIndex < activeIndex) {
                overIndex--;
            }

            const afterItemId =
                overIndex >= 0 ? currentSectionItems[overIndex].id : 0;

            if (activeItem.id) {
                onReorder(activeItem.id, afterItemId || 0);
            }
        },
        [items, onReorder, showStatus],
    );

    // Memoize item IDs for SortableContext
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
                <Typography variant="body2" sx={{ mt: 2, ml: 2 }}>
                    {loadingText}
                </Typography>
            </Box>
        );
    }

    if (error) {
        return (
            <Alert severity="error" sx={{ mx: 2 }}>
                Failed to load items. Please try again.
            </Alert>
        );
    }

    return (
        <Box
            sx={{
                position: "relative",
                pb: items.length > 0 ? 10 : 0,
                px: 0.5,
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
                                        onToggleStatus={onToggleStatus}
                                        onEdit={(editItem) =>
                                            onEdit(editItem as T)
                                        }
                                        onDelete={onDelete}
                                        isEditing={editingItem?.id === item.id}
                                    />
                                ))}
                            </List>
                        </SortableContext>
                    )}

                    {/* Divider between sections (only shown if we have both sections) */}
                    {showStatus && checkedItems.length > 0 && (
                        <Box sx={{ my: 1, textAlign: "center" }}>
                            <Divider ref={dividerRef} sx={{ m: 2 }} />
                        </Box>
                    )}

                    {/* Checked Items Section */}
                    {showStatus && checkedItems.length > 0 && (
                        <Box sx={{ bgcolor: "success.25" }}>
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
                                            onToggleStatus={onToggleStatus}
                                            onEdit={(editItem) =>
                                                onEdit(editItem as T)
                                            }
                                            onDelete={onDelete}
                                            isEditing={
                                                editingItem?.id === item.id
                                            }
                                        />
                                    ))}
                                </List>
                            </SortableContext>
                        </Box>
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
                                {emptyStateTitle}
                            </Typography>
                            <Typography
                                variant="body2"
                                color="text.secondary"
                                sx={{ mb: 2 }}
                            >
                                {emptyStateDescription}
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
        </Box>
    );
}

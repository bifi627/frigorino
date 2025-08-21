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
import React, { memo, useCallback, useMemo, useRef, useState } from "react";

// Minimal interface that sortable items must implement
export interface SortableItemInterface {
    id?: number | string | null;
    sortOrder?: number | null;
    status?: boolean; // For grouping checked/unchecked items
    [key: string]: unknown; // Index signature required for generic compatibility
}

import {
    SortableListItem,
    type DisplayableSortableItem,
} from "./SortableListItem";

// Generic memoized list item renderer interface
interface MemoizedSortableListItemProps<T extends SortableItemInterface> {
    item: T;
    isEditing: boolean;
    onToggleStatus: (itemId: number) => void;
    onEdit: (item: T) => void;
    onDelete: (itemId: number) => void;
    showDragHandles?: boolean;
    showCheckbox?: boolean;
    renderItem?: (
        item: T,
        isEditing: boolean,
        showDragHandles: boolean,
        handlers: {
            onToggleStatus: (itemId: number) => void;
            onEdit: (item: T) => void;
            onDelete: (itemId: number) => void;
        },
    ) => React.ReactNode;
}

// Memoized list item renderer to prevent unnecessary re-renders
function MemoizedSortableListItemComponent<T extends SortableItemInterface>({
    item,
    isEditing,
    onToggleStatus,
    onEdit,
    onDelete,
    showDragHandles,
    showCheckbox,
    renderItem,
}: MemoizedSortableListItemProps<T>) {
    if (renderItem) {
        return (
            <>
                {renderItem(item, isEditing, showDragHandles || false, {
                    onToggleStatus,
                    onEdit,
                    onDelete,
                })}
            </>
        );
    }

    // For the default renderer, we assume T is compatible with DisplayableSortableItem
    return (
        <SortableListItem
            key={item.id}
            item={item as DisplayableSortableItem}
            onToggleStatus={onToggleStatus}
            onEdit={onEdit as (item: DisplayableSortableItem) => void}
            onDelete={onDelete}
            isEditing={isEditing}
            showCheckbox={showCheckbox}
            showDragHandles={showDragHandles || false}
        />
    );
}

const MemoizedSortableListItem = memo(
    MemoizedSortableListItemComponent,
) as typeof MemoizedSortableListItemComponent;

export interface SortableListProps<
    T extends SortableItemInterface = DisplayableSortableItem,
> {
    // Data props
    items: T[];
    isLoading?: boolean;
    error?: Error | null;

    // Event handlers
    onReorder: (itemId: number, afterId: number) => Promise<void>;
    onToggleStatus: (itemId: number) => Promise<void>;
    onEdit: (item: T) => void;
    onDelete: (itemId: number) => Promise<void>;

    // UI props
    editingItem?: T | null;
    showDragHandles?: boolean;
    showCheckbox?: boolean;
    renderItem?: (
        item: T,
        isEditing: boolean,
        showDragHandles: boolean,
        handlers: {
            onToggleStatus: (itemId: number) => void;
            onEdit: (item: T) => void;
            onDelete: (itemId: number) => void;
        },
    ) => React.ReactNode;
}

export const SortableList = <
    T extends SortableItemInterface = DisplayableSortableItem,
>({
    items,
    isLoading = false,
    error = null,
    onReorder,
    onToggleStatus,
    onEdit,
    onDelete,
    editingItem: externalEditingItem,
    showDragHandles = false,
    showCheckbox = false,
    renderItem,
}: SortableListProps<T>) => {
    const [activeItem, setActiveItem] = useState<T | null>(null);
    const dividerRef = useRef<HTMLHRElement | null>(null);

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
            setActiveItem((item as T) || null);
        },
        [items],
    );

    const handleDragEnd = useCallback(
        async (event: DragEndEvent) => {
            const { active, over } = event;
            setActiveItem(null);

            if (!over || active.id === over.id) return;

            const activeItem = items.find(
                (item) => item.id?.toString() === active.id,
            );
            const overItem = items.find(
                (item) => item.id?.toString() === over.id,
            );

            if (!activeItem || !overItem || !activeItem.id) return;

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

            const numericActiveId =
                typeof activeItem.id === "string"
                    ? parseInt(activeItem.id)
                    : activeItem.id;
            const numericAfterId =
                typeof afterItemId === "string"
                    ? parseInt(afterItemId)
                    : afterItemId;

            if (numericActiveId) {
                await onReorder(numericActiveId, numericAfterId || 0);
            }
        },
        [items, onReorder],
    );

    const handleEditItem = useCallback(
        (item: T) => {
            onEdit(item);
        },
        [onEdit],
    );

    const handleDeleteItem = useCallback(
        async (itemId: number) => {
            await onDelete(itemId);
        },
        [onDelete],
    );

    const handleToggleStatus = useCallback(
        async (itemId: number) => {
            await onToggleStatus(itemId);
        },
        [onToggleStatus],
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
                                    item={item as T}
                                    onToggleStatus={handleToggleStatus}
                                    onEdit={handleEditItem}
                                    onDelete={handleDeleteItem}
                                    isEditing={
                                        externalEditingItem?.id === item.id
                                    }
                                    showCheckbox={showCheckbox}
                                    showDragHandles={showDragHandles}
                                    renderItem={renderItem}
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
                            data-section="checked-items"
                            sx={{
                                py: 0,
                                "& .MuiListItem-root": { mb: 0.5 },
                            }}
                        >
                            {checkedItems.map((item) => (
                                <MemoizedSortableListItem
                                    key={item.id}
                                    item={item as T}
                                    onToggleStatus={handleToggleStatus}
                                    onEdit={handleEditItem}
                                    onDelete={handleDeleteItem}
                                    isEditing={
                                        externalEditingItem?.id === item.id
                                    }
                                    showCheckbox={showCheckbox}
                                    showDragHandles={showDragHandles}
                                    renderItem={renderItem}
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
                            Füge deinen ersten Artikel hinzu, um zu beginnen!
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
                            {/* <SortableListItem
                                item={activeItem}
                                onToggleStatus={() => {}}
                                onEdit={() => {}}
                                onDelete={() => {}}
                                isEditing={false}
                                showDragHandles={false}
                            /> */}
                        </Paper>
                    ) : null}
                </DragOverlay>
            </DndContext>
        </Box>
    );
};

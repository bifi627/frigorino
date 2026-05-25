import {
    closestCenter,
    DndContext,
    DragOverlay,
    PointerSensor,
    TouchSensor,
    useSensor,
    useSensors,
    type DragEndEvent,
    type DragOverEvent,
    type DragStartEvent,
} from "@dnd-kit/core";
import {
    arrayMove,
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
import React, {
    useCallback,
    useEffect,
    useMemo,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import { SortableListItem } from "./SortableListItem";

// dnd-kit uses string ids; entity ids are number | string | null.
const idStr = (item: SortableItemInterface) => item.id?.toString();

// Minimal interface that sortable items must implement
export interface SortableItemInterface {
    id?: number | string | null;
    sortOrder?: number | null;
    status?: boolean; // For grouping checked/unchecked items
    [key: string]: unknown; // Index signature required for generic compatibility
}

export interface SortableListProps<T extends SortableItemInterface> {
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
    renderContent: (item: T) => React.ReactNode;

    // Sorting props
    sortMode?: "custom" | "expiryDateAsc" | "expiryDateDesc";
    skipInternalSort?: boolean;
}

export const SortableList = <T extends SortableItemInterface>({
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
    renderContent,
    sortMode = "custom",
    skipInternalSort = false,
}: SortableListProps<T>) => {
    const { t } = useTranslation();
    const [activeItem, setActiveItem] = useState<T | null>(null);
    // Live drag order: the library reorders this in onDragOver so the rows shift
    // symmetrically as the dragged item crosses each neighbour, and it holds the
    // dropped order until the server/optimistic data resyncs (no snap-back). Null
    // when idle — the list then renders straight from props.
    const [dragOrder, setDragOrder] = useState<{
        unchecked: T[];
        checked: T[];
    } | null>(null);
    // Guards the resync effect so a data refetch mid-drag (e.g. a previous
    // reorder's debounced invalidation) can't yank dragOrder out from under the
    // pointer. A ref, not state, so flipping it never triggers a render.
    const isDraggingRef = useRef(false);
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
        const sortItems = (itemsToSort: T[]) => {
            if (skipInternalSort) {
                return itemsToSort; // Return items as-is
            }

            if (sortMode === "custom" || !sortMode) {
                // Use existing sortOrder sorting
                return itemsToSort.sort(
                    (a, b) => (a.sortOrder || 0) - (b.sortOrder || 0),
                );
            } else if (sortMode === "expiryDateAsc") {
                // Sort by expiry date ascending (earliest first), null dates last
                return itemsToSort.sort((a, b) => {
                    const aDate =
                        "expiryDate" in a
                            ? (a.expiryDate as string | null)
                            : null;
                    const bDate =
                        "expiryDate" in b
                            ? (b.expiryDate as string | null)
                            : null;
                    if (!aDate && !bDate) return 0;
                    if (!aDate) return 1;
                    if (!bDate) return -1;
                    return (
                        new Date(aDate).getTime() - new Date(bDate).getTime()
                    );
                });
            } else if (sortMode === "expiryDateDesc") {
                // Sort by expiry date descending (latest first), null dates last
                return itemsToSort.sort((a, b) => {
                    const aDate =
                        "expiryDate" in a
                            ? (a.expiryDate as string | null)
                            : null;
                    const bDate =
                        "expiryDate" in b
                            ? (b.expiryDate as string | null)
                            : null;
                    if (!aDate && !bDate) return 0;
                    if (!aDate) return 1;
                    if (!bDate) return -1;
                    return (
                        new Date(bDate).getTime() - new Date(aDate).getTime()
                    );
                });
            }
            return itemsToSort;
        };

        const unchecked = sortItems(items.filter((item) => !item.status));
        const checked = sortItems(items.filter((item) => item.status));

        return { uncheckedItems: unchecked, checkedItems: checked };
    }, [items, sortMode, skipInternalSort]);

    // What actually renders: the live drag order while dragging, otherwise props.
    const displayUnchecked = dragOrder?.unchecked ?? uncheckedItems;
    const displayChecked = dragOrder?.checked ?? checkedItems;

    // Drop the live order whenever the underlying data changes — including the
    // optimistic update fired by the drop itself, which now matches the dragged
    // order, so the handoff is seamless. Skipped mid-drag (see isDraggingRef);
    // handleDragEnd clears the flag before the drop's optimistic update lands.
    useEffect(() => {
        if (!isDraggingRef.current) {
            setDragOrder(null);
        }
    }, [items]);

    const sectionOf = useCallback(
        (order: { unchecked: T[]; checked: T[] }, id: string) =>
            order.unchecked.some((item) => idStr(item) === id)
                ? "unchecked"
                : "checked",
        [],
    );

    const handleDragStart = useCallback(
        (event: DragStartEvent) => {
            const item = items.find((item) => idStr(item) === event.active.id);
            setActiveItem(item || null);
            setDragOrder({ unchecked: uncheckedItems, checked: checkedItems });
            isDraggingRef.current = true;
        },
        [items, uncheckedItems, checkedItems],
    );

    const handleDragOver = useCallback(
        (event: DragOverEvent) => {
            const { active, over } = event;
            if (!over || active.id === over.id) return;

            setDragOrder((prev) => {
                if (!prev) return prev;
                const key = sectionOf(prev, active.id.toString());
                const section = prev[key];
                const from = section.findIndex(
                    (item) => idStr(item) === active.id,
                );
                const to = section.findIndex((item) => idStr(item) === over.id);
                // Skip when the pointer is over the other section or nothing moved.
                if (from === -1 || to === -1 || from === to) return prev;
                return { ...prev, [key]: arrayMove(section, from, to) };
            });
        },
        [sectionOf],
    );

    const handleDragEnd = useCallback(
        (event: DragEndEvent) => {
            const { active, over } = event;
            setActiveItem(null);
            isDraggingRef.current = false;

            if (!over || !dragOrder) {
                setDragOrder(null);
                return;
            }

            // Persist the order the user actually sees: afterId is the item now
            // directly above the dragged one in its section (0 = top of section).
            const key = sectionOf(dragOrder, active.id.toString());
            const section = dragOrder[key];
            const index = section.findIndex(
                (item) => idStr(item) === active.id,
            );
            if (index === -1) {
                setDragOrder(null);
                return;
            }

            const afterId = index > 0 ? Number(section[index - 1].id) : 0;
            // Keep dragOrder in place — the resync effect clears it once the
            // optimistic update lands, avoiding a flash back to the old order.
            void onReorder(Number(active.id), afterId);
        },
        [dragOrder, sectionOf, onReorder],
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
        () => displayUnchecked.map((item) => idStr(item) || "0"),
        [displayUnchecked],
    );

    const checkedItemIds = useMemo(
        () => displayChecked.map((item) => idStr(item) || "0"),
        [displayChecked],
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
                {t("lists.failedToLoadList")}
            </Alert>
        );
    }

    return (
        <Box>
            <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragStart={handleDragStart}
                onDragOver={handleDragOver}
                onDragEnd={handleDragEnd}
            >
                {/* Unchecked Items Section */}
                {displayUnchecked.length > 0 && (
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
                            {displayUnchecked.map((item) => (
                                <SortableListItem
                                    key={item.id}
                                    item={item}
                                    onToggleStatus={handleToggleStatus}
                                    onEdit={handleEditItem}
                                    onDelete={handleDeleteItem}
                                    isEditing={
                                        externalEditingItem?.id === item.id
                                    }
                                    showCheckbox={showCheckbox}
                                    showDragHandles={showDragHandles}
                                    renderContent={renderContent}
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
                            {displayChecked.map((item) => (
                                <SortableListItem
                                    key={item.id}
                                    item={item}
                                    onToggleStatus={handleToggleStatus}
                                    onEdit={handleEditItem}
                                    onDelete={handleDeleteItem}
                                    isEditing={
                                        externalEditingItem?.id === item.id
                                    }
                                    showCheckbox={showCheckbox}
                                    showDragHandles={showDragHandles}
                                    renderContent={renderContent}
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
                            gutterBottom
                            sx={{
                                color: "text.secondary",
                            }}
                        >
                            List ist leer
                        </Typography>
                        <Typography
                            variant="body2"
                            sx={{
                                color: "text.secondary",
                                mb: 2,
                            }}
                        >
                            Füge deinen ersten Artikel hinzu, um zu beginnen!
                        </Typography>
                    </Paper>
                )}

                {/* Drag Overlay — a visible clone of the row that follows the cursor.
                    The left gutter matches the drag-handle width (SortableItem, 48px)
                    so the content sits to the right of the grab point instead of under
                    the finger on touch, keeping the dragged item visible. */}
                <DragOverlay>
                    {activeItem ? (
                        <Paper
                            elevation={8}
                            sx={{
                                pl: 10,
                                pr: 1.5,
                                py: 0.75,
                                borderRadius: 1,
                                opacity: 0.95,
                                cursor: "grabbing",
                            }}
                        >
                            {renderContent(activeItem)}
                        </Paper>
                    ) : null}
                </DragOverlay>
            </DndContext>
        </Box>
    );
};

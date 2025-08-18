import type { DragEndEvent, DragStartEvent } from "@dnd-kit/core";
import {
    DndContext,
    DragOverlay,
    PointerSensor,
    TouchSensor,
    useSensor,
    useSensors,
} from "@dnd-kit/core";
import {
    SortableContext,
    verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { Box, type SxProps } from "@mui/material";
import type { ReactNode } from "react";
import { useState } from "react";

export interface SortableItem {
    id?: number | string | null;
    [key: string]: unknown;
}

export interface SortableSection<T extends SortableItem> {
    id: string;
    title?: string;
    items: T[];
    renderItem: (item: T, isDragging?: boolean) => ReactNode;
}

export interface SortableListProps<T extends SortableItem> {
    sections: SortableSection<T>[];
    onReorder: (
        activeItem: T,
        overItem: T,
        activeSection: string,
        overSection: string,
    ) => void;
    allowCrossSectionDrag?: boolean;
    renderSectionHeader?: (section: SortableSection<T>) => ReactNode;
    renderActiveOverlay?: (item: T) => ReactNode;
    containerSx?: SxProps;
    sectionSx?: SxProps;
}

export function SortableList<T extends SortableItem>({
    sections,
    onReorder,
    allowCrossSectionDrag = false,
    renderSectionHeader,
    renderActiveOverlay,
    containerSx,
    sectionSx,
}: SortableListProps<T>) {
    const [activeItem, setActiveItem] = useState<T | null>(null);

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

    // Get all items for the sortable context
    const allItems = sections.flatMap((section) => section.items);

    // Find which section an item belongs to
    const findItemSection = (itemId: string | number) => {
        for (const section of sections) {
            if (
                section.items.find(
                    (item) => (item.id ?? "0").toString() === itemId.toString(),
                )
            ) {
                return section.id;
            }
        }
        return null;
    };

    const handleDragStart = (event: DragStartEvent) => {
        const { active } = event;
        const item = allItems.find(
            (item) => (item.id ?? "0").toString() === active.id,
        );
        setActiveItem(item || null);
    };

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        setActiveItem(null);

        if (!over || active.id === over.id) return;

        const activeItem = allItems.find(
            (item) => (item.id ?? "0").toString() === active.id,
        );
        const overItem = allItems.find(
            (item) => (item.id ?? "0").toString() === over.id,
        );

        if (!activeItem || !overItem) return;

        const activeSection = findItemSection(active.id);
        const overSection = findItemSection(over.id);

        if (!activeSection || !overSection) return;

        // Prevent cross-section dragging if not allowed
        if (!allowCrossSectionDrag && activeSection !== overSection) return;

        onReorder(activeItem, overItem, activeSection, overSection);
    };

    return (
        <Box sx={containerSx}>
            <DndContext
                sensors={sensors}
                onDragStart={handleDragStart}
                onDragEnd={handleDragEnd}
            >
                <SortableContext
                    items={allItems.map((item) => (item.id ?? "0").toString())}
                    strategy={verticalListSortingStrategy}
                >
                    {sections.map((section) => (
                        <Box key={section.id} sx={sectionSx}>
                            {renderSectionHeader &&
                                renderSectionHeader(section)}
                            {section.items.map((item) =>
                                section.renderItem(
                                    item,
                                    activeItem?.id === item.id,
                                ),
                            )}
                        </Box>
                    ))}
                </SortableContext>

                <DragOverlay>
                    {activeItem && (
                        <Box sx={{ opacity: 0.9 }}>
                            {renderActiveOverlay
                                ? renderActiveOverlay(activeItem)
                                : sections
                                      .find((section) =>
                                          section.items.some(
                                              (item) =>
                                                  item.id === activeItem.id,
                                          ),
                                      )
                                      ?.renderItem(activeItem, true)}
                        </Box>
                    )}
                </DragOverlay>
            </DndContext>
        </Box>
    );
}

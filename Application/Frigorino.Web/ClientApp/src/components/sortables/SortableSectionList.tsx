import {
    closestCenter,
    DndContext,
    PointerSensor,
    TouchSensor,
    useSensor,
    useSensors,
    type DraggableAttributes,
    type DragEndEvent,
} from "@dnd-kit/core";
import type { SyntheticListenerMap } from "@dnd-kit/core/dist/hooks/utilities";
import {
    SortableContext,
    useSortable,
    verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { Box } from "@mui/material";
import type { ReactNode } from "react";

export interface SectionDragHandle {
    attributes: DraggableAttributes;
    listeners: SyntheticListenerMap | undefined;
    setActivatorNodeRef: (el: HTMLElement | null) => void;
}

interface SortableSectionItem {
    id: number;
}

interface SortableSectionListProps<T extends SortableSectionItem> {
    sections: T[];
    onReorder: (sectionId: number, afterId: number) => Promise<void>;
    renderSection: (section: T, dragHandle: SectionDragHandle) => ReactNode;
}

function SortableSection<T extends SortableSectionItem>({
    section,
    renderSection,
}: {
    section: T;
    renderSection: (section: T, dragHandle: SectionDragHandle) => ReactNode;
}) {
    const {
        attributes,
        listeners,
        setNodeRef,
        setActivatorNodeRef,
        transform,
        transition,
        isDragging,
    } = useSortable({ id: section.id });

    return (
        <Box
            ref={setNodeRef}
            sx={{
                transform: CSS.Transform.toString(transform),
                transition,
                opacity: isDragging ? 0.5 : 1,
                mb: 2,
            }}
        >
            {renderSection(section, {
                attributes,
                listeners,
                setActivatorNodeRef,
            })}
        </Box>
    );
}

export function SortableSectionList<T extends SortableSectionItem>({
    sections,
    onReorder,
    renderSection,
}: SortableSectionListProps<T>) {
    const sensors = useSensors(
        useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
        useSensor(TouchSensor, {
            activationConstraint: { delay: 200, tolerance: 5 },
        }),
    );

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        if (!over || active.id === over.id) return;
        const ids = sections.map((s) => s.id);
        const from = ids.indexOf(Number(active.id));
        const to = ids.indexOf(Number(over.id));
        if (from === -1 || to === -1) return;
        // afterId = the section that will sit directly above the dropped one (0 = top).
        const afterId = to > from ? ids[to] : to > 0 ? ids[to - 1] : 0;
        void onReorder(Number(active.id), afterId);
    };

    return (
        <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={handleDragEnd}
        >
            <SortableContext
                items={sections.map((s) => s.id)}
                strategy={verticalListSortingStrategy}
            >
                {sections.map((section) => (
                    <SortableSection
                        key={section.id}
                        section={section}
                        renderSection={renderSection}
                    />
                ))}
            </SortableContext>
        </DndContext>
    );
}

import {
    closestCenter,
    DndContext,
    PointerSensor,
    TouchSensor,
    useSensor,
    useSensors,
    type DragEndEvent,
} from "@dnd-kit/core";
import {
    SortableContext,
    useSortable,
    verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { DragIndicator } from "@mui/icons-material";
import { Box } from "@mui/material";
import type { ReactNode } from "react";

interface SortableSectionItem {
    id: number;
}

interface SortableSectionListProps<T extends SortableSectionItem> {
    sections: T[];
    onReorder: (sectionId: number, afterId: number) => Promise<void>;
    // The drag handle is rendered here (where useSortable lives) and handed to the
    // card to place in its header — spreading the dnd-kit listeners/attributes in the
    // hook's own component is the pattern that satisfies the React Compiler ref rule.
    renderSection: (section: T, dragHandle: ReactNode) => ReactNode;
}

function SortableSection<T extends SortableSectionItem>({
    section,
    renderSection,
}: {
    section: T;
    renderSection: (section: T, dragHandle: ReactNode) => ReactNode;
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

    const dragHandle = (
        <Box
            ref={setActivatorNodeRef}
            {...attributes}
            {...listeners}
            onClick={(e) => e.stopPropagation()}
            sx={{
                display: "flex",
                alignItems: "center",
                cursor: "grab",
                mr: 1,
                touchAction: "none",
            }}
            data-testid={`section-drag-handle-${section.id}`}
        >
            <DragIndicator fontSize="small" color="action" />
        </Box>
    );

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
            {renderSection(section, dragHandle)}
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

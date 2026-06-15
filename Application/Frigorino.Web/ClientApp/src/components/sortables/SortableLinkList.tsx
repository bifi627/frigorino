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

interface SortableLinkItem {
    id: number;
}

interface SortableLinkListProps<T extends SortableLinkItem> {
    links: T[];
    onReorder: (linkId: number, afterId: number) => Promise<void>;
    // The drag handle is rendered here (where useSortable lives) and handed to the row to place —
    // spreading the dnd-kit listeners/attributes in the hook's own component satisfies the React
    // Compiler ref rule.
    renderLink: (link: T, dragHandle: ReactNode) => ReactNode;
}

function SortableLink<T extends SortableLinkItem>({
    link,
    renderLink,
    isLast,
}: {
    link: T;
    renderLink: (link: T, dragHandle: ReactNode) => ReactNode;
    isLast: boolean;
}) {
    const {
        attributes,
        listeners,
        setNodeRef,
        setActivatorNodeRef,
        transform,
        transition,
        isDragging,
    } = useSortable({ id: link.id });

    const dragHandle = (
        <Box
            ref={setActivatorNodeRef}
            {...attributes}
            {...listeners}
            sx={{
                display: "flex",
                alignItems: "center",
                cursor: "grab",
                mr: 1,
                touchAction: "none",
            }}
            data-testid={`recipe-link-drag-handle-${link.id}`}
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
                // Group each link's label+url pair: a divider + extra gap between links makes the
                // boundary clearer than the tight intra-row spacing. The last link drops both.
                pb: isLast ? 0 : 1.5,
                mb: isLast ? 0 : 1.5,
                borderBottom: isLast ? 0 : 1,
                borderColor: "divider",
            }}
        >
            {renderLink(link, dragHandle)}
        </Box>
    );
}

export function SortableLinkList<T extends SortableLinkItem>({
    links,
    onReorder,
    renderLink,
}: SortableLinkListProps<T>) {
    const sensors = useSensors(
        useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
        useSensor(TouchSensor, {
            activationConstraint: { delay: 200, tolerance: 5 },
        }),
    );

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        if (!over || active.id === over.id) return;
        const ids = links.map((l) => l.id);
        const from = ids.indexOf(Number(active.id));
        const to = ids.indexOf(Number(over.id));
        if (from === -1 || to === -1) return;
        // afterId = the link that will sit directly above the dropped one (0 = top).
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
                items={links.map((l) => l.id)}
                strategy={verticalListSortingStrategy}
            >
                {links.map((link, index) => (
                    <SortableLink
                        key={link.id}
                        link={link}
                        renderLink={renderLink}
                        isLast={index === links.length - 1}
                    />
                ))}
            </SortableContext>
        </DndContext>
    );
}

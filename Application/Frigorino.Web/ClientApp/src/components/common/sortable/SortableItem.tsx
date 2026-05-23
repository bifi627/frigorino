import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { DragIndicator } from "@mui/icons-material";
import { Box, type SxProps } from "@mui/material";
import type { ReactNode } from "react";

export interface SortableItemData {
    id?: number | string | null;
    [key: string]: unknown;
}

export interface SortableItemProps<T extends SortableItemData> {
    item: T;
    children: ReactNode;
    dragHandle?: "left" | "right" | "custom" | "none";
    renderDragHandle?: () => ReactNode;
    containerSx?: SxProps;
    dragHandleSx?: SxProps;
    dragHandleTestId?: string;
    isDragging?: boolean;
    disabled?: boolean;
}

export function SortableItem<T extends SortableItemData>({
    item,
    children,
    dragHandle = "left",
    renderDragHandle,
    containerSx,
    dragHandleSx,
    dragHandleTestId,
    isDragging: externalIsDragging,
    disabled = false,
}: SortableItemProps<T>) {
    if (dragHandle === "none") {
        return <Box sx={{ ...containerSx, pl: 1.5 }}>{children}</Box>;
    }

    return (
        <SortableItemInner
            item={item}
            dragHandle={dragHandle}
            renderDragHandle={renderDragHandle}
            containerSx={containerSx}
            dragHandleSx={dragHandleSx}
            dragHandleTestId={dragHandleTestId}
            isDragging={externalIsDragging}
            disabled={disabled}
        >
            {children}
        </SortableItemInner>
    );
}

function SortableItemInner<T extends SortableItemData>({
    item,
    children,
    dragHandle,
    renderDragHandle,
    containerSx,
    dragHandleSx,
    dragHandleTestId,
    isDragging: externalIsDragging,
    disabled,
}: SortableItemProps<T> & {
    dragHandle: "left" | "right" | "custom";
    disabled: boolean;
}) {
    const {
        attributes,
        listeners,
        setNodeRef,
        transform,
        transition,
        isDragging: internalIsDragging,
    } = useSortable({
        id: (item.id ?? "0").toString(),
        disabled,
        data: {
            type: "sortable-item",
            item,
        },
    });

    const isDragging = externalIsDragging ?? internalIsDragging;

    const style = {
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.5 : 1,
        zIndex: isDragging ? 1000 : 1,
    };

    const defaultDragHandleProps = {
        ...attributes,
        ...listeners,
    };

    const dragHandleElement = renderDragHandle ? (
        renderDragHandle()
    ) : (
        <Box
            data-testid={dragHandleTestId}
            sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                minWidth: 48,
                height: 48,
                cursor: disabled ? "default" : "grab",
                color: disabled ? "text.disabled" : "text.secondary",
                bgcolor: "transparent",
                borderRadius: 1,
                ...(!disabled && {
                    "&:hover": {
                        bgcolor: "action.hover",
                        color: "text.primary",
                    },
                    "&:active": {
                        cursor: "grabbing",
                        bgcolor: "action.selected",
                    },
                }),
                touchAction: "none",
                userSelect: "none",
                WebkitTouchCallout: "none",
                WebkitUserSelect: "none",
                ...dragHandleSx,
            }}
            {...(!disabled ? defaultDragHandleProps : {})}
        >
            <DragIndicator sx={{ fontSize: 20 }} />
        </Box>
    );

    if (dragHandle === "custom") {
        return (
            <Box
                ref={setNodeRef}
                style={style}
                sx={{
                    display: "flex",
                    alignItems: "center",
                    ...containerSx,
                }}
            >
                {children}
            </Box>
        );
    }

    return (
        <Box
            ref={setNodeRef}
            style={style}
            sx={{
                display: "flex",
                alignItems: "center",
                ...containerSx,
            }}
        >
            {dragHandle === "left" && dragHandleElement}
            <Box sx={{ flex: 1 }}>{children}</Box>
            {dragHandle === "right" && dragHandleElement}
        </Box>
    );
}

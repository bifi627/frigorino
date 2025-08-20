import { Container } from "@mui/material";
import { forwardRef, memo } from "react";
import type { ListItemDto } from "../../hooks/useListItemQueries";
import { SortableList } from "../sortables/SortableList";

interface ListContainerProps {
    householdId: number;
    listId: number;
    editingItem: ListItemDto | null;
    onEdit: (item: ListItemDto) => void;
    showDragHandles: boolean;
}

export const ListContainer = memo(
    forwardRef<HTMLDivElement, ListContainerProps>(
        (
            { householdId, listId, editingItem, onEdit, showDragHandles },
            ref,
        ) => {
            return (
                <Container
                    ref={ref}
                    maxWidth="sm"
                    sx={{
                        flex: 1,
                        overflow: "auto",
                        px: 3,
                        py: 0,
                        minHeight: 0,
                    }}
                >
                    <SortableList
                        householdId={householdId}
                        listId={listId}
                        editingItem={editingItem}
                        onEdit={onEdit}
                        showDragHandles={showDragHandles}
                    />
                </Container>
            );
        },
    ),
);

ListContainer.displayName = "ListContainer";

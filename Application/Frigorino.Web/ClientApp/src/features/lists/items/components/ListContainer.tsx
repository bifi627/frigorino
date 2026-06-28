import { Container, Paper, Typography } from "@mui/material";
import { forwardRef } from "react";
import { useTranslation } from "react-i18next";
import { featureContentPx } from "../../../../theme";
import { SortableList } from "../../../../components/sortables/SortableList";
import type { ListItemResponse } from "../../../../lib/api";
import { matchesQuery } from "../../../../utils/searchUtils";
import { useDeleteListItem } from "../useDeleteListItem";
import { useListItems } from "../useListItems";
import { useReorderListItem } from "../useReorderListItem";
import { useToggleListItemStatus } from "../useToggleListItemStatus";
import { ListItemContent } from "./ListItemContent";

interface ListContainerProps {
    householdId: number;
    listId: number;
    editingItem: ListItemResponse | null;
    onEdit: (item: ListItemResponse) => void;
    /** Opens edit mode with the quantity panel expanded (triggered by the quantity chip). */
    onEditQuantity: (item: ListItemResponse) => void;
    /** Opens edit mode with the comment panel expanded (triggered by tapping the comment). */
    onEditComment: (item: ListItemResponse) => void;
    showDragHandles: boolean;
    /** Per-row predicate: true while the row's async quantity extraction is still pending. */
    isItemExtracting?: (id: number) => boolean;
    searchQuery?: string;
}

// Items are searched across their text AND comment so both text-item notes and
// image/document captions (which live in `comment`) are matched.
const searchableText = (item: ListItemResponse): string =>
    [item.text, item.comment].filter(Boolean).join(" ");

export const ListContainer = forwardRef<HTMLDivElement, ListContainerProps>(
    (
        {
            householdId,
            listId,
            editingItem,
            onEdit,
            onEditQuantity,
            onEditComment,
            showDragHandles,
            isItemExtracting,
            searchQuery = "",
        },
        ref,
    ) => {
        const {
            data: items = [],
            isLoading,
            error,
        } = useListItems(householdId, listId);
        const deleteMutation = useDeleteListItem();
        const toggleMutation = useToggleListItemStatus();
        const reorderMutation = useReorderListItem();
        const { t } = useTranslation();

        const trimmedQuery = searchQuery.trim();
        const filterActive = trimmedQuery.length > 0;
        const visibleItems = filterActive
            ? items.filter((item) =>
                  matchesQuery(searchableText(item), trimmedQuery),
              )
            : items;
        const showNoMatches =
            filterActive && !isLoading && !error && visibleItems.length === 0;

        return (
            <Container
                ref={ref}
                maxWidth="sm"
                sx={{
                    flex: 1,
                    overflow: "auto",
                    px: featureContentPx,
                    py: 0,
                    minHeight: 0,
                }}
            >
                {showNoMatches ? (
                    <Paper
                        elevation={0}
                        data-testid="list-search-no-results"
                        sx={{
                            p: 3,
                            textAlign: "center",
                            border: "2px dashed",
                            borderColor: "divider",
                            mx: 1,
                        }}
                    >
                        <Typography variant="body2" color="text.secondary">
                            {t("lists.noSearchMatches")}
                        </Typography>
                    </Paper>
                ) : (
                    <SortableList
                        items={visibleItems}
                        dense
                        isLoading={isLoading}
                        error={error}
                        onReorder={async (itemId, afterId) => {
                            await reorderMutation.mutateAsync({
                                path: { householdId, listId, itemId },
                                body: { afterId },
                            });
                        }}
                        onToggleStatus={async (itemId) => {
                            await toggleMutation.mutateAsync({
                                path: { householdId, listId, itemId },
                            });
                        }}
                        onEdit={onEdit}
                        onDelete={async (itemId) => {
                            await deleteMutation.mutateAsync({
                                path: { householdId, listId, itemId },
                            });
                        }}
                        editingItem={editingItem}
                        showDragHandles={showDragHandles && !filterActive}
                        showCheckbox={true}
                        isItemProcessing={(item) =>
                            isItemExtracting?.(item.id) ?? false
                        }
                        renderContent={(item) => (
                            <ListItemContent
                                item={item}
                                onEditQuantity={() => onEditQuantity(item)}
                                onEditComment={() => onEditComment(item)}
                            />
                        )}
                    />
                )}
            </Container>
        );
    },
);

ListContainer.displayName = "ListContainer";

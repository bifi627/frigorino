import { Container, Paper, Typography } from "@mui/material";
import { forwardRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { featureContentPx } from "../../../../theme";
import { SortableList } from "../../../../components/sortables/SortableList";
import type { InventoryItemResponse } from "../../../../lib/api";
import { matchesQuery } from "../../../../utils/searchUtils";
import { useHouseholdLists } from "../../../lists/useHouseholdLists";
import { ReorderSheet } from "../../reorder/ReorderSheet";
import { useDeleteInventoryItem } from "../useDeleteInventoryItem";
import { useInventoryItems } from "../useInventoryItems";
import { useReorderInventoryItem } from "../useReorderInventoryItem";
import { InventoryItemContent } from "./InventoryItemContent";

type SortMode = "custom" | "expiryDateAsc" | "expiryDateDesc";

interface InventoryContainerProps {
    householdId: number;
    inventoryId: number;
    editingItem: InventoryItemResponse | null;
    onEdit: (item: InventoryItemResponse) => void;
    sortMode?: SortMode;
    searchQuery?: string;
}

export const InventoryContainer = forwardRef<
    HTMLDivElement,
    InventoryContainerProps
>(
    (
        {
            householdId,
            inventoryId,
            editingItem,
            onEdit,
            sortMode = "custom",
            searchQuery = "",
        },
        ref,
    ) => {
        const {
            data: items = [],
            isLoading,
            error,
        } = useInventoryItems(householdId, inventoryId);
        const deleteMutation = useDeleteInventoryItem();
        const reorderMutation = useReorderInventoryItem();
        const { t } = useTranslation();

        const [reorderItem, setReorderItem] =
            useState<InventoryItemResponse | null>(null);
        const { data: lists = [] } = useHouseholdLists(
            householdId,
            householdId > 0,
        );
        // Re-order needs a destination; hide the action entirely when the household has no list.
        const canReorder = lists.length > 0;

        const trimmedQuery = searchQuery.trim();
        const filterActive = trimmedQuery.length > 0;
        const visibleItems = filterActive
            ? items.filter((item) => matchesQuery(item.text, trimmedQuery))
            : items;
        const showNoMatches =
            filterActive && !isLoading && !error && visibleItems.length === 0;

        return (
            <>
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
                            data-testid="inventory-search-no-results"
                            sx={{
                                p: 3,
                                textAlign: "center",
                                border: "2px dashed",
                                borderColor: "divider",
                                mx: 1,
                            }}
                        >
                            <Typography variant="body2" color="text.secondary">
                                {t("inventory.noSearchMatches")}
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
                                    path: { householdId, inventoryId, itemId },
                                    body: { afterId },
                                });
                            }}
                            onToggleStatus={async () => {}}
                            onEdit={onEdit}
                            onDelete={async (itemId) => {
                                await deleteMutation.mutateAsync({
                                    path: { householdId, inventoryId, itemId },
                                });
                            }}
                            onAddToList={
                                canReorder
                                    ? (item) => setReorderItem(item)
                                    : undefined
                            }
                            editingItem={editingItem}
                            showDragHandles={
                                sortMode === "custom" && !filterActive
                            }
                            sortMode={sortMode}
                            renderContent={(item) => (
                                <InventoryItemContent item={item} />
                            )}
                        />
                    )}
                </Container>
                <ReorderSheet
                    open={reorderItem !== null}
                    onClose={() => setReorderItem(null)}
                    householdId={householdId}
                    item={reorderItem}
                />
            </>
        );
    },
);

InventoryContainer.displayName = "InventoryContainer";

import { DragIndicator, Edit, Schedule } from "@mui/icons-material";
import {
    Alert,
    Box,
    CircularProgress,
    Container,
    Snackbar,
    Typography,
} from "@mui/material";
import { createFileRoute, useRouter } from "@tanstack/react-router";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { requireAuth } from "../../../common/authGuard";
import { InventoryContainer } from "../../../components/inventory/InventoryContainer";
import { InventoryFooter } from "../../../components/inventory/InventoryFooter";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { useCurrentHousehold } from "../../../hooks/useHouseholdQueries";
import {
    useCreateInventoryItem,
    useInventoryItems,
    useUpdateInventoryItem,
    type CreateInventoryItemRequest,
    type InventoryItemDto,
    type UpdateInventoryItemRequest,
} from "../../../hooks/useInventoryItemQueries";
import { useInventory } from "../../../hooks/useInventoryQueries";

type SortMode = "custom" | "expiryDateAsc" | "expiryDateDesc";

export const Route = createFileRoute("/inventories/$inventoryId/view")({
    beforeLoad: requireAuth,
    component: RouteComponent,
});

function RouteComponent() {
    const router = useRouter();
    const { t } = useTranslation();
    const params = Route.useParams();
    const inventoryId = parseInt(params.inventoryId);
    // const [showDragHandles, setShowDragHandles] = useState(false);

    const scrollContainerRef = useRef<HTMLDivElement>(null);

    // LocalStorage key for sort mode persistence
    const SORT_MODE_STORAGE_KEY = `frigorino-inventory-sort-mode-${inventoryId}`;

    // Load sort mode from localStorage with error handling
    const loadSortMode = (): SortMode => {
        try {
            const saved = localStorage.getItem(SORT_MODE_STORAGE_KEY);
            if (
                saved &&
                ["custom", "expiryDateAsc", "expiryDateDesc"].includes(saved)
            ) {
                return saved as SortMode;
            }
            return "custom";
        } catch (error) {
            console.warn("Failed to load sort mode from localStorage:", error);
            return "custom";
        }
    };

    const [editingItem, setEditingItem] = useState<InventoryItemDto | null>(
        null,
    );
    const [sortMode, setSortMode] = useState<SortMode>(loadSortMode());

    // Persist sort mode changes to localStorage
    useEffect(() => {
        try {
            localStorage.setItem(SORT_MODE_STORAGE_KEY, sortMode);
        } catch (error) {
            console.warn("Failed to save sort mode to localStorage:", error);
        }
    }, [sortMode, SORT_MODE_STORAGE_KEY]);

    const { data: currentHousehold } = useCurrentHousehold();
    const {
        data: inventory,
        isLoading: inventoryLoading,
        error: inventoryError,
    } = useInventory(
        currentHousehold?.householdId || 0,
        inventoryId,
        !!currentHousehold?.householdId,
    );

    // Inventory items queries and mutations
    const { data: items = [] } = useInventoryItems(inventoryId, !!inventory);

    const createMutation = useCreateInventoryItem();
    const updateMutation = useUpdateInventoryItem();

    // Function to scroll to the last item in the unchecked section
    const scrollToLastUncheckedItem = useCallback(() => {
        if (scrollContainerRef.current) {
            // Find the unchecked items section and get its last item
            const uncheckedSection = scrollContainerRef.current.querySelector(
                '[data-section="unchecked-items"]',
            );
            if (uncheckedSection) {
                // Get all list items within the unchecked section
                const listItems =
                    uncheckedSection.querySelectorAll(".MuiListItem-root");
                // Get the last item in the unchecked section
                const lastItem = listItems[listItems.length - 1];
                if (lastItem) {
                    lastItem.scrollIntoView({
                        behavior: "smooth",
                        block: "center",
                    });
                }
            }
        }
    }, []);

    const handleEdit = () => {
        router.navigate({ to: `/inventories/${inventoryId}/edit` });
    };

    const handleToggleSortMode = useCallback(() => {
        setSortMode((prevMode) => {
            switch (prevMode) {
                case "custom":
                    return "expiryDateAsc";
                case "expiryDateAsc":
                    return "expiryDateDesc";
                case "expiryDateDesc":
                    return "custom";
                default:
                    return "custom";
            }
        });
    }, []);

    const getSortModeIcon = (mode: SortMode) => {
        switch (mode) {
            case "custom":
                return <DragIndicator />;
            case "expiryDateAsc":
                return <Schedule />;
            case "expiryDateDesc":
                return <Schedule style={{ transform: "scaleY(-1)" }} />;
            default:
                return <DragIndicator />;
        }
    };

    const handleAddItem = useCallback(
        (data: CreateInventoryItemRequest) => {
            createMutation.mutate({
                inventoryId,
                data,
            });
        },
        [createMutation, inventoryId],
    );

    const handleUpdateItem = useCallback(
        (data: UpdateInventoryItemRequest) => {
            if (editingItem?.id) {
                updateMutation.mutate({
                    inventoryId,
                    itemId: editingItem.id,
                    data,
                });
                setEditingItem(null);
            }
        },
        [editingItem?.id, updateMutation, inventoryId],
    );

    if (!currentHousehold?.householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="warning">
                    {t("common.pleaseSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    if (inventoryLoading) {
        return (
            <Container maxWidth="sm" sx={{ py: 4, textAlign: "center" }}>
                <CircularProgress />
                <Typography variant="body2" sx={{ mt: 2 }}>
                    {t("inventory.loadingInventory")}
                </Typography>
            </Container>
        );
    }

    if (inventoryError || !inventory) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="error" sx={{ mb: 2 }}>
                    {t("inventory.failedToLoadInventory")}
                </Alert>
            </Container>
        );
    }

    // Actions for HeadNavigation
    const directActions = [
        {
            icon: <Edit />,
            onClick: handleEdit,
        },
        {
            icon: getSortModeIcon(sortMode),
            onClick: handleToggleSortMode,
        },
    ];

    const menuActions: HeadNavigationAction[] = [];

    return (
        <Box
            sx={{
                height: "calc(100dvh - 56px)",
                display: "flex",
                flexDirection: "column",
                overflow: "hidden",
            }}
        >
            {/* Header Section */}
            <PageHeadActionBar
                title={inventory.name || t("inventory.untitledInventory")}
                subtitle={inventory.description || undefined}
                directActions={directActions}
                menuActions={menuActions}
            />

            {/* Scrollable Content Section */}
            <InventoryContainer
                ref={scrollContainerRef}
                inventoryId={inventoryId}
                editingItem={editingItem}
                onEdit={setEditingItem}
                sortMode={sortMode}
            />

            {/* Footer Section - AddInput */}
            <InventoryFooter
                editingItem={editingItem}
                existingItems={items}
                onAddItem={(data, quantity, expiryDate) =>
                    handleAddItem({
                        text: data,
                        quantity: quantity,
                        expiryDate: expiryDate?.toISOString(),
                    })
                }
                onUpdateItem={(data, quantity, expiryDate) =>
                    handleUpdateItem({
                        text: data,
                        quantity: quantity,
                        expiryDate: expiryDate?.toISOString(),
                    })
                }
                onCancelEdit={() => setEditingItem(null)}
                onUncheckExisting={() => {}}
                isLoading={createMutation.isPending || updateMutation.isPending}
                onScrollToLastUnchecked={scrollToLastUncheckedItem}
            />

            {/* Snackbar for feedback */}
            <Snackbar
                open={false}
                autoHideDuration={4000}
                onClose={() => {}}
                message={""}
                anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
            />
        </Box>
    );
}

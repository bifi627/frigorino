import { Edit } from "@mui/icons-material";
import {
    Alert,
    Box,
    CircularProgress,
    Container,
    Snackbar,
    Typography,
} from "@mui/material";
import { createFileRoute, useRouter } from "@tanstack/react-router";
import { useCallback, useRef, useState } from "react";
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

export const Route = createFileRoute("/inventories/$inventoryId/view")({
    beforeLoad: requireAuth,
    component: RouteComponent,
});

function RouteComponent() {
    const router = useRouter();
    const params = Route.useParams();
    const inventoryId = parseInt(params.inventoryId);
    // const [showDragHandles, setShowDragHandles] = useState(false);

    const scrollContainerRef = useRef<HTMLDivElement>(null);

    const [editingItem, setEditingItem] = useState<InventoryItemDto | null>(
        null,
    );

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
    // const deleteMutation = useDeleteInventoryItem();

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

    // const handleBack = () => {
    //     router.history.back();
    // };

    const handleEdit = () => {
        router.navigate({ to: `/inventories/${inventoryId}/edit` });
    };

    // const handleEditItem = useCallback((item: InventoryItemDto) => {
    //     setEditingItem(item);
    // }, []);

    // const handleCancelEdit = useCallback(() => {
    //     setEditingItem(null);
    // }, []);
    // const handleToggleDragHandles = useCallback(() => {
    //     setShowDragHandles(!showDragHandles);
    // }, [showDragHandles]);

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

    // const handleDeleteItem = useCallback(
    //     (itemId: number) => {
    //         deleteMutation.mutate({ inventoryId, itemId });
    //     },
    //     [deleteMutation, inventoryId],
    // );

    if (!currentHousehold?.householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="warning">
                    Please select a household first.
                </Alert>
            </Container>
        );
    }

    if (inventoryLoading) {
        return (
            <Container maxWidth="sm" sx={{ py: 4, textAlign: "center" }}>
                <CircularProgress />
                <Typography variant="body2" sx={{ mt: 2 }}>
                    Loading inventory...
                </Typography>
            </Container>
        );
    }

    if (inventoryError || !inventory) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="error" sx={{ mb: 2 }}>
                    Failed to load inventory. Please try again.
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
                title={inventory.name || "Untitled Inventory"}
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
            />

            {/* Footer Section - AddInput */}
            <InventoryFooter
                editingItem={editingItem}
                existingItems={items}
                onAddItem={(data, quantity) =>
                    handleAddItem({ text: data, quantity: quantity })
                }
                onUpdateItem={(data, quantity) =>
                    handleUpdateItem({ text: data, quantity: quantity })
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

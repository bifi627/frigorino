import { ArrowBack, Edit } from "@mui/icons-material";
import {
    Alert,
    Box,
    CircularProgress,
    Container,
    IconButton,
    Typography,
} from "@mui/material";
import { createFileRoute, useRouter } from "@tanstack/react-router";
import { useCallback, useState } from "react";
import { requireAuth } from "../../../common/authGuard";
import { AddItemInput } from "../../../components/shared/AddItemInput";
import { SortableItemList } from "../../../components/shared/SortableItemList";
import { useCurrentHousehold } from "../../../hooks/useHouseholdQueries";
import { useInventory } from "../../../hooks/useInventoryQueries";
import {
    useCreateInventoryItem,
    useDeleteInventoryItem,
    useInventoryItems,
    useUpdateInventoryItem,
    type CreateInventoryItemRequest,
    type InventoryItemDto,
    type UpdateInventoryItemRequest,
} from "../../../hooks/useInventoryItemQueries";

export const Route = createFileRoute("/inventories/$inventoryId/view")({
    beforeLoad: requireAuth,
    component: RouteComponent,
});

function RouteComponent() {
    const router = useRouter();
    const { inventoryId } = Route.useParams();
    const inventoryIdNum = parseInt(inventoryId, 10);

    const [editingItem, setEditingItem] = useState<InventoryItemDto | null>(null);

    const { data: currentHousehold } = useCurrentHousehold();
    const {
        data: inventory,
        isLoading: inventoryLoading,
        error: inventoryError,
    } = useInventory(
        currentHousehold?.householdId || 0,
        inventoryIdNum,
        !!currentHousehold?.householdId && !isNaN(inventoryIdNum),
    );

    // Inventory items queries and mutations
    const {
        data: items = [],
        isLoading: itemsLoading,
        error: itemsError,
    } = useInventoryItems(inventoryIdNum, !!inventory);

    const createMutation = useCreateInventoryItem();
    const updateMutation = useUpdateInventoryItem();
    const deleteMutation = useDeleteInventoryItem();

    const handleBack = () => {
        router.history.back();
    };

    const handleEdit = () => {
        router.navigate({ to: `/inventories/${inventoryId}/edit` });
    };

    const handleEditItem = useCallback((item: InventoryItemDto) => {
        setEditingItem(item);
    }, []);

    const handleCancelEdit = useCallback(() => {
        setEditingItem(null);
    }, []);

    const handleAddItem = useCallback(
        (data: CreateInventoryItemRequest) => {
            createMutation.mutate({
                inventoryId: inventoryIdNum,
                data,
            });
        },
        [createMutation, inventoryIdNum],
    );

    const handleUpdateItem = useCallback(
        (data: UpdateInventoryItemRequest) => {
            if (editingItem?.id) {
                updateMutation.mutate({
                    inventoryId: inventoryIdNum,
                    itemId: editingItem.id,
                    data,
                });
                setEditingItem(null);
            }
        },
        [editingItem?.id, updateMutation, inventoryIdNum],
    );

    const handleDeleteItem = useCallback(
        (itemId: number) => {
            deleteMutation.mutate({ inventoryId: inventoryIdNum, itemId });
        },
        [deleteMutation, inventoryIdNum],
    );

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

    return (
        <Container maxWidth="sm" sx={{ py: 3 }}>
            {/* Header */}
            <Box sx={{ display: "flex", alignItems: "center", gap: 2, mb: 3 }}>
                <IconButton onClick={handleBack} sx={{ p: 1 }}>
                    <ArrowBack />
                </IconButton>
                <Box sx={{ flex: 1 }}>
                    <Typography
                        variant="h5"
                        component="h1"
                        sx={{ fontWeight: 600, mb: 0.5 }}
                    >
                        {inventory.name}
                    </Typography>
                    {inventory.description && (
                        <Typography
                            variant="body2"
                            color="text.secondary"
                            sx={{ lineHeight: 1.4 }}
                        >
                            {inventory.description}
                        </Typography>
                    )}
                </Box>
                <Box sx={{ display: "flex", gap: 1 }}>
                    <IconButton
                        onClick={handleEdit}
                        sx={{
                            bgcolor: "primary.main",
                            color: "white",
                            "&:hover": { bgcolor: "primary.dark" },
                        }}
                    >
                        <Edit />
                    </IconButton>
                </Box>
            </Box>

            {/* Sortable Inventory Items List */}
            <SortableItemList
                items={items}
                isLoading={itemsLoading}
                error={itemsError}
                editingItem={editingItem}
                onEdit={handleEditItem}
                onDelete={handleDeleteItem}
                showStatus={false} // Inventory items don't have checked/unchecked status
                emptyStateTitle="Inventory is empty"
                emptyStateDescription="Add your first item to get started!"
                loadingText="Loading inventory items..."
            />

            {/* Add Item Input */}
            <AddItemInput
                onAdd={handleAddItem}
                onUpdate={handleUpdateItem}
                onCancelEdit={handleCancelEdit}
                editingItem={editingItem}
                existingItems={items}
                isLoading={createMutation.isPending || updateMutation.isPending}
                hasItems={items.length > 0}
                placeholder="Add inventory item..."
                editingPlaceholder="Edit inventory item..."
                showQuantity={true}
                showDatePicker={true}
                dateLabel="Expiry Date"
                quantityLabel="Quantity"
            />
        </Container>
    );
}

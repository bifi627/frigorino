import { ArrowBack } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    Snackbar,
    Typography,
} from "@mui/material";
import { createFileRoute } from "@tanstack/react-router";
import { useCallback, useRef, useState } from "react";
import { requireAuth } from "../../../common/authGuard";
import { ListContainer } from "../../../components/list/ListContainer";
import { ListFooter } from "../../../components/list/ListFooter";
import { ListViewHeader } from "../../../components/list/ListViewHeader";
import { useCurrentHousehold } from "../../../hooks/useHouseholdQueries";
import {
    useCompactListItems,
    useCreateListItem,
    useListItems,
    useToggleListItemStatus,
    useUpdateListItem,
    type ListItemDto,
} from "../../../hooks/useListItemQueries";
import { useList } from "../../../hooks/useListQueries";

export const Route = createFileRoute("/lists/$listId/view")({
    beforeLoad: requireAuth,
    component: RouteComponent,
});

function RouteComponent() {
    const { listId } = Route.useParams();
    const [snackbarOpen, setSnackbarOpen] = useState(false);
    const [snackbarMessage, setSnackbarMessage] = useState("");
    const [editingItem, setEditingItem] = useState<ListItemDto | null>(null);
    const [showDragHandles, setShowDragHandles] = useState(false);
    const scrollContainerRef = useRef<HTMLDivElement>(null);

    // Get current household and list data
    const { data: currentHousehold } = useCurrentHousehold();
    const {
        data: list,
        isLoading,
        error,
    } = useList(
        currentHousehold?.householdId || 0,
        parseInt(listId),
        !!currentHousehold?.householdId,
    );

    // Get list items for AddInput
    const { data: items = [] } = useListItems(
        currentHousehold?.householdId || 0,
        parseInt(listId),
    );

    // Mutations for AddInput
    const createMutation = useCreateListItem();
    const updateMutation = useUpdateListItem();
    const toggleMutation = useToggleListItemStatus();
    const compactListItems = useCompactListItems();

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

    const handleCompact = useCallback(async () => {
        if (!currentHousehold?.householdId) return;

        try {
            await compactListItems.mutateAsync({
                householdId: currentHousehold.householdId,
                listId: parseInt(listId),
            });
            setSnackbarMessage("List order compacted successfully!");
            setSnackbarOpen(true);
        } catch {
            setSnackbarMessage(
                "Failed to compact list order. Please try again.",
            );
            setSnackbarOpen(true);
        }
    }, [compactListItems, currentHousehold?.householdId, listId]);

    const handleToggleDragHandles = useCallback(() => {
        setShowDragHandles(!showDragHandles);
    }, [showDragHandles]);

    const handleSnackbarClose = useCallback(() => {
        setSnackbarOpen(false);
    }, []);

    // AddInput handlers
    const handleAddItem = useCallback(
        (data: string, quantity?: string) => {
            if (!currentHousehold?.householdId) return;

            const itemData = {
                text: data,
                quantity: quantity || undefined,
            };

            createMutation.mutate({
                householdId: currentHousehold.householdId,
                listId: parseInt(listId),
                data: itemData,
            });
        },
        [createMutation, currentHousehold?.householdId, listId],
    );

    const handleUpdateItem = useCallback(
        (data: string, quantity?: string) => {
            if (editingItem?.id && currentHousehold?.householdId) {
                const itemData = {
                    text: data,
                    quantity: quantity || undefined,
                };

                updateMutation.mutate({
                    householdId: currentHousehold.householdId,
                    listId: parseInt(listId),
                    itemId: editingItem.id,
                    data: itemData,
                });
                setEditingItem(null);
            }
        },
        [editingItem?.id, updateMutation, currentHousehold?.householdId, listId],
    );

    const handleCancelEdit = useCallback(() => {
        setEditingItem(null);
    }, []);

    const handleUncheckExisting = useCallback(
        (itemId: number) => {
            if (!currentHousehold?.householdId) return;
            toggleMutation.mutate({
                householdId: currentHousehold.householdId,
                listId: parseInt(listId),
                itemId,
            });
        },
        [toggleMutation, currentHousehold?.householdId, listId],
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

    if (isLoading) {
        return (
            <Container maxWidth="sm" sx={{ py: 4, textAlign: "center" }}>
                <CircularProgress />
                <Typography variant="body2" sx={{ mt: 2 }}>
                    Loading list...
                </Typography>
            </Container>
        );
    }

    if (error || !list) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="error" sx={{ mb: 2 }}>
                    Failed to load list. Please try again.
                </Alert>
                <Button
                    variant="outlined"
                    startIcon={<ArrowBack />}
                    onClick={() => window.history.back()}
                >
                    Back to Lists
                </Button>
            </Container>
        );
    }

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
            <ListViewHeader
                list={list}
                listId={listId}
                showDragHandles={showDragHandles}
                onToggleDragHandles={handleToggleDragHandles}
                onCompact={handleCompact}
                isCompacting={compactListItems.isPending}
            />

            {/* Scrollable Content Section */}
            <ListContainer
                ref={scrollContainerRef}
                householdId={currentHousehold.householdId}
                listId={parseInt(listId)}
                editingItem={editingItem}
                onEdit={setEditingItem}
                showDragHandles={showDragHandles}
            />

            {/* Footer Section - AddInput */}
            <ListFooter
                editingItem={editingItem}
                existingItems={items}
                onAddItem={handleAddItem}
                onUpdateItem={handleUpdateItem}
                onCancelEdit={handleCancelEdit}
                onUncheckExisting={handleUncheckExisting}
                isLoading={createMutation.isPending || updateMutation.isPending}
                onScrollToLastUnchecked={scrollToLastUncheckedItem}
            />

            {/* Snackbar for feedback */}
            <Snackbar
                open={snackbarOpen}
                autoHideDuration={4000}
                onClose={handleSnackbarClose}
                message={snackbarMessage}
                anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
            />
        </Box>
    );
}

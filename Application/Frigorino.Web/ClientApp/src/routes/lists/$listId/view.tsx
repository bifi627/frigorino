import { ArrowBack, Compress, Edit, MoreVert } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Snackbar,
    Typography,
} from "@mui/material";
import { createFileRoute, useRouter } from "@tanstack/react-router";
import { useCallback, useRef, useState } from "react";
import { requireAuth } from "../../../common/authGuard";
import { AddItemInput } from "../../../components/list/AddItemInput";
import { SortableList } from "../../../components/list/SortableList";
import { useCurrentHousehold } from "../../../hooks/useHouseholdQueries";
import {
    useCompactListItems,
    useCreateListItem,
    useListItems,
    useToggleListItemStatus,
    useUpdateListItem,
    type CreateListItemRequest,
    type ListItemDto,
    type UpdateListItemRequest,
} from "../../../hooks/useListItemQueries";
import { useList } from "../../../hooks/useListQueries";

export const Route = createFileRoute("/lists/$listId/view")({
    beforeLoad: requireAuth,
    component: RouteComponent,
});

function RouteComponent() {
    const router = useRouter();
    const { listId } = Route.useParams();
    const [menuAnchorEl, setMenuAnchorEl] = useState<null | HTMLElement>(null);
    const [snackbarOpen, setSnackbarOpen] = useState(false);
    const [snackbarMessage, setSnackbarMessage] = useState("");
    const [editingItem, setEditingItem] = useState<ListItemDto | null>(null);
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

    // Get list items for AddItemInput
    const { data: items = [] } = useListItems(
        currentHousehold?.householdId || 0,
        parseInt(listId),
    );

    // Mutations for AddItemInput
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

    const handleBack = () => {
        router.history.back();
    };

    const handleEdit = () => {
        router.navigate({
            to: `/lists/${listId}/edit`,
        });
    };

    const handleMenuOpen = (event: React.MouseEvent<HTMLElement>) => {
        setMenuAnchorEl(event.currentTarget);
    };

    const handleMenuClose = () => {
        setMenuAnchorEl(null);
    };

    const handleCompact = async () => {
        handleMenuClose();
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
    };

    const handleSnackbarClose = () => {
        setSnackbarOpen(false);
    };

    // AddItemInput handlers
    const handleAddItem = useCallback(
        (data: CreateListItemRequest) => {
            if (!currentHousehold?.householdId) return;
            createMutation.mutate(
                {
                    householdId: currentHousehold.householdId,
                    listId: parseInt(listId),
                    data,
                },
                {
                    onSuccess: () => {
                        // Scroll to the last item in the unchecked section
                        scrollToLastUncheckedItem();
                    },
                },
            );
        },
        [
            createMutation,
            currentHousehold?.householdId,
            listId,
            scrollToLastUncheckedItem,
        ],
    );

    const handleUpdateItem = useCallback(
        (data: UpdateListItemRequest) => {
            if (editingItem?.id && currentHousehold?.householdId) {
                updateMutation.mutate({
                    householdId: currentHousehold.householdId,
                    listId: parseInt(listId),
                    itemId: editingItem.id,
                    data,
                });
                setEditingItem(null);
            }
        },
        [
            editingItem?.id,
            updateMutation,
            currentHousehold?.householdId,
            listId,
        ],
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
                    onClick={handleBack}
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
            <Container maxWidth="sm" sx={{ px: 3, py: 3, flexShrink: 0 }}>
                <Box
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: 2,
                    }}
                >
                    <IconButton onClick={handleBack} sx={{ p: 1 }}>
                        <ArrowBack />
                    </IconButton>
                    <Box>
                        <Typography
                            variant="h5"
                            component="h1"
                            sx={{ fontWeight: 600, mb: 0.5 }}
                        >
                            {list.name}
                        </Typography>
                        {list.description && (
                            <Typography
                                variant="body2"
                                color="text.secondary"
                                sx={{ lineHeight: 1.4 }}
                            >
                                {list.description}
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
                        <IconButton
                            onClick={handleMenuOpen}
                            sx={{
                                bgcolor: "grey.100",
                                color: "grey.700",
                                "&:hover": { bgcolor: "grey.200" },
                            }}
                        >
                            <MoreVert />
                        </IconButton>
                    </Box>
                </Box>
            </Container>

            {/* Menu */}
            <Menu
                anchorEl={menuAnchorEl}
                open={Boolean(menuAnchorEl)}
                onClose={handleMenuClose}
                anchorOrigin={{
                    vertical: "bottom",
                    horizontal: "right",
                }}
                transformOrigin={{
                    vertical: "top",
                    horizontal: "right",
                }}
            >
                <MenuItem
                    onClick={handleCompact}
                    disabled={compactListItems.isPending}
                >
                    <ListItemIcon>
                        <Compress fontSize="small" />
                    </ListItemIcon>
                    <ListItemText
                        primary="Compact List Order"
                        secondary="Reorganize item sort order"
                    />
                </MenuItem>
            </Menu>

            {/* Scrollable Content Section */}
            <Container
                ref={scrollContainerRef}
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
                    householdId={currentHousehold.householdId}
                    listId={parseInt(listId)}
                    editingItem={editingItem}
                    onEdit={setEditingItem}
                />
            </Container>

            {/* Footer Section - AddItemInput */}
            <Container
                maxWidth="sm"
                sx={{
                    flexShrink: 0,
                    px: 3,
                    py: 2,
                    borderTop: 1,
                    borderColor: "divider",
                    bgcolor: "background.paper",
                }}
            >
                <AddItemInput
                    onAdd={handleAddItem}
                    onUpdate={handleUpdateItem}
                    onCancelEdit={handleCancelEdit}
                    onUncheckExisting={handleUncheckExisting}
                    editingItem={editingItem}
                    existingItems={items}
                    isLoading={
                        createMutation.isPending || updateMutation.isPending
                    }
                    hasItems={items.length > 0}
                />
            </Container>

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

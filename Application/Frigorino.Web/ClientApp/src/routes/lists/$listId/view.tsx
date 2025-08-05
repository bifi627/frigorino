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
import { useState } from "react";
import { requireAuth } from "../../../common/authGuard";
import { SortableList } from "../../../components/list/SortableList";
import { useCurrentHousehold } from "../../../hooks/useHouseholdQueries";
import { useCompactListItems } from "../../../hooks/useListItemQueries";
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

    // Compaction mutation
    const compactListItems = useCompactListItems();

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
        } catch (error) {
            setSnackbarMessage(
                "Failed to compact list order. Please try again.",
            );
            setSnackbarOpen(true);
        }
    };

    const handleSnackbarClose = () => {
        setSnackbarOpen(false);
    };

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
        <Container maxWidth="sm" sx={{ py: 3 }}>
            {/* Header */}
            <Box
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 2,
                    mb: 3,
                }}
            >
                <IconButton onClick={handleBack} sx={{ p: 1 }}>
                    <ArrowBack />
                </IconButton>
                <Box sx={{ flex: 1 }}>
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

            {/* Snackbar for feedback */}
            <Snackbar
                open={snackbarOpen}
                autoHideDuration={4000}
                onClose={handleSnackbarClose}
                message={snackbarMessage}
                anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
            />

            {/* Sortable List Items */}
            <SortableList
                householdId={currentHousehold.householdId}
                listId={parseInt(listId)}
            />
        </Container>
    );
}

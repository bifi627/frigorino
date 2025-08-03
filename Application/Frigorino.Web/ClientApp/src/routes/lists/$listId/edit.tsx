import {
    ArrowBack,
    Delete,
    List as ListIcon,
    MoreVert,
    Save,
} from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Container,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Skeleton,
    TextField,
    Typography,
} from "@mui/material";
import {
    createFileRoute,
    useNavigate,
    useRouter,
} from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { requireAuth } from "../../../common/authGuard";
import { useCurrentHouseholdWithDetails } from "../../../hooks/useHouseholdQueries";
import {
    useDeleteList,
    useList,
    useUpdateList,
    type UpdateListRequest,
} from "../../../hooks/useListQueries";

export const Route = createFileRoute("/lists/$listId/edit")({
    beforeLoad: requireAuth,
    component: ListEditPage,
});

function ListEditPage() {
    const navigate = useNavigate();
    const router = useRouter();
    const { listId } = Route.useParams();
    const listIdNum = parseInt(listId, 10);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    const [confirmationText, setConfirmationText] = useState("");
    const [editedName, setEditedName] = useState("");
    const [editedDescription, setEditedDescription] = useState("");

    // Get current household info
    const {
        currentHousehold,
        isLoading: householdLoading,
        error: householdError,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();

    // Get list data
    const {
        data: list,
        isLoading: listLoading,
        error: listError,
    } = useList(
        currentHousehold?.householdId || 0,
        listIdNum,
        hasActiveHousehold && !isNaN(listIdNum),
    );

    // Mutations
    const updateListMutation = useUpdateList();
    const deleteListMutation = useDeleteList();

    // Set initial form values when list data loads
    useEffect(() => {
        if (list) {
            setEditedName(list.name || "");
            setEditedDescription(list.description || "");
        }
    }, [list]);

    const handleBack = () => {
        router.history.back();
    };

    const handleMenuClick = (event: React.MouseEvent<HTMLElement>) => {
        setMenuAnchor(event.currentTarget);
    };

    const handleMenuClose = () => {
        setMenuAnchor(null);
    };

    const handleDeleteClick = () => {
        setDeleteDialogOpen(true);
        setConfirmationText("");
        handleMenuClose();
    };

    const handleSave = () => {
        if (!currentHousehold?.householdId || !list?.id) return;

        const updateData: UpdateListRequest = {
            name: editedName.trim(),
            description: editedDescription.trim() || null,
        };

        updateListMutation.mutate(
            {
                householdId: currentHousehold.householdId,
                listId: list.id,
                data: updateData,
            },
            {
                onSuccess: () => {
                    handleBack();
                },
            },
        );
    };

    const handleCancelEdit = () => {
        handleBack();
    };

    const handleDeleteConfirm = () => {
        if (
            currentHousehold?.householdId &&
            list?.id &&
            confirmationText === list.name
        ) {
            deleteListMutation.mutate(
                {
                    householdId: currentHousehold.householdId,
                    listId: list.id,
                },
                {
                    onSuccess: () => {
                        navigate({ to: "/" });
                    },
                },
            );
        }
    };

    const handleDeleteDialogClose = () => {
        setDeleteDialogOpen(false);
        setConfirmationText("");
    };

    const isLoading = householdLoading || listLoading;
    const error = householdError || listError;

    if (isLoading) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    {/* Header Skeleton */}
                    <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                        <Skeleton
                            variant="rectangular"
                            height={40}
                            sx={{ mb: 1, borderRadius: 1 }}
                        />
                        <Skeleton variant="text" width="60%" height={32} />
                    </Box>

                    {/* Content Skeleton */}
                    <Skeleton
                        variant="rectangular"
                        height={200}
                        sx={{ borderRadius: 2 }}
                    />
                </Box>
            </Container>
        );
    }

    if (error) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    <Alert severity="error" sx={{ borderRadius: 2 }}>
                        Failed to load list information
                    </Alert>
                </Box>
            </Container>
        );
    }

    if (!hasActiveHousehold || !currentHousehold?.householdId) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    <Alert severity="info" sx={{ borderRadius: 2 }}>
                        You need to create or select a household first.
                    </Alert>
                </Box>
            </Container>
        );
    }

    if (!list) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    <Alert severity="warning" sx={{ borderRadius: 2 }}>
                        List not found or you don't have access to it.
                    </Alert>
                </Box>
            </Container>
        );
    }

    const listName = list.name || "Untitled List";
    const isFormValid = editedName.trim().length > 0;

    return (
        <Container
            maxWidth="md"
            sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
        >
            <Box>
                {/* Mobile-friendly Header */}
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    {/* Top Navigation Bar */}
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: { xs: 1, sm: 2 },
                            mb: { xs: 2, sm: 3 },
                        }}
                    >
                        <IconButton onClick={handleBack} sx={{ p: 1 }}>
                            <ArrowBack />
                        </IconButton>

                        <Typography
                            variant="h5"
                            component="h1"
                            sx={{
                                fontWeight: 600,
                                fontSize: { xs: "1.25rem", sm: "1.5rem" },
                                color: "text.primary",
                                flexGrow: 1,
                            }}
                        >
                            Edit List
                        </Typography>

                        {/* Menu button */}
                        <IconButton
                            onClick={handleMenuClick}
                            size="small"
                            sx={{
                                bgcolor: "background.paper",
                                border: 1,
                                borderColor: "divider",
                                "&:hover": {
                                    bgcolor: "action.hover",
                                },
                            }}
                        >
                            <MoreVert fontSize="small" />
                        </IconButton>
                    </Box>

                    {/* List Info Card */}
                    <Box
                        sx={{
                            p: { xs: 2, sm: 2.5 },
                            bgcolor: "background.paper",
                            borderRadius: 2,
                            border: 1,
                            borderColor: "divider",
                            boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                        }}
                    >
                        <Box
                            sx={{
                                display: "flex",
                                flexDirection: "column",
                                gap: 2,
                            }}
                        >
                            <Box
                                sx={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 2,
                                    mb: 1,
                                }}
                            >
                                <Box
                                    sx={{
                                        p: 1,
                                        borderRadius: 1.5,
                                        bgcolor: "primary.main",
                                        color: "primary.contrastText",
                                        display: "flex",
                                        alignItems: "center",
                                    }}
                                >
                                    <ListIcon fontSize="small" />
                                </Box>
                                <Typography
                                    variant="h6"
                                    sx={{ fontWeight: 600 }}
                                >
                                    Edit List Details
                                </Typography>
                            </Box>

                            <TextField
                                label="List Name"
                                value={editedName}
                                onChange={(e) => setEditedName(e.target.value)}
                                fullWidth
                                required
                                error={editedName.trim().length === 0}
                                helperText={
                                    editedName.trim().length === 0
                                        ? "List name is required"
                                        : ""
                                }
                                sx={{
                                    "& .MuiOutlinedInput-root": {
                                        borderRadius: 2,
                                    },
                                }}
                            />

                            <TextField
                                label="Description (Optional)"
                                value={editedDescription}
                                onChange={(e) =>
                                    setEditedDescription(e.target.value)
                                }
                                fullWidth
                                multiline
                                rows={3}
                                sx={{
                                    "& .MuiOutlinedInput-root": {
                                        borderRadius: 2,
                                    },
                                }}
                            />
                        </Box>
                    </Box>

                    {/* Save/Cancel Buttons */}
                    <Box
                        sx={{
                            display: "flex",
                            gap: 2,
                            justifyContent: "flex-end",
                            mt: 3,
                        }}
                    >
                        <Button
                            variant="outlined"
                            onClick={handleCancelEdit}
                            disabled={updateListMutation.isPending}
                            sx={{ borderRadius: 2, minWidth: 100 }}
                        >
                            Cancel
                        </Button>
                        <Button
                            variant="contained"
                            onClick={handleSave}
                            disabled={
                                updateListMutation.isPending || !isFormValid
                            }
                            startIcon={<Save />}
                            sx={{ borderRadius: 2, minWidth: 100 }}
                        >
                            {updateListMutation.isPending
                                ? "Saving..."
                                : "Save"}
                        </Button>
                    </Box>
                </Box>
            </Box>

            {/* Menu */}
            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={handleMenuClose}
                anchorOrigin={{
                    vertical: "bottom",
                    horizontal: "right",
                }}
                transformOrigin={{
                    vertical: "top",
                    horizontal: "right",
                }}
                slotProps={{
                    paper: {
                        sx: {
                            minWidth: 200,
                            mt: 1,
                            borderRadius: 2,
                            boxShadow: "0 4px 20px rgba(0,0,0,0.1)",
                        },
                    },
                }}
            >
                <MenuItem
                    onClick={handleDeleteClick}
                    sx={{
                        color: "error.main",
                        py: 1.5,
                        "&:hover": {
                            bgcolor: "error.light",
                            color: "error.contrastText",
                        },
                    }}
                >
                    <ListItemIcon>
                        <Delete fontSize="small" color="error" />
                    </ListItemIcon>
                    <ListItemText primary="Delete List" />
                </MenuItem>
            </Menu>

            {/* Delete Confirmation Dialog */}
            <Dialog
                open={deleteDialogOpen}
                onClose={handleDeleteDialogClose}
                maxWidth="sm"
                fullWidth
            >
                <DialogTitle sx={{ pb: 1 }}>
                    <Typography
                        variant="h6"
                        component="div"
                        sx={{ fontWeight: 600 }}
                    >
                        Delete List
                    </Typography>
                </DialogTitle>
                <DialogContent>
                    <DialogContentText sx={{ mb: 2 }}>
                        Are you sure you want to delete "{listName}"? This
                        action cannot be undone.
                    </DialogContentText>

                    <Box
                        sx={{
                            bgcolor: "error.light",
                            borderRadius: 1,
                            p: 2,
                            border: 1,
                            borderColor: "error.main",
                            mb: 3,
                        }}
                    >
                        <Typography
                            variant="body2"
                            color="error.dark"
                            sx={{ fontWeight: 500 }}
                        >
                            ⚠️ Warning: This will permanently delete:
                        </Typography>
                        <Typography
                            variant="body2"
                            color="error.dark"
                            sx={{ mt: 1, ml: 2 }}
                        >
                            • The entire list and its settings
                        </Typography>
                        <Typography
                            variant="body2"
                            color="error.dark"
                            sx={{ ml: 2 }}
                        >
                            • All list items (future: when items are
                            implemented)
                        </Typography>
                        <Typography
                            variant="body2"
                            color="error.dark"
                            sx={{ ml: 2 }}
                        >
                            • All associated data and history
                        </Typography>
                    </Box>

                    <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                        To confirm, please type the list name:{" "}
                        <strong>{listName}</strong>
                    </Typography>
                    <TextField
                        fullWidth
                        variant="outlined"
                        value={confirmationText}
                        onChange={(e) => setConfirmationText(e.target.value)}
                        placeholder={`Type "${listName}" to confirm`}
                        disabled={deleteListMutation.isPending}
                        error={
                            confirmationText.length > 0 &&
                            confirmationText !== listName
                        }
                        helperText={
                            confirmationText.length > 0 &&
                            confirmationText !== listName
                                ? "Name doesn't match"
                                : ""
                        }
                        sx={{
                            "& .MuiOutlinedInput-root": {
                                borderRadius: 2,
                            },
                        }}
                    />
                </DialogContent>
                <DialogActions sx={{ p: 3, pt: 1 }}>
                    <Button
                        onClick={handleDeleteDialogClose}
                        disabled={deleteListMutation.isPending}
                        sx={{ borderRadius: 2 }}
                    >
                        Cancel
                    </Button>
                    <Button
                        onClick={handleDeleteConfirm}
                        color="error"
                        variant="contained"
                        disabled={
                            deleteListMutation.isPending ||
                            confirmationText !== listName
                        }
                        sx={{
                            borderRadius: 2,
                            fontWeight: 600,
                            minWidth: 120,
                        }}
                    >
                        {deleteListMutation.isPending
                            ? "Deleting..."
                            : "Delete List"}
                    </Button>
                </DialogActions>
            </Dialog>
        </Container>
    );
}

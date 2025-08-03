import {
    ArrowBack,
    Business,
    Delete,
    Group,
    MoreVert,
} from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Chip,
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
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { requireAuth } from "../../common/authGuard";
import { HouseholdMembers } from "../../components/household";
import {
    useCurrentHouseholdWithDetails,
    useDeleteHousehold,
} from "../../hooks/useHouseholdQueries";

export const Route = createFileRoute("/household/manage")({
    beforeLoad: requireAuth,
    component: HouseholdManagePage,
});

function HouseholdManagePage() {
    const navigate = useNavigate();
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    const [confirmationText, setConfirmationText] = useState("");

    // Use simplified hooks
    const {
        currentHousehold,
        currentHouseholdDetails,
        isLoading,
        error,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();

    // Delete household mutation
    const deleteHouseholdMutation = useDeleteHousehold();

    const handleBack = () => {
        navigate({ to: "/" });
    };

    const handleMenuClick = (event: React.MouseEvent<HTMLElement>) => {
        setMenuAnchor(event.currentTarget);
    };

    const handleMenuClose = () => {
        setMenuAnchor(null);
    };

    const handleDeleteClick = () => {
        setDeleteDialogOpen(true);
        setConfirmationText(""); // Reset confirmation text
        handleMenuClose();
    };

    const handleDeleteConfirm = () => {
        if (
            currentHousehold?.householdId &&
            confirmationText === householdName
        ) {
            deleteHouseholdMutation.mutate(currentHousehold.householdId);
        }
    };

    const handleDeleteDialogClose = () => {
        setDeleteDialogOpen(false);
        setConfirmationText("");
    };

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

    if (isLoading) {
        return (
            <Container maxWidth="lg">
                <Box p={3}>
                    <Typography>Loading...</Typography>
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
                        Failed to load household information
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

    const householdName = currentHouseholdDetails?.name || "Household";
    const memberCount = currentHouseholdDetails?.memberCount || 0;
    const userRole = currentHousehold.role || 0;

    const roleLabels: Record<number, string> = {
        0: "Member",
        1: "Admin",
        2: "Owner",
    };

    const roleColors: Record<
        number,
        | "default"
        | "primary"
        | "secondary"
        | "error"
        | "info"
        | "success"
        | "warning"
    > = {
        0: "default",
        1: "primary",
        2: "warning",
    };

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
                            Household Management
                        </Typography>

                        {/* Menu button for household owner */}
                        {userRole === 2 && ( // Only show for owners
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
                        )}
                    </Box>

                    {/* Household Info Card */}
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: { xs: 1.5, sm: 2 },
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
                                p: 1,
                                borderRadius: 1.5,
                                bgcolor: "primary.main",
                                color: "primary.contrastText",
                                display: "flex",
                                alignItems: "center",
                            }}
                        >
                            <Business fontSize="small" />
                        </Box>

                        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                            <Typography
                                variant="h6"
                                sx={{
                                    fontWeight: 600,
                                    fontSize: { xs: "1.1rem", sm: "1.25rem" },
                                    mb: 0.5,
                                    overflow: "hidden",
                                    textOverflow: "ellipsis",
                                    whiteSpace: "nowrap",
                                }}
                            >
                                {householdName}
                            </Typography>

                            <Stack
                                direction="row"
                                spacing={1}
                                alignItems="center"
                                sx={{ flexWrap: "wrap", gap: 0.5 }}
                            >
                                <Box
                                    sx={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 0.5,
                                    }}
                                >
                                    <Group
                                        sx={{
                                            fontSize: 14,
                                            color: "text.secondary",
                                        }}
                                    />
                                    <Typography
                                        variant="caption"
                                        color="text.secondary"
                                        sx={{
                                            fontSize: {
                                                xs: "0.7rem",
                                                sm: "0.75rem",
                                            },
                                        }}
                                    >
                                        {memberCount} members
                                    </Typography>
                                </Box>

                                <Chip
                                    label={roleLabels[userRole]}
                                    size="small"
                                    color={roleColors[userRole]}
                                    sx={{
                                        height: { xs: 20, sm: 24 },
                                        fontSize: {
                                            xs: "0.7rem",
                                            sm: "0.75rem",
                                        },
                                        "& .MuiChip-label": {
                                            px: { xs: 0.75, sm: 1 },
                                        },
                                    }}
                                />
                            </Stack>
                        </Box>
                    </Box>
                </Box>

                {/* Members Management */}
                <HouseholdMembers
                    householdId={currentHousehold.householdId}
                    currentUserRole={userRole}
                />
            </Box>

            {/* Household Actions Menu */}
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
                    <ListItemText primary="Delete Household" />
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
                        Delete Household
                    </Typography>
                </DialogTitle>
                <DialogContent>
                    <DialogContentText sx={{ mb: 2 }}>
                        Are you sure you want to delete "{householdName}"? This
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
                            • All household data and settings
                        </Typography>
                        <Typography
                            variant="body2"
                            color="error.dark"
                            sx={{ ml: 2 }}
                        >
                            • All member associations
                        </Typography>
                        <Typography
                            variant="body2"
                            color="error.dark"
                            sx={{ ml: 2 }}
                        >
                            • All shared content (future: lists, inventory,
                            etc.)
                        </Typography>
                    </Box>

                    <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                        To confirm, please type the household name:{" "}
                        <strong>{householdName}</strong>
                    </Typography>
                    <TextField
                        fullWidth
                        variant="outlined"
                        value={confirmationText}
                        onChange={(e) => setConfirmationText(e.target.value)}
                        placeholder={`Type "${householdName}" to confirm`}
                        disabled={deleteHouseholdMutation.isPending}
                        error={
                            confirmationText.length > 0 &&
                            confirmationText !== householdName
                        }
                        helperText={
                            confirmationText.length > 0 &&
                            confirmationText !== householdName
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
                        disabled={deleteHouseholdMutation.isPending}
                        sx={{ borderRadius: 2 }}
                    >
                        Cancel
                    </Button>
                    <Button
                        onClick={handleDeleteConfirm}
                        color="error"
                        variant="contained"
                        disabled={
                            deleteHouseholdMutation.isPending ||
                            confirmationText !== householdName
                        }
                        sx={{
                            borderRadius: 2,
                            fontWeight: 600,
                            minWidth: 120,
                        }}
                    >
                        {deleteHouseholdMutation.isPending
                            ? "Deleting..."
                            : "Delete Household"}
                    </Button>
                </DialogActions>
            </Dialog>
        </Container>
    );
}

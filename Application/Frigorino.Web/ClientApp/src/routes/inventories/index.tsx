import { Add, ArrowBack, Delete, Edit, MoreVert } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    Chip,
    CircularProgress,
    Container,
    IconButton,
    ListItem,
    ListItemText,
    Menu,
    MenuItem,
    List as MuiList,
    Stack,
    Typography,
} from "@mui/material";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { requireAuth } from "../../common/authGuard";
import { useCurrentHousehold } from "../../hooks/useHouseholdQueries";
import {
    useDeleteInventory,
    useHouseholdInventories,
} from "../../hooks/useInventoryQueries";
import type { InventoryDto } from "../../lib/api";

export const Route = createFileRoute("/inventories/")({
    beforeLoad: requireAuth,
    component: InventoriesPage,
});

function InventoriesPage() {
    const navigate = useNavigate();
    const { data: currentHousehold } = useCurrentHousehold();
    const {
        data: inventories,
        isLoading,
        error,
    } = useHouseholdInventories(
        currentHousehold?.householdId || 0,
        !!currentHousehold?.householdId,
    );
    const deleteInventoryMutation = useDeleteInventory();

    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [selectedInventory, setSelectedInventory] =
        useState<InventoryDto | null>(null);

    const handleBack = () => {
        navigate({ to: "/" });
    };

    const handleCreateInventory = () => {
        navigate({ to: "/inventories/create" });
    };

    const handleInventoryClick = (inventoryId: number) => {
        navigate({
            to: "/inventories/$inventoryId/view",
            params: { inventoryId: inventoryId.toString() },
        });
    };

    const handleMenuOpen = (
        event: React.MouseEvent<HTMLElement>,
        inventory: InventoryDto,
    ) => {
        setAnchorEl(event.currentTarget);
        setSelectedInventory(inventory);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
        setSelectedInventory(null);
    };

    const handleDeleteInventory = async () => {
        if (selectedInventory?.id && currentHousehold?.householdId) {
            try {
                await deleteInventoryMutation.mutateAsync({
                    householdId: currentHousehold.householdId,
                    inventoryId: selectedInventory.id,
                });
            } catch (error) {
                console.error("Failed to delete inventory:", error);
            }
        }
        handleMenuClose();
    };

    const handleEditInventory = () => {
        if (selectedInventory?.id) {
            navigate({
                to: "/inventories/$inventoryId/edit",
                params: { inventoryId: selectedInventory.id.toString() },
            });
        }
        handleMenuClose();
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString("de-DE", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
        });
    };

    if (!currentHousehold?.householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 3, px: 2 }}>
                <Alert severity="error" sx={{ borderRadius: 2 }}>
                    You need to select a household to view inventories.
                    <Button
                        onClick={handleBack}
                        sx={{ mt: 1, display: "block" }}
                    >
                        Go back to dashboard
                    </Button>
                </Alert>
            </Container>
        );
    }

    return (
        <Container maxWidth="sm" sx={{ py: 3, px: 2 }}>
            {/* Header */}
            <Box
                sx={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    mb: { xs: 2, sm: 3 },
                }}
            >
                <Box
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: { xs: 1, sm: 2 },
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
                            fontSize: { xs: "1.4rem", sm: "1.8rem" },
                        }}
                    >
                        Inventories
                    </Typography>
                </Box>

                <Button
                    variant="contained"
                    startIcon={<Add />}
                    onClick={handleCreateInventory}
                    sx={{
                        borderRadius: 2,
                        textTransform: "none",
                        fontWeight: 600,
                        px: { xs: 2, sm: 3 },
                        py: 1,
                    }}
                >
                    Create
                </Button>
            </Box>

            {/* Content */}
            {isLoading && (
                <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
                    <CircularProgress />
                </Box>
            )}

            {error && (
                <Alert severity="error" sx={{ borderRadius: 2, mb: 3 }}>
                    Failed to load inventories. Please try again.
                </Alert>
            )}

            {inventories && inventories.length === 0 && !isLoading && (
                <Card sx={{ borderRadius: 3, textAlign: "center", py: 4 }}>
                    <CardContent>
                        <Typography variant="h6" gutterBottom>
                            No inventories yet
                        </Typography>
                        <Typography
                            variant="body2"
                            color="text.secondary"
                            sx={{ mb: 3 }}
                        >
                            Create your first inventory to get started
                        </Typography>
                        <Button
                            variant="contained"
                            startIcon={<Add />}
                            onClick={handleCreateInventory}
                            sx={{
                                borderRadius: 2,
                                textTransform: "none",
                                fontWeight: 600,
                            }}
                        >
                            Create Your First Inventory
                        </Button>
                    </CardContent>
                </Card>
            )}

            {inventories && inventories.length > 0 && (
                <Stack spacing={2}>
                    {inventories.map((inventory) => (
                        <Card
                            key={inventory.id}
                            sx={{
                                borderRadius: 2,
                                boxShadow: "0 2px 8px rgba(0,0,0,0.1)",
                                "&:hover": {
                                    boxShadow: "0 4px 12px rgba(0,0,0,0.15)",
                                },
                                transition: "all 0.3s ease",
                            }}
                        >
                            <CardContent sx={{ py: 2 }}>
                                <MuiList disablePadding>
                                    <ListItem
                                        sx={{
                                            px: 0,
                                            cursor: "pointer",
                                            "&:hover": {
                                                bgcolor: "action.hover",
                                            },
                                        }}
                                        onClick={() =>
                                            handleInventoryClick(inventory.id!)
                                        }
                                        secondaryAction={
                                            <Box
                                                sx={{
                                                    display: "flex",
                                                    alignItems: "center",
                                                    gap: 1,
                                                }}
                                            >
                                                <Chip
                                                    label={
                                                        inventory.createdAt
                                                            ? formatDate(
                                                                  inventory.createdAt,
                                                              )
                                                            : ""
                                                    }
                                                    size="small"
                                                    variant="outlined"
                                                    sx={{ fontSize: "0.7rem" }}
                                                />
                                                <IconButton
                                                    size="small"
                                                    onClick={(e) => {
                                                        e.stopPropagation();
                                                        handleMenuOpen(
                                                            e,
                                                            inventory,
                                                        );
                                                    }}
                                                    disabled={
                                                        deleteInventoryMutation.isPending
                                                    }
                                                >
                                                    <MoreVert fontSize="small" />
                                                </IconButton>
                                            </Box>
                                        }
                                    >
                                        <ListItemText
                                            primary={
                                                <Typography
                                                    variant="body1"
                                                    sx={{ fontWeight: 600 }}
                                                >
                                                    {inventory.name}
                                                </Typography>
                                            }
                                            secondary={
                                                inventory.description && (
                                                    <Typography
                                                        variant="body2"
                                                        color="text.secondary"
                                                        sx={{ mt: 0.5 }}
                                                    >
                                                        {inventory.description}
                                                    </Typography>
                                                )
                                            }
                                        />
                                    </ListItem>
                                </MuiList>
                            </CardContent>
                        </Card>
                    ))}
                </Stack>
            )}

            {/* Context Menu */}
            <Menu
                anchorEl={anchorEl}
                open={Boolean(anchorEl)}
                onClose={handleMenuClose}
                PaperProps={{ sx: { borderRadius: 2, minWidth: 160 } }}
            >
                <MenuItem onClick={handleEditInventory}>
                    <Edit fontSize="small" sx={{ mr: 1 }} />
                    Edit
                </MenuItem>
                <MenuItem
                    onClick={handleDeleteInventory}
                    disabled={deleteInventoryMutation.isPending}
                    sx={{ color: "error.main" }}
                >
                    <Delete fontSize="small" sx={{ mr: 1 }} />
                    Delete
                </MenuItem>
            </Menu>

            <Box sx={{ height: 4 }} />
        </Container>
    );
}

export default InventoriesPage;

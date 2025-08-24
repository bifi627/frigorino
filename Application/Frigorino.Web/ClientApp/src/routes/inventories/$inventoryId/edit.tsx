import {
    ArrowBack,
    Delete,
    Inventory2,
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
import { useTranslation } from "react-i18next";
import { requireAuth } from "../../../common/authGuard";
import { useCurrentHouseholdWithDetails } from "../../../hooks/useHouseholdQueries";
import {
    useDeleteInventory,
    useInventory,
    useUpdateInventory,
    type UpdateInventoryRequest,
} from "../../../hooks/useInventoryQueries";

export const Route = createFileRoute("/inventories/$inventoryId/edit")({
    beforeLoad: requireAuth,
    component: InventoryEditPage,
});

function InventoryEditPage() {
    const navigate = useNavigate();
    const router = useRouter();
    const { inventoryId } = Route.useParams();
    const inventoryIdNum = parseInt(inventoryId, 10);
    const { t } = useTranslation();

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    const [confirmationText, setConfirmationText] = useState("");
    const [editedName, setEditedName] = useState("");

    const {
        currentHousehold,
        isLoading: householdLoading,
        error: householdError,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();

    const {
        data: inventory,
        isLoading: inventoryLoading,
        error: inventoryError,
    } = useInventory(
        currentHousehold?.householdId || 0,
        inventoryIdNum,
        hasActiveHousehold && !isNaN(inventoryIdNum),
    );

    const updateInventoryMutation = useUpdateInventory();
    const deleteInventoryMutation = useDeleteInventory();

    useEffect(() => {
        if (inventory) {
            setEditedName(inventory.name || "");
        }
    }, [inventory]);

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
        if (!currentHousehold?.householdId || !inventory?.id) return;

        const updateData: UpdateInventoryRequest = {
            name: editedName.trim(),
        };

        updateInventoryMutation.mutate(
            {
                householdId: currentHousehold.householdId,
                inventoryId: inventory.id,
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
            inventory?.id &&
            confirmationText === inventory.name
        ) {
            deleteInventoryMutation.mutate(
                {
                    householdId: currentHousehold.householdId,
                    inventoryId: inventory.id,
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

    const isLoading = householdLoading || inventoryLoading;
    const error = householdError || inventoryError;

    if (isLoading) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                        <Skeleton
                            variant="rectangular"
                            height={40}
                            sx={{ mb: 1, borderRadius: 1 }}
                        />
                        <Skeleton variant="text" width="60%" height={32} />
                    </Box>
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
                        {t("inventory.failedToLoadInventoryInformation")}
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
                        {t("common.createOrSelectHouseholdFirst")}
                    </Alert>
                </Box>
            </Container>
        );
    }

    if (!inventory) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    <Alert severity="warning" sx={{ borderRadius: 2 }}>
                        {t("inventory.inventoryNotFoundOrNoAccess")}
                    </Alert>
                </Box>
            </Container>
        );
    }

    const inventoryName = inventory.name || t("inventory.untitledInventory");
    const isFormValid = editedName.trim().length > 0;

    return (
        <Container
            maxWidth="md"
            sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
        >
            <Box>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
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
                            {t("inventory.editInventory")}
                        </Typography>

                        <IconButton
                            onClick={handleMenuClick}
                            size="small"
                            sx={{
                                bgcolor: "background.paper",
                                border: 1,
                                borderColor: "divider",
                                "&:hover": { bgcolor: "action.hover" },
                            }}
                        >
                            <MoreVert fontSize="small" />
                        </IconButton>
                    </Box>

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
                                    <Inventory2 fontSize="small" />
                                </Box>
                                <Typography
                                    variant="h6"
                                    sx={{ fontWeight: 600 }}
                                >
                                    {t("inventory.editInventoryDetails")}
                                </Typography>
                            </Box>

                            <TextField
                                label={t("inventory.inventoryName")}
                                value={editedName}
                                onChange={(e) => setEditedName(e.target.value)}
                                fullWidth
                                required
                                error={editedName.trim().length === 0}
                                helperText={
                                    editedName.trim().length === 0
                                        ? t("inventory.inventoryNameRequired")
                                        : ""
                                }
                                sx={{
                                    "& .MuiOutlinedInput-root": {
                                        borderRadius: 2,
                                    },
                                }}
                            />
                        </Box>
                    </Box>

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
                            disabled={updateInventoryMutation.isPending}
                            sx={{ borderRadius: 2, minWidth: 100 }}
                        >
                            {t("common.cancel")}
                        </Button>
                        <Button
                            variant="contained"
                            onClick={handleSave}
                            disabled={
                                updateInventoryMutation.isPending ||
                                !isFormValid
                            }
                            startIcon={<Save />}
                            sx={{ borderRadius: 2, minWidth: 100 }}
                        >
                            {updateInventoryMutation.isPending
                                ? t("common.saving")
                                : t("common.save")}
                        </Button>
                    </Box>
                </Box>
            </Box>

            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={handleMenuClose}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                transformOrigin={{ vertical: "top", horizontal: "right" }}
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
                    <ListItemText primary={t("inventory.deleteInventory")} />
                </MenuItem>
            </Menu>

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
                        {t("inventory.deleteInventory")}
                    </Typography>
                </DialogTitle>
                <DialogContent>
                    <DialogContentText sx={{ mb: 2 }}>
                        {t("inventory.confirmDeleteInventory", {
                            inventoryName,
                        })}
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
                            {t("common.warningPermanentlyDelete")}
                        </Typography>
                        <Typography
                            variant="body2"
                            color="error.dark"
                            sx={{ mt: 1, ml: 2 }}
                        >
                            {t("inventory.entireInventory")}
                        </Typography>
                        <Typography
                            variant="body2"
                            color="error.dark"
                            sx={{ ml: 2 }}
                        >
                            {t("inventory.allInventoryItems")}
                        </Typography>
                    </Box>

                    <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                        {t("inventory.confirmTypeInventoryName")}{" "}
                        <strong>{inventoryName}</strong>
                    </Typography>
                    <TextField
                        fullWidth
                        variant="outlined"
                        value={confirmationText}
                        onChange={(e) => setConfirmationText(e.target.value)}
                        placeholder={t("inventory.typeInventoryNameToConfirm", {
                            inventoryName,
                        })}
                        disabled={deleteInventoryMutation.isPending}
                        error={
                            confirmationText.length > 0 &&
                            confirmationText !== inventoryName
                        }
                        helperText={
                            confirmationText.length > 0 &&
                            confirmationText !== inventoryName
                                ? t("common.nameDoesNotMatch")
                                : ""
                        }
                        sx={{ "& .MuiOutlinedInput-root": { borderRadius: 2 } }}
                    />
                </DialogContent>
                <DialogActions sx={{ p: 3, pt: 1 }}>
                    <Button
                        onClick={handleDeleteDialogClose}
                        disabled={deleteInventoryMutation.isPending}
                        sx={{ borderRadius: 2 }}
                    >
                        {t("common.cancel")}
                    </Button>
                    <Button
                        onClick={handleDeleteConfirm}
                        color="error"
                        variant="contained"
                        disabled={
                            deleteInventoryMutation.isPending ||
                            confirmationText !== inventoryName
                        }
                        sx={{ borderRadius: 2, fontWeight: 600, minWidth: 120 }}
                    >
                        {deleteInventoryMutation.isPending
                            ? t("common.deleting")
                            : t("inventory.deleteInventory")}
                    </Button>
                </DialogActions>
            </Dialog>
        </Container>
    );
}

import { ArrowBack, Delete, MoreVert } from "@mui/icons-material";
import {
    Alert,
    Box,
    Container,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Skeleton,
    Typography,
} from "@mui/material";
import { useParams, useRouter } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { DeleteInventoryConfirmDialog } from "../components/DeleteInventoryConfirmDialog";
import { EditInventoryForm } from "../components/EditInventoryForm";
import { useInventory } from "../useInventory";

export const InventoryEditPage = () => {
    const router = useRouter();
    const { inventoryId } = useParams({ from: "/inventories/$inventoryId/edit" });
    const { t } = useTranslation();
    const inventoryIdNum = parseInt(inventoryId, 10);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);

    const {
        currentHousehold,
        isLoading: householdLoading,
        error: householdError,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();

    const householdId = currentHousehold?.householdId ?? 0;
    const {
        data: inventory,
        isLoading: inventoryLoading,
        error: inventoryError,
    } = useInventory(
        householdId,
        inventoryIdNum,
        hasActiveHousehold && !isNaN(inventoryIdNum),
    );

    const handleBack = () => router.history.back();
    const handleMenuClick = (event: React.MouseEvent<HTMLElement>) =>
        setMenuAnchor(event.currentTarget);
    const handleMenuClose = () => setMenuAnchor(null);
    const handleDeleteClick = () => {
        setDeleteDialogOpen(true);
        handleMenuClose();
    };

    const isLoading = householdLoading || inventoryLoading;
    const error = householdError || inventoryError;

    if (isLoading) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    <Skeleton variant="rectangular" height={40} sx={{ mb: 1 }} />
                    <Skeleton variant="text" width="60%" height={32} />
                </Box>
                <Skeleton variant="rectangular" height={200} />
            </Container>
        );
    }

    if (error) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("inventory.failedToLoadInventoryInformation")}
                </Alert>
            </Container>
        );
    }

    if (!hasActiveHousehold || !householdId) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    if (!inventory) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="warning">
                    {t("inventory.inventoryNotFoundOrNoAccess")}
                </Alert>
            </Container>
        );
    }

    const inventoryName = inventory.name || t("inventory.untitledInventory");

    return (
        <Container maxWidth="md" sx={pageContainerSx}>
            <Box
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: { xs: 1, sm: 2 },
                    mb: { xs: 2, sm: 3 },
                }}
            >
                <IconButton onClick={handleBack}>
                    <ArrowBack />
                </IconButton>

                <Typography
                    variant="h5"
                    component="h1"
                    sx={{ fontWeight: 600, flexGrow: 1 }}
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

            <EditInventoryForm
                householdId={householdId}
                inventory={inventory}
            />

            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={handleMenuClose}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                transformOrigin={{ vertical: "top", horizontal: "right" }}
                elevation={4}
                slotProps={{ paper: { sx: { minWidth: 200, mt: 1 } } }}
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

            {inventory.id && (
                <DeleteInventoryConfirmDialog
                    open={deleteDialogOpen}
                    onClose={() => setDeleteDialogOpen(false)}
                    householdId={householdId}
                    inventoryId={inventory.id}
                    inventoryName={inventoryName}
                />
            )}
        </Container>
    );
};

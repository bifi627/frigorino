import { Add } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    Stack,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { InventoryResponse } from "../../../lib/api";
import { pageContainerSx } from "../../../theme";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { InventoryActionsMenu } from "../components/InventoryActionsMenu";
import { InventorySummaryCard } from "../components/InventorySummaryCard";
import { useDeleteInventory } from "../useDeleteInventory";
import { useHouseholdInventories } from "../useHouseholdInventories";

export const InventoriesPage = () => {
    const navigate = useNavigate();
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: inventories,
        isLoading,
        error,
    } = useHouseholdInventories(householdId, householdId > 0);
    const deleteInventoryMutation = useDeleteInventory();

    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [selectedInventory, setSelectedInventory] =
        useState<InventoryResponse | null>(null);

    const handleBack = () => navigate({ to: "/" });
    const handleCreateInventory = () => navigate({ to: "/inventories/create" });
    const handleInventoryClick = (inventoryId: number) =>
        navigate({
            to: "/inventories/$inventoryId/view",
            params: { inventoryId: inventoryId.toString() },
        });

    const handleMenuOpen = (
        event: React.MouseEvent<HTMLElement>,
        inventory: InventoryResponse,
    ) => {
        setAnchorEl(event.currentTarget);
        setSelectedInventory(inventory);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
        setSelectedInventory(null);
    };

    const handleDeleteInventory = () => {
        if (selectedInventory?.id && householdId) {
            deleteInventoryMutation.mutate({
                path: { householdId, inventoryId: selectedInventory.id },
            });
        }
        handleMenuClose();
    };

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("inventory.selectHouseholdToViewInventories")}
                    <Button
                        onClick={handleBack}
                        sx={{ mt: 1, display: "block" }}
                    >
                        {t("common.goBackToDashboard")}
                    </Button>
                </Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={t("inventory.inventories")}
                directActions={[
                    { icon: <Add />, onClick: handleCreateInventory },
                ]}
                menuActions={[]}
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
                {isLoading && (
                    <Box
                        sx={{
                            display: "flex",
                            justifyContent: "center",
                            py: 4,
                        }}
                    >
                        <CircularProgress />
                    </Box>
                )}
                {error && (
                    <Alert severity="error" sx={{ mb: 3 }}>
                        {t("inventory.failedToLoadInventories")}
                    </Alert>
                )}
                {inventories && inventories.length === 0 && !isLoading && (
                    <Card elevation={1} sx={{ textAlign: "center", py: 4 }}>
                        <CardContent>
                            <Typography variant="h6" gutterBottom>
                                {t("inventory.noInventoriesYet")}
                            </Typography>
                            <Typography
                                variant="body2"
                                sx={{
                                    color: "text.secondary",
                                    mb: 3,
                                }}
                            >
                                {t("inventory.createFirstInventory")}
                            </Typography>
                            <Button
                                variant="contained"
                                startIcon={<Add />}
                                onClick={handleCreateInventory}
                                sx={{ fontWeight: 600 }}
                            >
                                {t("inventory.createYourFirstInventory")}
                            </Button>
                        </CardContent>
                    </Card>
                )}
                {inventories && inventories.length > 0 && (
                    <Stack spacing={2}>
                        {inventories.map((inventory) => (
                            <InventorySummaryCard
                                key={inventory.id}
                                inventory={inventory}
                                onClick={handleInventoryClick}
                                onMenuOpen={handleMenuOpen}
                                menuDisabled={deleteInventoryMutation.isPending}
                            />
                        ))}
                    </Stack>
                )}
                <InventoryActionsMenu
                    anchorEl={anchorEl}
                    onClose={handleMenuClose}
                    onDelete={handleDeleteInventory}
                    isDeleting={deleteInventoryMutation.isPending}
                />
            </Container>
        </>
    );
};

import { Delete } from "@mui/icons-material";
import { Alert, Box, Container, Skeleton } from "@mui/material";
import { useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { DeleteInventoryConfirmDialog } from "../components/DeleteInventoryConfirmDialog";
import { EditInventoryForm } from "../components/EditInventoryForm";
import { InventorySettingsCard } from "../components/InventorySettingsCard";
import { useInventory } from "../useInventory";

export const InventoryEditPage = () => {
    const { inventoryId } = useParams({
        from: "/inventories/$inventoryId/edit",
    });
    const { t } = useTranslation();
    const inventoryIdNum = parseInt(inventoryId, 10);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

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

    const handleDeleteClick = () => setDeleteDialogOpen(true);

    const isLoading = householdLoading || inventoryLoading;
    const error = householdError || inventoryError;

    if (isLoading) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    <Skeleton
                        variant="rectangular"
                        height={40}
                        sx={{ mb: 1 }}
                    />
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

    const menuActions: HeadNavigationAction[] = [
        {
            text: t("inventory.deleteInventory"),
            icon: <Delete fontSize="small" />,
            onClick: handleDeleteClick,
        },
    ];

    return (
        <>
            <PageHeadActionBar
                title={t("inventory.editInventory")}
                maxWidth="md"
                directActions={[]}
                menuActions={menuActions}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <EditInventoryForm
                    householdId={householdId}
                    inventory={inventory}
                />

                {inventory.id && (
                    <InventorySettingsCard
                        householdId={householdId}
                        inventoryId={inventory.id}
                    />
                )}

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
        </>
    );
};

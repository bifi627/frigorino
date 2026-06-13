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
import { toast } from "sonner";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import type { SortBlueprintResponse } from "../../../lib/api";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { BlueprintActionsMenu } from "../components/BlueprintActionsMenu";
import { BlueprintSummaryCard } from "../components/BlueprintSummaryCard";
import { useCreateSortBlueprint } from "../useCreateSortBlueprint";
import { useDeleteSortBlueprint } from "../useDeleteSortBlueprint";
import { useSortBlueprints } from "../useSortBlueprints";

export function BlueprintsPage() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { currentHousehold, isLoading, error, hasActiveHousehold } =
        useCurrentHouseholdWithDetails();

    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: blueprints,
        isLoading: blueprintsLoading,
        error: blueprintsError,
    } = useSortBlueprints(householdId, householdId > 0);

    const duplicateBlueprint = useCreateSortBlueprint();
    const deleteBlueprint = useDeleteSortBlueprint();

    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [selected, setSelected] = useState<SortBlueprintResponse | null>(
        null,
    );

    const handleCreate = () => navigate({ to: "/household/blueprints/create" });

    const handleOpenDetails = (blueprintId: number) =>
        navigate({
            to: "/household/blueprints/$blueprintId/view",
            params: { blueprintId: blueprintId.toString() },
        });

    const handleMenuOpen = (
        event: React.MouseEvent<HTMLElement>,
        blueprint: SortBlueprintResponse,
    ) => {
        setAnchorEl(event.currentTarget);
        setSelected(blueprint);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
        setSelected(null);
    };

    const handleEdit = () => {
        if (selected) {
            navigate({
                to: "/household/blueprints/$blueprintId/edit",
                params: { blueprintId: selected.id.toString() },
            });
        }
        handleMenuClose();
    };

    const handleDuplicate = () => {
        if (selected) {
            duplicateBlueprint.mutate(
                {
                    path: { householdId },
                    body: {
                        name: `${selected.name} ${t("blueprints.copySuffix")}`,
                        categories: selected.categories,
                    },
                },
                {
                    onSuccess: () => toast.success(t("blueprints.saved")),
                    onError: () => toast.error(t("blueprints.saveFailed")),
                },
            );
        }
        handleMenuClose();
    };

    const handleDelete = () => {
        if (selected) {
            // The hook surfaces the "deleted" toast with an Undo action on success.
            deleteBlueprint.mutate({
                path: { householdId, blueprintId: selected.id },
            });
        }
        handleMenuClose();
    };

    if (isLoading || (householdId > 0 && blueprintsLoading)) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
                    <CircularProgress />
                </Box>
            </Container>
        );
    }

    if (error || !hasActiveHousehold || householdId <= 0) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={t("blueprints.manage")}
                section="blueprints"
                maxWidth="md"
                directActions={[
                    {
                        icon: <Add />,
                        onClick: handleCreate,
                        testId: "blueprint-new",
                    },
                ]}
                menuActions={[]}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <Typography
                    variant="body2"
                    color="text.secondary"
                    sx={{ mb: 2 }}
                >
                    {t("blueprints.manageHint")}
                </Typography>

                {blueprintsError && (
                    <Alert severity="error" sx={{ mb: 3 }}>
                        {t("blueprints.saveFailed")}
                    </Alert>
                )}

                {blueprints && blueprints.length === 0 && (
                    <Card elevation={1} sx={{ textAlign: "center", py: 4 }}>
                        <CardContent>
                            <Typography variant="h6" gutterBottom>
                                {t("blueprints.empty")}
                            </Typography>
                            <Button
                                variant="contained"
                                startIcon={<Add />}
                                onClick={handleCreate}
                                sx={{ fontWeight: 600, mt: 1 }}
                            >
                                {t("blueprints.newBlueprint")}
                            </Button>
                        </CardContent>
                    </Card>
                )}

                {blueprints && blueprints.length > 0 && (
                    <Stack spacing={2}>
                        {blueprints.map((blueprint) => (
                            <BlueprintSummaryCard
                                key={blueprint.id}
                                blueprint={blueprint}
                                onClick={handleOpenDetails}
                                onMenuOpen={handleMenuOpen}
                                menuDisabled={deleteBlueprint.isPending}
                            />
                        ))}
                    </Stack>
                )}

                <BlueprintActionsMenu
                    anchorEl={anchorEl}
                    onClose={handleMenuClose}
                    onEdit={handleEdit}
                    onDuplicate={handleDuplicate}
                    onDelete={handleDelete}
                    isBusy={
                        duplicateBlueprint.isPending ||
                        deleteBlueprint.isPending
                    }
                />
            </Container>
        </>
    );
}

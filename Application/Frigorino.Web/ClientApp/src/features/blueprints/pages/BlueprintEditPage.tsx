import { Delete } from "@mui/icons-material";
import { Alert, Container, Skeleton } from "@mui/material";
import { useNavigate, useParams } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { BlueprintEditForm } from "../components/BlueprintEditForm";
import { useDeleteSortBlueprint } from "../useDeleteSortBlueprint";
import { useSortBlueprint } from "../useSortBlueprint";

export const BlueprintEditPage = () => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { blueprintId } = useParams({
        from: "/household/blueprints/$blueprintId/edit",
    });
    const blueprintIdNum = parseInt(blueprintId, 10);

    const {
        currentHousehold,
        isLoading: householdLoading,
        error: householdError,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: blueprint,
        isLoading: blueprintLoading,
        error: blueprintError,
    } = useSortBlueprint(
        householdId,
        blueprintIdNum,
        hasActiveHousehold && !isNaN(blueprintIdNum),
    );

    const deleteBlueprint = useDeleteSortBlueprint();

    const isLoading = householdLoading || blueprintLoading;
    const error = householdError || blueprintError;

    const handleDelete = () => {
        if (!blueprint) {
            return;
        }
        // The hook surfaces the "deleted" toast with an Undo action on success.
        deleteBlueprint.mutate(
            { path: { householdId, blueprintId: blueprint.id } },
            {
                onSuccess: () => navigate({ to: "/household/blueprints" }),
            },
        );
    };

    if (isLoading) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Skeleton variant="rectangular" height={40} sx={{ mb: 2 }} />
                <Skeleton variant="rectangular" height={200} />
            </Container>
        );
    }

    if (error) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="error">{t("blueprints.failedToLoad")}</Alert>
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

    if (!blueprint) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="warning">{t("blueprints.notFound")}</Alert>
            </Container>
        );
    }

    const menuActions: HeadNavigationAction[] = [
        {
            text: t("blueprints.delete"),
            icon: <Delete fontSize="small" color="error" />,
            onClick: handleDelete,
            color: "error",
            testId: "blueprint-delete-button",
        },
    ];

    return (
        <>
            <PageHeadActionBar
                title={t("blueprints.editTitle")}
                section="blueprints"
                maxWidth="md"
                directActions={[]}
                menuActions={menuActions}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <BlueprintEditForm
                    key={blueprint.id}
                    householdId={householdId}
                    blueprint={blueprint}
                />
            </Container>
        </>
    );
};

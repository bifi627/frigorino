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
import { DeleteListConfirmDialog } from "../components/DeleteListConfirmDialog";
import { EditListForm } from "../components/EditListForm";
import { useList } from "../useList";

export const ListEditPage = () => {
    const { listId } = useParams({ from: "/lists/$listId/edit" });
    const { t } = useTranslation();
    const listIdNum = parseInt(listId, 10);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

    const {
        currentHousehold,
        isLoading: householdLoading,
        error: householdError,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();

    const householdId = currentHousehold?.householdId ?? 0;
    const {
        data: list,
        isLoading: listLoading,
        error: listError,
    } = useList(
        householdId,
        listIdNum,
        hasActiveHousehold && !isNaN(listIdNum),
    );

    const handleDeleteClick = () => setDeleteDialogOpen(true);

    const isLoading = householdLoading || listLoading;
    const error = householdError || listError;

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
                    {t("lists.failedToLoadListInformation")}
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

    if (!list) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="warning">
                    {t("lists.listNotFoundOrNoAccess")}
                </Alert>
            </Container>
        );
    }

    const listName = list.name || t("lists.untitledList");

    const menuActions: HeadNavigationAction[] = [
        {
            text: t("lists.deleteList"),
            icon: <Delete fontSize="small" />,
            onClick: handleDeleteClick,
        },
    ];

    return (
        <>
            <PageHeadActionBar
                title={t("lists.editList")}
                maxWidth="md"
                directActions={[]}
                menuActions={menuActions}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <EditListForm householdId={householdId} list={list} />

                {list.id && (
                    <DeleteListConfirmDialog
                        open={deleteDialogOpen}
                        onClose={() => setDeleteDialogOpen(false)}
                        householdId={householdId}
                        listId={list.id}
                        listName={listName}
                    />
                )}
            </Container>
        </>
    );
};

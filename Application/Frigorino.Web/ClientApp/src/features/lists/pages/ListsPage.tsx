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
import type { ListResponse } from "../../../lib/api";
import { pageContainerSx } from "../../../theme";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { ListActionsMenu } from "../components/ListActionsMenu";
import { ListSummaryCard } from "../components/ListSummaryCard";
import { useDeleteList } from "../useDeleteList";
import { useHouseholdLists } from "../useHouseholdLists";

export const ListsPage = () => {
    const navigate = useNavigate();
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: lists,
        isLoading,
        error,
    } = useHouseholdLists(householdId, householdId > 0);
    const deleteListMutation = useDeleteList();

    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [selectedList, setSelectedList] = useState<ListResponse | null>(null);

    const handleBack = () => navigate({ to: "/" });
    const handleCreateList = () => navigate({ to: "/lists/create" });
    const handleListClick = (listId: number) =>
        navigate({
            to: "/lists/$listId/view",
            params: { listId: listId.toString() },
        });

    const handleMenuOpen = (
        event: React.MouseEvent<HTMLElement>,
        list: ListResponse,
    ) => {
        setAnchorEl(event.currentTarget);
        setSelectedList(list);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
        setSelectedList(null);
    };

    const handleDeleteList = () => {
        if (selectedList?.id && householdId) {
            deleteListMutation.mutate({
                path: { householdId, listId: selectedList.id },
            });
        }
        handleMenuClose();
    };

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("lists.selectHouseholdToViewLists")}
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
                title={t("lists.shoppingLists")}
                directActions={[{ icon: <Add />, onClick: handleCreateList }]}
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
                        {t("lists.failedToLoadLists")}
                    </Alert>
                )}
                {lists && lists.length === 0 && !isLoading && (
                    <Card elevation={1} sx={{ textAlign: "center", py: 4 }}>
                        <CardContent>
                            <Typography variant="h6" gutterBottom>
                                {t("lists.noListsYet")}
                            </Typography>
                            <Typography
                                variant="body2"
                                sx={{
                                    color: "text.secondary",
                                    mb: 3,
                                }}
                            >
                                {t("lists.createFirstShoppingList")}
                            </Typography>
                            <Button
                                variant="contained"
                                startIcon={<Add />}
                                onClick={handleCreateList}
                                sx={{ fontWeight: 600 }}
                            >
                                {t("lists.createYourFirstList")}
                            </Button>
                        </CardContent>
                    </Card>
                )}
                {lists && lists.length > 0 && (
                    <Stack spacing={2}>
                        {lists.map((list) => (
                            <ListSummaryCard
                                key={list.id}
                                list={list}
                                onClick={handleListClick}
                                onMenuOpen={handleMenuOpen}
                                menuDisabled={deleteListMutation.isPending}
                            />
                        ))}
                    </Stack>
                )}
                <ListActionsMenu
                    anchorEl={anchorEl}
                    onClose={handleMenuClose}
                    onDelete={handleDeleteList}
                    isDeleting={deleteListMutation.isPending}
                />
            </Container>
        </>
    );
};

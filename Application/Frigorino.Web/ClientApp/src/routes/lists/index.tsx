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
import { useTranslation } from "react-i18next";
import { requireAuth } from "../../common/authGuard";
import { useCurrentHousehold } from "../../hooks/useHouseholdQueries";
import { useDeleteList, useHouseholdLists } from "../../hooks/useListQueries";
import type { ListDto } from "../../lib/api";

export const Route = createFileRoute("/lists/")({
    beforeLoad: requireAuth,
    component: ListsPage,
});

function ListsPage() {
    const navigate = useNavigate();
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const {
        data: lists,
        isLoading,
        error,
    } = useHouseholdLists(
        currentHousehold?.householdId || 0,
        !!currentHousehold?.householdId,
    );
    const deleteListMutation = useDeleteList();

    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [selectedList, setSelectedList] = useState<ListDto | null>(null);

    const handleBack = () => {
        navigate({ to: "/" });
    };

    const handleCreateList = () => {
        navigate({ to: "/lists/create" });
    };

    const handleListClick = (listId: number) => {
        navigate({
            to: "/lists/$listId/view",
            params: { listId: listId.toString() },
        });
    };

    const handleMenuOpen = (
        event: React.MouseEvent<HTMLElement>,
        list: ListDto,
    ) => {
        setAnchorEl(event.currentTarget);
        setSelectedList(list);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
        setSelectedList(null);
    };

    const handleDeleteList = async () => {
        if (selectedList?.id && currentHousehold?.householdId) {
            try {
                await deleteListMutation.mutateAsync({
                    householdId: currentHousehold.householdId,
                    listId: selectedList.id,
                });
            } catch (error) {
                console.error("Failed to delete list:", error);
            }
        }
        handleMenuClose();
    };

    const handleEditList = () => {
        if (selectedList?.id) {
            // TODO: Navigate to edit page when implemented
            window.console.log("Edit list:", selectedList.id);
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

    // Show error if no current household
    if (!currentHousehold?.householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 3, px: 2 }}>
                <Alert severity="error" sx={{ borderRadius: 2 }}>
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
                        {t("lists.shoppingLists")}
                    </Typography>
                </Box>

                <Button
                    variant="contained"
                    startIcon={<Add />}
                    onClick={handleCreateList}
                    sx={{
                        borderRadius: 2,
                        textTransform: "none",
                        fontWeight: 600,
                        px: { xs: 2, sm: 3 },
                        py: 1,
                    }}
                >
                    {t("common.create")}
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
                    {t("lists.failedToLoadLists")}
                </Alert>
            )}

            {lists && lists.length === 0 && !isLoading && (
                <Card sx={{ borderRadius: 3, textAlign: "center", py: 4 }}>
                    <CardContent>
                        <Typography variant="h6" gutterBottom>
                            {t("lists.noListsYet")}
                        </Typography>
                        <Typography
                            variant="body2"
                            color="text.secondary"
                            sx={{ mb: 3 }}
                        >
                            {t("lists.createFirstShoppingList")}
                        </Typography>
                        <Button
                            variant="contained"
                            startIcon={<Add />}
                            onClick={handleCreateList}
                            sx={{
                                borderRadius: 2,
                                textTransform: "none",
                                fontWeight: 600,
                            }}
                        >
                            {t("lists.createYourFirstList")}
                        </Button>
                    </CardContent>
                </Card>
            )}

            {lists && lists.length > 0 && (
                <Stack spacing={2}>
                    {lists.map((list) => (
                        <Card
                            key={list.id}
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
                                            handleListClick(list.id!)
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
                                                        list.createdAt
                                                            ? formatDate(
                                                                  list.createdAt,
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
                                                        handleMenuOpen(e, list);
                                                    }}
                                                    disabled={
                                                        deleteListMutation.isPending
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
                                                    {list.name}
                                                </Typography>
                                            }
                                            secondary={
                                                list.description && (
                                                    <Typography
                                                        variant="body2"
                                                        color="text.secondary"
                                                        sx={{ mt: 0.5 }}
                                                    >
                                                        {list.description}
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
                PaperProps={{
                    sx: { borderRadius: 2, minWidth: 160 },
                }}
            >
                <MenuItem onClick={handleEditList} disabled>
                    <Edit fontSize="small" sx={{ mr: 1 }} />
                    {t("common.edit")}
                </MenuItem>
                <MenuItem
                    onClick={handleDeleteList}
                    disabled={deleteListMutation.isPending}
                    sx={{ color: "error.main" }}
                >
                    <Delete fontSize="small" sx={{ mr: 1 }} />
                    {t("common.delete")}
                </MenuItem>
            </Menu>

            {/* Bottom Spacing */}
            <Box sx={{ height: 4 }} />
        </Container>
    );
}

export default ListsPage;

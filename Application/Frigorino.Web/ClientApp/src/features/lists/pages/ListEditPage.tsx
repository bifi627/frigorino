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
import { DeleteListConfirmDialog } from "../components/DeleteListConfirmDialog";
import { EditListForm } from "../components/EditListForm";
import { useList } from "../useList";

export const ListEditPage = () => {
    const router = useRouter();
    const { listId } = useParams({ from: "/lists/$listId/edit" });
    const { t } = useTranslation();
    const listIdNum = parseInt(listId, 10);

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
        data: list,
        isLoading: listLoading,
        error: listError,
    } = useList(householdId, listIdNum, hasActiveHousehold && !isNaN(listIdNum));

    const handleBack = () => router.history.back();
    const handleMenuClick = (event: React.MouseEvent<HTMLElement>) =>
        setMenuAnchor(event.currentTarget);
    const handleMenuClose = () => setMenuAnchor(null);
    const handleDeleteClick = () => {
        setDeleteDialogOpen(true);
        handleMenuClose();
    };

    const isLoading = householdLoading || listLoading;
    const error = householdError || listError;

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
                    {t("lists.editList")}
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

            <EditListForm householdId={householdId} list={list} />

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
                    <ListItemText primary={t("lists.deleteList")} />
                </MenuItem>
            </Menu>

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
    );
};

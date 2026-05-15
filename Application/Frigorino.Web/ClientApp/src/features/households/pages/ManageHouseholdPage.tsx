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
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { DeleteHouseholdDialog } from "../components/DeleteHouseholdDialog";
import { HouseholdSummaryCard } from "../components/HouseholdSummaryCard";
import { HouseholdRoleValue } from "../householdRole";
import { MembersPanel } from "../members/components/MembersPanel";
import { useHouseholdMembers } from "../members/useHouseholdMembers";

export function ManageHouseholdPage() {
    const navigate = useNavigate();
    const { t } = useTranslation();
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);

    const {
        currentHousehold,
        currentHouseholdDetails,
        isLoading,
        error,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();

    const { data: members } = useHouseholdMembers(
        currentHousehold?.householdId ?? 0,
        !!currentHousehold?.householdId,
    );

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
                    {t("household.failedToLoadHouseholdInformation")}
                </Alert>
            </Container>
        );
    }

    if (!hasActiveHousehold || !currentHousehold?.householdId) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    const householdName =
        currentHouseholdDetails?.name || t("household.household");
    const userRole = currentHousehold.role || HouseholdRoleValue.Member;
    const isOwner = userRole === HouseholdRoleValue.Owner;

    return (
        <Container maxWidth="md" sx={pageContainerSx}>
            <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                <Box
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: { xs: 1, sm: 2 },
                        mb: { xs: 2, sm: 3 },
                    }}
                >
                    <IconButton onClick={() => navigate({ to: "/" })}>
                        <ArrowBack />
                    </IconButton>

                    <Typography
                        variant="h5"
                        component="h1"
                        sx={{ fontWeight: 600, flexGrow: 1 }}
                    >
                        {t("household.householdManagement")}
                    </Typography>

                    {isOwner && (
                        <IconButton
                            data-testid="household-manage-menu-toggle"
                            onClick={(e) => setMenuAnchor(e.currentTarget)}
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
                    )}
                </Box>

                <HouseholdSummaryCard
                    householdName={householdName}
                    memberCount={members?.length ?? 0}
                    userRole={userRole}
                />
            </Box>

            <MembersPanel
                householdId={currentHousehold.householdId}
                currentUserRole={userRole}
            />

            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={() => setMenuAnchor(null)}
                elevation={4}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                transformOrigin={{ vertical: "top", horizontal: "right" }}
                slotProps={{
                    paper: { sx: { minWidth: 200, mt: 1 } },
                }}
            >
                <MenuItem
                    data-testid="household-manage-menu-delete"
                    onClick={() => {
                        setDeleteDialogOpen(true);
                        setMenuAnchor(null);
                    }}
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
                    <ListItemText primary={t("household.deleteHousehold")} />
                </MenuItem>
            </Menu>

            <DeleteHouseholdDialog
                open={deleteDialogOpen}
                onClose={() => setDeleteDialogOpen(false)}
                householdId={currentHousehold.householdId}
                householdName={householdName}
            />
        </Container>
    );
}

import {
    ArrowBack,
    Business,
    Delete,
    Group,
    MoreVert,
} from "@mui/icons-material";
import {
    Alert,
    Box,
    Chip,
    Container,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Skeleton,
    Stack,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { useHouseholdMembers } from "../members/useHouseholdMembers";
import { MembersPanel } from "../members/components/MembersPanel";
import { DeleteHouseholdDialog } from "../components/DeleteHouseholdDialog";

export function ManageHouseholdPage() {
    const navigate = useNavigate();
    const { t } = useTranslation();
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);

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

    const handleBack = () => {
        navigate({ to: "/" });
    };

    const handleMenuClick = (event: React.MouseEvent<HTMLElement>) => {
        setMenuAnchor(event.currentTarget);
    };

    const handleMenuClose = () => {
        setMenuAnchor(null);
    };

    const handleDeleteClick = () => {
        setDeleteDialogOpen(true);
        handleMenuClose();
    };

    if (isLoading) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                        <Skeleton
                            variant="rectangular"
                            height={40}
                            sx={{ mb: 1, borderRadius: 1 }}
                        />
                        <Skeleton variant="text" width="60%" height={32} />
                    </Box>
                    <Skeleton
                        variant="rectangular"
                        height={200}
                        sx={{ borderRadius: 2 }}
                    />
                </Box>
            </Container>
        );
    }

    if (error) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    <Alert severity="error" sx={{ borderRadius: 2 }}>
                        {t("household.failedToLoadHouseholdInformation")}
                    </Alert>
                </Box>
            </Container>
        );
    }

    if (!hasActiveHousehold || !currentHousehold?.householdId) {
        return (
            <Container
                maxWidth="md"
                sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
            >
                <Box>
                    <Alert severity="info" sx={{ borderRadius: 2 }}>
                        {t("common.createOrSelectHouseholdFirst")}
                    </Alert>
                </Box>
            </Container>
        );
    }

    const householdName =
        currentHouseholdDetails?.name || t("household.household");
    const memberCount = members?.length ?? 0;
    const userRole = currentHousehold.role || 0;

    const roleLabels: Record<number, string> = {
        0: t("household.member"),
        1: t("household.admin"),
        2: t("household.owner"),
    };

    const roleColors: Record<
        number,
        | "default"
        | "primary"
        | "secondary"
        | "error"
        | "info"
        | "success"
        | "warning"
    > = {
        0: "default",
        1: "primary",
        2: "warning",
    };

    return (
        <Container
            maxWidth="md"
            sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
        >
            <Box>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: { xs: 1, sm: 2 },
                            mb: { xs: 2, sm: 3 },
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
                                fontSize: { xs: "1.25rem", sm: "1.5rem" },
                                color: "text.primary",
                                flexGrow: 1,
                            }}
                        >
                            {t("household.householdManagement")}
                        </Typography>

                        {userRole === 2 && (
                            <IconButton
                                data-testid="household-manage-menu-toggle"
                                onClick={handleMenuClick}
                                size="small"
                                sx={{
                                    bgcolor: "background.paper",
                                    border: 1,
                                    borderColor: "divider",
                                    "&:hover": {
                                        bgcolor: "action.hover",
                                    },
                                }}
                            >
                                <MoreVert fontSize="small" />
                            </IconButton>
                        )}
                    </Box>

                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: { xs: 1.5, sm: 2 },
                            p: { xs: 2, sm: 2.5 },
                            bgcolor: "background.paper",
                            borderRadius: 2,
                            border: 1,
                            borderColor: "divider",
                            boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                        }}
                    >
                        <Box
                            sx={{
                                p: 1,
                                borderRadius: 1.5,
                                bgcolor: "primary.main",
                                color: "primary.contrastText",
                                display: "flex",
                                alignItems: "center",
                            }}
                        >
                            <Business fontSize="small" />
                        </Box>

                        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                            <Typography
                                variant="h6"
                                sx={{
                                    fontWeight: 600,
                                    fontSize: { xs: "1.1rem", sm: "1.25rem" },
                                    mb: 0.5,
                                    overflow: "hidden",
                                    textOverflow: "ellipsis",
                                    whiteSpace: "nowrap",
                                }}
                            >
                                {householdName}
                            </Typography>

                            <Stack
                                direction="row"
                                spacing={1}
                                alignItems="center"
                                sx={{ flexWrap: "wrap", gap: 0.5 }}
                            >
                                <Box
                                    sx={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 0.5,
                                    }}
                                >
                                    <Group
                                        sx={{
                                            fontSize: 14,
                                            color: "text.secondary",
                                        }}
                                    />
                                    <Typography
                                        variant="caption"
                                        color="text.secondary"
                                        sx={{
                                            fontSize: {
                                                xs: "0.7rem",
                                                sm: "0.75rem",
                                            },
                                        }}
                                    >
                                        {memberCount} {t("household.members")}
                                    </Typography>
                                </Box>

                                <Chip
                                    label={roleLabels[userRole]}
                                    size="small"
                                    color={roleColors[userRole]}
                                    sx={{
                                        height: { xs: 20, sm: 24 },
                                        fontSize: {
                                            xs: "0.7rem",
                                            sm: "0.75rem",
                                        },
                                        "& .MuiChip-label": {
                                            px: { xs: 0.75, sm: 1 },
                                        },
                                    }}
                                />
                            </Stack>
                        </Box>
                    </Box>
                </Box>

                <MembersPanel
                    householdId={currentHousehold.householdId}
                    currentUserRole={userRole}
                />
            </Box>

            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={handleMenuClose}
                anchorOrigin={{
                    vertical: "bottom",
                    horizontal: "right",
                }}
                transformOrigin={{
                    vertical: "top",
                    horizontal: "right",
                }}
                slotProps={{
                    paper: {
                        sx: {
                            minWidth: 200,
                            mt: 1,
                            borderRadius: 2,
                            boxShadow: "0 4px 20px rgba(0,0,0,0.1)",
                        },
                    },
                }}
            >
                <MenuItem
                    data-testid="household-manage-menu-delete"
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

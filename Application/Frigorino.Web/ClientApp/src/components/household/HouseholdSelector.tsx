import { Add, Check, ExpandMore, Home, People } from "@mui/icons-material";
import {
    Box,
    Button,
    Card,
    CardContent,
    Chip,
    CircularProgress,
    Divider,
    IconButton,
    Menu,
    MenuItem,
    Typography,
} from "@mui/material";
import React, { useState } from "react";
import {
    useCurrentHousehold,
    useSetCurrentHousehold,
    useUserHouseholds,
} from "../../hooks/useHouseholdQueries";
import type { HouseholdRole } from "../../lib/api";

interface HouseholdSelectorProps {
    onCreateHousehold?: () => void;
}

export const HouseholdSelector: React.FC<HouseholdSelectorProps> = ({
    onCreateHousehold,
}) => {
    const { data: households = [], isLoading: householdsLoading } =
        useUserHouseholds();
    const { data: currentHousehold } = useCurrentHousehold();
    const setCurrentHouseholdMutation = useSetCurrentHousehold();
    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);

    const open = Boolean(anchorEl);
    const isLoading =
        householdsLoading || setCurrentHouseholdMutation.isPending;

    const handleClick = (event: React.MouseEvent<HTMLButtonElement>) => {
        setAnchorEl(event.currentTarget);
    };

    const handleClose = () => {
        setAnchorEl(null);
    };

    const handleSelectHousehold = async (householdId: number) => {
        try {
            await setCurrentHouseholdMutation.mutateAsync(householdId);
            handleClose();
        } catch (err) {
            console.error("Failed to set current household:", err);
        }
    };

    const handleCreateNew = () => {
        handleClose();
        onCreateHousehold?.();
    };

    const getRoleColor = (
        role: HouseholdRole | undefined,
    ):
        | "default"
        | "primary"
        | "secondary"
        | "error"
        | "info"
        | "success"
        | "warning" => {
        switch (role) {
            case 2: // Owner
                return "error";
            case 1: // Admin
                return "warning";
            default: // Member or undefined
                return "default";
        }
    };

    const getRoleText = (role: HouseholdRole | undefined) => {
        switch (role) {
            case 2: // Owner
                return "Owner";
            case 1: // Admin
                return "Admin";
            default: // Member or undefined
                return "Member";
        }
    };

    // Find the current household from the households list using the current household ID
    const selectedHousehold = households.find(
        (h) => h.id === currentHousehold?.householdId,
    );

    if (isLoading) {
        return (
            <Card sx={{ borderRadius: 2, mb: 3 }}>
                <CardContent sx={{ py: 2 }}>
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                        }}
                    >
                        <CircularProgress size={24} sx={{ mr: 2 }} />
                        <Typography color="text.secondary">
                            Loading households...
                        </Typography>
                    </Box>
                </CardContent>
            </Card>
        );
    }

    if (households.length === 0) {
        return (
            <Card sx={{ borderRadius: 2, mb: 3 }}>
                <CardContent sx={{ py: 3, textAlign: "center" }}>
                    <Home
                        sx={{ fontSize: 48, color: "text.secondary", mb: 2 }}
                    />
                    <Typography variant="h6" gutterBottom>
                        No Households Yet
                    </Typography>
                    <Typography color="text.secondary" sx={{ mb: 3 }}>
                        Create your first household to start organizing your
                        kitchen
                    </Typography>
                    <Button
                        variant="contained"
                        startIcon={<Add />}
                        onClick={onCreateHousehold}
                        sx={{ borderRadius: 2 }}
                    >
                        Create Household
                    </Button>
                </CardContent>
            </Card>
        );
    }

    return (
        <Card sx={{ borderRadius: 2, mb: 3 }}>
            <CardContent sx={{ py: 2 }}>
                <Box
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                    }}
                >
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                        {currentHousehold?.hasActiveHousehold &&
                        selectedHousehold ? (
                            <Box>
                                <Typography
                                    variant="h6"
                                    sx={{
                                        fontWeight: 600,
                                        fontSize: "1.1rem",
                                        mb: 0.5,
                                        overflow: "hidden",
                                        textOverflow: "ellipsis",
                                        whiteSpace: "nowrap",
                                    }}
                                >
                                    {selectedHousehold?.name ||
                                        "Unknown Household"}
                                </Typography>
                                <Box
                                    sx={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 1,
                                    }}
                                >
                                    <Chip
                                        label={getRoleText(
                                            currentHousehold?.role,
                                        )}
                                        size="small"
                                        color={getRoleColor(
                                            currentHousehold?.role,
                                        )}
                                        sx={{ fontSize: "0.75rem" }}
                                    />
                                    <Typography
                                        variant="caption"
                                        color="text.secondary"
                                        sx={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: 0.5,
                                        }}
                                    >
                                        <People sx={{ fontSize: 14 }} />
                                        {selectedHousehold?.memberCount ||
                                            0}{" "}
                                        member
                                        {(selectedHousehold?.memberCount ||
                                            0) !== 1
                                            ? "s"
                                            : ""}
                                    </Typography>
                                </Box>
                            </Box>
                        ) : (
                            <Typography color="text.secondary">
                                No household selected
                            </Typography>
                        )}
                    </Box>

                    <IconButton
                        onClick={handleClick}
                        disabled={isLoading}
                        sx={{
                            bgcolor: "grey.100",
                            "&:hover": { bgcolor: "grey.200" },
                        }}
                    >
                        <ExpandMore />
                    </IconButton>
                </Box>

                <Menu
                    anchorEl={anchorEl}
                    open={open}
                    onClose={handleClose}
                    PaperProps={{
                        sx: {
                            borderRadius: 2,
                            mt: 1,
                            minWidth: 280,
                            maxHeight: 400,
                        },
                    }}
                >
                    {households.map((household) => (
                        <MenuItem
                            key={household.id}
                            onClick={() =>
                                household.id &&
                                handleSelectHousehold(household.id)
                            }
                            selected={
                                household.id === currentHousehold?.householdId
                            }
                            sx={{ py: 1.5 }}
                        >
                            <Box sx={{ flex: 1, minWidth: 0 }}>
                                <Box
                                    sx={{
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "space-between",
                                        mb: 0.5,
                                    }}
                                >
                                    <Typography
                                        sx={{
                                            fontWeight: 500,
                                            overflow: "hidden",
                                            textOverflow: "ellipsis",
                                            whiteSpace: "nowrap",
                                            flex: 1,
                                            mr: 1,
                                        }}
                                    >
                                        {household.name}
                                    </Typography>
                                    {household.id ===
                                        currentHousehold?.householdId && (
                                        <Check
                                            sx={{
                                                fontSize: 18,
                                                color: "primary.main",
                                            }}
                                        />
                                    )}
                                </Box>
                                <Box
                                    sx={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 1,
                                    }}
                                >
                                    <Chip
                                        label={getRoleText(
                                            household.currentUserRole,
                                        )}
                                        size="small"
                                        color={getRoleColor(
                                            household.currentUserRole,
                                        )}
                                        sx={{ fontSize: "0.7rem", height: 20 }}
                                    />
                                    <Typography
                                        variant="caption"
                                        color="text.secondary"
                                        sx={{ fontSize: "0.7rem" }}
                                    >
                                        {household.memberCount} member
                                        {household.memberCount !== 1 ? "s" : ""}
                                    </Typography>
                                </Box>
                            </Box>
                        </MenuItem>
                    ))}

                    <Divider sx={{ my: 1 }} />

                    <MenuItem onClick={handleCreateNew} sx={{ py: 1.5 }}>
                        <Add sx={{ mr: 2, color: "primary.main" }} />
                        <Typography
                            color="primary.main"
                            sx={{ fontWeight: 500 }}
                        >
                            Create New Household
                        </Typography>
                    </MenuItem>
                </Menu>
            </CardContent>
        </Card>
    );
};

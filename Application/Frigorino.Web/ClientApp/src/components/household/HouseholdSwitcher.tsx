import {
    Add,
    Business,
    Edit,
    House,
    KeyboardArrowDown,
} from "@mui/icons-material";
import {
    Box,
    Button,
    CircularProgress,
    Divider,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import {
    useCurrentHousehold,
    useSetCurrentHousehold,
    useUserHouseholds,
} from "../../hooks/useHouseholdQueries";

interface HouseholdSwitcherProps {
    onCreateHousehold: () => void;
}

const roleLabels: Record<number, string> = {
    0: "Member",
    1: "Admin",
    2: "Owner",
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

export const HouseholdSwitcher = ({
    onCreateHousehold,
}: HouseholdSwitcherProps) => {
    const navigate = useNavigate();
    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const open = Boolean(anchorEl);

    const { data: households, isLoading } = useUserHouseholds();
    const { data: currentHousehold } = useCurrentHousehold();
    const { mutate: switchHousehold, isPending: isSwitching } =
        useSetCurrentHousehold();

    const handleClick = (event: React.MouseEvent<HTMLElement>) => {
        setAnchorEl(event.currentTarget);
    };

    const handleClose = () => {
        setAnchorEl(null);
    };

    const handleHouseholdSelect = (householdId: number) => {
        if (householdId !== currentHousehold?.householdId) {
            switchHousehold(householdId, {
                onSuccess: () => {
                    handleClose();
                },
            });
        } else {
            handleClose();
        }
    };

    const handleHouseHoldEdit = () => {
        navigate({ to: "/household/manage" });
    };

    // Find current household details
    const currentHouseholdDetails = households?.find(
        (h) => h.id === currentHousehold?.householdId,
    );
    const currentHouseholdName =
        currentHouseholdDetails?.name || "No Household";
    const householdCount = households?.length || 0;

    if (isLoading) {
        return (
            <CircularProgress
                size={20}
                sx={{
                    color: "text.secondary",
                }}
            />
        );
    }

    return (
        <>
            <Button
                onClick={handleClick}
                variant="outlined"
                size="small"
                endIcon={<KeyboardArrowDown sx={{ fontSize: 16 }} />}
                disabled={isSwitching}
                sx={{
                    borderRadius: 2,
                    textTransform: "none",
                    minWidth: { xs: 120, sm: 140 },
                    maxWidth: { xs: 160, sm: 200 },
                    height: 32,
                    justifyContent: "space-between",
                    bgcolor: "background.paper",
                    borderColor: "divider",
                    px: { xs: 1, sm: 1.5 },
                    "&:hover": {
                        bgcolor: "action.hover",
                        borderColor: "primary.main",
                    },
                }}
            >
                <Box
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: { xs: 0.5, sm: 0.75 },
                        minWidth: 0, // Allow shrinking
                    }}
                >
                    <Business sx={{ fontSize: { xs: 14, sm: 16 } }} />
                    <Typography
                        variant="body2"
                        sx={{
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                            fontSize: { xs: "0.75rem", sm: "0.875rem" },
                            fontWeight: 500,
                            minWidth: 0, // Allow shrinking
                        }}
                    >
                        {currentHouseholdName}
                    </Typography>
                </Box>
            </Button>

            <Menu
                anchorEl={anchorEl}
                open={open}
                onClose={handleClose}
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
                            minWidth: { xs: 280, sm: 320 },
                            maxWidth: { xs: "90vw", sm: 400 },
                            mt: 1,
                            borderRadius: 2,
                            boxShadow: "0 4px 20px rgba(0,0,0,0.1)",
                        },
                    },
                }}
            >
                {households && households.length > 0 ? (
                    households.map((household) => (
                        <MenuItem
                            key={household.id}
                            onClick={() => handleHouseholdSelect(household.id!)}
                            selected={
                                household.id === currentHousehold?.householdId
                            }
                            disabled={isSwitching}
                            sx={{
                                py: { xs: 1.5, sm: 2 },
                                px: 2,
                                minHeight: { xs: 56, sm: 64 },
                            }}
                        >
                            <ListItemIcon sx={{ minWidth: { xs: 32, sm: 40 } }}>
                                <House sx={{ fontSize: { xs: 18, sm: 20 } }} />
                            </ListItemIcon>
                            <ListItemText
                                primary={
                                    <Box
                                        flexDirection={"row"}
                                        display="flex"
                                        alignItems="center"
                                        justifyContent={"space-between"}
                                    >
                                        <Typography
                                            variant="body2"
                                            sx={{
                                                fontWeight: 500,
                                                fontSize: {
                                                    xs: "0.875rem",
                                                    sm: "1rem",
                                                },
                                            }}
                                        >
                                            {household.name}
                                        </Typography>
                                        <IconButton
                                            onClick={handleHouseHoldEdit}
                                            sx={{
                                                visibility:
                                                    household.id ===
                                                    currentHousehold?.householdId
                                                        ? "visible"
                                                        : "hidden",
                                            }}
                                        >
                                            <Edit />
                                        </IconButton>
                                    </Box>
                                }
                            />
                        </MenuItem>
                    ))
                ) : (
                    <MenuItem disabled sx={{ py: 2 }}>
                        <ListItemText
                            primary={
                                <Typography
                                    variant="body2"
                                    color="text.secondary"
                                >
                                    No households yet
                                </Typography>
                            }
                            secondary={
                                <Typography
                                    variant="caption"
                                    color="text.secondary"
                                >
                                    Create your first household below
                                </Typography>
                            }
                        />
                    </MenuItem>
                )}
                <Divider />
                <MenuItem
                    onClick={() => {
                        handleClose();
                        onCreateHousehold();
                    }}
                    sx={{
                        py: { xs: 1.5, sm: 2 },
                        color: "primary.main",
                        minHeight: { xs: 48, sm: 56 },
                    }}
                >
                    <ListItemIcon sx={{ minWidth: { xs: 32, sm: 40 } }}>
                        <Add
                            sx={{ fontSize: { xs: 18, sm: 20 } }}
                            color="primary"
                        />
                    </ListItemIcon>
                    <ListItemText
                        primary={
                            <Typography
                                variant="body2"
                                color="primary.main"
                                sx={{
                                    fontWeight: 500,
                                    fontSize: { xs: "0.875rem", sm: "1rem" },
                                }}
                            >
                                Create New Household
                            </Typography>
                        }
                    />
                </MenuItem>
            </Menu>
        </>
    );
};

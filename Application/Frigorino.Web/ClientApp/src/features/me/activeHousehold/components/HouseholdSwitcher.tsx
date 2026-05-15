import { Add, Business, KeyboardArrowDown } from "@mui/icons-material";
import {
    Box,
    Button,
    CircularProgress,
    Divider,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useUserHouseholds } from "../../../households/useUserHouseholds";
import { useCurrentHousehold } from "../useCurrentHousehold";
import { useSetCurrentHousehold } from "../useSetCurrentHousehold";
import { HouseholdSwitcherMenuItem } from "./HouseholdSwitcherMenuItem";

interface HouseholdSwitcherProps {
    onCreateHousehold: () => void;
}

export const HouseholdSwitcher = ({
    onCreateHousehold,
}: HouseholdSwitcherProps) => {
    const navigate = useNavigate();
    const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
    const open = Boolean(anchorEl);

    const { data: households, isLoading } = useUserHouseholds();
    const { data: currentHousehold } = useCurrentHousehold();
    const { mutate: switchHousehold, isPending: isSwitching } =
        useSetCurrentHousehold();

    const handleClose = () => setAnchorEl(null);

    const handleHouseholdSelect = (householdId: number) => {
        if (householdId === currentHousehold?.householdId) {
            handleClose();
            return;
        }
        switchHousehold(householdId, { onSuccess: handleClose });
    };

    const currentHouseholdName =
        households?.find((h) => h.id === currentHousehold?.householdId)?.name ||
        "No Household";

    if (isLoading) {
        return <CircularProgress size={20} sx={{ color: "text.secondary" }} />;
    }

    return (
        <>
            <Button
                onClick={(e) => setAnchorEl(e.currentTarget)}
                variant="outlined"
                size="small"
                endIcon={<KeyboardArrowDown fontSize="small" />}
                disabled={isSwitching}
                data-testid="household-switcher-toggle"
                sx={{
                    minWidth: { xs: 120, sm: 140 },
                    maxWidth: { xs: 160, sm: 200 },
                    justifyContent: "space-between",
                    bgcolor: "background.paper",
                    borderColor: "divider",
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
                        gap: 0.75,
                        minWidth: 0,
                    }}
                >
                    <Business fontSize="small" />
                    <Typography
                        variant="body2"
                        data-testid="household-switcher-current-name"
                        sx={{
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                            fontWeight: 500,
                            minWidth: 0,
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
                elevation={4}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                transformOrigin={{ vertical: "top", horizontal: "right" }}
                slotProps={{
                    paper: {
                        sx: {
                            minWidth: { xs: 280, sm: 320 },
                            maxWidth: { xs: "90vw", sm: 400 },
                            mt: 1,
                        },
                    },
                }}
            >
                {households && households.length > 0 ? (
                    households.map((household) => (
                        <HouseholdSwitcherMenuItem
                            key={household.id}
                            household={household}
                            isCurrent={
                                household.id === currentHousehold?.householdId
                            }
                            disabled={isSwitching}
                            onSelect={() => handleHouseholdSelect(household.id!)}
                            onEdit={() =>
                                navigate({ to: "/household/manage" })
                            }
                        />
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
                    sx={{ py: { xs: 1.5, sm: 2 }, color: "primary.main" }}
                >
                    <ListItemIcon>
                        <Add fontSize="small" color="primary" />
                    </ListItemIcon>
                    <ListItemText
                        primary={
                            <Typography
                                variant="body2"
                                color="primary.main"
                                sx={{ fontWeight: 500 }}
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

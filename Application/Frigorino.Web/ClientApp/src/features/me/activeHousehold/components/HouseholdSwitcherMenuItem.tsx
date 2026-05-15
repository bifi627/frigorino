import { Edit, House } from "@mui/icons-material";
import {
    Box,
    IconButton,
    ListItemIcon,
    ListItemText,
    MenuItem,
    Typography,
} from "@mui/material";
import type { HouseholdResponse } from "../../../../lib/api";

interface HouseholdSwitcherMenuItemProps {
    household: HouseholdResponse;
    isCurrent: boolean;
    disabled: boolean;
    onSelect: () => void;
    onEdit: () => void;
}

export const HouseholdSwitcherMenuItem = ({
    household,
    isCurrent,
    disabled,
    onSelect,
    onEdit,
}: HouseholdSwitcherMenuItemProps) => {
    return (
        <MenuItem
            data-testid={`household-switcher-option-${household.name}`}
            onClick={onSelect}
            selected={isCurrent}
            disabled={disabled}
            sx={{ py: { xs: 1.5, sm: 2 } }}
        >
            <ListItemIcon>
                <House fontSize="small" />
            </ListItemIcon>
            <ListItemText
                primary={
                    <Box
                        display="flex"
                        alignItems="center"
                        justifyContent="space-between"
                    >
                        <Typography variant="body2" sx={{ fontWeight: 500 }}>
                            {household.name}
                        </Typography>
                        <IconButton
                            onClick={onEdit}
                            sx={{
                                visibility: isCurrent ? "visible" : "hidden",
                            }}
                        >
                            <Edit fontSize="small" />
                        </IconButton>
                    </Box>
                }
            />
        </MenuItem>
    );
};

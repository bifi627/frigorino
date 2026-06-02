import { Delete } from "@mui/icons-material";
import { Menu, MenuItem } from "@mui/material";
import { useTranslation } from "react-i18next";

interface InventoryActionsMenuProps {
    anchorEl: HTMLElement | null;
    onClose: () => void;
    onDelete: () => void;
    isDeleting?: boolean;
}

export const InventoryActionsMenu = ({
    anchorEl,
    onClose,
    onDelete,
    isDeleting = false,
}: InventoryActionsMenuProps) => {
    const { t } = useTranslation();

    return (
        <Menu
            anchorEl={anchorEl}
            open={Boolean(anchorEl)}
            onClose={onClose}
            elevation={4}
            slotProps={{ paper: { sx: { minWidth: 160 } } }}
        >
            <MenuItem
                onClick={onDelete}
                disabled={isDeleting}
                data-testid="delete-inventory-button"
                sx={{ color: "error.main" }}
            >
                <Delete fontSize="small" sx={{ mr: 1 }} />
                {t("common.delete")}
            </MenuItem>
        </Menu>
    );
};

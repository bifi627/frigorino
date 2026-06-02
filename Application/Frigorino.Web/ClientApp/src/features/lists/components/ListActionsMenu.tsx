import { Delete } from "@mui/icons-material";
import { Menu, MenuItem } from "@mui/material";
import { useTranslation } from "react-i18next";

interface ListActionsMenuProps {
    anchorEl: HTMLElement | null;
    onClose: () => void;
    onDelete: () => void;
    isDeleting?: boolean;
}

export const ListActionsMenu = ({
    anchorEl,
    onClose,
    onDelete,
    isDeleting = false,
}: ListActionsMenuProps) => {
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
                data-testid="delete-list-button"
                sx={{ color: "error.main" }}
            >
                <Delete fontSize="small" sx={{ mr: 1 }} />
                {t("common.delete")}
            </MenuItem>
        </Menu>
    );
};

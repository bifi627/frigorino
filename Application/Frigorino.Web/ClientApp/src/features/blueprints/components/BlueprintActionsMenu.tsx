import { ContentCopy, Delete, Edit } from "@mui/icons-material";
import { Divider, ListItemIcon, Menu, MenuItem } from "@mui/material";
import { useTranslation } from "react-i18next";

interface BlueprintActionsMenuProps {
    anchorEl: HTMLElement | null;
    onClose: () => void;
    onEdit: () => void;
    onDuplicate: () => void;
    onDelete: () => void;
    isBusy?: boolean;
}

export const BlueprintActionsMenu = ({
    anchorEl,
    onClose,
    onEdit,
    onDuplicate,
    onDelete,
    isBusy = false,
}: BlueprintActionsMenuProps) => {
    const { t } = useTranslation();

    return (
        <Menu
            anchorEl={anchorEl}
            open={Boolean(anchorEl)}
            onClose={onClose}
            elevation={4}
            slotProps={{ paper: { sx: { minWidth: 180 } } }}
        >
            <MenuItem onClick={onEdit} data-testid="blueprint-edit-button">
                <ListItemIcon>
                    <Edit fontSize="small" />
                </ListItemIcon>
                {t("common.edit")}
            </MenuItem>
            <MenuItem
                onClick={onDuplicate}
                disabled={isBusy}
                data-testid="blueprint-duplicate-button"
            >
                <ListItemIcon>
                    <ContentCopy fontSize="small" />
                </ListItemIcon>
                {t("blueprints.duplicate")}
            </MenuItem>
            <Divider />
            <MenuItem
                onClick={onDelete}
                disabled={isBusy}
                data-testid="blueprint-delete-button"
                sx={{ color: "error.main" }}
            >
                <ListItemIcon>
                    <Delete fontSize="small" color="error" />
                </ListItemIcon>
                {t("common.delete")}
            </MenuItem>
        </Menu>
    );
};

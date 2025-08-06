import { Delete, Edit, MoreVert } from "@mui/icons-material";
import {
    Box,
    Checkbox,
    IconButton,
    ListItem,
    ListItemButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Typography,
} from "@mui/material";
import { useState } from "react";
import type { ListItemDto } from "../../hooks/useListItemQueries";
import { SortableItem } from "../common/sortable/SortableItem";

interface SortableListItemProps {
    item: ListItemDto;
    onToggleStatus: (itemId: number) => void;
    onEdit: (item: ListItemDto) => void;
    onDelete: (itemId: number) => void;
    isDragging?: boolean;
    isEditing?: boolean;
}

export const SortableListItem = ({
    item,
    onToggleStatus,
    onEdit,
    onDelete,
    isDragging = false,
    isEditing = false,
}: SortableListItemProps) => {
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);

    const handleMenuOpen = (event: React.MouseEvent<HTMLElement>) => {
        event.stopPropagation();
        setMenuAnchor(event.currentTarget);
    };

    const handleMenuClose = () => {
        setMenuAnchor(null);
    };

    const handleEdit = () => {
        onEdit(item);
        handleMenuClose();
    };

    const handleDelete = () => {
        if (item.id) {
            onDelete(item.id);
        }
        handleMenuClose();
    };

    const handleToggle = (event: React.MouseEvent) => {
        event.stopPropagation();
        if (item.id) {
            onToggleStatus(item.id);
        }
    };

    return (
        <SortableItem
            item={item}
            isDragging={isDragging}
            dragHandle="left"
            containerSx={{
                borderRadius: 1,
                mb: 1,
                bgcolor: isEditing ? "warning.50" : "background.paper",
                border: "2px solid",
                borderColor: isEditing ? "warning.main" : "divider",
                boxShadow: isDragging ? 3 : isEditing ? 2 : 1,
                opacity: item.status ? 0.7 : 1,
                transition: "all 0.2s ease",
                ...(isEditing && {
                    animation: "pulse 2s ease-in-out infinite",
                    "@keyframes pulse": {
                        "0%": {
                            boxShadow: "0 0 0 0 rgba(237, 108, 2, 0.4)",
                        },
                        "70%": {
                            boxShadow: "0 0 0 10px rgba(237, 108, 2, 0)",
                        },
                        "100%": {
                            boxShadow: "0 0 0 0 rgba(237, 108, 2, 0)",
                        },
                    },
                }),
            }}
        >
            <ListItem sx={{ px: 0, py: 0 }} disablePadding>
                {/* Main Content */}
                <ListItemButton
                    sx={{
                        flex: 1,
                        py: 1.5,
                        px: 1,
                        "&:hover": { bgcolor: "transparent" },
                    }}
                    onClick={handleToggle}
                >
                    <ListItemIcon sx={{ minWidth: 36 }}>
                        <Checkbox
                            edge="start"
                            checked={item.status}
                            tabIndex={-1}
                            disableRipple
                            sx={{
                                color: item.status
                                    ? "success.main"
                                    : "text.secondary",
                                "&.Mui-checked": {
                                    color: "success.main",
                                },
                            }}
                        />
                    </ListItemIcon>

                    <ListItemText
                        primary={
                            <Typography variant="body1">{item.text}</Typography>
                        }
                        secondary={
                            item.quantity && (
                                <Typography
                                    variant="body2"
                                    sx={{
                                        color: item.status
                                            ? "text.disabled"
                                            : "text.secondary",
                                        textDecoration: item.status
                                            ? "line-through"
                                            : "none",
                                    }}
                                >
                                    {item.quantity}
                                </Typography>
                            )
                        }
                    />
                </ListItemButton>

                {/* Actions Menu */}
                <Box sx={{ pr: 1 }}>
                    <IconButton
                        size="small"
                        onClick={handleMenuOpen}
                        sx={{
                            color: "text.secondary",
                            "&:hover": { color: "text.primary" },
                        }}
                    >
                        <MoreVert />
                    </IconButton>

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
                    >
                        <MenuItem onClick={handleEdit}>
                            <Edit fontSize="small" sx={{ mr: 1 }} />
                            Edit
                        </MenuItem>
                        <MenuItem
                            onClick={handleDelete}
                            sx={{ color: "error.main" }}
                        >
                            <Delete fontSize="small" sx={{ mr: 1 }} />
                            Delete
                        </MenuItem>
                    </Menu>
                </Box>
            </ListItem>
        </SortableItem>
    );
};

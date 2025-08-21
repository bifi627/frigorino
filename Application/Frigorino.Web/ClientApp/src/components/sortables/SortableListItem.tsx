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
import React, { memo, useCallback, useState } from "react";
import { SortableItem } from "../common/sortable/SortableItem";
import type { SortableItemInterface } from "./SortableList";

// Extended interface for displayable sortable items
export interface DisplayableSortableItem extends SortableItemInterface {
    text?: string | null;
    quantity?: string | null;
    [key: string]: unknown; // Required for SortableItem compatibility
}

interface SortableListItemProps<
    T extends DisplayableSortableItem = DisplayableSortableItem,
> {
    item: T;
    onToggleStatus: (itemId: number) => void;
    onEdit: (item: T) => void;
    onDelete: (itemId: number) => void;
    isDragging?: boolean;
    isEditing?: boolean;
    showDragHandles?: boolean;
}

function SortableListItemComponent<
    T extends DisplayableSortableItem = DisplayableSortableItem,
>({
    item,
    onToggleStatus,
    onEdit,
    onDelete,
    isDragging = false,
    isEditing = false,
    showDragHandles = false,
}: SortableListItemProps<T>) {
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);

    const handleMenuOpen = useCallback(
        (event: React.MouseEvent<HTMLElement>) => {
            event.stopPropagation();
            setMenuAnchor(event.currentTarget);
        },
        [],
    );

    const handleMenuClose = useCallback(() => {
        setMenuAnchor(null);
    }, []);

    const handleEdit = useCallback(() => {
        onEdit(item);
        handleMenuClose();
    }, [onEdit, item, handleMenuClose]);

    const handleDelete = useCallback(() => {
        if (item.id) {
            const numericId =
                typeof item.id === "string" ? parseInt(item.id) : item.id;
            onDelete(numericId);
        }
        handleMenuClose();
    }, [item.id, onDelete, handleMenuClose]);

    const handleToggle = useCallback(
        (event: React.MouseEvent) => {
            event.stopPropagation();
            if (item.id) {
                const numericId =
                    typeof item.id === "string" ? parseInt(item.id) : item.id;
                onToggleStatus(numericId);
            }
        },
        [item.id, onToggleStatus],
    );

    return (
        <SortableItem
            item={item}
            isDragging={isDragging}
            dragHandle={showDragHandles ? "left" : "none"}
            containerSx={{
                borderRadius: 1,
                mb: 0.5, // Reduced margin bottom for denser layout
                bgcolor: isEditing ? "warning.50" : "background.paper",
                border: "1px solid", // Reduced border width
                borderColor: isEditing ? "warning.main" : "divider",
                boxShadow: isDragging ? 3 : isEditing ? 2 : 0, // Reduced shadow for cleaner look
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
                        py: 0.75, // Reduced vertical padding for denser layout
                        px: 0.75, // Reduced horizontal padding
                        "&:hover": { bgcolor: "transparent" },
                    }}
                    onClick={handleToggle}
                >
                    <ListItemIcon sx={{ minWidth: 32 }}>
                        <Checkbox
                            edge="start"
                            checked={item.status}
                            tabIndex={-1}
                            disableRipple
                            size="small"
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
                            <Typography
                                variant="body2"
                                sx={{
                                    fontWeight: 500,
                                    wordBreak: "break-word",
                                }}
                            >
                                {item.text}
                            </Typography>
                        }
                        secondary={
                            item.quantity && (
                                <Typography
                                    variant="caption"
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
                <Box sx={{ pr: 0.5 }}>
                    <IconButton
                        size="small"
                        onClick={handleMenuOpen}
                        sx={{
                            color: "text.secondary",
                            "&:hover": { color: "text.primary" },
                            p: 0.5, // Reduced padding for smaller touch target
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
}

// Export the memoized version
export const SortableListItem = memo(SortableListItemComponent);

// Add display name for debugging
SortableListItem.displayName = "SortableListItem";

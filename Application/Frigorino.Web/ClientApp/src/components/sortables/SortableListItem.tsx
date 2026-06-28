import { AddShoppingCart, Delete, Edit, MoreVert } from "@mui/icons-material";
import {
    Box,
    Checkbox,
    IconButton,
    ListItem,
    ListItemButton,
    ListItemIcon,
    Menu,
    MenuItem,
} from "@mui/material";
import React, { memo, useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { SortableItem } from "../common/sortable/SortableItem";
import type { SortableItemInterface } from "./SortableList";

export interface SortableListItemProps<T extends SortableItemInterface> {
    item: T;
    onToggleStatus: (itemId: number) => void;
    onEdit: (item: T) => void;
    onDelete: (itemId: number) => void;
    /** When provided, shows an "Add to list" menu entry (used for inventory rows). */
    onAddToList?: (item: T) => void;
    isDragging?: boolean;
    isEditing?: boolean;
    /** Background async work is happening on this row — shows a pulsing accent border. */
    isProcessing?: boolean;
    showDragHandles?: boolean;
    showCheckbox?: boolean;
    /** Opt-in compact chrome: bottom hairline divider instead of a per-item card. Default false. */
    dense?: boolean;
    renderContent: (item: T) => React.ReactNode;
}

function SortableListItemComponent<T extends SortableItemInterface>({
    item,
    onToggleStatus,
    onEdit,
    onDelete,
    onAddToList,
    isDragging = false,
    isEditing = false,
    isProcessing = false,
    showDragHandles = false,
    showCheckbox = false,
    dense = false,
    renderContent,
}: SortableListItemProps<T>) {
    const { t } = useTranslation();
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    const itemLabel = String(item.text ?? item.id ?? "");

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

    const handleAddToList = useCallback(() => {
        onAddToList?.(item);
        handleMenuClose();
    }, [onAddToList, item, handleMenuClose]);

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

    let borderAccent: string;
    if (isEditing) {
        borderAccent = "warning.main";
    } else if (isProcessing) {
        borderAccent = "primary.main";
    } else {
        borderAccent = "divider";
    }

    let elevation: number;
    if (isDragging) {
        elevation = 3;
    } else if (isEditing) {
        elevation = 2;
    } else {
        elevation = 0;
    }

    return (
        <SortableItem
            item={item}
            isDragging={isDragging}
            dragHandle={showDragHandles ? "left" : "none"}
            dragHandleTestId={`drag-handle-item-${itemLabel}`}
            containerSx={{
                bgcolor: isEditing ? "warning.50" : "background.paper",
                opacity: item.status ? 0.7 : 1,
                transition: "all 0.2s ease",
                boxShadow: elevation,
                ...(dense
                    ? {
                          borderBottom: "1px solid",
                          borderBottomColor: borderAccent,
                      }
                    : {
                          borderRadius: 1,
                          mb: 0.5,
                          border: "1px solid",
                          borderColor: borderAccent,
                      }),
                // Editing wins over processing (you can't edit a row mid-extraction).
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
                ...(!isEditing &&
                    isProcessing && {
                        animation: "processingPulse 1.4s ease-in-out infinite",
                        "@keyframes processingPulse": {
                            "0%": {
                                boxShadow: "0 0 0 0 rgba(25, 118, 210, 0.4)",
                            },
                            "70%": {
                                boxShadow: "0 0 0 8px rgba(25, 118, 210, 0)",
                            },
                            "100%": {
                                boxShadow: "0 0 0 0 rgba(25, 118, 210, 0)",
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
                >
                    {showCheckbox && (
                        <ListItemIcon
                            sx={{ minWidth: 32 }}
                            onClick={handleToggle}
                            data-testid={`toggle-item-${itemLabel}`}
                        >
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
                    )}
                    {renderContent(item)}
                </ListItemButton>

                {/* Actions Menu */}
                <Box sx={{ pr: 0.5 }}>
                    <IconButton
                        size="small"
                        onClick={handleMenuOpen}
                        data-testid={`item-menu-button-${itemLabel}`}
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
                        {onAddToList && (
                            <MenuItem
                                onClick={handleAddToList}
                                data-testid="add-to-list-button"
                            >
                                <AddShoppingCart
                                    fontSize="small"
                                    sx={{ mr: 1 }}
                                />
                                {t("reorder.addToList")}
                            </MenuItem>
                        )}
                        <MenuItem
                            onClick={handleEdit}
                            data-testid="edit-item-button"
                        >
                            <Edit fontSize="small" sx={{ mr: 1 }} />
                            {t("common.edit")}
                        </MenuItem>
                        <MenuItem
                            onClick={handleDelete}
                            data-testid="delete-item-button"
                            sx={{ color: "error.main" }}
                        >
                            <Delete fontSize="small" sx={{ mr: 1 }} />
                            {t("common.delete")}
                        </MenuItem>
                    </Menu>
                </Box>
            </ListItem>
        </SortableItem>
    );
}

// Export the memoized version
export const SortableListItem = memo(SortableListItemComponent) as <
    T extends SortableItemInterface,
>(
    props: SortableListItemProps<T>,
) => React.ReactElement;

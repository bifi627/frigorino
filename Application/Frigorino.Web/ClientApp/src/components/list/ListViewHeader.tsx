import {
    ArrowBack,
    Compress,
    DragIndicator,
    Edit,
    MoreVert,
} from "@mui/icons-material";
import {
    Box,
    Container,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Typography,
} from "@mui/material";
import { useRouter } from "@tanstack/react-router";
import { memo, useCallback, useState } from "react";

interface ListViewHeaderProps {
    list: {
        name?: string | null | undefined;
        description?: string | null | undefined;
    };
    listId: string;
    showDragHandles: boolean;
    onToggleDragHandles: () => void;
    onCompact: () => void;
    isCompacting?: boolean;
}

export const ListViewHeader = memo(
    ({
        list,
        listId,
        showDragHandles,
        onToggleDragHandles,
        onCompact,
        isCompacting = false,
    }: ListViewHeaderProps) => {
        const router = useRouter();
        const [menuAnchorEl, setMenuAnchorEl] = useState<null | HTMLElement>(
            null,
        );

        const handleBack = useCallback(() => {
            router.history.back();
        }, [router]);

        const handleEdit = useCallback(() => {
            router.navigate({
                to: `/lists/${listId}/edit`,
            });
        }, [router, listId]);

        const handleMenuOpen = useCallback(
            (event: React.MouseEvent<HTMLElement>) => {
                setMenuAnchorEl(event.currentTarget);
            },
            [],
        );

        const handleMenuClose = useCallback(() => {
            setMenuAnchorEl(null);
        }, []);

        const handleToggleDragHandles = useCallback(() => {
            onToggleDragHandles();
            handleMenuClose();
        }, [onToggleDragHandles, handleMenuClose]);

        const handleCompact = useCallback(() => {
            handleMenuClose();
            onCompact();
        }, [onCompact, handleMenuClose]);

        return (
            <>
                <Container
                    maxWidth="sm"
                    sx={{ px: 1.5, py: 1.5, flexShrink: 0 }}
                >
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: 2,
                        }}
                    >
                        <IconButton onClick={handleBack} sx={{ p: 1 }}>
                            <ArrowBack />
                        </IconButton>
                        <Box sx={{ flex: 1 }}>
                            <Typography
                                variant="h5"
                                component="h1"
                                sx={{ fontWeight: 600, mb: 0.5 }}
                            >
                                {list.name || "Untitled List"}
                            </Typography>
                            {list.description && (
                                <Typography
                                    variant="body2"
                                    color="text.secondary"
                                    sx={{ lineHeight: 1.4 }}
                                >
                                    {list.description}
                                </Typography>
                            )}
                        </Box>
                        <Box sx={{ display: "flex", gap: 1, ml: "auto" }}>
                            <IconButton
                                onClick={handleEdit}
                                sx={{
                                    bgcolor: "primary.main",
                                    color: "white",
                                    "&:hover": { bgcolor: "primary.dark" },
                                }}
                            >
                                <Edit />
                            </IconButton>
                            <IconButton
                                onClick={handleMenuOpen}
                                sx={{
                                    bgcolor: "grey.100",
                                    color: "grey.700",
                                    "&:hover": { bgcolor: "grey.200" },
                                }}
                            >
                                <MoreVert />
                            </IconButton>
                        </Box>
                    </Box>
                </Container>

                <Menu
                    anchorEl={menuAnchorEl}
                    open={Boolean(menuAnchorEl)}
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
                    <MenuItem onClick={handleToggleDragHandles}>
                        <ListItemIcon>
                            <DragIndicator fontSize="small" />
                        </ListItemIcon>
                        <ListItemText
                            primary={
                                showDragHandles
                                    ? "Hide Drag Handles"
                                    : "Show Drag Handles"
                            }
                            secondary="Toggle reorder handles visibility"
                        />
                    </MenuItem>
                    <MenuItem onClick={handleCompact} disabled={isCompacting}>
                        <ListItemIcon>
                            <Compress fontSize="small" />
                        </ListItemIcon>
                        <ListItemText
                            primary="Compact List Order"
                            secondary="Reorganize item sort order"
                        />
                    </MenuItem>
                </Menu>
            </>
        );
    },
);

ListViewHeader.displayName = "ListViewHeader";

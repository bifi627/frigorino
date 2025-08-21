import { ArrowBack, MoreVert } from "@mui/icons-material";
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

export type HeadNavigationAction = {
    text?: string;
    secondaryText?: string;
    icon?: React.ReactNode;
    onClick: () => void;
};

export interface HeadNavigationProps {
    title: string;
    subtitle?: string;
    menuActions: HeadNavigationAction[];
    directActions: HeadNavigationAction[];
}

export const PageHeadActionBar = memo(
    ({ title, subtitle, menuActions, directActions }: HeadNavigationProps) => {
        const router = useRouter();
        const [menuAnchorEl, setMenuAnchorEl] = useState<null | HTMLElement>(
            null,
        );

        const handleBack = useCallback(() => {
            router.history.back();
        }, [router]);

        const handleMenuOpen = useCallback(
            (event: React.MouseEvent<HTMLElement>) => {
                setMenuAnchorEl(event.currentTarget);
            },
            [],
        );

        const handleMenuClose = useCallback(() => {
            setMenuAnchorEl(null);
        }, []);

        const handleMenuAction = useCallback(
            (action: HeadNavigationAction) => {
                action.onClick();
                handleMenuClose();
            },
            [handleMenuClose],
        );

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
                                {title}
                            </Typography>
                            {subtitle && (
                                <Typography
                                    variant="body2"
                                    color="text.secondary"
                                    sx={{ lineHeight: 1.4 }}
                                >
                                    {subtitle}
                                </Typography>
                            )}
                        </Box>
                        <Box sx={{ display: "flex", gap: 1, ml: "auto" }}>
                            {directActions.map((action, index) => (
                                <IconButton
                                    key={index}
                                    onClick={action.onClick}
                                    sx={{
                                        bgcolor:
                                            index === 0
                                                ? "primary.main"
                                                : "grey.100",
                                        color:
                                            index === 0 ? "white" : "grey.700",
                                        "&:hover": {
                                            bgcolor:
                                                index === 0
                                                    ? "primary.dark"
                                                    : "grey.200",
                                        },
                                    }}
                                >
                                    {action.icon}
                                </IconButton>
                            ))}
                            {menuActions.length > 0 && (
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
                            )}
                        </Box>
                    </Box>
                </Container>

                {menuActions.length > 0 && (
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
                        {menuActions.map((action, index) => (
                            <MenuItem
                                key={index}
                                onClick={() => handleMenuAction(action)}
                            >
                                {action.icon && (
                                    <ListItemIcon>{action.icon}</ListItemIcon>
                                )}
                                <ListItemText
                                    primary={action.text}
                                    secondary={action.secondaryText}
                                />
                            </MenuItem>
                        ))}
                    </Menu>
                )}
            </>
        );
    },
);

PageHeadActionBar.displayName = "HeadNavigation";

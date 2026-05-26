import { AccountCircle, Logout } from "@mui/icons-material";
import {
    AppBar,
    Avatar,
    Box,
    Button,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Toolbar,
    Typography,
} from "@mui/material";
import { Link, useRouter } from "@tanstack/react-router";
import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../hooks/useAuth";
import { LanguageSwitcher } from "../common/LanguageSwitcher";

export const Navigation: React.FC = () => {
    const { t } = useTranslation();
    const { isAuthenticated, logout, user } = useAuth();
    const router = useRouter();
    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);

    const handleMenuClick = (event: React.MouseEvent<HTMLElement>) => {
        setAnchorEl(event.currentTarget);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
    };

    const handleLogout = async () => {
        handleMenuClose();
        await logout();
        router.navigate({ to: "." });
    };

    return (
        <AppBar position="static">
            <Toolbar>
                <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
                    <Link
                        to="/"
                        style={{ color: "inherit", textDecoration: "none" }}
                    >
                        Frigorino
                    </Link>
                </Typography>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <LanguageSwitcher />
                    {isAuthenticated ? (
                        <>
                            <IconButton
                                onClick={handleMenuClick}
                                size="small"
                                sx={{ color: "inherit" }}
                                aria-controls={
                                    anchorEl ? "user-menu" : undefined
                                }
                                aria-haspopup="true"
                                aria-expanded={anchorEl ? "true" : undefined}
                            >
                                {user?.photoURL ? (
                                    <Avatar
                                        src={user.photoURL}
                                        sx={{ width: 32, height: 32 }}
                                    />
                                ) : (
                                    <AccountCircle sx={{ fontSize: 32 }} />
                                )}
                            </IconButton>

                            <Menu
                                id="user-menu"
                                anchorEl={anchorEl}
                                open={Boolean(anchorEl)}
                                onClose={handleMenuClose}
                                anchorOrigin={{
                                    vertical: "bottom",
                                    horizontal: "right",
                                }}
                                transformOrigin={{
                                    vertical: "top",
                                    horizontal: "right",
                                }}
                                slotProps={{
                                    paper: {
                                        sx: {
                                            mt: 1,
                                            minWidth: 150,
                                            borderRadius: 2,
                                        },
                                    },
                                }}
                            >
                                <MenuItem onClick={handleLogout}>
                                    <ListItemIcon>
                                        <Logout fontSize="small" />
                                    </ListItemIcon>
                                    <ListItemText primary={t("auth.logout")} />
                                </MenuItem>
                            </Menu>
                        </>
                    ) : (
                        <Button
                            color="inherit"
                            component={Link}
                            to="/auth/login"
                        >
                            {t("auth.login")}
                        </Button>
                    )}
                </Box>
            </Toolbar>
        </AppBar>
    );
};

import { AccountCircle, Dashboard, Logout } from "@mui/icons-material";
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
import { getAuth } from "firebase/auth";
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

    const adminEmails = (import.meta.env.VITE_ADMIN_EMAILS ?? "")
        .split(",")
        .map((e: string) => e.trim().toLowerCase())
        .filter(Boolean);
    const isAdmin =
        !!user?.email && adminEmails.includes(user.email.toLowerCase());

    const handleOpenHangfire = async () => {
        handleMenuClose();
        const token = await getAuth().currentUser?.getIdToken(true);
        if (!token) {
            return;
        }
        // Exchange the bearer token for a server-set HttpOnly cookie that the dashboard's browser
        // sub-requests carry (see Program.cs POST /api/hangfire/session + FirebaseAuth cookie shim).
        // The token never lives in a JS-readable cookie.
        const res = await fetch("/api/hangfire/session", {
            method: "POST",
            headers: { Authorization: `Bearer ${token}` },
        });
        if (!res.ok) {
            return;
        }
        window.open("/hangfire", "_blank", "noopener,noreferrer");
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
                                {isAdmin && (
                                    <MenuItem onClick={handleOpenHangfire}>
                                        <ListItemIcon>
                                            <Dashboard fontSize="small" />
                                        </ListItemIcon>
                                        <ListItemText
                                            primary={t(
                                                "admin.openHangfireDashboard",
                                            )}
                                        />
                                    </MenuItem>
                                )}
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

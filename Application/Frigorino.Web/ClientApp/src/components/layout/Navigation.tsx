import { AppBar, Box, Button, Toolbar, Typography } from "@mui/material";
import { Link, useRouter } from "@tanstack/react-router";
import React from "react";
import { useAuth } from "../../hooks/useAuth";

export const Navigation: React.FC = () => {
    const { isAuthenticated, logout, user } = useAuth();
    const router = useRouter();

    const handleLogout = async () => {
        await logout();
        router.navigate({ to: "/auth/login" });
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
                <Box>
                    {isAuthenticated ? (
                        <>
                            <Button
                                color="inherit"
                                sx={{
                                    display: { xs: "none", sm: "inline-flex" },
                                    maxWidth: "200px",
                                }}
                            >
                                Welcome, {user?.email?.split("@")[0]}
                            </Button>
                            <Button color="inherit" onClick={handleLogout}>
                                Logout
                            </Button>
                        </>
                    ) : (
                        <Button
                            color="inherit"
                            component={Link}
                            to="/auth/login"
                        >
                            Login
                        </Button>
                    )}
                </Box>
            </Toolbar>
        </AppBar>
    );
};

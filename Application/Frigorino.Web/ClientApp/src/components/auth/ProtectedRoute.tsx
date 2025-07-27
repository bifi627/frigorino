import { Box, CircularProgress } from "@mui/material";
import { Navigate } from "@tanstack/react-router";
import React from "react";
import { useAuthStore } from "../../common/authProvider";
import { useAuth } from "../../hooks/useAuth";

interface ProtectedRouteProps {
    children: React.ReactNode;
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children }) => {
    const { isAuthenticated } = useAuth();
    const { loading } = useAuthStore();

    if (loading) {
        return (
            <Box
                display="flex"
                justifyContent="center"
                alignItems="center"
                minHeight="50vh"
            >
                <CircularProgress />
            </Box>
        );
    }

    if (!isAuthenticated) {
        return <Navigate to="/auth/login" />;
    }

    return <>{children}</>;
};

import { Box, Container } from "@mui/material";
import { createFileRoute, Navigate, useSearch } from "@tanstack/react-router";
import { useState } from "react";
import { LoginForm } from "../../components/auth/LoginForm";
import { RegisterForm } from "../../components/auth/RegisterForm";
import { useAuth } from "../../hooks/useAuth";

// Define search params type
type AuthSearch = {
    redirect?: string;
};

export const Route = createFileRoute("/auth/login")({
    component: AuthPage,
    validateSearch: (search: Record<string, unknown>): AuthSearch => {
        return {
            redirect:
                typeof search.redirect === "string"
                    ? search.redirect
                    : undefined,
        };
    },
});

function AuthPage() {
    const [isLogin, setIsLogin] = useState(true);
    const { isAuthenticated } = useAuth();
    const search = useSearch({ from: "/auth/login" });

    // Redirect if already authenticated
    if (isAuthenticated) {
        // Redirect to the original page or home
        const redirectTo = search.redirect || "/";
        return <Navigate to={redirectTo} replace />;
    }

    return (
        <Container maxWidth="sm">
            <Box
                sx={{
                    mt: { xs: 4, sm: 8 },
                    px: { xs: 2, sm: 0 },
                }}
            >
                {isLogin ? (
                    <LoginForm onSwitchToRegister={() => setIsLogin(false)} />
                ) : (
                    <RegisterForm onSwitchToLogin={() => setIsLogin(true)} />
                )}
            </Box>
        </Container>
    );
}

export default AuthPage;

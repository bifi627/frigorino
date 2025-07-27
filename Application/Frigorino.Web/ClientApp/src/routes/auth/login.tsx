import { Box, Container } from "@mui/material";
import { createFileRoute, Navigate } from "@tanstack/react-router";
import { useState } from "react";
import { LoginForm } from "../../components/auth/LoginForm";
import { RegisterForm } from "../../components/auth/RegisterForm";
import { useAuth } from "../../hooks/useAuth";

export const Route = createFileRoute("/auth/login")({
    component: AuthPage,
});

function AuthPage() {
    const [isLogin, setIsLogin] = useState(true);
    const { isAuthenticated } = useAuth();

    // Redirect if already authenticated
    if (isAuthenticated) {
        return <Navigate to="/" />;
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

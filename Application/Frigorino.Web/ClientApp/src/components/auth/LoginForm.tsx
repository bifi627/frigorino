import { Google } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Divider,
    Paper,
    TextField,
    Typography,
} from "@mui/material";
import React, { useState } from "react";
import { useAuth } from "../../hooks/useAuth";

interface LoginFormProps {
    onSwitchToRegister: () => void;
}

export const LoginForm: React.FC<LoginFormProps> = ({ onSwitchToRegister }) => {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const { login, loginWithGoogle, loading, error } = useAuth();

    const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        await login(email, password);
    };

    const handleGoogleLogin = async (
        e: React.MouseEvent<HTMLButtonElement>,
    ) => {
        e.preventDefault();
        await loginWithGoogle();
    };

    return (
        <Paper
            elevation={3}
            sx={{
                p: { xs: 3, sm: 4 },
                maxWidth: 400,
                mx: "auto",
                borderRadius: 2,
            }}
        >
            <Typography
                variant="h4"
                component="h1"
                gutterBottom
                align="center"
                sx={{
                    fontSize: { xs: "1.8rem", sm: "2.125rem" },
                    fontWeight: 600,
                }}
            >
                Login
            </Typography>

            {error && (
                <Alert severity="error" sx={{ mb: 2 }}>
                    {error}
                </Alert>
            )}

            <Box component="form" onSubmit={handleSubmit}>
                <TextField
                    fullWidth
                    label="Email"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    margin="normal"
                    required
                />
                <TextField
                    fullWidth
                    label="Password"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    margin="normal"
                    required
                />
                <Button
                    type="submit"
                    fullWidth
                    variant="contained"
                    sx={{ mt: 3, mb: 2 }}
                    disabled={loading}
                >
                    {loading ? <CircularProgress size={24} /> : "Login"}
                </Button>

                <Divider sx={{ my: 2 }}>OR</Divider>

                <Button
                    fullWidth
                    variant="outlined"
                    onClick={handleGoogleLogin}
                    disabled={loading}
                    sx={{ mb: 2 }}
                    startIcon={<Google />}
                >
                    Continue with Google
                </Button>

                <Button fullWidth variant="text" onClick={onSwitchToRegister}>
                    Don't have an account? Register
                </Button>
            </Box>
        </Paper>
    );
};

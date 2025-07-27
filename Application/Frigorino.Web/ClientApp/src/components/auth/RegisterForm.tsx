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

interface RegisterFormProps {
    onSwitchToLogin: () => void;
}

export const RegisterForm: React.FC<RegisterFormProps> = ({
    onSwitchToLogin,
}) => {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [confirmPassword, setConfirmPassword] = useState("");
    const [localError, setLocalError] = useState<string | null>(null);
    const { register, loginWithGoogle, loading, error } = useAuth();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLocalError(null);

        if (password !== confirmPassword) {
            setLocalError("Passwords do not match");
            return;
        }

        if (password.length < 6) {
            setLocalError("Password must be at least 6 characters");
            return;
        }

        await register(email, password);
    };

    const handleGoogleLogin = async () => {
        await loginWithGoogle();
    };

    const displayError = localError || error;

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
                Register
            </Typography>

            {displayError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                    {displayError}
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
                <TextField
                    fullWidth
                    label="Confirm Password"
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
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
                    {loading ? <CircularProgress size={24} /> : "Register"}
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

                <Button fullWidth variant="text" onClick={onSwitchToLogin}>
                    Already have an account? Login
                </Button>
            </Box>
        </Paper>
    );
};

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
import { useTranslation } from "react-i18next";
import { useAuth } from "../../hooks/useAuth";

interface RegisterFormProps {
    onSwitchToLogin: () => void;
}

export const RegisterForm: React.FC<RegisterFormProps> = ({
    onSwitchToLogin,
}) => {
    const { t } = useTranslation();
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [confirmPassword, setConfirmPassword] = useState("");
    const [localError, setLocalError] = useState<string | null>(null);
    const { register, loginWithGoogle, loading, error } = useAuth();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLocalError(null);

        if (password !== confirmPassword) {
            setLocalError(t("auth.passwordMismatch"));
            return;
        }

        if (password.length < 6) {
            setLocalError(t("auth.passwordMinLength"));
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
                {t("auth.register")}
            </Typography>

            {displayError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                    {displayError}
                </Alert>
            )}

            <Box component="form" onSubmit={handleSubmit}>
                <TextField
                    fullWidth
                    label={t("auth.email")}
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    margin="normal"
                    required
                />
                <TextField
                    fullWidth
                    label={t("auth.password")}
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    margin="normal"
                    required
                />
                <TextField
                    fullWidth
                    label={t("auth.confirmPassword")}
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
                    {loading ? (
                        <CircularProgress size={24} />
                    ) : (
                        t("auth.register")
                    )}
                </Button>

                <Divider sx={{ my: 2 }}>{t("auth.or")}</Divider>

                <Button
                    fullWidth
                    variant="outlined"
                    onClick={handleGoogleLogin}
                    disabled={loading}
                    sx={{ mb: 2 }}
                    startIcon={<Google />}
                >
                    {t("auth.continueWithGoogle")}
                </Button>

                <Button fullWidth variant="text" onClick={onSwitchToLogin}>
                    {t("auth.hasAccountLogin")}
                </Button>
            </Box>
        </Paper>
    );
};

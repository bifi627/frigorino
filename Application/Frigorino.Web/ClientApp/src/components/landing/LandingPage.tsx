import { AppRegistrationOutlined, LoginOutlined } from "@mui/icons-material";
import { Box, Button, Container, Stack, Typography } from "@mui/material";
import { Link } from "@tanstack/react-router";

export const LandingPage = () => {
    return (
        <Container
            maxWidth="sm"
            sx={{
                minHeight: "100vh",
                display: "flex",
                flexDirection: "column",
                justifyContent: "center",
                alignItems: "center",
                textAlign: "center",
                px: 3,
                py: 4,
            }}
        >
            <Box sx={{ mb: 6 }}>
                <Typography
                    variant="h2"
                    component="h1"
                    gutterBottom
                    sx={{
                        fontWeight: "bold",
                        fontSize: { xs: "2.5rem", sm: "3.5rem" },
                        background:
                            "linear-gradient(45deg, #2196F3 30%, #21CBF3 90%)",
                        backgroundClip: "text",
                        WebkitBackgroundClip: "text",
                        WebkitTextFillColor: "transparent",
                        mb: 2,
                    }}
                >
                    Frigorino
                </Typography>

                <Typography
                    variant="h5"
                    component="h2"
                    color="text.secondary"
                    sx={{
                        fontSize: { xs: "1.1rem", sm: "1.5rem" },
                        fontWeight: 400,
                        mb: 4,
                        lineHeight: 1.4,
                    }}
                >
                    Your smart kitchen companion
                </Typography>

                <Typography
                    variant="body1"
                    color="text.secondary"
                    sx={{
                        fontSize: { xs: "0.95rem", sm: "1.1rem" },
                        mb: 4,
                        maxWidth: "400px",
                        mx: "auto",
                        lineHeight: 1.6,
                    }}
                >
                    Manage your refrigerator, track expiration dates, and never
                    waste food again. Get started today!
                </Typography>
            </Box>

            <Stack
                spacing={2}
                direction="column"
                sx={{
                    width: "100%",
                    maxWidth: "300px",
                }}
            >
                <Button
                    component={Link}
                    to="/auth/login"
                    variant="contained"
                    size="large"
                    startIcon={<LoginOutlined />}
                    sx={{
                        py: 1.5,
                        fontSize: "1.1rem",
                        fontWeight: 600,
                        borderRadius: 2,
                        boxShadow: "0 4px 12px rgba(33, 150, 243, 0.3)",
                        "&:hover": {
                            transform: "translateY(-2px)",
                            boxShadow: "0 6px 16px rgba(33, 150, 243, 0.4)",
                        },
                        transition: "all 0.3s ease",
                    }}
                >
                    Sign In
                </Button>

                <Button
                    component={Link}
                    to="/auth/login"
                    variant="outlined"
                    size="large"
                    startIcon={<AppRegistrationOutlined />}
                    sx={{
                        py: 1.5,
                        fontSize: "1.1rem",
                        fontWeight: 600,
                        borderRadius: 2,
                        borderWidth: 2,
                        "&:hover": {
                            borderWidth: 2,
                            transform: "translateY(-2px)",
                        },
                        transition: "all 0.3s ease",
                    }}
                >
                    Get Started
                </Button>
            </Stack>

            <Box sx={{ mt: 6, opacity: 0.7 }}>
                <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{ fontSize: "0.8rem" }}
                >
                    Join thousands of users reducing food waste
                </Typography>
            </Box>
        </Container>
    );
};

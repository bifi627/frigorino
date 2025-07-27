import {
    KitchenOutlined,
    RestaurantOutlined,
    TimerOutlined,
    TrendingUpOutlined,
} from "@mui/icons-material";
import {
    Avatar,
    Box,
    Card,
    CardContent,
    Chip,
    Container,
    Divider,
    Stack,
    Typography,
} from "@mui/material";
import { useAuth } from "../../hooks/useAuth";

export const WelcomePage = () => {
    const { user } = useAuth();

    const stats = [
        {
            label: "Items in Fridge",
            value: "0",
            icon: <KitchenOutlined />,
            color: "#2196F3",
        },
        {
            label: "Expiring Soon",
            value: "0",
            icon: <TimerOutlined />,
            color: "#FF9800",
        },
        {
            label: "Recipes Available",
            value: "0",
            icon: <RestaurantOutlined />,
            color: "#4CAF50",
        },
        {
            label: "Food Saved",
            value: "0%",
            icon: <TrendingUpOutlined />,
            color: "#9C27B0",
        },
    ];

    return (
        <Container maxWidth="sm" sx={{ py: 3, px: 2 }}>
            {/* Welcome Header */}
            <Box sx={{ textAlign: "center", mb: 4 }}>
                <Avatar
                    sx={{
                        width: 80,
                        height: 80,
                        mx: "auto",
                        mb: 2,
                        bgcolor: "primary.main",
                        fontSize: "2rem",
                    }}
                >
                    {user?.email?.charAt(0).toUpperCase() || "U"}
                </Avatar>

                <Typography
                    variant="h4"
                    component="h1"
                    gutterBottom
                    sx={{
                        fontWeight: 600,
                        fontSize: { xs: "1.8rem", sm: "2.2rem" },
                    }}
                >
                    Welcome back!
                </Typography>

                <Chip
                    label={user?.email || "User"}
                    variant="outlined"
                    sx={{
                        fontSize: "0.9rem",
                        px: 1,
                    }}
                />
            </Box>

            <Divider sx={{ mb: 4 }} />

            {/* Quick Stats */}
            <Typography
                variant="h6"
                gutterBottom
                sx={{
                    fontWeight: 600,
                    mb: 3,
                    color: "text.primary",
                }}
            >
                Your Kitchen Overview
            </Typography>

            <Stack spacing={2} sx={{ mb: 4 }}>
                {stats.map((stat, index) => (
                    <Card
                        key={index}
                        sx={{
                            borderRadius: 2,
                            boxShadow: "0 2px 8px rgba(0,0,0,0.1)",
                            "&:hover": {
                                transform: "translateY(-2px)",
                                boxShadow: "0 4px 12px rgba(0,0,0,0.15)",
                            },
                            transition: "all 0.3s ease",
                        }}
                    >
                        <CardContent sx={{ py: 2.5 }}>
                            <Box
                                sx={{
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "space-between",
                                }}
                            >
                                <Box
                                    sx={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 2,
                                    }}
                                >
                                    <Box
                                        sx={{
                                            p: 1.5,
                                            borderRadius: 2,
                                            bgcolor: `${stat.color}15`,
                                            color: stat.color,
                                            display: "flex",
                                            alignItems: "center",
                                        }}
                                    >
                                        {stat.icon}
                                    </Box>
                                    <Typography
                                        variant="body1"
                                        sx={{
                                            fontWeight: 500,
                                            fontSize: "0.95rem",
                                        }}
                                    >
                                        {stat.label}
                                    </Typography>
                                </Box>

                                <Typography
                                    variant="h6"
                                    sx={{
                                        fontWeight: 700,
                                        color: stat.color,
                                        fontSize: "1.3rem",
                                    }}
                                >
                                    {stat.value}
                                </Typography>
                            </Box>
                        </CardContent>
                    </Card>
                ))}
            </Stack>

            {/* Coming Soon Section */}
            <Card
                sx={{
                    borderRadius: 2,
                    background:
                        "linear-gradient(135deg, #667eea 0%, #764ba2 100%)",
                    color: "white",
                    textAlign: "center",
                }}
            >
                <CardContent sx={{ py: 4 }}>
                    <Typography
                        variant="h6"
                        gutterBottom
                        sx={{ fontWeight: 600 }}
                    >
                        More Features Coming Soon!
                    </Typography>
                    <Typography
                        variant="body2"
                        sx={{
                            opacity: 0.9,
                            fontSize: "0.9rem",
                            lineHeight: 1.5,
                        }}
                    >
                        We're working hard to bring you inventory management,
                        expiration tracking, and smart recipe suggestions.
                    </Typography>
                </CardContent>
            </Card>
        </Container>
    );
};

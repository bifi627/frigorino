import {
    KitchenOutlined,
    ManageAccounts,
    RestaurantOutlined,
    TimerOutlined,
    TrendingUpOutlined,
} from "@mui/icons-material";
import {
    Avatar,
    Box,
    Button,
    Card,
    CardContent,
    Chip,
    Container,
    Divider,
    Stack,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useAuth } from "../../hooks/useAuth";
import { HouseholdSwitcher } from "../household/HouseholdSwitcher";

export const WelcomePage = () => {
    const { user } = useAuth();
    const navigate = useNavigate();

    const handleCreateHousehold = () => {
        navigate({ to: "/household/create" });
    };

    const handleManageHousehold = () => {
        navigate({ to: "/household/manage" });
    };

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
        <Container
            maxWidth="sm"
            sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
        >
            {/* Header with User Info and Household Switcher */}
            <Box sx={{ mb: { xs: 3, sm: 4 } }}>
                {/* Top Row - User Email and Household Switcher */}
                <Box
                    sx={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        mb: { xs: 2, sm: 3 },
                        gap: 1,
                    }}
                >
                    <Chip
                        label={user?.email || "User"}
                        variant="outlined"
                        size="small"
                        sx={{
                            fontSize: { xs: "0.7rem", sm: "0.8rem" },
                            maxWidth: { xs: "50%", sm: "60%" },
                            "& .MuiChip-label": {
                                px: { xs: 0.75, sm: 1 },
                                overflow: "hidden",
                                textOverflow: "ellipsis",
                            },
                        }}
                    />
                    <HouseholdSwitcher
                        onCreateHousehold={handleCreateHousehold}
                    />
                </Box>

                {/* Welcome Section */}
                <Box sx={{ textAlign: "center" }}>
                    <Avatar
                        sx={{
                            width: { xs: 64, sm: 80 },
                            height: { xs: 64, sm: 80 },
                            mx: "auto",
                            mb: { xs: 1.5, sm: 2 },
                            bgcolor: "primary.main",
                            fontSize: { xs: "1.5rem", sm: "2rem" },
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
                            fontSize: {
                                xs: "1.5rem",
                                sm: "1.8rem",
                                md: "2.2rem",
                            },
                            mb: { xs: 1, sm: 2 },
                        }}
                    >
                        Welcome back!
                    </Typography>

                    {/* Action Button for Household Management */}
                    <Button
                        variant="outlined"
                        size="small"
                        startIcon={<ManageAccounts />}
                        onClick={handleManageHousehold}
                        sx={{
                            textTransform: "none",
                            fontSize: { xs: "0.75rem", sm: "0.875rem" },
                            px: { xs: 1.5, sm: 2 },
                            py: { xs: 0.5, sm: 0.75 },
                            borderRadius: 2,
                        }}
                    >
                        Manage Household
                    </Button>
                </Box>
            </Box>

            <Divider sx={{ mb: { xs: 3, sm: 4 } }} />

            {/* Quick Stats */}
            <Typography
                variant="h6"
                gutterBottom
                sx={{
                    fontWeight: 600,
                    mb: { xs: 2, sm: 3 },
                    color: "text.primary",
                    fontSize: { xs: "1.1rem", sm: "1.25rem" },
                }}
            >
                Your Kitchen Overview
            </Typography>

            <Stack spacing={{ xs: 1.5, sm: 2 }} sx={{ mb: { xs: 3, sm: 4 } }}>
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
                        <CardContent sx={{ py: { xs: 2, sm: 2.5 } }}>
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

import { Add, ArrowBack, Home, People } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    IconButton,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import {
    useCreateHousehold,
    useSetCurrentHousehold,
} from "../../hooks/useHousehold";

export const Route = createFileRoute("/household/create")({
    component: CreateHouseholdPage,
});

interface CreateHouseholdFormData {
    name: string;
    description: string;
}

function CreateHouseholdPage() {
    const navigate = useNavigate();
    const createHouseholdMutation = useCreateHousehold();
    const setCurrentHouseholdMutation = useSetCurrentHousehold();
    const [formData, setFormData] = useState<CreateHouseholdFormData>({
        name: "",
        description: "",
    });

    const isLoading =
        createHouseholdMutation.isPending ||
        setCurrentHouseholdMutation.isPending;
    const error =
        createHouseholdMutation.error || setCurrentHouseholdMutation.error;

    const handleInputChange =
        (field: keyof CreateHouseholdFormData) =>
        (event: React.ChangeEvent<HTMLInputElement>) => {
            setFormData((prev) => ({
                ...prev,
                [field]: event.target.value,
            }));
        };

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();

        if (!formData.name.trim()) {
            return;
        }

        try {
            const household = await createHouseholdMutation.mutateAsync({
                name: formData.name.trim(),
                description: formData.description.trim() || undefined,
            });

            // Set as current household
            if (household.id) {
                await setCurrentHouseholdMutation.mutateAsync(household.id);
            }

            // Navigate back to main page
            navigate({ to: "/" });
        } catch (err) {
            // Error is handled by the mutation
            console.error("Failed to create household:", err);
        }
    };

    const handleBack = () => {
        navigate({ to: "/" });
    };

    return (
        <Container maxWidth="sm" sx={{ py: 3, px: 2 }}>
            {/* Header */}
            <Box sx={{ display: "flex", alignItems: "center", mb: 4 }}>
                <IconButton
                    onClick={handleBack}
                    sx={{
                        mr: 2,
                        bgcolor: "grey.100",
                        "&:hover": { bgcolor: "grey.200" },
                    }}
                >
                    <ArrowBack />
                </IconButton>
                <Typography
                    variant="h5"
                    component="h1"
                    sx={{
                        fontWeight: 600,
                        fontSize: { xs: "1.4rem", sm: "1.8rem" },
                    }}
                >
                    Create New Household
                </Typography>
            </Box>

            {/* Info Card */}
            <Card
                sx={{
                    mb: 4,
                    borderRadius: 3,
                    background:
                        "linear-gradient(135deg, #667eea 0%, #764ba2 100%)",
                    color: "white",
                }}
            >
                <CardContent sx={{ py: 3 }}>
                    <Box sx={{ display: "flex", alignItems: "center", mb: 2 }}>
                        <Home sx={{ mr: 2, fontSize: "2rem" }} />
                        <Typography variant="h6" sx={{ fontWeight: 600 }}>
                            Start Your Kitchen Journey
                        </Typography>
                    </Box>
                    <Typography
                        variant="body2"
                        sx={{ opacity: 0.9, lineHeight: 1.6 }}
                    >
                        Create a household to organize your kitchen with family,
                        roommates, or friends. Share grocery lists, track
                        expiration dates, and never waste food again!
                    </Typography>
                </CardContent>
            </Card>

            {/* Form */}
            <Card
                sx={{
                    borderRadius: 3,
                    boxShadow: "0 4px 20px rgba(0,0,0,0.1)",
                }}
            >
                <CardContent sx={{ p: 4 }}>
                    <form onSubmit={handleSubmit}>
                        <Stack spacing={3}>
                            {/* Error Alert */}
                            {error && (
                                <Alert
                                    severity="error"
                                    sx={{ borderRadius: 2 }}
                                >
                                    {error instanceof Error
                                        ? error.message
                                        : "An error occurred"}
                                </Alert>
                            )}

                            {/* Household Name */}
                            <Box>
                                <Typography
                                    variant="subtitle1"
                                    sx={{ fontWeight: 600, mb: 1 }}
                                >
                                    Household Name *
                                </Typography>
                                <TextField
                                    fullWidth
                                    placeholder="e.g., The Smith Family, Apartment 4B, Our Kitchen"
                                    value={formData.name}
                                    onChange={handleInputChange("name")}
                                    disabled={isLoading}
                                    error={
                                        !formData.name.trim() &&
                                        formData.name.length > 0
                                    }
                                    helperText={
                                        !formData.name.trim() &&
                                        formData.name.length > 0
                                            ? "Household name is required"
                                            : "Choose a name that everyone in your household will recognize"
                                    }
                                    sx={{
                                        "& .MuiOutlinedInput-root": {
                                            borderRadius: 2,
                                        },
                                    }}
                                />
                            </Box>

                            {/* Description */}
                            <Box>
                                <Typography
                                    variant="subtitle1"
                                    sx={{ fontWeight: 600, mb: 1 }}
                                >
                                    Description (Optional)
                                </Typography>
                                <TextField
                                    fullWidth
                                    multiline
                                    rows={3}
                                    placeholder="Tell us about your household... Who lives here? What are your kitchen goals?"
                                    value={formData.description}
                                    onChange={handleInputChange("description")}
                                    disabled={isLoading}
                                    sx={{
                                        "& .MuiOutlinedInput-root": {
                                            borderRadius: 2,
                                        },
                                    }}
                                />
                            </Box>

                            {/* Features Info */}
                            <Box
                                sx={{
                                    bgcolor: "grey.50",
                                    borderRadius: 2,
                                    p: 3,
                                }}
                            >
                                <Box
                                    sx={{
                                        display: "flex",
                                        alignItems: "center",
                                        mb: 2,
                                    }}
                                >
                                    <People
                                        sx={{ mr: 2, color: "primary.main" }}
                                    />
                                    <Typography
                                        variant="subtitle1"
                                        sx={{ fontWeight: 600 }}
                                    >
                                        What you can do:
                                    </Typography>
                                </Box>
                                <Stack spacing={1}>
                                    <Typography
                                        variant="body2"
                                        color="text.secondary"
                                    >
                                        • Invite family and friends to join
                                    </Typography>
                                    <Typography
                                        variant="body2"
                                        color="text.secondary"
                                    >
                                        • Share grocery lists and meal plans
                                    </Typography>
                                    <Typography
                                        variant="body2"
                                        color="text.secondary"
                                    >
                                        • Track food inventory together
                                    </Typography>
                                    <Typography
                                        variant="body2"
                                        color="text.secondary"
                                    >
                                        • Get notified about expiring items
                                    </Typography>
                                </Stack>
                            </Box>

                            {/* Submit Button */}
                            <Button
                                type="submit"
                                variant="contained"
                                size="large"
                                disabled={isLoading || !formData.name.trim()}
                                startIcon={
                                    isLoading ? (
                                        <CircularProgress
                                            size={20}
                                            color="inherit"
                                        />
                                    ) : (
                                        <Add />
                                    )
                                }
                                sx={{
                                    borderRadius: 2,
                                    py: 1.5,
                                    fontSize: "1rem",
                                    fontWeight: 600,
                                    mt: 2,
                                }}
                            >
                                {isLoading ? "Creating..." : "Create Household"}
                            </Button>
                        </Stack>
                    </form>
                </CardContent>
            </Card>

            {/* Bottom Spacing */}
            <Box sx={{ height: 4 }} />
        </Container>
    );
}
export default CreateHouseholdPage;

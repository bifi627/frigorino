import { Add, ArrowBack } from "@mui/icons-material";
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
} from "../../hooks/useHouseholdQueries";

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
            <Box
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: { xs: 1, sm: 2 },
                    mb: { xs: 2, sm: 3 },
                }}
            >
                <IconButton
                    onClick={handleBack}
                    size="small"
                    sx={{
                        bgcolor: "background.paper",
                        border: 1,
                        borderColor: "divider",
                        "&:hover": {
                            bgcolor: "action.hover",
                        },
                    }}
                >
                    <ArrowBack fontSize="small" />
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

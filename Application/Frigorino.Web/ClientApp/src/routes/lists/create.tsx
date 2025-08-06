import { Add, ArrowBack, ListAlt } from "@mui/icons-material";
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
import {
    createFileRoute,
    useNavigate,
    useRouter,
} from "@tanstack/react-router";
import { useState } from "react";
import { requireAuth } from "../../common/authGuard";
import { useCurrentHousehold } from "../../hooks/useHouseholdQueries";
import { useCreateList } from "../../hooks/useListQueries";

export const Route = createFileRoute("/lists/create")({
    beforeLoad: requireAuth,
    component: CreateListPage,
});

interface CreateListFormData {
    name: string;
    description: string;
}

function CreateListPage() {
    const navigate = useNavigate();
    const router = useRouter();
    const createListMutation = useCreateList();
    const { data: currentHousehold } = useCurrentHousehold();

    const [formData, setFormData] = useState<CreateListFormData>({
        name: "",
        description: "",
    });

    const isLoading = createListMutation.isPending;
    const error = createListMutation.error;

    const handleInputChange =
        (field: keyof CreateListFormData) =>
        (event: React.ChangeEvent<HTMLInputElement>) => {
            setFormData((prev) => ({
                ...prev,
                [field]: event.target.value,
            }));
        };

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();

        if (!formData.name.trim() || !currentHousehold?.householdId) {
            return;
        }

        try {
            const response = await createListMutation.mutateAsync({
                householdId: currentHousehold.householdId,
                data: {
                    name: formData.name.trim(),
                    description: formData.description.trim() || undefined,
                },
            });

            navigate({ to: `/lists/${response.id}/view` });
        } catch (err) {
            // Error is handled by the mutation
            console.error("Failed to create list:", err);
        }
    };

    const handleBack = () => {
        router.history.back();
    };

    // Show error if no current household
    if (!currentHousehold?.householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 3, px: 2 }}>
                <Alert severity="error" sx={{ borderRadius: 2 }}>
                    You need to select a household before creating a list.
                    <Button
                        onClick={handleBack}
                        sx={{ mt: 1, display: "block" }}
                    >
                        Go back to dashboard
                    </Button>
                </Alert>
            </Container>
        );
    }

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
                <IconButton onClick={handleBack} sx={{ p: 1 }}>
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
                    Neue Einkaufsliste erstellen
                </Typography>
            </Box>

            {/* Info Card */}
            <Card
                sx={{
                    mb: 3,
                    borderRadius: 3,
                    bgcolor: "primary.main",
                    color: "primary.contrastText",
                }}
            >
                <CardContent sx={{ p: 3 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
                        <Box
                            sx={{
                                p: 1.5,
                                borderRadius: 2,
                                bgcolor: "rgba(255,255,255,0.2)",
                                display: "flex",
                                alignItems: "center",
                            }}
                        >
                            <ListAlt />
                        </Box>
                        <Box>
                            <Typography
                                variant="h6"
                                sx={{ fontWeight: 600, mb: 0.5 }}
                            >
                                Einkaufsliste
                            </Typography>
                            {/* <Typography variant="body2" sx={{ opacity: 0.9 }}>
                                Create organized lists for your household
                                shopping needs
                            </Typography> */}
                        </Box>
                    </Box>
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

                            {/* List Name */}
                            <Box>
                                <Typography
                                    variant="subtitle1"
                                    sx={{ fontWeight: 600, mb: 1 }}
                                >
                                    Name *
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
                                            ? "Name is required"
                                            : ""
                                    }
                                    sx={{
                                        "& .MuiOutlinedInput-root": {
                                            borderRadius: 2,
                                        },
                                    }}
                                />
                            </Box>

                            {/* Description */}
                            {/* <Box>
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
                                    value={formData.description}
                                    onChange={handleInputChange("description")}
                                    disabled={isLoading}
                                    helperText="Add any additional details about this list"
                                    sx={{
                                        "& .MuiOutlinedInput-root": {
                                            borderRadius: 2,
                                        },
                                    }}
                                />
                            </Box> */}

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
                                {isLoading ? "Creating..." : "Create List"}
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

export default CreateListPage;

import { Add, ArrowBack, Inventory2 } from "@mui/icons-material";
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
import { useCreateInventory } from "../../hooks/useInventoryQueries";

export const Route = createFileRoute("/inventories/create")({
    beforeLoad: requireAuth,
    component: CreateInventoryPage,
});

interface CreateInventoryFormData {
    name: string;
    description: string;
}

function CreateInventoryPage() {
    const navigate = useNavigate();
    const router = useRouter();
    const createInventoryMutation = useCreateInventory();
    const { data: currentHousehold } = useCurrentHousehold();

    const [formData, setFormData] = useState<CreateInventoryFormData>({
        name: "",
        description: "",
    });

    const isLoading = createInventoryMutation.isPending;
    const error = createInventoryMutation.error;

    const handleInputChange =
        (field: keyof CreateInventoryFormData) =>
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
            const response = await createInventoryMutation.mutateAsync({
                householdId: currentHousehold.householdId,
                data: {
                    name: formData.name.trim(),
                    description: formData.description.trim() || undefined,
                },
            });

            navigate({ to: `/inventories/${response.id}/view` });
        } catch (err) {
            console.error("Failed to create inventory:", err);
        }
    };

    const handleBack = () => {
        router.history.back();
    };

    if (!currentHousehold?.householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 3, px: 2 }}>
                <Alert severity="error" sx={{ borderRadius: 2 }}>
                    You need to select a household before creating an inventory.
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
                    Create Inventory
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
                            <Inventory2 />
                        </Box>
                        <Box>
                            <Typography
                                variant="h6"
                                sx={{ fontWeight: 600, mb: 0.5 }}
                            >
                                Household Inventory
                            </Typography>
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

                            {/* Optional Description */}
                            {/* <Box>
                                <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
                                    Description (Optional)
                                </Typography>
                                <TextField
                                    fullWidth
                                    multiline
                                    rows={3}
                                    value={formData.description}
                                    onChange={handleInputChange("description")}
                                    disabled={isLoading}
                                    sx={{ "& .MuiOutlinedInput-root": { borderRadius: 2 } }}
                                />
                            </Box> */}

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
                                {isLoading ? "Creating..." : "Create Inventory"}
                            </Button>
                        </Stack>
                    </form>
                </CardContent>
            </Card>

            <Box sx={{ height: 4 }} />
        </Container>
    );
}

export default CreateInventoryPage;

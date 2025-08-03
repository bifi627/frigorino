import { ArrowBack, Edit } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    IconButton,
    Typography,
} from "@mui/material";
import { createFileRoute, useRouter } from "@tanstack/react-router";
import { useCurrentHousehold } from "../../../hooks/useHouseholdQueries";
import { useList } from "../../../hooks/useListQueries";

export const Route = createFileRoute("/lists/$listId/view")({
    component: RouteComponent,
});

function RouteComponent() {
    const router = useRouter();
    const { listId } = Route.useParams();

    // Get current household and list data
    const { data: currentHousehold } = useCurrentHousehold();
    const {
        data: list,
        isLoading,
        error,
    } = useList(
        currentHousehold?.householdId || 0,
        parseInt(listId),
        !!currentHousehold?.householdId,
    );

    const handleBack = () => {
        router.history.back();
    };

    const handleEdit = () => {
        router.navigate({
            to: `/lists/${listId}/edit`,
        });
    };

    if (!currentHousehold?.householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="warning">
                    Please select a household first.
                </Alert>
            </Container>
        );
    }

    if (isLoading) {
        return (
            <Container maxWidth="sm" sx={{ py: 4, textAlign: "center" }}>
                <CircularProgress />
                <Typography variant="body2" sx={{ mt: 2 }}>
                    Loading list...
                </Typography>
            </Container>
        );
    }

    if (error || !list) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="error" sx={{ mb: 2 }}>
                    Failed to load list. Please try again.
                </Alert>
                <Button
                    variant="outlined"
                    startIcon={<ArrowBack />}
                    onClick={handleBack}
                >
                    Back to Lists
                </Button>
            </Container>
        );
    }

    return (
        <Container maxWidth="sm" sx={{ py: 3 }}>
            {/* Header */}
            <Box
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 2,
                    mb: 3,
                }}
            >
                <IconButton onClick={handleBack} sx={{ p: 1 }}>
                    <ArrowBack />
                </IconButton>
                <Typography
                    variant="h5"
                    component="h1"
                    sx={{ fontWeight: 600, flex: 1 }}
                >
                    {list.name}
                </Typography>
                <Box sx={{ display: "flex", gap: 1 }}>
                    <IconButton
                        onClick={handleEdit}
                        sx={{
                            bgcolor: "primary.main",
                            color: "white",
                            "&:hover": { bgcolor: "primary.dark" },
                        }}
                    >
                        <Edit />
                    </IconButton>
                </Box>
            </Box>
        </Container>
    );
}

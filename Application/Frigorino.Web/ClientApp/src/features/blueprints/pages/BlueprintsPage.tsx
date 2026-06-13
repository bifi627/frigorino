import { Add } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Container,
    Skeleton,
    Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { BlueprintCard } from "../components/BlueprintCard";
import { useSortBlueprints } from "../useSortBlueprints";

export function BlueprintsPage() {
    const { t } = useTranslation();
    const { currentHousehold, isLoading, error, hasActiveHousehold } =
        useCurrentHouseholdWithDetails();
    const [showDraft, setShowDraft] = useState(false);

    const householdId = currentHousehold?.householdId ?? 0;

    const { data: blueprints, isLoading: blueprintsLoading } =
        useSortBlueprints(householdId, householdId > 0);

    if (isLoading || (householdId > 0 && blueprintsLoading)) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Skeleton variant="rectangular" height={200} />
            </Container>
        );
    }

    if (error || !hasActiveHousehold || householdId <= 0) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={t("blueprints.manage")}
                section="household"
                maxWidth="md"
                directActions={[]}
                menuActions={[]}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <Typography
                    variant="body2"
                    color="text.secondary"
                    sx={{ mb: 2 }}
                >
                    {t("blueprints.manageHint")}
                </Typography>

                {(blueprints ?? []).map((blueprint) => (
                    <BlueprintCard
                        key={blueprint.id}
                        householdId={householdId}
                        blueprint={blueprint}
                    />
                ))}

                {showDraft && (
                    <BlueprintCard
                        householdId={householdId}
                        blueprint={null}
                        onCreated={() => setShowDraft(false)}
                    />
                )}

                {!showDraft && (
                    <Box
                        sx={{
                            display: "flex",
                            justifyContent: "center",
                            mt: 2,
                        }}
                    >
                        <Button
                            variant="outlined"
                            startIcon={<Add />}
                            onClick={() => setShowDraft(true)}
                            data-testid="blueprint-new"
                        >
                            {t("blueprints.newBlueprint")}
                        </Button>
                    </Box>
                )}
            </Container>
        </>
    );
}

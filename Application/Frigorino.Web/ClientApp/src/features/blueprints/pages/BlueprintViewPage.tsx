import { Edit } from "@mui/icons-material";
import {
    Alert,
    Box,
    CircularProgress,
    Container,
    Skeleton,
    Typography,
} from "@mui/material";
import { useNavigate, useParams } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import type { ProductCategory, SortBlueprintResponse } from "../../../lib/api";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { BlueprintEditor } from "../components/BlueprintEditor";
import { useSortBlueprint } from "../useSortBlueprint";
import { useUpdateSortBlueprint } from "../useUpdateSortBlueprint";

// Debounced auto-save for the category order. Compares against the last-persisted snapshot
// (rather than a "skip first render" flag, which double-fires under StrictMode): the
// prop-hydrated state matches the snapshot, so mounting never saves — only genuine edits
// diverge and persist. While the order is momentarily empty we hold off, since the backend
// rejects a blueprint with no aisles.
const AUTO_SAVE_DELAY_MS = 600;

function BlueprintArranger({
    householdId,
    blueprint,
}: {
    householdId: number;
    blueprint: SortBlueprintResponse;
}) {
    const { t } = useTranslation();
    const update = useUpdateSortBlueprint();
    const [included, setIncluded] = useState<ProductCategory[]>(
        blueprint.categories,
    );

    const saveBlueprint = update.mutateAsync;
    const lastSaved = useRef(JSON.stringify(blueprint.categories));
    useEffect(() => {
        if (included.length === 0) {
            return;
        }
        const snapshot = JSON.stringify(included);
        if (snapshot === lastSaved.current) {
            return;
        }
        const handle = window.setTimeout(() => {
            saveBlueprint({
                path: { householdId, blueprintId: blueprint.id },
                body: { name: blueprint.name, categories: included },
            })
                .then(() => {
                    lastSaved.current = snapshot;
                })
                .catch(() => toast.error(t("blueprints.saveFailed")));
        }, AUTO_SAVE_DELAY_MS);
        return () => window.clearTimeout(handle);
    }, [included, householdId, blueprint.id, blueprint.name, saveBlueprint, t]);

    return (
        <>
            <Box
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                    minHeight: 24,
                    mb: 1,
                }}
            >
                <Typography variant="body2" color="text.secondary">
                    {t("blueprints.manageHint")}
                </Typography>
                {update.isPending && (
                    <CircularProgress
                        size={18}
                        data-testid="blueprint-saving"
                    />
                )}
            </Box>
            <BlueprintEditor included={included} onChange={setIncluded} />
        </>
    );
}

export const BlueprintViewPage = () => {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { blueprintId } = useParams({
        from: "/household/blueprints/$blueprintId/view",
    });
    const blueprintIdNum = parseInt(blueprintId, 10);

    const {
        currentHousehold,
        isLoading: householdLoading,
        error: householdError,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: blueprint,
        isLoading: blueprintLoading,
        error: blueprintError,
    } = useSortBlueprint(
        householdId,
        blueprintIdNum,
        hasActiveHousehold && !isNaN(blueprintIdNum),
    );

    const isLoading = householdLoading || blueprintLoading;
    const error = householdError || blueprintError;

    if (isLoading) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Skeleton variant="rectangular" height={40} sx={{ mb: 2 }} />
                <Skeleton variant="rectangular" height={300} />
            </Container>
        );
    }

    if (error) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="error">{t("blueprints.failedToLoad")}</Alert>
            </Container>
        );
    }

    if (!hasActiveHousehold || !householdId) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    if (!blueprint) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="warning">{t("blueprints.notFound")}</Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={blueprint.name}
                section="blueprints"
                maxWidth="md"
                directActions={[
                    {
                        icon: <Edit />,
                        onClick: () =>
                            navigate({
                                to: "/household/blueprints/$blueprintId/edit",
                                params: {
                                    blueprintId: blueprint.id.toString(),
                                },
                            }),
                        testId: "blueprint-edit-title",
                    },
                ]}
                menuActions={[]}
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <BlueprintArranger
                    key={blueprint.id}
                    householdId={householdId}
                    blueprint={blueprint}
                />
            </Container>
        </>
    );
};

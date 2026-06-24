import { Add, Edit, Remove, Search, ShoppingCart } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    IconButton,
    Stack,
    Typography,
} from "@mui/material";
import { useParams, useRouter } from "@tanstack/react-router";
import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { SearchInputRow } from "../../../components/shared/SearchInputRow";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { CopyToListSheet } from "../copyToList/CopyToListSheet";
import { RecipeViewList } from "../items/components/RecipeViewList";
import { useRecipeRevision } from "../items/useRecipeRevision";
import { RecipeViewAttachments } from "../attachments/components/RecipeViewAttachments";
import { RecipeTagChips } from "../components/RecipeTagChips";
import { RecipeViewLinks } from "../links/components/RecipeViewLinks";
import { useRecipe } from "../useRecipe";

export const RecipeViewPage = () => {
    const router = useRouter();
    const { t } = useTranslation();
    const { recipeId: recipeIdParam } = useParams({
        from: "/recipes/$recipeId/view",
    });
    const recipeId = parseInt(recipeIdParam);

    const [searchOpen, setSearchOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");
    // Display-only scaling. targetServings overrides the base; null = no override (shows base).
    const [targetServings, setTargetServings] = useState<number | null>(null);
    const [copyOpen, setCopyOpen] = useState(false);

    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: recipe,
        isLoading: recipeLoading,
        error: recipeError,
    } = useRecipe(householdId, recipeId, householdId > 0);

    useRecipeRevision(householdId, recipeId);

    const handleEdit = useCallback(() => {
        router.navigate({
            to: "/recipes/$recipeId/edit",
            params: { recipeId: recipeId.toString() },
        });
    }, [router, recipeId]);

    const handleToggleSearch = useCallback(() => {
        setSearchOpen((prev) => {
            // Clear the query when collapsing so the filter resets (ephemeral by design).
            if (prev) {
                setSearchQuery("");
            }
            return !prev;
        });
    }, []);

    const baseServings = recipe?.servings ?? null;
    const effectiveServings = targetServings ?? baseServings;
    const isScaled =
        baseServings != null &&
        effectiveServings != null &&
        effectiveServings !== baseServings;
    const multiplier =
        baseServings != null && effectiveServings != null && baseServings > 0
            ? effectiveServings / baseServings
            : 1;

    const stepServings = (delta: number) => {
        if (baseServings == null || effectiveServings == null) return;
        const next = Math.min(99, Math.max(1, effectiveServings + delta));
        setTargetServings(next);
    };

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="warning">
                    {t("common.pleaseSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    if (recipeLoading) {
        return (
            <Container maxWidth="sm" sx={{ py: 4, textAlign: "center" }}>
                <CircularProgress />
                <Typography variant="body2" sx={{ mt: 2 }}>
                    {t("recipes.loadingRecipe")}
                </Typography>
            </Container>
        );
    }

    if (recipeError || !recipe) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="error" sx={{ mb: 2 }}>
                    {t("recipes.failedToLoadRecipe")}
                </Alert>
            </Container>
        );
    }

    const directActions: HeadNavigationAction[] = [
        {
            icon: <Edit fontSize="small" />,
            onClick: handleEdit,
            testId: "recipe-edit-button",
        },
    ];
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("common.search"),
            icon: <Search fontSize="small" />,
            onClick: handleToggleSearch,
            testId: "recipe-search-button",
        },
        {
            text: t("copyToList.menuAction"),
            icon: <ShoppingCart fontSize="small" />,
            onClick: () => setCopyOpen(true),
            testId: "recipe-copy-to-list-button",
        },
    ];

    return (
        <Box
            sx={{
                height: "calc(100dvh - 56px)",
                display: "flex",
                flexDirection: "column",
                overflow: "hidden",
            }}
        >
            <PageHeadActionBar
                title={recipe.name || t("recipes.untitledRecipe")}
                section="recipes"
                directActions={directActions}
                menuActions={menuActions}
                menuButtonTestId="recipe-header-menu-toggle"
            />

            <Box
                data-testid="recipe-view-scroll"
                sx={{ flex: 1, overflow: "auto", minHeight: 0 }}
            >
                {recipe.description ? (
                    <Container maxWidth="sm" sx={{ px: 2, pb: 1.5 }}>
                        <Typography
                            data-testid="recipe-description"
                            variant="body2"
                            sx={{
                                color: "text.secondary",
                                fontStyle: "italic",
                                whiteSpace: "pre-wrap",
                                wordBreak: "break-word",
                                lineHeight: 1.5,
                            }}
                        >
                            {recipe.description}
                        </Typography>
                    </Container>
                ) : null}

                {recipe.tags && recipe.tags.length > 0 ? (
                    <Container maxWidth="sm" sx={{ px: 2, pb: 1.5 }}>
                        <RecipeTagChips tags={recipe.tags} />
                    </Container>
                ) : null}

                <RecipeViewLinks
                    householdId={householdId}
                    recipeId={recipeId}
                />

                <RecipeViewAttachments
                    householdId={householdId}
                    recipeId={recipeId}
                />

                <SearchInputRow
                    open={searchOpen}
                    query={searchQuery}
                    onQueryChange={setSearchQuery}
                    onClose={handleToggleSearch}
                    placeholder={t("recipes.searchPlaceholder")}
                    testIdPrefix="recipe-search"
                />

                <Container maxWidth="sm" sx={{ px: 2, pt: 1, pb: 0.5 }}>
                    <Stack
                        direction="row"
                        sx={{
                            alignItems: "center",
                            justifyContent: "space-between",
                        }}
                    >
                        <Typography
                            variant="overline"
                            color="text.secondary"
                            sx={{ fontWeight: 700, letterSpacing: 1 }}
                        >
                            {t("recipes.ingredientsHeading")}
                        </Typography>
                        {baseServings != null ? (
                            <Stack
                                direction="row"
                                sx={{ alignItems: "center" }}
                                spacing={0.5}
                            >
                                {isScaled ? (
                                    <Button
                                        size="small"
                                        onClick={() => setTargetServings(null)}
                                        data-testid="recipe-servings-reset"
                                    >
                                        {t("recipes.resetServings")}
                                    </Button>
                                ) : null}
                                <IconButton
                                    size="small"
                                    onClick={() => stepServings(-1)}
                                    disabled={
                                        effectiveServings != null &&
                                        effectiveServings <= 1
                                    }
                                    data-testid="recipe-servings-decrement"
                                >
                                    <Remove fontSize="small" />
                                </IconButton>
                                <Typography
                                    variant="body2"
                                    sx={{
                                        minWidth: 20,
                                        textAlign: "center",
                                        fontWeight: 600,
                                    }}
                                    data-testid="recipe-servings-value"
                                >
                                    {effectiveServings}
                                </Typography>
                                <IconButton
                                    size="small"
                                    onClick={() => stepServings(1)}
                                    disabled={
                                        effectiveServings != null &&
                                        effectiveServings >= 99
                                    }
                                    data-testid="recipe-servings-increment"
                                >
                                    <Add fontSize="small" />
                                </IconButton>
                            </Stack>
                        ) : null}
                    </Stack>
                    {baseServings != null ? (
                        <Typography
                            variant="caption"
                            color="text.secondary"
                            data-testid="recipe-servings-subline"
                        >
                            {isScaled
                                ? t("recipes.scaledFrom", {
                                      count: baseServings,
                                  })
                                : t("recipes.servingsFor", {
                                      count: baseServings,
                                  })}
                        </Typography>
                    ) : null}
                </Container>

                <RecipeViewList
                    householdId={householdId}
                    recipeId={recipeId}
                    searchQuery={searchQuery}
                    multiplier={multiplier}
                />
            </Box>

            <CopyToListSheet
                open={copyOpen}
                onClose={() => setCopyOpen(false)}
                householdId={householdId}
                recipeId={recipeId}
                multiplier={multiplier}
            />
        </Box>
    );
};

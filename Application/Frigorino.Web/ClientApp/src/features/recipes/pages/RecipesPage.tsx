import { Add, Search } from "@mui/icons-material";
import {
    Alert,
    Autocomplete,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    Container,
    InputAdornment,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse, RecipeTag } from "../../../lib/api";
import { pageContainerSx } from "../../../theme";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { DeleteRecipeConfirmDialog } from "../components/DeleteRecipeConfirmDialog";
import { RecipeActionsMenu } from "../components/RecipeActionsMenu";
import { RecipeSummaryCard } from "../components/RecipeSummaryCard";
import { RecipeTagFilter } from "../components/RecipeTagFilter";
import { rankRecipes } from "../searchRecipes";
import { useHouseholdRecipes } from "../useHouseholdRecipes";

export const RecipesPage = () => {
    const navigate = useNavigate();
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: recipes,
        isLoading,
        error,
    } = useHouseholdRecipes(householdId, householdId > 0);

    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [selectedRecipe, setSelectedRecipe] = useState<RecipeResponse | null>(
        null,
    );
    // Deletion is confirmed via DeleteRecipeConfirmDialog (type-the-name guard), so we keep the
    // target recipe in its own slot — the menu's selectedRecipe is cleared on menu close, which
    // happens before the dialog resolves.
    const [recipeToDelete, setRecipeToDelete] = useState<RecipeResponse | null>(
        null,
    );
    const [expandedRecipeId, setExpandedRecipeId] = useState<number | null>(
        null,
    );
    // Committed chips (each an AND term) plus the pending text the user is still typing. The
    // pending text also filters live, so a single typed word behaves exactly like the old box
    // and selecting/Entering an ingredient just pins it as a chip.
    const [terms, setTerms] = useState<string[]>([]);
    const [inputValue, setInputValue] = useState("");
    const [selectedTags, setSelectedTags] = useState<RecipeTag[]>([]);

    const toggleTag = (tag: RecipeTag) =>
        setSelectedTags((cur) =>
            cur.includes(tag) ? cur.filter((x) => x !== tag) : [...cur, tag],
        );

    const byTags = useMemo(() => {
        const all = recipes ?? [];
        if (selectedTags.length === 0) {
            return all;
        }
        return all.filter((r) =>
            selectedTags.every((tag) => (r.tags ?? []).includes(tag)),
        );
    }, [recipes, selectedTags]);

    const committedTerms = useMemo(
        () => terms.map((s) => s.trim()).filter(Boolean),
        [terms],
    );

    // Recipes that already match the committed chips + tags (ignoring the half-typed text). Each
    // chip narrows this pool, and the next ingredient suggestion is drawn only from it — so you
    // can only add ingredients that still lead to a result.
    const suggestionPool = useMemo(
        () => rankRecipes(byTags, committedTerms),
        [byTags, committedTerms],
    );

    const ingredientOptions = useMemo(() => {
        const committed = new Set(committedTerms.map((s) => s.toLowerCase()));
        const seen = new Set<string>();
        for (const recipe of suggestionPool) {
            for (const ingredient of recipe.ingredients ?? []) {
                if (!committed.has(ingredient.toLowerCase())) {
                    seen.add(ingredient);
                }
            }
        }
        return [...seen].sort((a, b) => a.localeCompare(b));
    }, [suggestionPool, committedTerms]);

    const searchTerms = useMemo(
        () => [...terms, inputValue].map((s) => s.trim()).filter(Boolean),
        [terms, inputValue],
    );

    const visibleRecipes = useMemo(
        () => rankRecipes(byTags, searchTerms),
        [byTags, searchTerms],
    );

    // Only offer tag filters still present in the visible subset (plus any already selected, so
    // they stay toggleable) — narrowing by ingredient or tag shrinks the row to what's reachable.
    const availableTags = useMemo(() => {
        const set = new Set<RecipeTag>(selectedTags);
        for (const recipe of visibleRecipes) {
            for (const tag of recipe.tags ?? []) {
                set.add(tag);
            }
        }
        return set;
    }, [visibleRecipes, selectedTags]);

    const handleBack = () => navigate({ to: "/" });
    const handleCreateRecipe = () => navigate({ to: "/recipes/create" });
    const handleRecipeClick = (recipeId: number) =>
        navigate({
            to: "/recipes/$recipeId/view",
            params: { recipeId: recipeId.toString() },
        });

    const handleMenuOpen = (
        event: React.MouseEvent<HTMLElement>,
        recipe: RecipeResponse,
    ) => {
        setAnchorEl(event.currentTarget);
        setSelectedRecipe(recipe);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
        setSelectedRecipe(null);
    };

    const handleDeleteRecipe = () => {
        if (selectedRecipe?.id) {
            setRecipeToDelete(selectedRecipe);
        }
        handleMenuClose();
    };

    const handleToggleExpand = (recipeId: number) =>
        setExpandedRecipeId((current) =>
            current === recipeId ? null : recipeId,
        );

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("recipes.selectHouseholdToViewRecipes")}
                    <Button
                        onClick={handleBack}
                        sx={{ mt: 1, display: "block" }}
                    >
                        {t("common.goBackToDashboard")}
                    </Button>
                </Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={t("recipes.recipes")}
                section="recipes"
                directActions={[{ icon: <Add />, onClick: handleCreateRecipe }]}
                menuActions={[]}
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
                {isLoading && (
                    <Box
                        sx={{
                            display: "flex",
                            justifyContent: "center",
                            py: 4,
                        }}
                    >
                        <CircularProgress />
                    </Box>
                )}
                {error && (
                    <Alert severity="error" sx={{ mb: 3 }}>
                        {t("recipes.failedToLoadRecipes")}
                    </Alert>
                )}
                {recipes && recipes.length === 0 && !isLoading && (
                    <Card elevation={1} sx={{ textAlign: "center", py: 4 }}>
                        <CardContent>
                            <Typography variant="h6" gutterBottom>
                                {t("recipes.noRecipesYet")}
                            </Typography>
                            <Typography
                                variant="body2"
                                sx={{
                                    color: "text.secondary",
                                    mb: 3,
                                }}
                            >
                                {t("recipes.createFirstRecipe")}
                            </Typography>
                            <Button
                                variant="contained"
                                startIcon={<Add />}
                                onClick={handleCreateRecipe}
                                sx={{ fontWeight: 600 }}
                            >
                                {t("recipes.createYourFirstRecipe")}
                            </Button>
                        </CardContent>
                    </Card>
                )}
                {recipes && recipes.length > 0 && (
                    <>
                        <RecipeTagFilter
                            selected={selectedTags}
                            available={availableTags}
                            onToggle={toggleTag}
                        />
                        <Autocomplete
                            multiple
                            freeSolo
                            size="small"
                            options={ingredientOptions}
                            value={terms}
                            onChange={(_, value) => {
                                setTerms(value);
                                setInputValue("");
                            }}
                            inputValue={inputValue}
                            onInputChange={(_, value, reason) => {
                                // Track typing and the clear button; ignore MUI's "reset"
                                // (fired after a chip is committed) — we clear inputValue in
                                // onChange so the box empties but the chip stays.
                                if (reason === "input" || reason === "clear") {
                                    setInputValue(value);
                                }
                            }}
                            filterOptions={(opts, state) => {
                                // Suggest only after 3 chars, then substring-match within the
                                // committed-chip subset (opts is already that subset).
                                const input = state.inputValue
                                    .trim()
                                    .toLowerCase();
                                if (input.length < 3) {
                                    return [];
                                }
                                return opts.filter((o) =>
                                    o.toLowerCase().includes(input),
                                );
                            }}
                            noOptionsText={
                                inputValue.trim().length >= 3
                                    ? t("common.noMatchingItems")
                                    : t("common.typeAtLeastCharacters")
                            }
                            renderInput={(params) => (
                                <TextField
                                    {...params}
                                    placeholder={
                                        terms.length === 0
                                            ? t(
                                                  "recipes.searchRecipesPlaceholder",
                                              )
                                            : undefined
                                    }
                                    slotProps={{
                                        ...params.slotProps,
                                        input: {
                                            ...params.slotProps.input,
                                            startAdornment: (
                                                <>
                                                    <InputAdornment position="start">
                                                        <Search fontSize="small" />
                                                    </InputAdornment>
                                                    {
                                                        params.slotProps.input
                                                            ?.startAdornment
                                                    }
                                                </>
                                            ),
                                        },
                                        htmlInput: {
                                            ...params.slotProps.htmlInput,
                                            "data-testid":
                                                "recipe-search-input",
                                        },
                                    }}
                                />
                            )}
                            sx={{ mb: 2 }}
                        />
                        {visibleRecipes.length > 0 ? (
                            <Stack spacing={1.5}>
                                {visibleRecipes.map((recipe) => (
                                    <RecipeSummaryCard
                                        key={recipe.id}
                                        recipe={recipe}
                                        householdId={householdId}
                                        expanded={
                                            expandedRecipeId === recipe.id
                                        }
                                        searchTerms={searchTerms}
                                        onToggleExpand={handleToggleExpand}
                                        onOpen={handleRecipeClick}
                                        onMenuOpen={handleMenuOpen}
                                    />
                                ))}
                            </Stack>
                        ) : (
                            <Typography
                                sx={{
                                    color: "text.secondary",
                                    textAlign: "center",
                                    py: 4,
                                }}
                            >
                                {t("recipes.noRecipeMatches")}
                            </Typography>
                        )}
                    </>
                )}
                <RecipeActionsMenu
                    anchorEl={anchorEl}
                    onClose={handleMenuClose}
                    onDelete={handleDeleteRecipe}
                />
                {recipeToDelete?.id && (
                    <DeleteRecipeConfirmDialog
                        open={Boolean(recipeToDelete)}
                        onClose={() => setRecipeToDelete(null)}
                        householdId={householdId}
                        recipeId={recipeToDelete.id}
                        recipeName={
                            recipeToDelete.name || t("recipes.untitledRecipe")
                        }
                    />
                )}
            </Container>
        </>
    );
};

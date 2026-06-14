import { Edit, Search } from "@mui/icons-material";
import {
    Alert,
    Box,
    CircularProgress,
    Container,
    Typography,
} from "@mui/material";
import { useParams, useRouter } from "@tanstack/react-router";
import { useCallback, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { SearchInputRow } from "../../../components/shared/SearchInputRow";
import type { RecipeItemResponse } from "../../../lib/api";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { RecipeContainer } from "../items/components/RecipeContainer";
import { RecipeFooter } from "../items/components/RecipeFooter";
import { useCreateRecipeItem } from "../items/useCreateRecipeItem";
import { useRecipeExtractionPoll } from "../items/useRecipeExtractionPoll";
import { useRecipeItems } from "../items/useRecipeItems";
import { useRecipeRevision } from "../items/useRecipeRevision";
import { useUpdateRecipeItem } from "../items/useUpdateRecipeItem";
import { useRecipe } from "../useRecipe";

export const RecipeViewPage = () => {
    const router = useRouter();
    const { t } = useTranslation();
    // useParams strict: false — route tree is generated in T14; the "/recipes/$recipeId/view"
    // from-string would be the typed form but the tree doesn't exist yet.
    const { recipeId: recipeIdParam } = useParams({ strict: false }) as {
        recipeId: string;
    };
    const recipeId = parseInt(recipeIdParam);

    const scrollContainerRef = useRef<HTMLDivElement>(null);

    const [editingItem, setEditingItem] = useState<RecipeItemResponse | null>(
        null,
    );
    const [searchOpen, setSearchOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");
    const [pendingExtraction, setPendingExtraction] = useState<{
        id: number;
        extractionPending: boolean;
    } | null>(null);

    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: recipe,
        isLoading: recipeLoading,
        error: recipeError,
    } = useRecipe(householdId, recipeId, householdId > 0);

    const { data: items = [] } = useRecipeItems(
        householdId,
        recipeId,
        !!recipe,
    );
    useRecipeRevision(householdId, recipeId);

    const createMutation = useCreateRecipeItem();
    const updateMutation = useUpdateRecipeItem();

    const { isExtracting, extractingItemId } = useRecipeExtractionPoll(
        householdId,
        recipeId,
        pendingExtraction?.id ?? null,
        pendingExtraction?.extractionPending ?? false,
    );

    const scrollToLastItem = useCallback(() => {
        if (scrollContainerRef.current) {
            const listItems =
                scrollContainerRef.current.querySelectorAll(".MuiListItem-root");
            const lastItem = listItems[listItems.length - 1];
            if (lastItem) {
                lastItem.scrollIntoView({
                    behavior: "smooth",
                    block: "center",
                });
            }
        }
    }, []);

    const handleEdit = useCallback(() => {
        // Route wired in T14; cast until routeTree.gen.ts is regenerated.
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        router.navigate({ to: `/recipes/${recipeId}/edit` as any });
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

    const handleAddItem = useCallback(
        async (text: string, comment: string | null) => {
            if (!householdId) return;
            try {
                const created = await createMutation.mutateAsync({
                    path: { householdId, recipeId },
                    body: { text, comment },
                });
                // Only the latest add is polled for extraction; rapid successive adds
                // replace this, so just the last item shows the extracting spinner.
                // The server's create response is the single authority on whether an async
                // extraction was enqueued — no client-side gate to drift from it.
                setPendingExtraction({
                    id: created.id,
                    extractionPending: created.extractionPending,
                });
            } catch {
                // createMutation.onError rolls back the optimistic item; nothing to do here.
            }
        },
        [createMutation, householdId, recipeId],
    );

    const handleUpdateItem = useCallback(
        (
            text: string,
            quantity: import("../../../lib/api").QuantityDto | null,
            comment: string | null,
        ) => {
            if (editingItem?.id && householdId) {
                updateMutation.mutate({
                    path: {
                        householdId,
                        recipeId,
                        itemId: editingItem.id,
                    },
                    body: {
                        text,
                        quantity,
                        clearQuantity: quantity === null,
                        comment,
                    },
                });
                setEditingItem(null);
            }
        },
        [editingItem, updateMutation, householdId, recipeId],
    );

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

    const directActions: HeadNavigationAction[] = [];
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("common.edit"),
            icon: <Edit fontSize="small" />,
            onClick: handleEdit,
            testId: "recipe-edit-button",
        },
        {
            text: t("common.search"),
            icon: <Search fontSize="small" />,
            onClick: handleToggleSearch,
            testId: "recipe-search-button",
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
                subtitle={recipe.description || undefined}
                section="recipes"
                directActions={directActions}
                menuActions={menuActions}
                menuButtonTestId="recipe-header-menu-toggle"
            />

            <SearchInputRow
                open={searchOpen}
                query={searchQuery}
                onQueryChange={setSearchQuery}
                onClose={handleToggleSearch}
                placeholder={t("recipes.searchPlaceholder")}
                testIdPrefix="recipe-search"
            />

            <RecipeContainer
                ref={scrollContainerRef}
                householdId={householdId}
                recipeId={recipeId}
                editingItem={editingItem}
                onEdit={setEditingItem}
                isExtracting={isExtracting}
                extractingItemId={extractingItemId}
            />

            <RecipeFooter
                editingItem={editingItem}
                existingItems={items}
                onAddItem={handleAddItem}
                onUpdateItem={handleUpdateItem}
                onCancelEdit={() => setEditingItem(null)}
                isLoading={createMutation.isPending || updateMutation.isPending}
                onScrollToLast={scrollToLastItem}
            />
        </Box>
    );
};

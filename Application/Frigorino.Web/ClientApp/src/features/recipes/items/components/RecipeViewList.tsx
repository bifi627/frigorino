import { Box, CircularProgress, Container, Paper, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { RecipeItemResponse } from "../../../../lib/api";
import { featureContentPx } from "../../../../theme";
import { matchesQuery } from "../../../../utils/searchUtils";
import { useRecipeItems } from "../useRecipeItems";
import { RecipeViewItem } from "./RecipeViewItem";

interface RecipeViewListProps {
    householdId: number;
    recipeId: number;
    searchQuery?: string;
    multiplier?: number;
}

// Search across text AND comment so ingredient notes match too (mirrors RecipeContainer).
const searchableText = (item: RecipeItemResponse): string =>
    [item.text, item.comment].filter(Boolean).join(" ");

export function RecipeViewList({
    householdId,
    recipeId,
    searchQuery = "",
    multiplier = 1,
}: RecipeViewListProps) {
    const { data: items = [], isLoading, error } = useRecipeItems(
        householdId,
        recipeId,
    );
    const { t } = useTranslation();

    const trimmedQuery = searchQuery.trim();
    const filterActive = trimmedQuery.length > 0;
    const visibleItems = filterActive
        ? items.filter((item) =>
              matchesQuery(searchableText(item), trimmedQuery),
          )
        : items;

    const showNoMatches =
        filterActive && !isLoading && !error && visibleItems.length === 0;
    const showEmpty =
        !filterActive && !isLoading && !error && items.length === 0;

    return (
        <Container
            maxWidth="sm"
            data-testid="recipe-items"
            sx={{
                flex: 1,
                overflow: "auto",
                px: featureContentPx,
                py: 1,
                minHeight: 0,
            }}
        >
            {isLoading ? (
                <Box sx={{ textAlign: "center", py: 4 }}>
                    <CircularProgress />
                </Box>
            ) : null}

            {showEmpty ? (
                <Paper
                    elevation={0}
                    data-testid="recipe-empty"
                    sx={{
                        p: 3,
                        textAlign: "center",
                        border: "2px dashed",
                        borderColor: "divider",
                        mx: 1,
                    }}
                >
                    <Typography variant="body2" color="text.secondary">
                        {t("recipes.emptyIngredients")}
                    </Typography>
                </Paper>
            ) : null}

            {showNoMatches ? (
                <Paper
                    elevation={0}
                    data-testid="recipe-search-no-results"
                    sx={{
                        p: 3,
                        textAlign: "center",
                        border: "2px dashed",
                        borderColor: "divider",
                        mx: 1,
                    }}
                >
                    <Typography variant="body2" color="text.secondary">
                        {t("recipes.noSearchMatches")}
                    </Typography>
                </Paper>
            ) : null}

            {!isLoading && !error
                ? visibleItems.map((item) => (
                      <RecipeViewItem
                          key={item.id}
                          item={item}
                          multiplier={multiplier}
                      />
                  ))
                : null}
        </Container>
    );
}

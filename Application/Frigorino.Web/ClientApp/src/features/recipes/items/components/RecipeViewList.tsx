import {
    Box,
    CircularProgress,
    Container,
    Paper,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type {
    RecipeItemResponse,
    RecipeSectionResponse,
} from "../../../../lib/api";
import { featureContentPx } from "../../../../theme";
import { matchesQuery } from "../../../../utils/searchUtils";
import { useRecipeSections } from "../../sections/useRecipeSections";
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
    const { t } = useTranslation();
    const {
        data: items = [],
        isLoading: itemsLoading,
        error: itemsError,
    } = useRecipeItems(householdId, recipeId);
    const { data: sections = [], isLoading: sectionsLoading } =
        useRecipeSections(householdId, recipeId);

    const isLoading = itemsLoading || sectionsLoading;
    const trimmedQuery = searchQuery.trim();
    const filterActive = trimmedQuery.length > 0;

    const visibleItems = filterActive
        ? items.filter((item) => matchesQuery(searchableText(item), trimmedQuery))
        : items;

    // Section rows: each active section + its (filtered) items, dropping sections that have
    // neither items nor a description.
    const grouped = sections
        .map((section) => ({
            section,
            sectionItems: visibleItems.filter((i) => i.sectionId === section.id),
        }))
        .filter(
            ({ section, sectionItems }) =>
                sectionItems.length > 0 || Boolean(section.description?.trim()),
        );

    const showNoMatches =
        filterActive && !isLoading && !itemsError && visibleItems.length === 0;
    const showEmpty =
        !filterActive && !isLoading && !itemsError && items.length === 0;

    const sectionHeader = (section: RecipeSectionResponse) =>
        section.name?.trim() || t("recipes.ingredientsHeading");

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

            {!isLoading && !itemsError
                ? grouped.map(({ section, sectionItems }) => (
                      <Box
                          key={section.id}
                          data-testid={`recipe-view-section-${section.id}`}
                          sx={{ mb: 2 }}
                      >
                          <Typography
                              variant="subtitle2"
                              data-testid={`recipe-view-section-${section.id}-header`}
                              sx={{ fontWeight: 700, mt: 1 }}
                          >
                              {sectionHeader(section)}
                          </Typography>
                          {section.description?.trim() ? (
                              <Typography
                                  variant="body2"
                                  data-testid={`recipe-view-section-${section.id}-description`}
                                  sx={{
                                      color: "text.secondary",
                                      fontStyle: "italic",
                                      whiteSpace: "pre-wrap",
                                      wordBreak: "break-word",
                                      mb: 0.5,
                                  }}
                              >
                                  {section.description}
                              </Typography>
                          ) : null}
                          {sectionItems.map((item) => (
                              <RecipeViewItem
                                  key={item.id}
                                  item={item}
                                  multiplier={multiplier}
                              />
                          ))}
                      </Box>
                  ))
                : null}
        </Container>
    );
}

import { Box, Chip, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { RecipeTag } from "../../../lib/api";
import { COURSE_TAGS, DIETARY_TAGS, useTagLabel } from "../tags";

interface RecipeTagFilterProps {
    selected: RecipeTag[];
    // Tags still present in the visible subset (RecipesPage). A tag is shown only if it's
    // reachable or already selected — so the row shrinks as the list narrows.
    available: ReadonlySet<RecipeTag>;
    onToggle: (tag: RecipeTag) => void;
}

// Overview filter chip row, grouped by facet. Selecting chips narrows the list (AND across
// selected tags, combined with the search query in RecipesPage).
export const RecipeTagFilter = ({
    selected,
    available,
    onToggle,
}: RecipeTagFilterProps) => {
    const { t } = useTranslation();
    const tagLabel = useTagLabel();

    const renderRow = (label: string, tags: readonly RecipeTag[]) => {
        const shown = tags.filter(
            (tag) => available.has(tag) || selected.includes(tag),
        );
        if (shown.length === 0) {
            return null;
        }
        return (
            <Box>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                    {label}
                </Typography>
                <Box
                    sx={{
                        display: "flex",
                        flexWrap: "wrap",
                        gap: 0.5,
                        mt: 0.25,
                    }}
                >
                    {shown.map((tag) => {
                        const isSelected = selected.includes(tag);
                        return (
                            <Chip
                                key={tag}
                                label={tagLabel(tag)}
                                size="small"
                                color={isSelected ? "primary" : "default"}
                                variant={isSelected ? "filled" : "outlined"}
                                onClick={() => onToggle(tag)}
                                data-testid={`recipe-filter-tag-${tag}`}
                            />
                        );
                    })}
                </Box>
            </Box>
        );
    };

    return (
        <Stack spacing={1} sx={{ mb: 2 }} data-testid="recipe-tag-filter">
            {renderRow(t("recipes.courseHeading"), COURSE_TAGS)}
            {renderRow(t("recipes.dietaryHeading"), DIETARY_TAGS)}
        </Stack>
    );
};

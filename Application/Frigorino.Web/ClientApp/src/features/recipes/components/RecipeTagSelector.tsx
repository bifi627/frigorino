import { AutoAwesome } from "@mui/icons-material";
import {
    Box,
    Button,
    Chip,
    CircularProgress,
    Stack,
    Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse, RecipeTag } from "../../../lib/api";
import { COURSE_TAGS, DIETARY_TAGS, useTagLabel } from "../tags";
import { useSetRecipeTags } from "../useSetRecipeTags";
import { useSuggestRecipeTags } from "../useSuggestRecipeTags";

interface RecipeTagSelectorProps {
    householdId: number;
    recipe: RecipeResponse;
}

export const RecipeTagSelector = ({
    householdId,
    recipe,
}: RecipeTagSelectorProps) => {
    const { t } = useTranslation();
    const tagLabel = useTagLabel();
    const setTags = useSetRecipeTags();
    const suggest = useSuggestRecipeTags();

    // Optimistic local copy of the selected set so chip toggles feel instant. Seeded once on mount;
    // the parent renders EditRecipeForm with key={recipe.id}, so switching recipes remounts this and
    // re-seeds — no reset effect needed.
    const [selected, setSelected] = useState<RecipeTag[]>(recipe.tags ?? []);
    const [ghosts, setGhosts] = useState<RecipeTag[]>([]);

    const persist = (next: RecipeTag[]) => {
        setSelected(next);
        setTags.mutate({
            path: { householdId, recipeId: recipe.id },
            body: { tags: next },
        });
    };

    const toggle = (tag: RecipeTag) => {
        const next = selected.includes(tag)
            ? selected.filter((x) => x !== tag)
            : [...selected, tag];
        persist(next);
    };

    const acceptGhost = (tag: RecipeTag) => {
        setGhosts((g) => g.filter((x) => x !== tag));
        if (!selected.includes(tag)) {
            persist([...selected, tag]);
        }
    };

    const handleSuggest = async () => {
        const res = await suggest.mutateAsync({
            path: { householdId, recipeId: recipe.id },
        });
        setGhosts(
            (res.suggestedTags ?? []).filter((tg) => !selected.includes(tg)),
        );
    };

    const renderGroup = (heading: string, tags: readonly RecipeTag[]) => (
        <Box>
            <Typography variant="overline" sx={{ color: "text.secondary" }}>
                {heading}
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5, mt: 0.5 }}>
                {tags.map((tag) => {
                    const isSelected = selected.includes(tag);
                    return (
                        <Chip
                            key={tag}
                            label={tagLabel(tag)}
                            size="small"
                            color={isSelected ? "primary" : "default"}
                            variant={isSelected ? "filled" : "outlined"}
                            onClick={() => toggle(tag)}
                            data-testid={`recipe-tag-select-${tag}`}
                        />
                    );
                })}
            </Box>
        </Box>
    );

    return (
        <Stack spacing={1} data-testid="recipe-tag-selector">
            <Typography variant="overline" sx={{ color: "text.secondary" }}>
                {t("recipes.tagsHeading")}
            </Typography>
            {renderGroup(t("recipes.courseHeading"), COURSE_TAGS)}
            {renderGroup(t("recipes.dietaryHeading"), DIETARY_TAGS)}

            <Box>
                <Button
                    size="small"
                    variant="text"
                    startIcon={
                        suggest.isPending ? (
                            <CircularProgress size={16} />
                        ) : (
                            <AutoAwesome fontSize="small" />
                        )
                    }
                    onClick={handleSuggest}
                    disabled={suggest.isPending}
                    data-testid="recipe-suggest-tags"
                >
                    {t("recipes.suggestTags")}
                </Button>
                {ghosts.length > 0 && (
                    <Box
                        sx={{
                            display: "flex",
                            flexWrap: "wrap",
                            gap: 0.5,
                            mt: 0.5,
                        }}
                    >
                        {ghosts.map((tag) => (
                            <Chip
                                key={tag}
                                label={tagLabel(tag)}
                                size="small"
                                variant="outlined"
                                color="secondary"
                                onClick={() => acceptGhost(tag)}
                                data-testid={`recipe-tag-suggested-${tag}`}
                            />
                        ))}
                    </Box>
                )}
            </Box>
        </Stack>
    );
};

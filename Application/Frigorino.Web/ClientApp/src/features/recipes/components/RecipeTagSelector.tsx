import { AutoAwesome } from "@mui/icons-material";
import {
    Box,
    Button,
    Chip,
    CircularProgress,
    Stack,
    Typography,
} from "@mui/material";
import { useRef, useState } from "react";
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
    // True only after a Suggest call that produced no new chips — drives the "no suggestions" hint.
    const [emptySuggestion, setEmptySuggestion] = useState(false);

    // Authoritative latest set, read by the handlers instead of the `selected` render closure so
    // rapid consecutive taps each build on the previous one (no lost update). `selected` still
    // drives rendering; persist keeps the two in lockstep.
    const selectedRef = useRef<RecipeTag[]>(selected);

    const persist = (next: RecipeTag[]) => {
        const previous = selectedRef.current;
        selectedRef.current = next;
        setSelected(next);
        setTags.mutate(
            {
                path: { householdId, recipeId: recipe.id },
                body: { tags: next },
            },
            {
                // Roll the optimistic set back if the write fails, so local state can't silently
                // diverge from the server.
                onError: () => {
                    selectedRef.current = previous;
                    setSelected(previous);
                },
            },
        );
    };

    const toggle = (tag: RecipeTag) => {
        const current = selectedRef.current;
        const next = current.includes(tag)
            ? current.filter((x) => x !== tag)
            : [...current, tag];
        persist(next);
    };

    const acceptGhost = (tag: RecipeTag) => {
        setGhosts((g) => g.filter((x) => x !== tag));
        setEmptySuggestion(false);
        if (!selectedRef.current.includes(tag)) {
            persist([...selectedRef.current, tag]);
        }
    };

    const handleSuggest = async () => {
        const res = await suggest.mutateAsync({
            path: { householdId, recipeId: recipe.id },
        });
        const fresh = (res.suggestedTags ?? []).filter(
            (tg) => !selectedRef.current.includes(tg),
        );
        setGhosts(fresh);
        setEmptySuggestion(fresh.length === 0);
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
                {emptySuggestion && (
                    <Typography
                        variant="caption"
                        sx={{
                            display: "block",
                            mt: 0.5,
                            color: "text.secondary",
                        }}
                        data-testid="recipe-no-tag-suggestions"
                    >
                        {t("recipes.noTagSuggestions")}
                    </Typography>
                )}
            </Box>
        </Stack>
    );
};

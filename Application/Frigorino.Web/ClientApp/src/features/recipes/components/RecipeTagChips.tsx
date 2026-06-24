import { Box, Chip } from "@mui/material";
import type { RecipeTag } from "../../../lib/api";
import { ALL_TAGS, useTagLabel } from "../tags";

interface RecipeTagChipsProps {
    tags: RecipeTag[];
    size?: "small" | "medium";
}

// Read-only tag display (recipe view + summary peek). Orders by the canonical facet order and
// renders a testid per tag. Renders nothing when there are no tags.
export const RecipeTagChips = ({
    tags,
    size = "small",
}: RecipeTagChipsProps) => {
    const tagLabel = useTagLabel();
    if (!tags || tags.length === 0) {
        return null;
    }
    const ordered = ALL_TAGS.filter((t) => tags.includes(t));
    return (
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
            {ordered.map((tag) => (
                <Chip
                    key={tag}
                    label={tagLabel(tag)}
                    size={size}
                    variant="outlined"
                    data-testid={`recipe-tag-${tag}`}
                />
            ))}
        </Box>
    );
};

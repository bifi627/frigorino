import type { RecipeResponse } from "../../lib/api";

// Multi-term AND search. Each term is matched independently and a recipe is kept only if EVERY
// term hits some field — so several expiring ingredients can be required at once. A term's hit
// tier ranks a name match (3) above a description match (2) above an ingredient-only match (1);
// a recipe's score is the sum across terms, so broader/stronger matches float up. Empty term
// list returns the list unchanged (already newest-first from the API). Array.sort is stable, so
// ties keep the API order (newest-first) — that is the tiebreak.
export const rankRecipes = (
    recipes: RecipeResponse[],
    terms: string[],
): RecipeResponse[] => {
    const queries = terms.map((t) => t.trim().toLowerCase()).filter(Boolean);
    if (queries.length === 0) {
        return recipes;
    }

    const termScore = (r: RecipeResponse, q: string): number => {
        if (r.name?.toLowerCase().includes(q)) {
            return 3;
        }
        if (r.description?.toLowerCase().includes(q)) {
            return 2;
        }
        if (r.ingredients?.some((i) => i.toLowerCase().includes(q))) {
            return 1;
        }
        return 0;
    };

    return recipes
        .map((recipe) => ({
            recipe,
            scores: queries.map((q) => termScore(recipe, q)),
        }))
        .filter((x) => x.scores.every((s) => s > 0))
        .map((x) => ({
            recipe: x.recipe,
            total: x.scores.reduce((a, b) => a + b, 0),
        }))
        .sort((a, b) => b.total - a.total)
        .map((x) => x.recipe);
};

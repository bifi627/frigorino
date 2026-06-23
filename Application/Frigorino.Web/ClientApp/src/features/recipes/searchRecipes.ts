import type { RecipeResponse } from "../../lib/api";

// Tiered relevance: a name hit (3) outranks a description hit (2), which outranks an
// ingredient-only hit (1). Empty query returns the list unchanged (already newest-first
// from the API). Non-matching recipes are dropped when a query is present. Array.sort is
// stable, so ties keep the API order (newest-first) — that is the tiebreak.
export const rankRecipes = (
    recipes: RecipeResponse[],
    query: string,
): RecipeResponse[] => {
    const q = query.trim().toLowerCase();
    if (!q) {
        return recipes;
    }

    const score = (r: RecipeResponse): number => {
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
        .map((recipe) => ({ recipe, s: score(recipe) }))
        .filter((x) => x.s > 0)
        .sort((a, b) => b.s - a.s)
        .map((x) => x.recipe);
};

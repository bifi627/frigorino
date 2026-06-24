import { useTranslation } from "react-i18next";
import { useCallback } from "react";
import type { RecipeTag } from "../../lib/api";

// Facet ordering for every surface (selector, view, filter). The numeric enum ranges group the
// facets on the backend; the frontend uses these explicit arrays.
export const COURSE_TAGS: readonly RecipeTag[] = [
    "Breakfast",
    "Starter",
    "Main",
    "Side",
    "Salad",
    "Soup",
    "Dessert",
    "Snack",
    "Drink",
    "Sauce",
    "Baking",
    "Bread",
];

export const DIETARY_TAGS: readonly RecipeTag[] = [
    "Vegetarian",
    "Vegan",
    "GlutenFree",
    "DairyFree",
    "LactoseFree",
    "LowCarb",
];

export const ALL_TAGS: readonly RecipeTag[] = [...COURSE_TAGS, ...DIETARY_TAGS];

// Translated label for a tag, e.g. "recipes.tagLabels.GlutenFree" -> "Gluten-free".
export const useTagLabel = () => {
    const { t } = useTranslation();
    return useCallback((tag: RecipeTag) => t(`recipes.tagLabels.${tag}`), [t]);
};

import type { TFunction } from "i18next";

// Maps an import/preview error (hey-api throws the parsed response body on non-2xx) to a user message.
// 422 bodies carry a `code`; 400 ValidationProblem carries `{ errors: { Url: [...] } }` and no code.
export const recipeImportErrorMessage = (err: unknown, t: TFunction): string => {
    const code = (err as { code?: string } | null)?.code;
    if (code === "no_recipe_found") {
        return t("recipes.import.noRecipeFound");
    }
    if (code === "page_too_large") {
        return t("recipes.import.pageTooLarge");
    }
    if (code === "fetch_failed") {
        return t("recipes.import.fetchFailed");
    }
    const errors = (err as { errors?: Record<string, string[]> } | null)?.errors;
    if (errors && Object.keys(errors).length > 0) {
        return t("recipes.import.invalidUrl");
    }
    return t("common.errorOccurred");
};

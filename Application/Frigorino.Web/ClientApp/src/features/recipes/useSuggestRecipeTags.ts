import { useMutation } from "@tanstack/react-query";
import { suggestRecipeTagsMutation } from "../../lib/api/@tanstack/react-query.gen";

// Stateless on-demand suggestion: the caller passes { path: { householdId, recipeId } } to
// mutateAsync and reads response.suggestedTags. No cache invalidation — suggestions aren't persisted.
export const useSuggestRecipeTags = () =>
    useMutation({
        ...suggestRecipeTagsMutation(),
    });

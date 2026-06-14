import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeItemsQueryKey,
    getRecipeSectionsQueryKey,
    restoreRecipeSectionMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreRecipeSection = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreRecipeSectionMutation(),
        onSuccess: (_data, variables) => {
            const path = {
                householdId: variables.path.householdId,
                recipeId: variables.path.recipeId,
            };
            queryClient.invalidateQueries({
                queryKey: getRecipeSectionsQueryKey({ path }),
            });
            queryClient.invalidateQueries({
                queryKey: getRecipeItemsQueryKey({ path }),
            });
        },
    });
};

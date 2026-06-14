import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeQueryKey,
    getRecipesQueryKey,
    updateRecipeMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateRecipe = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...updateRecipeMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeQueryKey({
                    path: { householdId: variables.path.householdId, recipeId: variables.path.recipeId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({ path: { householdId: variables.path.householdId } }),
            });
        },
    });
};

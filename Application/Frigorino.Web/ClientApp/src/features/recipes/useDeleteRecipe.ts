import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    deleteRecipeMutation,
    getRecipeQueryKey,
    getRecipesQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useDeleteRecipe = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...deleteRecipeMutation(),
        onSuccess: (_data, variables) => {
            queryClient.removeQueries({
                queryKey: getRecipeQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getRecipesQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};

import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeQueryKey,
    getRecipesQueryKey,
    setRecipeTagsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useSetRecipeTags = () => {
    const queryClient = useQueryClient();
    return useMutation({
        ...setRecipeTagsMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
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

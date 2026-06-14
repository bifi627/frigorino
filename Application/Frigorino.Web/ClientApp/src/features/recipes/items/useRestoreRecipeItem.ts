import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeItemsQueryKey,
    restoreRecipeItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreRecipeItem = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreRecipeItemMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
        },
    });
};

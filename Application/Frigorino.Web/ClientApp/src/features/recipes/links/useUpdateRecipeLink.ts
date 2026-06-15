import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeLinksQueryKey,
    updateRecipeLinkMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useUpdateRecipeLink = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateRecipeLinkMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeLinksQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
        },
    });
};

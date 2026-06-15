import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeLinksQueryKey,
    restoreRecipeLinkMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreRecipeLink = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreRecipeLinkMutation(),
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

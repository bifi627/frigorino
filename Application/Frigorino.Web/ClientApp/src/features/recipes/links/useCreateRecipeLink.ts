import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createRecipeLinkMutation,
    getRecipeLinksQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useCreateRecipeLink = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createRecipeLinkMutation(),
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

import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeSectionsQueryKey,
    updateRecipeSectionMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useUpdateRecipeSection = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateRecipeSectionMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeSectionsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
        },
    });
};

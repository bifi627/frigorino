import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createRecipeSectionMutation,
    getRecipeSectionsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useCreateRecipeSection = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createRecipeSectionMutation(),
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

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    createRecipeItemMutation,
    getRecipeItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeItemResponse } from "../../../lib/api/types.gen";

export const useCreateRecipeItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...createRecipeItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });

            await queryClient.cancelQueries({ queryKey });

            const previousItems =
                queryClient.getQueryData<RecipeItemResponse[]>(queryKey);

            const optimisticItem: RecipeItemResponse = {
                id: Date.now(),
                recipeId: variables.path.recipeId,
                text: variables.body.text,
                comment: variables.body.comment ?? null,
                quantity: null,
                // Placeholder rank — rendering trusts array order (appended at the end, matching
                // the server's append). The authoritative rank arrives on the refetch in onSuccess.
                rank: "",
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
                extractionPending: false,
            };

            queryClient.setQueryData<RecipeItemResponse[]>(
                queryKey,
                (old) => (old ? [...old, optimisticItem] : [optimisticItem]),
            );

            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getRecipeItemsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_data, variables) => {
            debouncedInvalidate(
                getRecipeItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};

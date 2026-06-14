import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getRecipeItemsQueryKey,
    reorderRecipeItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeItemResponse } from "../../../lib/api/types.gen";

export const useReorderRecipeItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderRecipeItemMutation(),
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

            // The server mints the authoritative rank; optimistically we just move the dragged
            // element to its new array position (single section). The real rank arrives on refetch.
            queryClient.setQueryData<RecipeItemResponse[]>(
                queryKey,
                (old) => {
                    if (!old) return old;
                    const moved = old.find(
                        (i) => i.id === variables.path.itemId,
                    );
                    if (!moved) return old;

                    const others = old.filter((i) => i.id !== moved.id);
                    const afterId = variables.body.afterId;
                    if (!afterId) {
                        // Top of the list.
                        others.unshift(moved);
                        return others;
                    }
                    const anchorIdx = others.findIndex((i) => i.id === afterId);
                    others.splice(
                        anchorIdx === -1 ? others.length : anchorIdx + 1,
                        0,
                        moved,
                    );
                    return others;
                },
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
        onSettled: (_data, _error, variables) => {
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

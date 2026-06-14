import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getRecipeItemsQueryKey,
    updateRecipeItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeItemResponse } from "../../../lib/api/types.gen";

export const useUpdateRecipeItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...updateRecipeItemMutation(),
        onMutate: async (variables) => {
            const listQueryKey = getRecipeItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });

            await queryClient.cancelQueries({ queryKey: listQueryKey });

            const previousItems =
                queryClient.getQueryData<RecipeItemResponse[]>(listQueryKey);

            // Text preserves on null (??); clearQuantity removes the quantity, otherwise null =
            // preserve (mirrors the domain's UpdateItem). Comment: null = preserve, trimmed-empty
            // string = clear (domain rule: comment == null ? old : (comment.trim() || null)).
            queryClient.setQueryData<RecipeItemResponse[]>(
                listQueryKey,
                (old) => {
                    if (!old) return old;
                    return old.map((item) => {
                        if (item.id !== variables.path.itemId) {
                            return item;
                        }
                        const comment =
                            variables.body.comment == null
                                ? item.comment
                                : variables.body.comment.trim() || null;
                        return {
                            ...item,
                            text: variables.body.text ?? item.text,
                            quantity: variables.body.clearQuantity
                                ? null
                                : (variables.body.quantity ?? item.quantity),
                            comment,
                            updatedAt: new Date().toISOString(),
                        };
                    });
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

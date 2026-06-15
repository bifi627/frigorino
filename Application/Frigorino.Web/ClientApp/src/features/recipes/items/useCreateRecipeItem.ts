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

            const tempId = Date.now();
            const optimisticItem: RecipeItemResponse = {
                id: tempId,
                recipeId: variables.path.recipeId,
                sectionId: variables.body.sectionId,
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

            queryClient.setQueryData<RecipeItemResponse[]>(queryKey, (old) =>
                old ? [...old, optimisticItem] : [optimisticItem],
            );

            return { previousItems, tempId };
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
        onSuccess: (data, variables, context) => {
            const queryKey = getRecipeItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            // Swap the temp-id optimistic row for the real server item immediately. Anything
            // keyed on the real id — the extraction-poll row highlight, and an edit/reorder fired
            // before the debounced refetch lands — must see the real id right away. Otherwise the
            // PUT/PATCH targets the Date.now() temp id, which overflows the {itemId:int} route
            // constraint and falls through to the SPA fallback (HTTP 500). Mirrors useCreateListItem.
            if (context?.tempId !== undefined) {
                queryClient.setQueryData<RecipeItemResponse[]>(
                    queryKey,
                    (old) =>
                        old?.map((i) => (i.id === context.tempId ? data : i)) ??
                        old,
                );
            }
            debouncedInvalidate(queryKey);
        },
    });
};

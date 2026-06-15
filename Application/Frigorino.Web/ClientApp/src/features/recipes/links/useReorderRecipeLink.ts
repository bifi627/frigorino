import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getRecipeLinksQueryKey,
    reorderRecipeLinkMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeLinkResponse } from "../../../lib/api/types.gen";

export const useReorderRecipeLink = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderRecipeLinkMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeLinksQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousLinks =
                queryClient.getQueryData<RecipeLinkResponse[]>(queryKey);

            queryClient.setQueryData<RecipeLinkResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const moved = old.find((l) => l.id === variables.path.linkId);
                if (!moved) return old;
                const others = old.filter((l) => l.id !== moved.id);
                const afterId = variables.body.afterId;
                if (!afterId) {
                    others.unshift(moved);
                    return others;
                }
                const anchorIdx = others.findIndex((l) => l.id === afterId);
                others.splice(
                    anchorIdx === -1 ? others.length : anchorIdx + 1,
                    0,
                    moved,
                );
                return others;
            });

            return { previousLinks };
        },
        onError: (_data, variables, context) => {
            if (context?.previousLinks) {
                queryClient.setQueryData(
                    getRecipeLinksQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousLinks,
                );
            }
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeLinksQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};

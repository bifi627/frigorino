import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getRecipeSectionsQueryKey,
    reorderRecipeSectionMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeSectionResponse } from "../../../lib/api/types.gen";

export const useReorderRecipeSection = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderRecipeSectionMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeSectionsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousSections =
                queryClient.getQueryData<RecipeSectionResponse[]>(queryKey);

            queryClient.setQueryData<RecipeSectionResponse[]>(
                queryKey,
                (old) => {
                    if (!old) return old;
                    const moved = old.find(
                        (s) => s.id === variables.path.sectionId,
                    );
                    if (!moved) return old;
                    const others = old.filter((s) => s.id !== moved.id);
                    const afterId = variables.body.afterId;
                    if (!afterId) {
                        others.unshift(moved);
                        return others;
                    }
                    const anchorIdx = others.findIndex((s) => s.id === afterId);
                    others.splice(
                        anchorIdx === -1 ? others.length : anchorIdx + 1,
                        0,
                        moved,
                    );
                    return others;
                },
            );

            return { previousSections };
        },
        onError: (_data, variables, context) => {
            if (context?.previousSections) {
                queryClient.setQueryData(
                    getRecipeSectionsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousSections,
                );
            }
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeSectionsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};

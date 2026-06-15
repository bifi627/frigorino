import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getRecipeAttachmentsQueryKey,
    reorderRecipeAttachmentMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeAttachmentResponse } from "../../../lib/api/types.gen";

export const useReorderRecipeAttachment = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderRecipeAttachmentMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeAttachmentsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });
            await queryClient.cancelQueries({ queryKey });
            const previousAttachments =
                queryClient.getQueryData<RecipeAttachmentResponse[]>(queryKey);

            queryClient.setQueryData<RecipeAttachmentResponse[]>(
                queryKey,
                (old) => {
                    if (!old) return old;
                    const moved = old.find(
                        (a) => a.id === variables.path.attachmentId,
                    );
                    if (!moved) return old;
                    const others = old.filter((a) => a.id !== moved.id);
                    const afterId = variables.body.afterId;
                    if (!afterId) {
                        others.unshift(moved);
                        return others;
                    }
                    const anchorIdx = others.findIndex((a) => a.id === afterId);
                    others.splice(
                        anchorIdx === -1 ? others.length : anchorIdx + 1,
                        0,
                        moved,
                    );
                    return others;
                },
            );

            return { previousAttachments };
        },
        onError: (_data, variables, context) => {
            if (context?.previousAttachments) {
                queryClient.setQueryData(
                    getRecipeAttachmentsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousAttachments,
                );
            }
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeAttachmentsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};

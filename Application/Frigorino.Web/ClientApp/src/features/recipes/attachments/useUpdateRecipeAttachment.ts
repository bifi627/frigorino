import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeAttachmentsQueryKey,
    updateRecipeAttachmentMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useUpdateRecipeAttachment = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateRecipeAttachmentMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getRecipeAttachmentsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            });
        },
    });
};

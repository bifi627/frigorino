import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getRecipeAttachmentsQueryKey,
    restoreRecipeAttachmentMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreRecipeAttachment = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreRecipeAttachmentMutation(),
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

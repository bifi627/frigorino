import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createRecipeAttachmentMutation,
    getRecipeAttachmentsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

// Arg-less, per the hook convention. Caller passes
//   { path: { householdId, recipeId }, body: { file, caption } }.
// hey-api serializes the body via formDataBodySerializer (FormData); do NOT set Content-Type — the
// browser sets the multipart boundary. No optimistic insert (upload shows a busy state);
// invalidate the attachments query on success.
export const useCreateRecipeAttachment = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createRecipeAttachmentMutation(),
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

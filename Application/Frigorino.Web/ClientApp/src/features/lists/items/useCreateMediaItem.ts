import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createMediaItemMutation,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

// Arg-less, per the hook convention. Caller passes
//   { path: { householdId, listId }, body: { file, type, caption } }.
// hey-api serializes the body via formDataBodySerializer (FormData); do NOT set Content-Type — the
// browser sets the multipart boundary. No optimistic insert (uploads show progress in the sheet);
// invalidate the items query on success.
export const useCreateMediaItem = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createMediaItemMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            });
        },
    });
};

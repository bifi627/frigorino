import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getItemsQueryKey,
    reorderItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";

export const useReorderListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                },
            });

            await queryClient.cancelQueries({ queryKey });

            const previousItems =
                queryClient.getQueryData<ListItemResponse[]>(queryKey);

            // The server mints the authoritative rank; optimistically we just move the dragged
            // element to its new array position (visual order only). The real rank arrives on
            // refetch and reconciles.
            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const moved = old.find((i) => i.id === variables.path.itemId);
                if (!moved) return old;

                const others = old.filter((i) => i.id !== moved.id);
                const afterId = variables.body.afterId;
                if (!afterId) {
                    // Top of the moved item's status section.
                    const firstSameStatus = others.findIndex(
                        (i) => i.status === moved.status,
                    );
                    const insertAt =
                        firstSameStatus === -1
                            ? others.length
                            : firstSameStatus;
                    others.splice(insertAt, 0, moved);
                    return others;
                }
                const anchorIdx = others.findIndex((i) => i.id === afterId);
                others.splice(
                    anchorIdx === -1 ? others.length : anchorIdx + 1,
                    0,
                    moved,
                );
                return others;
            });

            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getItemsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            listId: variables.path.listId,
                        },
                    }),
                    context.previousItems,
                );
            }
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
        },
    });
};

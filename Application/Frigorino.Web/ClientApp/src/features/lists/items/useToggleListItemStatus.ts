import { useMutation, useQueryClient } from "@tanstack/react-query";
import { computeAppendSortOrder } from "../../../common/sortOrder";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getItemsQueryKey,
    toggleItemStatusMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";

export const useToggleListItemStatus = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...toggleItemStatusMutation(),
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

            // Optimistic mirror of server math; see common/sortOrder.ts.
            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const movedItem = old.find(
                    (i) => i.id === variables.path.itemId,
                );
                if (!movedItem) return old;

                const newStatus = !movedItem.status;
                const targetSection = old.filter(
                    (i) => i.status === newStatus,
                );
                const newSortOrder = computeAppendSortOrder(
                    targetSection,
                    newStatus,
                );

                return old.map((i) =>
                    i.id === movedItem.id
                        ? { ...i, status: newStatus, sortOrder: newSortOrder }
                        : i,
                );
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
            // Deliberate: no `onSuccess` invalidate. The optimistic update is the only UI
            // signal we want — `onSettled` covers both success and rollback paths with a
            // single debounced refetch. Don't add an `onSuccess` invalidate "for consistency".
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

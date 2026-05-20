import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    computeAppendSortOrder,
    computeReorderSortOrder,
} from "../../../common/sortOrder";
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

            // Optimistic mirror of server math; see common/sortOrder.ts.
            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const movedItem = old.find(
                    (i) => i.id === variables.path.itemId,
                );
                if (!movedItem) return old;

                const section = old.filter(
                    (i) =>
                        i.status === movedItem.status &&
                        i.id !== movedItem.id,
                );

                const newSortOrder = computeReorderSortOrder({
                    section,
                    afterId: variables.body.afterId,
                    emptyDefault: computeAppendSortOrder([], movedItem.status),
                });

                return old.map((i) =>
                    i.id === movedItem.id
                        ? { ...i, sortOrder: newSortOrder }
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

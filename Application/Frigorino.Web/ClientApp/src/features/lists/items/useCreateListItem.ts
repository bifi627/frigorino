import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    createItemMutation,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";

export const useCreateListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...createItemMutation(),
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

            // Append below the last unchecked item to match the server's AddItem semantics
            // (List.ComputeAppendSortOrder: last + DefaultGap). Falls back to a sentinel so the
            // item appears at the bottom of unchecked when the cache is empty.
            const lastUncheckedSortOrder =
                previousItems
                    ?.filter((i) => !i.status)
                    .reduce((max, i) => Math.max(max, i.sortOrder), 0) ?? 0;

            const tempId = Date.now();
            const optimisticItem: ListItemResponse = {
                id: tempId,
                text: variables.body.text,
                quantity: null,
                status: false,
                sortOrder: lastUncheckedSortOrder + 1,
                listId: variables.path.listId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) =>
                old ? [...old, optimisticItem] : [optimisticItem],
            );

            return { previousItems, tempId };
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
        onSuccess: (data, variables, context) => {
            const queryKey = getItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                },
            });
            // Swap the temp-id optimistic row for the real server item immediately, so
            // anything keyed on the real id (e.g. the extraction-poll row highlight) matches
            // right away instead of waiting for the debounced refetch ~1s later.
            if (context?.tempId !== undefined) {
                queryClient.setQueryData<ListItemResponse[]>(
                    queryKey,
                    (old) =>
                        old?.map((i) => (i.id === context.tempId ? data : i)) ??
                        old,
                );
            }
            debouncedInvalidate(queryKey);
        },
    });
};

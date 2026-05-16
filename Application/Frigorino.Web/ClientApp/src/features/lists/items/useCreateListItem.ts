import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type { CreateItemRequest, ListItemResponse } from "../../../lib/api";
import { listItemKeys } from "./listItemKeys";

export const useCreateListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            listId,
            data,
        }: {
            householdId: number;
            listId: number;
            data: CreateItemRequest;
        }) => ClientApi.listItems.createItem(householdId, listId, data),
        onMutate: async (variables) => {
            await queryClient.cancelQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });

            const previousItems = queryClient.getQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Append below the last unchecked item to match the server's AddItem semantics
            // (List.ComputeAppendSortOrder: last + DefaultGap). Falls back to a sentinel so the
            // item appears at the bottom of unchecked when the cache is empty.
            const lastUncheckedSortOrder =
                previousItems
                    ?.filter((i) => !i.status)
                    .reduce((max, i) => Math.max(max, i.sortOrder), 0) ?? 0;

            const optimisticItem: ListItemResponse = {
                id: Date.now(),
                text: variables.data.text,
                quantity: variables.data.quantity,
                status: false,
                sortOrder: lastUncheckedSortOrder + 1,
                listId: variables.listId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => (old ? [...old, optimisticItem] : [optimisticItem]),
            );

            return { previousItems };
        },
        onError: (_, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    listItemKeys.byList(
                        variables.householdId,
                        variables.listId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_, variables) => {
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};

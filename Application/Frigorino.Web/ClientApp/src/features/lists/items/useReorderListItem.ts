import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import {
    computeAppendSortOrder,
    computeReorderSortOrder,
} from "../../../common/sortOrder";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type { ListItemResponse, ReorderItemRequest } from "../../../lib/api";
import { listItemKeys } from "./listItemKeys";

export const useReorderListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            listId,
            itemId,
            data,
        }: {
            householdId: number;
            listId: number;
            itemId: number;
            data: ReorderItemRequest;
        }) =>
            ClientApi.listItems.reorderItem(householdId, listId, itemId, data),
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

            // Optimistic mirror of server math; see common/sortOrder.ts.
            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    const movedItem = old.find((i) => i.id === variables.itemId);
                    if (!movedItem) return old;

                    const section = old.filter(
                        (i) =>
                            i.status === movedItem.status &&
                            i.id !== movedItem.id,
                    );

                    const newSortOrder = computeReorderSortOrder({
                        section,
                        afterId: variables.data.afterId,
                        emptyDefault: computeAppendSortOrder([], movedItem.status),
                    });

                    return old.map((i) =>
                        i.id === movedItem.id
                            ? { ...i, sortOrder: newSortOrder }
                            : i,
                    );
                },
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
        onSettled: (_, __, variables) => {
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};

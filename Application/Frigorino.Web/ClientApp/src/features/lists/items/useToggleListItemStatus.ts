import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { computeAppendSortOrder } from "../../../common/sortOrder";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type { ListItemResponse } from "../../../lib/api";
import { listItemKeys } from "./listItemKeys";

export const useToggleListItemStatus = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            listId,
            itemId,
        }: {
            householdId: number;
            listId: number;
            itemId: number;
        }) =>
            ClientApi.listItems.toggleItemStatus(householdId, listId, itemId),
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
            // Deliberate: no `onSuccess` invalidate. The optimistic update is the only UI
            // signal we want — `onSettled` covers both success and rollback paths with a
            // single debounced refetch. Don't add an `onSuccess` invalidate "for consistency".
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};

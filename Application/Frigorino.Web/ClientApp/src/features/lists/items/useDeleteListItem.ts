import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type { ListItemResponse } from "../../../lib/api";
import { listItemKeys } from "./listItemKeys";

export const useDeleteListItem = () => {
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
        }) => ClientApi.listItems.deleteItem(householdId, listId, itemId),
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

            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    return old.filter((item) => item.id !== variables.itemId);
                },
            );

            queryClient.removeQueries({
                queryKey: listItemKeys.detail(variables.itemId),
            });

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

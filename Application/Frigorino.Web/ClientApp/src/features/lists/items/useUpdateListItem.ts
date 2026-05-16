import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type { ListItemResponse, UpdateItemRequest } from "../../../lib/api";
import { listItemKeys } from "./listItemKeys";

export const useUpdateListItem = () => {
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
            data: UpdateItemRequest;
        }) =>
            ClientApi.listItems.updateItem(householdId, listId, itemId, data),
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
                    return old.map((item) =>
                        item.id === variables.itemId
                            ? {
                                  ...item,
                                  text: variables.data.text ?? item.text,
                                  quantity:
                                      variables.data.quantity ?? item.quantity,
                                  updatedAt: new Date().toISOString(),
                              }
                            : item,
                    );
                },
            );

            const currentItem = queryClient.getQueryData<ListItemResponse>(
                listItemKeys.detail(variables.itemId),
            );
            if (currentItem) {
                queryClient.setQueryData<ListItemResponse>(
                    listItemKeys.detail(variables.itemId),
                    {
                        ...currentItem,
                        text: variables.data.text ?? currentItem.text,
                        quantity:
                            variables.data.quantity ?? currentItem.quantity,
                        updatedAt: new Date().toISOString(),
                    },
                );
            }

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
            debouncedInvalidate(listItemKeys.detail(variables.itemId));
        },
    });
};

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getItemQueryKey,
    getItemsQueryKey,
    updateItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";

export const useUpdateListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...updateItemMutation(),
        onMutate: async (variables) => {
            const listQueryKey = getItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                },
            });

            await queryClient.cancelQueries({ queryKey: listQueryKey });

            const previousItems =
                queryClient.getQueryData<ListItemResponse[]>(listQueryKey);

            queryClient.setQueryData<ListItemResponse[]>(
                listQueryKey,
                (old) => {
                    if (!old) return old;
                    return old.map((item) =>
                        item.id === variables.path.itemId
                            ? {
                                  ...item,
                                  text: variables.body.text ?? item.text,
                                  quantity:
                                      variables.body.quantity ?? item.quantity,
                                  updatedAt: new Date().toISOString(),
                              }
                            : item,
                    );
                },
            );

            const detailKey = getItemQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                    itemId: variables.path.itemId,
                },
            });
            const currentItem =
                queryClient.getQueryData<ListItemResponse>(detailKey);
            if (currentItem) {
                queryClient.setQueryData<ListItemResponse>(detailKey, {
                    ...currentItem,
                    text: variables.body.text ?? currentItem.text,
                    quantity: variables.body.quantity ?? currentItem.quantity,
                    updatedAt: new Date().toISOString(),
                });
            }

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
        onSuccess: (_data, variables) => {
            debouncedInvalidate(
                getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
            debouncedInvalidate(
                getItemQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                        itemId: variables.path.itemId,
                    },
                }),
            );
        },
    });
};

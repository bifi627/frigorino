import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getInventoryItemsQueryKey,
    updateInventoryItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { InventoryItemResponse } from "../../../lib/api/types.gen";

export const useUpdateInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...updateInventoryItemMutation(),
        onMutate: async (variables) => {
            const listQueryKey = getInventoryItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    inventoryId: variables.path.inventoryId,
                },
            });

            await queryClient.cancelQueries({ queryKey: listQueryKey });

            const previousItems =
                queryClient.getQueryData<InventoryItemResponse[]>(listQueryKey);

            // Text preserves on null (??); clearQuantity removes the quantity, otherwise null =
            // preserve (mirrors the domain's UpdateItem). ExpiryDate is write-through (=) — null
            // clears the value.
            queryClient.setQueryData<InventoryItemResponse[]>(
                listQueryKey,
                (old) => {
                    if (!old) return old;
                    return old.map((item) =>
                        item.id === variables.path.itemId
                            ? {
                                  ...item,
                                  text: variables.body.text ?? item.text,
                                  quantity: variables.body.clearQuantity
                                      ? null
                                      : (variables.body.quantity ??
                                        item.quantity),
                                  expiryDate: variables.body.expiryDate,
                                  updatedAt: new Date().toISOString(),
                              }
                            : item,
                    );
                },
            );

            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getInventoryItemsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            inventoryId: variables.path.inventoryId,
                        },
                    }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_data, variables) => {
            debouncedInvalidate(
                getInventoryItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        inventoryId: variables.path.inventoryId,
                    },
                }),
            );
        },
    });
};

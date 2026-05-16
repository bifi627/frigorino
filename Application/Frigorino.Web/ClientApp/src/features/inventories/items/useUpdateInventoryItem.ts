import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type {
    InventoryItemResponse,
    UpdateInventoryItemRequest,
} from "../../../lib/api";
import { inventoryItemKeys } from "./inventoryItemKeys";

export const useUpdateInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            inventoryId,
            itemId,
            data,
        }: {
            householdId: number;
            inventoryId: number;
            itemId: number;
            data: UpdateInventoryItemRequest;
        }) =>
            ClientApi.inventoryItems.updateInventoryItem(
                householdId,
                inventoryId,
                itemId,
                data,
            ),
        onMutate: async (variables) => {
            await queryClient.cancelQueries({
                queryKey: inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
            });

            const previousItems = queryClient.getQueryData<
                InventoryItemResponse[]
            >(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
            );

            // Text and quantity preserve on null (??); expiryDate is write-through (=),
            // mirroring the server's UpdateItem asymmetry — null clears the value.
            queryClient.setQueryData<InventoryItemResponse[]>(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
                (old) => {
                    if (!old) return old;
                    return old.map((item) =>
                        item.id === variables.itemId
                            ? {
                                  ...item,
                                  text: variables.data.text ?? item.text,
                                  quantity:
                                      variables.data.quantity ?? item.quantity,
                                  expiryDate: variables.data.expiryDate,
                                  updatedAt: new Date().toISOString(),
                              }
                            : item,
                    );
                },
            );

            const currentItem = queryClient.getQueryData<InventoryItemResponse>(
                inventoryItemKeys.detail(variables.itemId),
            );
            if (currentItem) {
                queryClient.setQueryData<InventoryItemResponse>(
                    inventoryItemKeys.detail(variables.itemId),
                    {
                        ...currentItem,
                        text: variables.data.text ?? currentItem.text,
                        quantity:
                            variables.data.quantity ?? currentItem.quantity,
                        expiryDate: variables.data.expiryDate,
                        updatedAt: new Date().toISOString(),
                    },
                );
            }

            return { previousItems };
        },
        onError: (_, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    inventoryItemKeys.byInventory(
                        variables.householdId,
                        variables.inventoryId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_, variables) => {
            debouncedInvalidate(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
            );
            debouncedInvalidate(inventoryItemKeys.detail(variables.itemId));
        },
    });
};

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type { InventoryItemResponse } from "../../../lib/api";
import { inventoryItemKeys } from "./inventoryItemKeys";

export const useDeleteInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            inventoryId,
            itemId,
        }: {
            householdId: number;
            inventoryId: number;
            itemId: number;
        }) =>
            ClientApi.inventoryItems.deleteInventoryItem(
                householdId,
                inventoryId,
                itemId,
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

            queryClient.setQueryData<InventoryItemResponse[]>(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
                (old) => {
                    if (!old) return old;
                    return old.filter((item) => item.id !== variables.itemId);
                },
            );

            queryClient.removeQueries({
                queryKey: inventoryItemKeys.detail(variables.itemId),
            });

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
        onSettled: (_, __, variables) => {
            debouncedInvalidate(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
            );
        },
    });
};

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type {
    CreateInventoryItemRequest,
    InventoryItemResponse,
} from "../../../lib/api";
import { inventoryItemKeys } from "./inventoryItemKeys";

export const useCreateInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            inventoryId,
            data,
        }: {
            householdId: number;
            inventoryId: number;
            data: CreateInventoryItemRequest;
        }) =>
            ClientApi.inventoryItems.createInventoryItem(
                householdId,
                inventoryId,
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

            // Append below the last item to match the server's AddItem semantics
            // (Inventory.ComputeAppendSortOrder: last + DefaultGap). Single section
            // — InventoryItems have no status/checked split.
            const lastSortOrder =
                previousItems?.reduce(
                    (max, i) => Math.max(max, i.sortOrder ?? 0),
                    0,
                ) ?? 0;

            const optimisticItem: InventoryItemResponse = {
                id: Date.now(),
                text: variables.data.text,
                quantity: variables.data.quantity,
                expiryDate: variables.data.expiryDate,
                sortOrder: lastSortOrder + 1,
                inventoryId: variables.inventoryId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
                isExpiring: false,
            };

            queryClient.setQueryData<InventoryItemResponse[]>(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
                (old) => (old ? [...old, optimisticItem] : [optimisticItem]),
            );

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
        },
    });
};

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    createInventoryItemMutation,
    getInventoryItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { InventoryItemResponse } from "../../../lib/api/types.gen";

export const useCreateInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...createInventoryItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getInventoryItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    inventoryId: variables.path.inventoryId,
                },
            });

            await queryClient.cancelQueries({ queryKey });

            const previousItems =
                queryClient.getQueryData<InventoryItemResponse[]>(queryKey);

            const optimisticItem: InventoryItemResponse = {
                id: Date.now(),
                text: variables.body.text,
                quantity: variables.body.quantity,
                expiryDate: variables.body.expiryDate,
                // Placeholder rank — rendering trusts array order (appended at the end, matching
                // the server's append). The authoritative rank arrives on the refetch in onSuccess.
                rank: "",
                inventoryId: variables.path.inventoryId,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
                isExpiring: false,
            };

            queryClient.setQueryData<InventoryItemResponse[]>(
                queryKey,
                (old) => (old ? [...old, optimisticItem] : [optimisticItem]),
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

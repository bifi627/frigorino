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

            const tempId = Date.now();
            const optimisticItem: InventoryItemResponse = {
                id: tempId,
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

            return { previousItems, tempId };
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
        onSuccess: (data, variables, context) => {
            const queryKey = getInventoryItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    inventoryId: variables.path.inventoryId,
                },
            });
            // Swap the temp-id optimistic row for the real server item immediately, so an
            // edit/reorder fired before the debounced refetch lands targets the real id. Otherwise
            // the PUT/PATCH targets the Date.now() temp id, which overflows the {itemId:int} route
            // constraint and falls through to the SPA fallback (HTTP 500). Mirrors useCreateListItem.
            if (context?.tempId !== undefined) {
                queryClient.setQueryData<InventoryItemResponse[]>(
                    queryKey,
                    (old) =>
                        old?.map((i) => (i.id === context.tempId ? data : i)) ??
                        old,
                );
            }
            debouncedInvalidate(queryKey);
        },
    });
};

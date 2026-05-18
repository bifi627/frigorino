import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import {
    computeAppendSortOrder,
    computeReorderSortOrder,
} from "../../../common/sortOrder";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type {
    InventoryItemResponse,
    ReorderItemRequest,
} from "../../../lib/api";
import { inventoryItemKeys } from "./inventoryItemKeys";

export const useReorderInventoryItem = () => {
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
            data: ReorderItemRequest;
        }) =>
            ClientApi.inventoryItems.reorderInventoryItem(
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

            // Optimistic mirror of server math; see common/sortOrder.ts.
            // Inventory has a single section — treat it as the "unchecked" range.
            queryClient.setQueryData<InventoryItemResponse[]>(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
                (old) => {
                    if (!old) return old;
                    const movedItem = old.find((i) => i.id === variables.itemId);
                    if (!movedItem) return old;

                    const section = old.filter((i) => i.id !== movedItem.id);

                    const newSortOrder = computeReorderSortOrder({
                        section,
                        afterId: variables.data.afterId,
                        emptyDefault: computeAppendSortOrder([], false),
                    });

                    return old.map((i) =>
                        i.id === movedItem.id
                            ? { ...i, sortOrder: newSortOrder }
                            : i,
                    );
                },
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

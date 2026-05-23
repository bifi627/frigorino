import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    computeAppendSortOrder,
    computeReorderSortOrder,
} from "../../../common/sortOrder";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getInventoryItemsQueryKey,
    reorderInventoryItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { InventoryItemResponse } from "../../../lib/api/types.gen";

export const useReorderInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...reorderInventoryItemMutation(),
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

            // Optimistic mirror of server math; see common/sortOrder.ts.
            // Inventory has a single section — treat it as the "unchecked" range.
            queryClient.setQueryData<InventoryItemResponse[]>(
                queryKey,
                (old) => {
                    if (!old) return old;
                    const movedItem = old.find(
                        (i) => i.id === variables.path.itemId,
                    );
                    if (!movedItem) return old;

                    const section = old.filter((i) => i.id !== movedItem.id);

                    const newSortOrder = computeReorderSortOrder({
                        section,
                        afterId: variables.body.afterId,
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
        onSettled: (_data, _error, variables) => {
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

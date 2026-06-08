import { useMutation, useQueryClient } from "@tanstack/react-query";
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

            // The server mints the authoritative rank; optimistically we just move the dragged
            // element to its new array position (single section — no status split). The real rank
            // arrives on refetch and reconciles.
            queryClient.setQueryData<InventoryItemResponse[]>(
                queryKey,
                (old) => {
                    if (!old) return old;
                    const moved = old.find(
                        (i) => i.id === variables.path.itemId,
                    );
                    if (!moved) return old;

                    const others = old.filter((i) => i.id !== moved.id);
                    const afterId = variables.body.afterId;
                    if (!afterId) {
                        // Top of the single section.
                        others.unshift(moved);
                        return others;
                    }
                    const anchorIdx = others.findIndex((i) => i.id === afterId);
                    others.splice(
                        anchorIdx === -1 ? others.length : anchorIdx + 1,
                        0,
                        moved,
                    );
                    return others;
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

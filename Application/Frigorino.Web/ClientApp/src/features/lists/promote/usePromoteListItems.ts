import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getInventoryItemsQueryKey,
    getListQueryKey,
    getPendingPromotionsQueryKey,
    promoteListItemsMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

// Atomic batch promote. Caller passes { path: { householdId, listId }, body: { inventoryId, items } }.
// Invalidates the pending batch, the list (for PendingPromotionCount), and the target inventory.
export const usePromoteListItems = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...promoteListItemsMutation(),
        onSuccess: (_data, variables) => {
            const { householdId, listId } = variables.path;
            queryClient.invalidateQueries({
                queryKey: getPendingPromotionsQueryKey({
                    path: { householdId, listId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getListQueryKey({ path: { householdId, listId } }),
            });
            queryClient.invalidateQueries({
                queryKey: getInventoryItemsQueryKey({
                    path: {
                        householdId,
                        inventoryId: variables.body.inventoryId,
                    },
                }),
            });
        },
    });
};

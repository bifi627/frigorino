import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getExpiryCalendarQueryKey,
    getInventoryItemsQueryKey,
    restoreInventoryItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreInventoryItem = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreInventoryItemMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getInventoryItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        inventoryId: variables.path.inventoryId,
                    },
                }),
            });
            // Keep the expiry calendar (a separate query) in sync when an item is
            // restored via the undo toast.
            queryClient.invalidateQueries({
                queryKey: getExpiryCalendarQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};

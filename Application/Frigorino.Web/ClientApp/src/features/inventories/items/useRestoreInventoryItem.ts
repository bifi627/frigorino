import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
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
        },
    });
};

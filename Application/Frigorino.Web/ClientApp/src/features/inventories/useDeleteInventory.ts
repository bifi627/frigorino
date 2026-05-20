import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    deleteInventoryMutation,
    getInventoriesQueryKey,
    getInventoryQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useDeleteInventory = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...deleteInventoryMutation(),
        onSuccess: (_data, variables) => {
            queryClient.removeQueries({
                queryKey: getInventoryQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        inventoryId: variables.path.inventoryId,
                    },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getInventoriesQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};

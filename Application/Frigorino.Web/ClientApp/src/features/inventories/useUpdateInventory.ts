import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getInventoriesQueryKey,
    updateInventoryMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateInventory = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateInventoryMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getInventoriesQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};

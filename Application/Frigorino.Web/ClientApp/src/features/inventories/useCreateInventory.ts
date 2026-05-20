import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createInventoryMutation,
    getInventoriesQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useCreateInventory = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createInventoryMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getInventoriesQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};

import { useMutation } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getInventoryItemsQueryKey,
    restoreInventoryItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreInventoryItem = () => {
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...restoreInventoryItemMutation(),
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

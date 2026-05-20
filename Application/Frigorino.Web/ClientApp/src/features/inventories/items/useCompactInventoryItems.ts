import { useMutation } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    compactInventoryItemsMutation,
    getInventoryItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useCompactInventoryItems = () => {
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...compactInventoryItemsMutation(),
        onSuccess: (_data, variables) => {
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

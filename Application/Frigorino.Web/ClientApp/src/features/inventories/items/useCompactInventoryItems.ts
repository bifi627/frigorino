import { useMutation } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import { inventoryItemKeys } from "./inventoryItemKeys";

export const useCompactInventoryItems = () => {
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            inventoryId,
        }: {
            householdId: number;
            inventoryId: number;
        }) =>
            ClientApi.inventoryItems.compactInventoryItems(
                householdId,
                inventoryId,
            ),
        onSuccess: (_, variables) => {
            debouncedInvalidate(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
            );
        },
    });
};

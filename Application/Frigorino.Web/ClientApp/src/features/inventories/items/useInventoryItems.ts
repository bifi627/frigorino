import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { inventoryItemKeys } from "./inventoryItemKeys";

export const useInventoryItems = (
    householdId: number,
    inventoryId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: inventoryItemKeys.byInventory(householdId, inventoryId),
        queryFn: () =>
            ClientApi.inventoryItems.getInventoryItems(
                householdId,
                inventoryId,
            ),
        enabled: enabled && householdId > 0 && inventoryId > 0,
        staleTime: 1000 * 30,
    });
};

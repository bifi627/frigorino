import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import { inventoryKeys } from "./inventoryKeys";

export const useInventory = (
    householdId: number,
    inventoryId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: inventoryKeys.detail(inventoryId),
        queryFn: () =>
            ClientApi.inventories.getInventory(householdId, inventoryId),
        enabled: enabled && inventoryId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
};

import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import { inventoryKeys } from "./inventoryKeys";

export const useHouseholdInventories = (
    householdId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: inventoryKeys.byHousehold(householdId),
        queryFn: () => ClientApi.inventories.getInventories(householdId),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
};

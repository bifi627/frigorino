import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import { listKeys } from "./listKeys";

export const useHouseholdLists = (householdId: number, enabled = true) => {
    return useQuery({
        queryKey: listKeys.byHousehold(householdId),
        queryFn: () => ClientApi.lists.getLists(householdId),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
};

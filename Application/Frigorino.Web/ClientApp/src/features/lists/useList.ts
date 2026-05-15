import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import { listKeys } from "./listKeys";

export const useList = (
    householdId: number,
    listId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: listKeys.detail(listId),
        queryFn: () => ClientApi.lists.getList(householdId, listId),
        enabled: enabled && listId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
};

import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { listItemKeys } from "./listItemKeys";

export const useListItems = (
    householdId: number,
    listId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: listItemKeys.byList(householdId, listId),
        queryFn: () => ClientApi.listItems.getItems(householdId, listId),
        enabled: enabled && listId > 0 && householdId > 0,
        staleTime: 1000 * 30,
    });
};

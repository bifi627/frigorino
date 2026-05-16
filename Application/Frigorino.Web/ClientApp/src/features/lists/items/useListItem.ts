import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { listItemKeys } from "./listItemKeys";

export const useListItem = (
    householdId: number,
    listId: number,
    itemId: number,
    enabled = true,
) => {
    return useQuery({
        queryKey: listItemKeys.detail(itemId),
        queryFn: () =>
            ClientApi.listItems.getItem(householdId, listId, itemId),
        enabled: enabled && itemId > 0 && listId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
};

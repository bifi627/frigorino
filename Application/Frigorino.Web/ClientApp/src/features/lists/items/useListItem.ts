import { useQuery } from "@tanstack/react-query";
import { getItemOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useListItem = (
    householdId: number,
    listId: number,
    itemId: number,
    enabled = true,
) =>
    useQuery({
        ...getItemOptions({ path: { householdId, listId, itemId } }),
        enabled: enabled && itemId > 0 && listId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });

import { useQuery } from "@tanstack/react-query";
import { getItemsOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useListItems = (
    householdId: number,
    listId: number,
    enabled = true,
) =>
    useQuery({
        ...getItemsOptions({ path: { householdId, listId } }),
        enabled: enabled && listId > 0 && householdId > 0,
        staleTime: 1000 * 30,
    });

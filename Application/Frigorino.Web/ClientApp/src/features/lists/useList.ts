import { useQuery } from "@tanstack/react-query";
import { getListOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useList = (
    householdId: number,
    listId: number,
    enabled = true,
) =>
    useQuery({
        ...getListOptions({ path: { householdId, listId } }),
        enabled: enabled && listId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });

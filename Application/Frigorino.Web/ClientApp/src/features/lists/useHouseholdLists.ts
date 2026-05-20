import { useQuery } from "@tanstack/react-query";
import { getListsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useHouseholdLists = (householdId: number, enabled = true) =>
    useQuery({
        ...getListsOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });

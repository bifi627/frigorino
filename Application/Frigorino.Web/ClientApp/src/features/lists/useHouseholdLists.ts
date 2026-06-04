import { useQuery } from "@tanstack/react-query";
import { getListsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useHouseholdLists = (householdId: number, enabled = true) =>
    useQuery({
        ...getListsOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
        // Summary counts derive from item-level edits made on child routes, which only
        // invalidate the single-list query. Refetch on mount so the dashboard and the lists
        // index reflect those edits after navigating back.
        refetchOnMount: "always",
    });

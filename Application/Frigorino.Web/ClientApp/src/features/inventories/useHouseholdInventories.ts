import { useQuery } from "@tanstack/react-query";
import { getInventoriesOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useHouseholdInventories = (householdId: number, enabled = true) =>
    useQuery({
        ...getInventoriesOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
        // Summary counts/earliest-expiry derive from item-level edits made on child routes,
        // which only invalidate the single-inventory query. Refetch on mount so the dashboard
        // and the inventories index reflect those edits after navigating back.
        refetchOnMount: "always",
    });
